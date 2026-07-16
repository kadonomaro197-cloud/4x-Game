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
    /// This test loads the real DevTest conquest sandbox, OPENS the four gates, and drives the NPC brain DIRECTLY
    /// (<see cref="NPCDecisionProcessor.ProcessManager"/> on the GlobalManager, the exact call the hotloop makes — decide
    /// + gated act + record, all in one tick) a few times, then asserts an NPC faction EMITTED A REAL ORDER — its
    /// <see cref="StrategicObjectiveDB.LastActionKind"/> is set to something other than "" (the gate never ran) or "None"
    /// (a resolver ran but had no legal step). That turns "acting-unverified" green-or-red without the SDL client — the
    /// gate the whole B5→Phase-D build stacks on.
    ///
    /// It ticks the processor DIRECTLY rather than advancing the master clock, on purpose: with the AI fully on in a war
    /// sandbox, advancing the clock triggers the combat-interrupt fine-stepping (5-second sub-steps), which turns a
    /// multi-day advance into millions of steps (an effective hang in CI). The direct tick exercises the same acting
    /// path (UpdateStrategicObjective → EmitOrders → the recorder) in milliseconds. The four gates are process-global
    /// statics, captured and RESTORED in a finally so every other test stays byte-identical.
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
        [Description("With the order gate OPEN, an NPC faction in the DevTest sandbox actually ACTS — driving the brain a "
                   + "few ticks emits a real order (a non-empty, non-\"None\" LastActionKind). The acting-AI CI sensor: "
                   + "proves the loop DECIDES *and DOES* without the SDL client, and without the slow sim advance.")]
        public void OrderGateOpen_AnNPC_EmitsARealOrder()
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

                // OPEN THE VALVES — exactly what CreateGameCore / the DevTest button do for a real game.
                NPCDecisionProcessor.EnableOrderEmission = true;
                NPCDecisionProcessor.EnableDiplomaticProposals = true;
                NPCDecisionProcessor.EnableEspionageMirror = true;
                NPCDecisionProcessor.EnableIntelLedger = true;

                // Drive the brain DIRECTLY — the exact call the hotloop makes (ProcessManager → ProcessEntity → Tick →
                // decide + gated EmitOrders + record), on the GlobalManager where faction entities live. No master-clock
                // advance → no combat fine-stepping → fast + can't hang. A couple of ticks: the first settles each NPC's
                // objective, and EmitOrders (NOT monthly-gated) runs every tick, so the action lands.
                var processor = new NPCDecisionProcessor();
                processor.Init(game);
                for (int i = 0; i < 3; i++)
                    processor.ProcessManager(game.GlobalManager, 86400);

                var npcs = game.Factions.Values
                    .Where(f => f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>() && f.GetDataBlob<FactionInfoDB>().IsNPC)
                    .ToList();
                Assert.That(npcs.Count, Is.GreaterThanOrEqualTo(2), "expected the two NPC factions (UMF + Kithrin).");

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

                TestContext.Progress.WriteLine("[acting-sensor] tickErr=" + NPCDecisionProcessor.TickErrorCount +
                    " lastErr='" + NPCDecisionProcessor.LastTickError + "' objectives=[" + string.Join(", ",
                        settled.Select(so => so.Objective + "→" + (string.IsNullOrEmpty(so.LastActionKind) ? "<none-ran>" : so.LastActionKind))) + "]");

                Assert.That(acted, Is.Not.Null,
                    "NO NPC emitted a real order with the gate OPEN — the acting path is dark even with EnableOrderEmission=true. "
                    + "Settled objectives: [" + string.Join(", ",
                        settled.Select(so => so.Objective + ":" + (string.IsNullOrEmpty(so.LastActionKind) ? "<none-ran>" : so.LastActionKind))) + "]"
                    + " (last tick error: '" + NPCDecisionProcessor.LastTickError + "')");
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
