using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Extensions;   // GetDefaultName() (FactionInfoDB has no Name property — it lives on the entity's NameDB)
using Pulsar4X.Factions;
using Pulsar4X.Modding;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// GROUND-TRUTH READOUT for the "UMF never lands an invasion" dig (task #36). The whole AI conquest chain
    /// (objective → war → target → mass a strike fleet → build+load a transport → sail → LAND → capture) is built + wired
    /// + tested piece-by-piece, but the END-TO-END never completes in a running game, and CI can't run the client. This
    /// READOUT opens the acting gates on the real DevTest sandbox, drives the NPC brain DIRECTLY (the same call the
    /// hotloop makes — no master-clock advance, so no combat fine-step hang), and PRINTS what the militarist UMF actually
    /// DECIDES each tick — its objective + the concrete order it emits (StrategicObjectiveDB.LastActionKind, e.g.
    /// "QueueWarship" = still massing, "StrikeFleet"/"SailTransport"/"LandInvasion" = advancing the chain, "None" = a
    /// resolver ran but had no legal step). Plus the decision TAPE and whether UMF is at war. So we can SEE which rung
    /// the chain settles on in CI. **As of the reactor-charge fix (task #36, 2026-07-17) it also ASSERTS the chain
    /// advances past massing** — the end-to-end gauge that the fix (charging JSON-loaded fleets so they can warp) let the
    /// strike actually sail, not just that "an order" came out. The four gates are process-global statics, captured +
    /// RESTORED in a finally so every other test stays byte-identical.
    /// </summary>
    [TestFixture]
    public class DevTestInvasionReadout
    {
        private const string ScenarioDir = "Data/basemod/ScenarioFiles";
        private static void Log(string m) => TestContext.Progress.WriteLine("[invasion] " + m);

        private static Game NewGame()
        {
            var modDataStore = new ModDataStore();
            new ModLoader().LoadModManifest("Data/basemod/modInfo.json", modDataStore);
            return GameFactory.CreateGame(modDataStore, new NewGameSettings
            {
                MaxSystems = 1, CreatePlayerFaction = false, DefaultSolStart = true, MasterSeed = 12345, EleStart = true
            });
        }

        [Test]
        [Description("Fix gauge (task #36): with the acting gates OPEN, drive the DevTest UMF brain many ticks; print which rung of the conquest chain it settles on + the decision tape + war status, AND assert the invasion chain advances past massing (emits a real STRIKE/SAIL/LAND order) — the end-to-end proof the reactor-charge fix let the strike sail.")]
        public void UMF_ConquestChain_WhereDoesItStall()
        {
            bool o = NPCDecisionProcessor.EnableOrderEmission;
            bool d = NPCDecisionProcessor.EnableDiplomaticProposals;
            bool e = NPCDecisionProcessor.EnableEspionageMirror;
            bool il = NPCDecisionProcessor.EnableIntelLedger;
            try
            {
                var game = NewGame();
                var (player, _) = DevTestStartFactory.CreateDevTest(
                    game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });
                Assert.That(player, Is.Not.Null, "DevTest returned no player faction.");

                NPCDecisionProcessor.EnableOrderEmission = true;
                NPCDecisionProcessor.EnableDiplomaticProposals = true;
                NPCDecisionProcessor.EnableEspionageMirror = true;
                NPCDecisionProcessor.EnableIntelLedger = true;

                // The UMF = the NPC with the multi-world Martian empire (>= 4 colonies), the aggressor.
                Entity umf = game.Factions.Values.FirstOrDefault(f =>
                    f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>()
                    && f.GetDataBlob<FactionInfoDB>().IsNPC && f.GetDataBlob<FactionInfoDB>().Colonies.Count >= 4);
                Assert.That(umf, Is.Not.Null, "no multi-world NPC (UMF) in the DevTest.");
                var umfInfo = umf.GetDataBlob<FactionInfoDB>();
                Log($"UMF = '{umf.GetDefaultName()}' ({umfInfo.Colonies.Count} colonies), player = '{player.GetDefaultName()}'");

                var processor = new NPCDecisionProcessor();
                processor.Init(game);

                // Drive the brain many ticks (direct — no clock advance → no combat fine-step hang). Print UMF's decision
                // each tick: does it reach Conquer, and which concrete order does it emit (which rung fires)? Collect the
                // SET of concrete actions it emits across all ticks — the gauge below asserts the invasion chain ADVANCES.
                string lastLine = "";
                var actionsSeen = new HashSet<string>();
                for (int i = 0; i < 24; i++)
                {
                    processor.ProcessManager(game.GlobalManager, 86400);
                    string obj = "-", act = "<none-ran>";
                    if (umf.HasDataBlob<StrategicObjectiveDB>())
                    {
                        var so = umf.GetDataBlob<StrategicObjectiveDB>();
                        obj = so.Objective.ToString();
                        act = string.IsNullOrEmpty(so.LastActionKind) ? "<none-ran>" : so.LastActionKind;
                        if (!string.IsNullOrEmpty(so.LastActionKind)) actionsSeen.Add(so.LastActionKind);
                    }
                    string line = $"obj={obj} action={act}";
                    if (line != lastLine) { Log($"tick {i,2}: {line}"); lastLine = line; }   // print only on change (steady-state is obvious)
                }
                Log("actions emitted across the run: " + (actionsSeen.Count == 0 ? "(none)" : string.Join(", ", actionsSeen)));

                // War status: is UMF actually AT WAR with anyone (the target-scoring prerequisite)?
                bool atWar = umf.HasDataBlob<DiplomacyDB>() && umf.GetDataBlob<DiplomacyDB>().IsAtWarWithAnyone();
                Log($"UMF at war with anyone: {atWar}");

                // The decision TAPE — the last several recorded decisions (the sensed picture + action).
                try { Log("UMF decision tape:\n" + PlanReadout.DecisionTape(umf, 8)); }
                catch { Log("(decision tape unavailable)"); }

                // Always-on half: the brain settled an objective (the decide loop ran).
                Assert.That(umf.HasDataBlob<StrategicObjectiveDB>(), Is.True, "UMF never settled a strategic objective.");

                // THE FIX GAUGE (task #36): the invasion chain now ADVANCES past massing. Before the reactor-charge fix
                // (FactionFactory fleet loader → ChargeReactors/FillFuelTanks), the massed Mars Home Guard could never
                // warp — ConquerResolver Rung 1's `reach.HasRange` read false — so the UMF sat forever at "QueueWarship"
                // and the invasion never sailed. With the fleet now charged, all six Rung-1 STRIKE gates pass (odds vs the
                // near-zero-military UEF, a ready 3-hull strike fleet, same-system Sol target, HasRange now true, a
                // scenario fleet not gated by the home-reserve doctrine), so the UMF emits a real STRIKE/SAIL/LAND order.
                // Asserting the ADVANCE (not just "an order") is what makes this the end-to-end proof the warp fix works —
                // "QueueWarship" alone would pass NPCActingSensorTests but means the chain is STILL STALLED.
                var advanceActions = new[] { "StrikeFleet", "StrikeJump", "SailTransport", "LoadInvasion", "LandInvasion" };
                bool advanced = actionsSeen.Overlaps(advanceActions);
                Assert.That(advanced, Is.True,
                    "UMF never advanced past massing — the conquest chain is still stalled. Emitted only: ["
                    + string.Join(", ", actionsSeen) + "] over 24 ticks; expected one of ["
                    + string.Join(", ", advanceActions) + "]. (at war: " + atWar + ")");
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
