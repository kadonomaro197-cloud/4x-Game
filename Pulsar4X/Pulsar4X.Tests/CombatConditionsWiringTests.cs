using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Galaxy;
using Pulsar4X.Hazards;
using Pulsar4X.Movement;
using Pulsar4X.Orbital;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// E-env slice 2b (OPERATION BLUEPRINT-TO-STEEL) — the LIVE wire that makes space combat environment-aware.
    /// Slice 1 built the <see cref="CombatConditions"/> reader; slice 2a added the accuracy hook to the shared kernel
    /// + the <see cref="FleetCombatStateDB.Conditions"/> storage. 2b (a) SEEDS a fleet's conditions from WHERE it
    /// fights at engagement start (<see cref="CombatEngagement.StartEngagement"/>) and (b) THREADS the defender's
    /// <c>Conditions.Accuracy</c> through the resolver's fire path (<c>ApplyCasualties → LandedFraction → kernel</c>),
    /// so a battle fought inside a nebula lands less fire. All behind <see cref="CombatEngagement.EnableCombatConditions"/>
    /// (default OFF → conditions stay Clean, accuracy 1.0 → byte-identical); these fixtures opt in and reset it in finally.
    /// </summary>
    [TestFixture]
    public class CombatConditionsWiringTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[cond-wire] " + m);

        private static Entity FirstStar(TestScenario s)
            => s.StartingSystem.GetAllDataBlobsOfType<StarInfoDB>().First().OwningEntity;

        private static Entity MakeFleet(TestScenario s, Entity faction, string name)
            => FleetFactory.Create(s.StartingSystem, faction.Id, name);

        /// <summary>A corvette under the fleet with a stamped, deterministic combat value (one always-hit beam).</summary>
        private static Entity AddShip(TestScenario s, Entity owner, Entity fleet, double evasion = 0,
            double firepower = 1e6, double toughness = 1e7)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns["default-ship-design-test-corvette"];
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "ship");
            ship.FactionOwnerID = owner.Id;
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(owner.Id, fleet, ship));

            var cv = new ShipCombatValueDB(firepower, toughness, 1.0) { Evasion = evasion };
            if (firepower > 0)   // firepower 0 => an UNARMED hull (a defender that only takes fire), like DodgeResolveTests.Hull
                cv.Weapons = new List<WeaponProfile> { new WeaponProfile(firepower, 3e8, 1.0, 1.0, 0) };  // beam: always-hit, range 0
            ship.SetDataBlob(cv);
            return ship;
        }

        private static CombatConditions Conditions(Entity fleet)
            => fleet.GetDataBlob<FleetCombatStateDB>().Conditions;

        [Test]
        [Description("SEED: with the flag ON, StartEngagement reads each fleet's battle conditions from WHERE it fights " +
                     "— inside a gas cloud, InHazard is true and Accuracy is cut below 1. With the flag OFF the conditions " +
                     "stay Clean (Accuracy 1.0), byte-identical.")]
        public void StartEngagement_InAHazard_SeedsCutConditions_FlagGated()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var blue = MakeFleet(s, s.Faction, "Blue");
            var red = MakeFleet(s, reds, "Red");
            var blueShip = AddShip(s, s.Faction, blue);
            AddShip(s, reds, red);

            // Wrap the fleets' position in a gas cloud so they fight INSIDE a hazard.
            var star = FirstStar(s);
            var starPos = star.GetDataBlob<PositionDB>().AbsolutePosition;
            var shipPos = blueShip.GetDataBlob<PositionDB>().AbsolutePosition;
            SpaceHazardFactory.CreateGasCloud(s.StartingSystem, star, shipPos - starPos, Distance.AuToMt(0.5));

            // Flag OFF → Clean (byte-identical).
            CombatEngagement.StartEngagement(blue, red);
            Assert.That(Conditions(blue).InHazard, Is.False, "flag off: conditions stay Clean, no hazard read");
            Assert.That(Conditions(blue).Accuracy, Is.EqualTo(1.0).Within(1e-9));

            // Clear the state, flag ON → the hazard is read into Conditions.
            blue.RemoveDataBlob<FleetCombatStateDB>();
            red.RemoveDataBlob<FleetCombatStateDB>();
            CombatEngagement.EnableCombatConditions = true;
            try
            {
                CombatEngagement.StartEngagement(blue, red);
                Log($"in-cloud conditions: InHazard={Conditions(blue).InHazard} Accuracy={Conditions(blue).Accuracy:0.###}");
                Assert.That(Conditions(blue).InHazard, Is.True, "flag on + in a gas cloud: the fight is inside a hazard");
                Assert.That(Conditions(blue).Accuracy, Is.LessThan(1.0), "the cloud cuts accuracy (a nebula is cover)");
                Assert.That(Conditions(red).InHazard, Is.True, "both fleets read the hazard they share");
            }
            finally { CombatEngagement.EnableCombatConditions = false; }
        }

        [Test]
        [Description("THREAD: accuracy is a to-hit modifier (like evasion) — it raises effective toughness " +
                     "(Toughness ÷ landed), so it changes how many SALVOS kill the ship, not the raw damage pool. " +
                     "With the flag ON, a defender whose Conditions.Accuracy is cut takes MORE salvos to kill (poor " +
                     "visibility lands fewer shots per salvo) than the same defender in Clean conditions. With the flag " +
                     "OFF the cut Conditions are ignored → byte-identical. (Mirrors DodgeResolveTests' run-until-dead " +
                     "idiom — accuracy and evasion share the same landed-fraction mechanism.)")]
        public void ApplyCasualties_ReadsConditionsAccuracy_LessFireLands_FlagGated()
        {
            // Salvos to kill ONE unarmed defender hull under a steady always-hit beam. accuracy cuts landed → raises
            // effective toughness → more salvos. Calibration-free: only the RELATION (jammed > clean, flag-off == clean).
            int StepsToKill(bool flagOn, double accuracy)
            {
                var s = TestScenario.CreateWithColony();
                var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
                var attacker = MakeFleet(s, s.Faction, "Attacker");
                var defender = MakeFleet(s, reds, "Defender");
                AddShip(s, s.Faction, attacker, evasion: 0, firepower: 1e6);          // armed always-hit beam
                var defShip = AddShip(s, reds, defender, evasion: 0, firepower: 0, toughness: 1e6);  // UNARMED hull

                CombatEngagement.EnableCombatConditions = flagOn;
                try
                {
                    CombatEngagement.StartEngagement(attacker, defender);   // clean space → Clean seed
                    // Override the DEFENDER's conditions to a jammed (cut-accuracy) environment.
                    defender.GetDataBlob<FleetCombatStateDB>().Conditions = CombatConditions.FromHazard(new HazardModifiers
                    {
                        InAnyHazard = true, SensorRangeMultiplier = accuracy, MoveSpeedMultiplier = 1.0,
                        WarpSpeedMultiplier = 1.0, DamagePerSecond = 0.0, BlindsSensors = false,
                    });
                    int steps = 0;
                    while (defShip.IsValid && defender.HasDataBlob<FleetCombatStateDB>() && steps < 5000)
                    {
                        CombatEngagement.StepEngagement(attacker, defender, 5.0);
                        steps++;
                    }
                    return steps;
                }
                finally { CombatEngagement.EnableCombatConditions = false; }
            }

            int clean = StepsToKill(flagOn: true, accuracy: 1.0);    // Accuracy 1.0 = full fire → dies fast
            int jammed = StepsToKill(flagOn: true, accuracy: 0.3);   // Accuracy 0.3 = a nebula cut → dies slower
            int flagOff = StepsToKill(flagOn: false, accuracy: 0.3); // cut ignored when the flag is off

            Log($"salvos to kill the defender — clean(acc 1.0)={clean}  jammed(acc 0.3)={jammed}  flag-off={flagOff}");
            Assert.That(clean, Is.GreaterThan(0).And.LessThan(5000), "sanity: the defender dies under full fire within the cap");
            Assert.That(jammed, Is.GreaterThan(clean),
                "flag on: a cut-accuracy defender takes MORE salvos to kill (poor visibility lands fewer shots) — the 2b thread");
            Assert.That(flagOff, Is.EqualTo(clean),
                "flag off: the cut Conditions are ignored → byte-identical to the full-fire (Clean) result");
        }

        // ─────────────────────────────────────────────────────────────────────────────────────────────────────────
        // E-env SLICE 3 — the remaining coefficients: ambient DoT (the environment itself GRINDS ships), the attacker's
        // Firepower cut, the defender's ShieldRegen suppression, and the defender's additive Cover. Each is threaded
        // into StepEngagementGroup; each is byte-identical flag-off (identity value). All these gauges are RELATIONAL
        // (config A vs config B), so the exact tuning constants can move without breaking them.
        // ─────────────────────────────────────────────────────────────────────────────────────────────────────────

        private static CombatConditions WithAmbient(double jps)     { var c = CombatConditions.Clean; c.InHazard = true; c.AmbientDoT_Jps = jps; return c; }
        private static CombatConditions WithFirepower(double f)     { var c = CombatConditions.Clean; c.InHazard = true; c.Firepower = f;       return c; }
        private static CombatConditions WithShieldRegen(double r)   { var c = CombatConditions.Clean; c.InHazard = true; c.ShieldRegen = r;     return c; }
        private static CombatConditions WithCover(double cov)       { var c = CombatConditions.Clean; c.InHazard = true; c.Cover = cov;         return c; }

        /// <summary>Salvos to kill ONE unarmed defender hull under a steady attacker, with each fleet's battle
        /// conditions set by <paramref name="configure"/> (called after StartEngagement seeds them Clean). Optionally
        /// stamps a shield pool on the defender, or gives the attacker a DODGEABLE slow slug (so evasion/cover bites —
        /// the default always-hit beam ignores evasion). Calibration-free: assertions compare RELATIONS between
        /// configs, never absolute counts.</summary>
        private static int StepsToKill(bool flagOn, System.Action<FleetCombatStateDB, FleetCombatStateDB> configure,
            double defToughness = 1e7, double defShieldCapacity = 0, double defShieldRegen = 0, bool dodgeableAttacker = false)
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            var attacker = MakeFleet(s, s.Faction, "Attacker");
            var defender = MakeFleet(s, reds, "Defender");
            var atkShip = AddShip(s, s.Faction, attacker, evasion: 0, firepower: 1e6);                 // armed
            var defShip = AddShip(s, reds, defender, evasion: 0, firepower: 0, toughness: defToughness); // UNARMED hull

            if (dodgeableAttacker)   // a slow slug (vel ≪ VelocityReference, tracking 0) — a nimble/covered target dodges it
                atkShip.GetDataBlob<ShipCombatValueDB>().Weapons =
                    new List<WeaponProfile> { new WeaponProfile(1e6, 1e4, 0.0, 1.0, 0) };
            if (defShieldCapacity > 0)   // internal setters, reachable via InternalsVisibleTo("Pulsar4X.Tests")
            {
                var dcv = defShip.GetDataBlob<ShipCombatValueDB>();
                dcv.ShieldCapacity_J = defShieldCapacity;
                dcv.ShieldRegen_Jps = defShieldRegen;
            }

            CombatEngagement.EnableCombatConditions = flagOn;
            try
            {
                CombatEngagement.StartEngagement(attacker, defender);   // clean space → Clean seed
                configure(attacker.GetDataBlob<FleetCombatStateDB>(), defender.GetDataBlob<FleetCombatStateDB>());
                int steps = 0;
                while (defShip.IsValid && defender.HasDataBlob<FleetCombatStateDB>() && steps < 5000)
                {
                    CombatEngagement.StepEngagement(attacker, defender, 5.0);
                    steps++;
                }
                return steps;
            }
            finally { CombatEngagement.EnableCombatConditions = false; }
        }

        [Test]
        [Description("AMBIENT DoT: a defender fighting INSIDE a hazard (AmbientDoT_Jps > 0) takes the environment's " +
                     "damage every step ON TOP of the enemy fire, so it dies in FEWER salvos than the same defender in " +
                     "clean space. Flag OFF → the ambient rate is ignored → byte-identical to clean.")]
        public void StepEngagement_AmbientDoT_KillsFaster_FlagGated()
        {
            int clean     = StepsToKill(flagOn: true,  (atk, def) => { });                       // no ambient
            int corroding = StepsToKill(flagOn: true,  (atk, def) => def.Conditions = WithAmbient(2e5)); // the nebula gnaws
            int flagOff   = StepsToKill(flagOn: false, (atk, def) => def.Conditions = WithAmbient(2e5)); // rate ignored

            Log($"salvos to kill — clean(0 DoT)={clean}  corroding(2e5 J/s DoT)={corroding}  flag-off={flagOff}");
            Assert.That(clean, Is.GreaterThan(0).And.LessThan(5000), "sanity: the defender dies under fire within the cap");
            Assert.That(corroding, Is.LessThan(clean),
                "flag on: ambient DoT adds undodgeable damage each step → the defender dies sooner — the slice-3 wire");
            Assert.That(flagOff, Is.EqualTo(clean),
                "flag off: the ambient rate is ignored → byte-identical to clean space");
        }

        [Test]
        [Description("MURK (no attacker): a fleet fighting inside a hazard with NOBODY shooting it this step STILL " +
                     "takes the ambient DoT and loses ships — the fix for the old bare `continue` that skipped a " +
                     "no-attacker fleet. Flag OFF → the fleet is untouched (byte-identical).")]
        public void StepEngagementGroup_AmbientDoT_HurtsAFleetWithNoAttacker_FlagGated()
        {
            Entity RunOneStep(bool flagOn)
            {
                var s = TestScenario.CreateWithColony();
                var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
                var lone = MakeFleet(s, s.Faction, "Lone");
                var other = MakeFleet(s, reds, "Other");
                var loneShip = AddShip(s, s.Faction, lone, evasion: 0, firepower: 0, toughness: 1e6); // unarmed, un-shot hull
                AddShip(s, reds, other, evasion: 0, firepower: 1e6);

                CombatEngagement.EnableCombatConditions = flagOn;
                try
                {
                    CombatEngagement.StartEngagement(lone, other);   // gives `lone` a FleetCombatStateDB
                    var c = CombatConditions.Clean; c.InHazard = true; c.AmbientDoT_Jps = 1e6; // 1e6 J/s × 5 s ≥ the 1e6 hull
                    lone.GetDataBlob<FleetCombatStateDB>().Conditions = c;
                    // Step ONLY `lone`, so it has NO attacker in this group — the murk path.
                    CombatEngagement.StepEngagementGroup(new List<Entity> { lone }, 5.0);
                    return loneShip;
                }
                finally { CombatEngagement.EnableCombatConditions = false; }
            }

            Assert.That(RunOneStep(flagOn: true).IsValid, Is.False,
                "flag on: the ambient DoT ALONE destroyed the un-shot hull (no attacker needed) — the murk-path fix");
            Assert.That(RunOneStep(flagOn: false).IsValid, Is.True,
                "flag off: no ambient → the un-shot hull is untouched (byte-identical to the old `continue`)");
        }

        [Test]
        [Description("FIREPOWER: an ATTACKER whose environment chokes its firepower (Conditions.Firepower < 1) lands " +
                     "less damage, so its defender takes MORE salvos to kill than an attacker in clean space. Flag OFF " +
                     "→ the cut is ignored → byte-identical.")]
        public void StepEngagement_ChokedFirepower_KillsSlower_FlagGated()
        {
            int clean   = StepsToKill(flagOn: true,  (atk, def) => { });                             // full firepower
            int choked  = StepsToKill(flagOn: true,  (atk, def) => atk.Conditions = WithFirepower(0.4)); // corona chokes it
            int flagOff = StepsToKill(flagOn: false, (atk, def) => atk.Conditions = WithFirepower(0.4)); // cut ignored

            Log($"salvos to kill — clean(fp 1.0)={clean}  choked(fp 0.4)={choked}  flag-off={flagOff}");
            Assert.That(clean, Is.GreaterThan(0).And.LessThan(5000));
            Assert.That(choked, Is.GreaterThan(clean),
                "flag on: a firepower-choked attacker lands less → the defender takes longer to kill — the slice-3 wire");
            Assert.That(flagOff, Is.EqualTo(clean), "flag off: the firepower cut is ignored → byte-identical");
        }

        [Test]
        [Description("COVER: a DEFENDER whose environment grants cover (Conditions.Cover raises evasion) dodges more of " +
                     "a dodgeable slug, so it takes MORE salvos to kill than the same defender in the open. Flag OFF → " +
                     "the cover is ignored → byte-identical. (Needs a dodgeable attacker — the always-hit beam ignores " +
                     "evasion, so cover would be invisible against it.)")]
        public void StepEngagement_EnvironmentalCover_DodgesMore_FlagGated()
        {
            int open    = StepsToKill(flagOn: true,  (atk, def) => { }, dodgeableAttacker: true);                       // no cover
            int covered = StepsToKill(flagOn: true,  (atk, def) => def.Conditions = WithCover(0.9), dodgeableAttacker: true); // debris
            int flagOff = StepsToKill(flagOn: false, (atk, def) => def.Conditions = WithCover(0.9), dodgeableAttacker: true);

            Log($"salvos to kill — open(cover 0)={open}  covered(cover 0.9)={covered}  flag-off={flagOff}");
            Assert.That(open, Is.GreaterThan(0).And.LessThan(5000));
            Assert.That(covered, Is.GreaterThan(open),
                "flag on: cover raises the defender's evasion → fewer slugs land → a slower kill — the slice-3 wire");
            Assert.That(flagOff, Is.EqualTo(open), "flag off: the cover is ignored → byte-identical");
        }

        [Test]
        [Description("SHIELD REGEN: a DEFENDER whose environment suppresses shield regen (Conditions.ShieldRegen 0, an " +
                     "ion storm) can't recharge, so its shield depletes and the hull dies SOONER than the same shielded " +
                     "defender at full regen. Flag OFF → regen runs at full (mult 1.0) → byte-identical to the full-regen " +
                     "case.")]
        public void StepEngagement_SuppressedShieldRegen_KillsFaster_FlagGated()
        {
            int regenFull       = StepsToKill(flagOn: true,  (atk, def) => def.Conditions = WithShieldRegen(1.0),
                                              defToughness: 1e6, defShieldCapacity: 4e6, defShieldRegen: 6e4);
            int regenSuppressed = StepsToKill(flagOn: true,  (atk, def) => def.Conditions = WithShieldRegen(0.0),
                                              defToughness: 1e6, defShieldCapacity: 4e6, defShieldRegen: 6e4);
            int flagOff         = StepsToKill(flagOn: false, (atk, def) => def.Conditions = WithShieldRegen(0.0),
                                              defToughness: 1e6, defShieldCapacity: 4e6, defShieldRegen: 6e4);

            Log($"salvos to kill — full-regen={regenFull}  suppressed-regen={regenSuppressed}  flag-off={flagOff}");
            Assert.That(regenFull, Is.GreaterThan(0).And.LessThan(5000), "sanity: even at full regen the shield eventually falls");
            Assert.That(regenSuppressed, Is.LessThan(regenFull),
                "flag on: suppressed regen → the shield depletes → the hull dies sooner — the slice-3 wire");
            Assert.That(flagOff, Is.EqualTo(regenFull),
                "flag off: regen runs at full (mult 1.0) → byte-identical to the full-regen case");
        }
    }
}
