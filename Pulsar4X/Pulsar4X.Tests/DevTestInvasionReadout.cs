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
    /// the chain settles on in CI. Always green (a readout) — it prints the stall, it doesn't assert the (broken) finish.
    /// The four gates are process-global statics, captured + RESTORED in a finally so every other test stays byte-identical.
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
        [Description("Readout: with the acting gates OPEN, drive the DevTest UMF brain many ticks and print which rung of the conquest chain it settles on (objective + LastActionKind), the decision tape, and its war status — the ground-truth for where the invasion chain stalls.")]
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
                // each tick: does it reach Conquer, and which concrete order does it emit (which rung fires)?
                string lastLine = "";
                for (int i = 0; i < 24; i++)
                {
                    processor.ProcessManager(game.GlobalManager, 86400);
                    string obj = "-", act = "<none-ran>";
                    if (umf.HasDataBlob<StrategicObjectiveDB>())
                    {
                        var so = umf.GetDataBlob<StrategicObjectiveDB>();
                        obj = so.Objective.ToString();
                        act = string.IsNullOrEmpty(so.LastActionKind) ? "<none-ran>" : so.LastActionKind;
                    }
                    string line = $"obj={obj} action={act}";
                    if (line != lastLine) { Log($"tick {i,2}: {line}"); lastLine = line; }   // print only on change (steady-state is obvious)
                }

                // War status: is UMF actually AT WAR with anyone (the target-scoring prerequisite)?
                bool atWar = umf.HasDataBlob<DiplomacyDB>() && umf.GetDataBlob<DiplomacyDB>().IsAtWarWithAnyone();
                Log($"UMF at war with anyone: {atWar}");

                // The decision TAPE — the last several recorded decisions (the sensed picture + action).
                try { Log("UMF decision tape:\n" + PlanReadout.DecisionTape(umf, 8)); }
                catch { Log("(decision tape unavailable)"); }

                // A readout, not a pass/fail on the finish: just assert the brain settled SOMETHING (the always-on half),
                // so this is a real gauge and not a no-op. WHERE it stalls is read off the printout above.
                Assert.That(umf.HasDataBlob<StrategicObjectiveDB>(), Is.True, "UMF never settled a strategic objective.");
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
