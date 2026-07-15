using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Modding;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// THE ACTING-AI SENSOR — the CI gauge the survey found missing. Every AI arm is tested in isolation, but nothing
    /// ran the WHOLE acting scenario with the order VALVE OPEN and asserted that an order actually came out. The NPC
    /// brain's action-arms ship gated OFF by default (byte-identity), and the DevTest button (client) is the only thing
    /// that opens them — which CI can't run. So "the AI acts" rested on code-reading, not a green test.
    ///
    /// This test loads the real DevTest conquest sandbox, OPENS the four gates (exactly as CreateGameCore / the DevTest
    /// button do for a real game), advances the clock several monthly cycles, and asserts an NPC faction EMITTED A REAL
    /// ORDER — its <see cref="StrategicObjectiveDB.LastActionKind"/> is set to something other than "" (the gate never
    /// ran) or "None" (a resolver ran but had no legal step). That turns "acting-unverified" green-or-red without the
    /// SDL client — the gate the whole B5→Phase-D build stacks on.
    ///
    /// The four flags are process-global statics (shared across the whole test process), so they are captured and
    /// RESTORED in a finally — this test flips them ON only for its own body (every other test stays byte-identical).
    /// </summary>
    [TestFixture]
    public class NPCActingSensorTests
    {
        private const string ScenarioDir = "Data/basemod/ScenarioFiles";

        private static Game NewGame()
        {
            var modDataStore = new ModDataStore();
            var modLoader = new ModLoader();
            modLoader.LoadModManifest("Data/basemod/modInfo.json", modDataStore);

            var gameSettings = new NewGameSettings
            {
                MaxSystems = 1,
                CreatePlayerFaction = false,   // DevTest authors its own factions from JSON
                DefaultSolStart = true,
                MasterSeed = 12345,
                EleStart = true
            };
            return GameFactory.CreateGame(modDataStore, gameSettings);
        }

        [Test]
        [Description("With the order gate OPEN, an NPC faction in the DevTest sandbox actually ACTS — its brain emits a "
                   + "real order (a non-empty, non-\"None\" LastActionKind) within a few monthly cycles. The acting-AI CI "
                   + "sensor: proves the loop DECIDES *and DOES* without the SDL client.")]
        public void OrderGateOpen_AnNPC_EmitsARealOrder_WithinAFewCycles()
        {
            // Capture the process-global gates so we restore them no matter what (they leak across tests otherwise).
            bool o = NPCDecisionProcessor.EnableOrderEmission;
            bool d = NPCDecisionProcessor.EnableDiplomaticProposals;
            bool e = NPCDecisionProcessor.EnableEspionageMirror;
            bool il = NPCDecisionProcessor.EnableIntelLedger;
            try
            {
                var game = NewGame();
                var (player, startingSystemId) = DevTestStartFactory.CreateDevTest(
                    game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });
                Assert.That(player, Is.Not.Null, "DevTest returned no player faction.");

                // Promote the starting system out of Stasis, or nothing in it processes (Tests/CLAUDE.md gotcha 6).
                var startingSystem = game.Systems.FirstOrDefault(s => s.ID.Equals(startingSystemId));
                Assert.That(startingSystem, Is.Not.Null, "the starting system did not load.");
                startingSystem.IncrementExternalObserver(priority: true);

                // OPEN THE VALVES — exactly what CreateGameCore / the DevTest button do for a real game.
                NPCDecisionProcessor.EnableOrderEmission = true;
                NPCDecisionProcessor.EnableDiplomaticProposals = true;
                NPCDecisionProcessor.EnableEspionageMirror = true;
                NPCDecisionProcessor.EnableIntelLedger = true;

                // Advance several monthly cycles. NPCDecisionProcessor: FirstRunOffset 5d, RunFrequency 30d → it fires at
                // ~5/35/65/95/125 days. Single-threaded so a processor throw surfaces on THIS thread (L2/L4), not the pool.
                // 5-day steps keep the sim cheap; the per-system scheduler still fires the monthly processor at its
                // scheduled instants inside each step, so it hits the ~5/35/65/95/125-day cycles.
                game.Settings.EnforceSingleThread = true;
                game.TimePulse.Ticklength = TimeSpan.FromDays(5);
                for (int i = 0; i < 30; i++)   // ~150 days → ~5 monthly cycles
                    game.TimePulse.TimeStep();

                var npcs = game.Factions.Values
                    .Where(f => f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>() && f.GetDataBlob<FactionInfoDB>().IsNPC)
                    .ToList();
                Assert.That(npcs.Count, Is.GreaterThanOrEqualTo(2), "expected the two NPC factions (UMF + Kithrin).");

                // The processor fired at all (the GlobalManager-iteration keystone).
                Assert.That(NPCDecisionProcessor.TickCount, Is.GreaterThan(0),
                    "the NPC brain never fired — the GlobalManager isn't being iterated (keystone L5).");

                // Each NPC should have SETTLED an objective (the always-on decide half).
                var settled = npcs
                    .Where(f => f.HasDataBlob<StrategicObjectiveDB>())
                    .Select(f => f.GetDataBlob<StrategicObjectiveDB>())
                    .ToList();
                Assert.That(settled.Any(so => so.Objective != StrategicObjective.None), Is.True,
                    "no NPC settled a strategic objective — the always-on decide half didn't run.");

                // THE SENSOR: at least one NPC ACTED — emitted a real order (a resolver executed a concrete step, so
                // LastActionKind is set to something other than "" (gate never ran) or "None" (no legal step)).
                var acted = settled.FirstOrDefault(so =>
                    !string.IsNullOrEmpty(so.LastActionKind) && so.LastActionKind != "None");

                TestContext.Progress.WriteLine("[acting-sensor] TickCount=" + NPCDecisionProcessor.TickCount +
                    " objectives=[" + string.Join(", ",
                        settled.Select(so => so.Objective + "→" + (string.IsNullOrEmpty(so.LastActionKind) ? "<none-ran>" : so.LastActionKind))) + "]");

                Assert.That(acted, Is.Not.Null,
                    "NO NPC emitted a real order with the gate OPEN — the acting path is dark even with EnableOrderEmission=true. "
                    + "Settled objectives: [" + string.Join(", ",
                        settled.Select(so => so.Objective + ":" + (string.IsNullOrEmpty(so.LastActionKind) ? "<none-ran>" : so.LastActionKind))) + "]");
            }
            finally
            {
                NPCDecisionProcessor.EnableOrderEmission = o;
                NPCDecisionProcessor.EnableDiplomaticProposals = d;
                NPCDecisionProcessor.EnableEspionageMirror = e;
                NPCDecisionProcessor.EnableIntelLedger = il;
            }
        }
    }
}
