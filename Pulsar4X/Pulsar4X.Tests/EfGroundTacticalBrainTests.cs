using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Galaxy;        // PlanetRegionsDB, Region
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Operation Earthfall G2.2 — THE GROUND TACTICAL BRAIN (the answer to "is the AI smart enough to know when to be
    /// defensive vs offensive"). The §4 acceptance gauges from docs/earthfall/GROUND-TACTICAL-AI-DESIGN.md, driven mostly
    /// as DIRECT calls to the pure <see cref="GroundTactics.DecidePosture"/> (no sim advance, deterministic), plus the
    /// fog-honest <see cref="GroundThreat"/> read and the wire's ORDER-OWNERSHIP rule (a player order is sacrosanct).
    /// Engine-only → CI (`rest` shard).
    /// </summary>
    [TestFixture]
    public class EfGroundTacticalBrainTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[brain] " + m);

        /// <summary>A neutral baseline context: a lone attacker at parity, no fort/terrain/ammo/support, scouted. Each
        /// test overrides only the fields it exercises, so the assertion isolates one rule.</summary>
        private static GroundTacticsContext BaseCtx() => new GroundTacticsContext
        {
            OwnStrength = 100, EnemyStrength = 0, RiskTrait = 0.5, AggressionTrait = 0.5,
            IsHomelandDefender = false, HasOrbitalSupport = false, FortificationMult = 1.0,
            DefensibleTerrain = false, HasAmmoWeapons = false, AmmoFraction = 1.0,
            ReserveIntact = true, HasFallback = false, FallbackRegion = -1,
            HasAdvanceTarget = false, AdvanceRegion = -1, Blind = false,
        };

        private static GroundUnitDesign Inf() => new GroundUnitDesign
        {
            UniqueID = "efb-inf", Name = "Infantry", UnitType = GroundUnitType.Infantry,
            Attack = 100, Defense = 10, HitPoints = 500,
        };

        // ───────────────────────── §4 acceptance gauges (pure DecidePosture) ─────────────────────────

        [Test]
        [Description("§4.1 — an outnumbered defender on a fortified region picks Defensive + Hold and does NOT advance (even with a target).")]
        public void Outnumbered_FortifiedDefender_DigsInAndHolds()
        {
            var c = BaseCtx();
            c.IsHomelandDefender = true; c.OwnStrength = 100; c.EnemyStrength = 300; // 1:3, outnumbered
            c.FortificationMult = 1.5;                                                // a prepared line
            c.HasAdvanceTarget = true; c.AdvanceRegion = 1;                           // a target exists — must still NOT advance

            var p = GroundTactics.DecidePosture(c);
            Assert.That(p.StanceFamily, Is.EqualTo(GroundTactics.Defensive));
            Assert.That(p.Roe, Is.EqualTo(GroundEngagementStance.HoldGround));
            Assert.That(p.Intent, Is.EqualTo(GroundIntent.Hold));
            Assert.That(p.MoveTargetRegion, Is.EqualTo(-1), "a dug-in defender does not march off its line");
            Log($"fortified defender 100v300 → {p.StanceFamily}/{p.Roe}/{p.Intent}: {p.Reason}");
        }

        [Test]
        [Description("§4.2 — an attacker with a 2:1 edge picks Offensive + Close + Advance toward the target region.")]
        public void Attacker_With2To1Edge_PressesTheAssault()
        {
            var c = BaseCtx();
            c.OwnStrength = 200; c.EnemyStrength = 100;               // 2:1
            c.HasAdvanceTarget = true; c.AdvanceRegion = 3;

            var p = GroundTactics.DecidePosture(c);
            Assert.That(p.StanceFamily, Is.EqualTo(GroundTactics.Offensive));
            Assert.That(p.Roe, Is.EqualTo(GroundEngagementStance.CloseToEngage));
            Assert.That(p.Intent, Is.EqualTo(GroundIntent.Advance));
            Assert.That(p.MoveTargetRegion, Is.EqualTo(3));
            Log($"attacker 200v100 → {p.StanceFamily}/{p.Roe}/{p.Intent} → region {p.MoveTargetRegion + 1}: {p.Reason}");
        }

        [Test]
        [Description("§4.3 — losing 1:4 with friendly ground behind → Retreat toward it; cornered (no fallback) → dig in instead (never a suicide march). Both break the stance cooldown (a survival shift is never time-locked).")]
        public void LosingHard_WithFallback_Retreats_CorneredDigsIn()
        {
            var c = BaseCtx();
            c.OwnStrength = 100; c.EnemyStrength = 400;               // 1:4 — losing hard
            c.HasFallback = true; c.FallbackRegion = 2;

            var retreat = GroundTactics.DecidePosture(c);
            Assert.That(retreat.Intent, Is.EqualTo(GroundIntent.Retreat));
            Assert.That(retreat.MoveTargetRegion, Is.EqualTo(2));
            Assert.That(retreat.StanceFamily, Is.EqualTo(GroundTactics.Defensive));
            Assert.That(retreat.Roe, Is.EqualTo(GroundEngagementStance.StandOff));
            Assert.That(retreat.BreakGlass, Is.True, "a fighting withdrawal is never blocked by the stance cooldown");

            c.HasFallback = false; c.FallbackRegion = -1;             // nowhere to run
            var cornered = GroundTactics.DecidePosture(c);
            Assert.That(cornered.Intent, Is.EqualTo(GroundIntent.Hold), "cornered units fight, they don't march into the enemy");
            Assert.That(cornered.MoveTargetRegion, Is.EqualTo(-1));
            Assert.That(cornered.StanceFamily, Is.EqualTo(GroundTactics.Defensive));
            Assert.That(cornered.Roe, Is.EqualTo(GroundEngagementStance.HoldGround));
            Assert.That(cornered.BreakGlass, Is.True);
            Log($"1:4 → retreat→{retreat.MoveTargetRegion + 1}; cornered → {cornered.Intent}");
        }

        [Test]
        [Description("§4.4 — at rough parity (odds below the personality bar but not outnumbered) → Balanced + StandOff (probe with the range advantage).")]
        public void Parity_ProbesBalancedStandOff()
        {
            var c = BaseCtx();
            c.OwnStrength = 100; c.EnemyStrength = 100;               // 1:1, neutral needs 1.5 → not enough to commit

            var p = GroundTactics.DecidePosture(c);
            Assert.That(p.StanceFamily, Is.EqualTo(GroundTactics.Balanced));
            Assert.That(p.Roe, Is.EqualTo(GroundEngagementStance.StandOff));
            Log($"parity 100v100 → {p.StanceFamily}/{p.Roe}: {p.Reason}");
        }

        [Test]
        [Description("§4.5 — personality BITES: at the SAME odds a BOLD faction (Risk 1, commits at parity) attacks where a CAUTIOUS one (Risk 0, demands 2x) refuses and only probes — the SAME CombatRisk curve the fleet AI uses.")]
        public void PersonalityBites_BoldCommitsWhereCautiousRefuses()
        {
            var c = BaseCtx();
            c.OwnStrength = 150; c.EnemyStrength = 100;               // 1.5:1 — between parity (bold) and 2x (cautious)

            c.RiskTrait = 1.0;                                        // bold → required 1.0 → commits
            var bold = GroundTactics.DecidePosture(c);
            Assert.That(bold.StanceFamily, Is.EqualTo(GroundTactics.Offensive), "the bold faction presses at 1.5:1");

            c.RiskTrait = 0.0;                                        // cautious → required 2.0 → refuses
            var cautious = GroundTactics.DecidePosture(c);
            Assert.That(cautious.StanceFamily, Is.EqualTo(GroundTactics.Balanced), "the cautious faction probes at the same odds");
            Log($"1.5:1 → bold {bold.StanceFamily} vs cautious {cautious.StanceFamily} (the curve bites)");
        }

        [Test]
        [Description("§4.6 — BLIND (an un-scouted neighbour) biases cautious: odds that would commit while seeing become a probe when blind (what you can't see can hurt you).")]
        public void Blind_BiasesCautious()
        {
            var c = BaseCtx();
            c.OwnStrength = 150; c.EnemyStrength = 100; c.RiskTrait = 0.5;  // needs 1.5 → 150 exactly commits WHILE SEEING

            var seeing = GroundTactics.DecidePosture(c);
            Assert.That(seeing.StanceFamily, Is.EqualTo(GroundTactics.Offensive), "with a clear picture it commits");

            c.Blind = true;                                          // required x1.5 → 2.25 → 150 no longer enough
            var blind = GroundTactics.DecidePosture(c);
            Assert.That(blind.StanceFamily, Is.EqualTo(GroundTactics.Balanced), "blind on the enemy → probe, don't charge");
            Log($"same 1.5:1 → seeing {seeing.StanceFamily} vs blind {blind.StanceFamily}");
        }

        [Test]
        [Description("Dry ammo is NEVER Offensive: a 3:1 edge that would press becomes a probe once the guns run dry (a silent gun line doesn't charge).")]
        public void DryAmmo_NeverOffensive()
        {
            var c = BaseCtx();
            c.OwnStrength = 300; c.EnemyStrength = 100; c.HasAmmoWeapons = true; c.AmmoFraction = 1.0;

            var wet = GroundTactics.DecidePosture(c);
            Assert.That(wet.StanceFamily, Is.EqualTo(GroundTactics.Offensive), "3:1 with ammo → presses");

            c.AmmoFraction = 0.02;                                   // below DryAmmoThreshold
            var dry = GroundTactics.DecidePosture(c);
            Assert.That(dry.StanceFamily, Is.Not.EqualTo(GroundTactics.Offensive), "dry → never Offensive");
            Assert.That(dry.StanceFamily, Is.EqualTo(GroundTactics.Balanced));
            Log($"3:1 → wet {wet.StanceFamily}, dry {dry.StanceFamily}");
        }

        // ───────────────────────── the fog-honest read (GroundThreat) ─────────────────────────

        [Test]
        [Description("GroundThreat is FOG-HONEST: an enemy in an UN-detected adjacent region counts ZERO; one in a region the viewer holds/scouts counts; revealing a hidden region changes the read (the recon→decision loop).")]
        public void GroundThreat_CountsOnlyDetectedEnemy_RevealChangesTheRead()
        {
            const int viewer = 1, enemy = 2;
            // region 0 (viewer-held) neighbours region 1 and region 2, both enemy-owned.
            var r0 = new Region { OwnerFactionID = viewer, Neighbors = new List<int> { 1, 2 } };
            var r1 = new Region { OwnerFactionID = enemy,  Neighbors = new List<int> { 0 } };
            var r2 = new Region { OwnerFactionID = enemy,  Neighbors = new List<int> { 0 } };
            var regionsDB = new PlanetRegionsDB(new List<Region> { r0, r1, r2 });

            var forces = new GroundForcesDB();
            forces.Units.Add(new GroundUnit { FactionOwnerID = enemy, RegionIndex = 1, Attack = 50, Health = 100 });
            forces.Units.Add(new GroundUnit { FactionOwnerID = enemy, RegionIndex = 2, Attack = 70, Health = 100 });

            // Nothing revealed: both neighbours are enemy-owned + un-scouted → invisible.
            Assert.That(GroundThreat.DetectedEnemyStrength(forces, regionsDB, viewer, 0), Is.EqualTo(0.0),
                "an un-scouted enemy neighbour contributes nothing (fog)");

            regionsDB.RevealRegionFor(viewer, 2);                    // scout region 2
            Assert.That(GroundThreat.DetectedEnemyStrength(forces, regionsDB, viewer, 0), Is.EqualTo(70.0),
                "the scouted region's enemy is now counted; the un-scouted one still hidden");

            regionsDB.RevealRegionFor(viewer, 1);                    // scout region 1 too
            Assert.That(GroundThreat.DetectedEnemyStrength(forces, regionsDB, viewer, 0), Is.EqualTo(120.0),
                "both scouted → the full picture");

            Assert.That(GroundThreat.IsBlind((Entity)null, viewer, 0), Is.False,
                "IsBlind is defensively false on a null body (never throws in the hotloop)");
            Log("fog read: 0 (blind) → 70 (one scouted) → 120 (both)");
        }

        // ───────────────────────── the wire (order-ownership + byte-identity) ─────────────────────────

        [Test]
        [Description("The WIRE: GroundTacticalBrain.Run decides a posture for an order-free AI battalion (records a Reason), but leaves a battalion holding a PLAYER order ENTIRELY alone (the human is sovereign — §3.5). And the master flag defaults OFF (byte-identical).")]
        public void Wire_BrainSetsPosture_ButAPlayerOrderIsSacrosanct()
        {
            Assert.That(GroundForcesProcessor.EnableGroundTacticalAI, Is.False,
                "the tactical brain is default-OFF → a factory game / the test suite is byte-identical");

            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            int fid = s.Faction.Id;
            s.Faction.GetDataBlob<FactionInfoDB>().IsNPC = true;      // the brain only drives NPC factions

            var regions = body.GetDataBlob<PlanetRegionsDB>();

            var uA = GroundForces.RaiseUnit(body, Inf(), fid, 0);
            var alpha = GroundForces.CreateFormation(body, fid, "Alpha");
            GroundForces.AssignUnit(alpha, uA);                      // order-free → the brain may command it

            var uB = GroundForces.RaiseUnit(body, Inf(), fid, 0);
            var bravo = GroundForces.CreateFormation(body, fid, "Bravo");
            GroundForces.AssignUnit(bravo, uB);
            GroundForces.SetFormationOrder(bravo, GroundOrder.MoveRegion(1));  // a PLAYER order (default Issuer)
            Assert.That(bravo.Orders[0].Issuer, Is.EqualTo(GroundOrderIssuer.Player), "precondition: a client order is the player's");

            var forces = body.GetDataBlob<GroundForcesDB>();
            Assert.That(alpha.TacticalReason, Is.Null, "precondition: the brain hasn't run yet");

            GroundTacticalBrain.Run(body, forces, regions, body.StarSysDateTime);

            Assert.That(alpha.TacticalReason, Is.Not.Null, "the brain decided a posture for the order-free AI battalion (the AI-tape explain)");
            Assert.That(bravo.TacticalReason, Is.Null, "a battalion holding a PLAYER order is left entirely alone");
            Assert.That(bravo.Orders.Count, Is.GreaterThanOrEqualTo(1), "the player's order is untouched");
            Assert.That(bravo.Orders[0].Issuer, Is.EqualTo(GroundOrderIssuer.Player));
            Log($"wire: Alpha decided \"{alpha.TacticalReason}\"; Bravo (player order) left alone");
        }

        [Test]
        [Description("Audit M2 — posture hysteresis: a set stance HOLDS against tick-to-tick jitter, but a big odds swing or the min-hold elapsing releases it; the first set is never held.")]
        public void PostureHysteresis_HoldsAgainstJitter_ReleasesOnSwingOrTimeout()
        {
            var t0 = new System.DateTime(2050, 1, 1, 0, 0, 0);
            // First set: no current stance → never held (the battalion must be able to pick an initial posture).
            Assert.That(GroundTactics.ShouldHoldStance(false, System.DateTime.MinValue, 0.0, 1.0, t0), Is.False,
                "the first stance set is never suppressed");
            // A stance was just set at odds 1.0. One hour later the odds jitter to 1.05 (a hair) — HOLD (no flip).
            var oneHour = t0 + System.TimeSpan.FromHours(1);
            Assert.That(GroundTactics.ShouldHoldStance(true, t0, 1.0, 1.05, oneHour), Is.True,
                "small jitter within the band and inside the min-hold window holds the stance");
            // Same hour, but the odds SWING past the band (1.0 → 1.4) — RELEASE (a real change turns the line).
            Assert.That(GroundTactics.ShouldHoldStance(true, t0, 1.0, 1.4, oneHour), Is.False,
                "an odds swing past the band releases the hold even inside the min-hold window");
            // Small jitter again, but now the minimum hold has elapsed — RELEASE (the time gate opened).
            var afterHold = t0 + System.TimeSpan.FromHours(GroundTactics.MinHoldHours + 1);
            Assert.That(GroundTactics.ShouldHoldStance(true, t0, 1.0, 1.05, afterHold), Is.False,
                "once the minimum hold elapses even small jitter may change the stance");
            Log($"hysteresis: jitter@1h held, swing@1h released, jitter@{GroundTactics.MinHoldHours + 1}h released");
        }
    }
}
