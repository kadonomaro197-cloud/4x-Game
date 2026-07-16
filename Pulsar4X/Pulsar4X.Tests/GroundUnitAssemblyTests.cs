using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// GROUND-UNIT DESIGNER track, slice G-D3 — THE ASSEMBLER. A ground unit's stats EMERGE from its parts (like a
    /// ship), and the one rule ships lack — the carry gate — is enforced: a bare human frame can't shoulder the heavy
    /// autocannon, but bolt on power armour (+strength) and it can (the developer's core "Space Marine" story, now a
    /// live, CI-tested rule). Uses the real base-mod parts through the faction's designs. Engine-only → runs in CI.
    /// Design: docs/economy/UNIVERSAL-ASSEMBLY-DESIGN.md → §2 (emergence) + §4 (the gate).
    /// </summary>
    [TestFixture]
    public class GroundUnitAssemblyTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[assemble] " + m);

        private static TestScenario _s;
        private static ComponentDesign Part(string id)
            => (ComponentDesign)_s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns[id];

        [Test]
        [Description("G-D3 emergence: a Guardsman (human frame + rifle + plating) assembles legally and its stats are the SUM of the parts — attack from the rifle, HP from frame + plating, reach from the weapon.")]
        public void Guardsman_AssemblesLegally_AndStatsEmergeFromParts()
        {
            _s = TestScenario.CreateWithColony();
            var frame = Part("default-design-human-frame");
            var parts = new List<(ComponentDesign, int)>
            {
                (Part("default-design-ground-rifle"), 1),
                (Part("default-design-ground-plating"), 1),
            };
            var r = GroundUnitAssembly.Compute(frame, parts);
            Log($"Guardsman: valid={r.Valid} atk={r.Attack:0} hp={r.HitPoints:0} range={r.Range} used={r.UsedCapacity:0}/{r.CarryCapacity:0}");

            Assert.That(r.Valid, Is.True, "the loadout fits the frame's carry budget");
            Assert.That(r.Attack, Is.EqualTo(40), "attack = the rifle (emergent)");
            Assert.That(r.Range, Is.EqualTo(1), "reach = the rifle");
            Assert.That(r.HitPoints, Is.EqualTo(350), "HP = frame 200 + plating 150 (emergent)");
            Assert.That(r.CarryClass, Is.EqualTo(GroundCarryClass.Personnel), "carry-class from the frame (→ troop bay)");
        }

        [Test]
        [Description("G-D3 the gate BLOCKS: a bare human frame cannot mount the heavy autocannon — it's over both the per-item weight limit and the total carry budget.")]
        public void BareHuman_CannotCarryTheAutocannon()
        {
            _s = TestScenario.CreateWithColony();
            var frame = Part("default-design-human-frame");
            var parts = new List<(ComponentDesign, int)> { (Part("default-design-ground-autocannon"), 1) };

            var r = GroundUnitAssembly.Compute(frame, parts);
            var probs = string.Join(" | ", r.Problems);
            Log($"bare+autocannon: valid={r.Valid} problems={probs}");
            Assert.That(r.Valid, Is.False, "a bare human can't shoulder a 120-mass autocannon (strength 100, max-item 50)");
            Assert.That(r.Problems, Is.Not.Empty, "and it says why");
        }

        [Test]
        [Description("G-D3 the gate UNLOCKS (the Space Marine story): the SAME human frame + power armour CAN mount the autocannon — the augment's +300 strength raises the carry budget (100 → 400) and the max-item limit, so the heavy weapon now fits and the unit's attack is the autocannon's.")]
        public void PowerArmour_UnlocksTheAutocannon_OnTheSameFrame()
        {
            _s = TestScenario.CreateWithColony();
            var frame = Part("default-design-human-frame");
            var parts = new List<(ComponentDesign, int)>
            {
                (Part("default-design-power-armor"), 1),
                (Part("default-design-ground-autocannon"), 1),
            };
            var r = GroundUnitAssembly.Compute(frame, parts);
            Log($"Marine: valid={r.Valid} cap={r.CarryCapacity:0} used={r.UsedCapacity:0} maxItem={r.MaxItemWeight:0} atk={r.Attack:0} hp={r.HitPoints:0}");

            Assert.That(r.CarryCapacity, Is.EqualTo(400), "power armour's +300 strength raised the budget from 100 to 400");
            Assert.That(r.Valid, Is.True, "so the SAME frame that couldn't carry the autocannon now can");
            Assert.That(r.Attack, Is.EqualTo(140), "and it hits with the autocannon (emergent)");
            Assert.That(r.HitPoints, Is.EqualTo(240), "HP = frame 200 × 1.2 toughness from the power armour");
        }

        [Test]
        [Description("G-D3b the SAME assembler builds a VEHICLE (essence-axis coverage): a Battle Tank = tracked vehicle frame + tank cannon + plating assembles legally, its big frame carries the heavy cannon a human never could, its stats emerge, and it's a Vehicle carry-class (hauled by a vehicle bay, not a troop bay).")]
        public void BattleTank_AssemblesOnAVehicleFrame_AndIsVehicleCarryClass()
        {
            _s = TestScenario.CreateWithColony();
            var frame = Part("default-design-vehicle-frame");
            var parts = new List<(ComponentDesign, int)>
            {
                (Part("default-design-ground-cannon"), 1),
                (Part("default-design-ground-plating"), 1),
            };
            var r = GroundUnitAssembly.Compute(frame, parts);
            Log($"Tank: valid={r.Valid} cap={r.CarryCapacity:0} used={r.UsedCapacity:0} atk={r.Attack:0} range={r.Range} hp={r.HitPoints:0} class={r.CarryClass}");

            Assert.That(r.Valid, Is.True, "the vehicle frame (strength 800) easily carries the 300-mass cannon");
            Assert.That(r.Attack, Is.EqualTo(220), "attack = the tank cannon (emergent)");
            Assert.That(r.Range, Is.EqualTo(3), "reach = the cannon");
            Assert.That(r.HitPoints, Is.EqualTo(1650), "HP = frame 1500 + plating 150");
            Assert.That(r.CarryClass, Is.EqualTo(GroundCarryClass.Vehicle), "a vehicle — hauled by a vehicle bay, not a troop bay");

            // and the cannon is beyond ANY infantry frame — the gate holds across scales
            var human = Part("default-design-human-frame");
            var onHuman = GroundUnitAssembly.Compute(human, new List<(ComponentDesign, int)> { (Part("default-design-ground-cannon"), 1) });
            Assert.That(onHuman.Valid, Is.False, "no human frame can shoulder a 300-mass tank cannon");
        }

        [Test]
        [Description("G-D3c the CONNECT: an assembly becomes a BUILDABLE unit and, when raised, the unit carries the EMERGENT stats. ToGroundUnitDesign(frame+parts) → a GroundUnitDesign with summed stats + costs → GroundForces.RaiseUnit → a GroundUnit whose attack/HP are the assembly's. The designer is now wired to a real unit on the ground.")]
        public void Assembly_BecomesABuildableUnit_ThatRaisesWithEmergentStats()
        {
            _s = TestScenario.CreateWithColony();
            var frame = Part("default-design-human-frame");
            var parts = new List<(ComponentDesign, int)>
            {
                (Part("default-design-ground-rifle"), 1),
                (Part("default-design-ground-plating"), 1),
            };

            var design = GroundUnitAssembly.ToGroundUnitDesign("test-guardsman", "Guardsman", frame, parts);
            Assert.That(design.Attack, Is.EqualTo(40), "the buildable design carries the emergent attack");
            Assert.That(design.HitPoints, Is.EqualTo(350), "…and the emergent HP");
            Assert.That(design.Range, Is.EqualTo(1), "…and reach");
            Assert.That(design.UnitType, Is.EqualTo(GroundUnitType.Infantry), "a foot/personnel frame → Infantry (triangle still works)");
            Assert.That(design.ResourceCosts, Is.Not.Empty, "cost = the sum of the parts' costs (frame + rifle + plating)");
            Assert.That(design.IndustryPointCosts, Is.GreaterThan(0), "and build points sum too");

            // the full chain: build it → a real unit on the ground with the assembled stats
            var unit = GroundForces.RaiseUnit(_s.StartingBody, design, _s.Faction.Id, 0, "1st Guards");
            Assert.That(unit.Attack, Is.EqualTo(40), "the RAISED unit fights with the assembly's attack");
            Assert.That(unit.MaxHealth, Is.EqualTo(350), "and the assembly's HP — parts → design → unit, end to end");

            // a vehicle assembly derives Armor
            var tank = GroundUnitAssembly.ToGroundUnitDesign("test-tank", "Tank", Part("default-design-vehicle-frame"),
                new List<(ComponentDesign, int)> { (Part("default-design-ground-cannon"), 1) });
            Assert.That(tank.UnitType, Is.EqualTo(GroundUnitType.Armor), "a vehicle frame → Armor");
        }

        [Test]
        [Description("G-D3d mobility + damage-type axes: a Battlemech (walker frame + energy weapon + plating) assembles — the same parts bin now spans a striding energy-armed war-machine, extending coverage past infantry and tracked vehicles.")]
        public void Battlemech_AssemblesOnAWalkerFrame_WithAnEnergyWeapon()
        {
            _s = TestScenario.CreateWithColony();
            var r = GroundUnitAssembly.Compute(Part("default-design-walker-frame"), new List<(ComponentDesign, int)>
            {
                (Part("default-design-energy-weapon"), 1),
                (Part("default-design-ground-plating"), 1),
            });
            Log($"Mech: valid={r.Valid} atk={r.Attack:0} range={r.Range} hp={r.HitPoints:0} class={r.CarryClass}");
            Assert.That(r.Valid, Is.True, "the walker frame (strength 400) carries the plasma projector easily");
            Assert.That(r.Attack, Is.EqualTo(90), "attack = the energy weapon (emergent)");
            Assert.That(r.Range, Is.EqualTo(2), "reach = the energy weapon");
            Assert.That(r.HitPoints, Is.EqualTo(1150), "HP = walker frame 1000 + plating 150");
            Assert.That(r.CarryClass, Is.EqualTo(GroundCarryClass.Vehicle), "a walker is a vehicle-class unit");
        }

        [Test]
        [Description("System ① plumbing (slice B): evasion, shield, and damage-type now flow from the parts all the way to the RAISED unit. An evasive unit (reflex booster) carries its dodge; a shielded unit (shield generator) carries its soak pool; and the unit's damage-type is its heaviest weapon's flavour. Nothing reads these yet (that's the matrix, slice A) — this proves they're carried.")]
        public void Evasion_Shield_AndDamageType_FlowFromPartsToTheRaisedUnit()
        {
            _s = TestScenario.CreateWithColony();
            var human = Part("default-design-human-frame");

            // a dodger — reflex booster gives evasion; rifle is ballistic
            var dodger = GroundUnitAssembly.ToGroundUnitDesign("test-dodger", "Scout", human, new List<(ComponentDesign, int)>
            {
                (Part("default-design-ground-rifle"), 1),
                (Part("default-design-reflex-booster"), 1),
            });
            Assert.That(dodger.Evasion, Is.EqualTo(0.4), "the design carries the reflex booster's evasion");
            Assert.That(dodger.DamageType, Is.EqualTo(GroundWeaponMode.Ballistic), "its damage flavour is the rifle's (ballistic)");
            var dodgerUnit = GroundForces.RaiseUnit(_s.StartingBody, dodger, _s.Faction.Id, 0, "Scout");
            Assert.That(dodgerUnit.Evasion, Is.EqualTo(0.4), "and the RAISED unit carries it — survivability-by-dodge is on the unit now");

            // a shielded energy trooper — shield generator gives a soak pool; energy weapon sets the flavour
            var shielded = GroundUnitAssembly.ToGroundUnitDesign("test-shielded", "Guardian", human, new List<(ComponentDesign, int)>
            {
                (Part("default-design-energy-weapon"), 1),
                (Part("default-design-shield-generator"), 1),
            });
            Assert.That(shielded.Shield, Is.EqualTo(150), "the design carries the shield generator's soak pool");
            Assert.That(shielded.DamageType, Is.EqualTo(GroundWeaponMode.Energy), "its damage flavour is the plasma weapon's (energy)");
            var shieldedUnit = GroundForces.RaiseUnit(_s.StartingBody, shielded, _s.Faction.Id, 0, "Guardian");
            Assert.That(shieldedUnit.Shield, Is.EqualTo(150), "and the RAISED unit carries the shield");
            Assert.That(shieldedUnit.DamageType, Is.EqualTo(GroundWeaponMode.Energy), "and its damage-type — the matrix (slice A) will read these");
        }

        [Test]
        [Description("System ① cradle-to-grave for ARMOUR: the base-mod plating part carries a Defense value that flows part → assembly → design → RAISED unit, and that flowed Defense actually bites in the resolver's own math (GroundDamageMatrix.ArmourSoak reduces a hit for the plated unit, not the bare one). This closes the vertical loop for flat armour — the third defence flavour is reachable from the designer, not just synthetic test units.")]
        public void Armour_Defense_FlowsFromThePlatingPart_ToTheRaisedUnit_AndBitesInCombat()
        {
            _s = TestScenario.CreateWithColony();
            var human = Part("default-design-human-frame");
            var plating = Part("default-design-ground-plating");
            double platingDef = plating.GetAttribute<GroundArmorAtb>().Defense;   // read the part's value — no hardcoded number
            Assert.That(platingDef, Is.GreaterThan(0), "precondition: the plating part actually carries damage mitigation (Defense)");

            // bare (frame + rifle) vs plated (frame + rifle + plating) — the ONLY difference is the armour part
            var bare = GroundUnitAssembly.ToGroundUnitDesign("test-bare", "Bare", human,
                new List<(ComponentDesign, int)> { (Part("default-design-ground-rifle"), 1) });
            var plated = GroundUnitAssembly.ToGroundUnitDesign("test-plated", "Plated", human,
                new List<(ComponentDesign, int)> { (Part("default-design-ground-rifle"), 1), (plating, 1) });

            Assert.That(bare.Defense, Is.EqualTo(0), "a frame + rifle contribute no armour — Defense is 0");
            Assert.That(plated.Defense, Is.EqualTo(platingDef), "the plating's Defense is the design's armour (frame + rifle add none)");

            // …and it flows to the RAISED unit (the build-time snapshot the resolver reads)
            var platedUnit = GroundForces.RaiseUnit(_s.StartingBody, plated, _s.Faction.Id, 0, "Plated");
            var bareUnit = GroundForces.RaiseUnit(_s.StartingBody, bare, _s.Faction.Id, 1, "Bare");
            Assert.That(platedUnit.Defense, Is.EqualTo(platingDef), "the raised plated unit carries the armour — part → design → unit");
            Assert.That(bareUnit.Defense, Is.EqualTo(0), "the raised bare unit has none");

            // …and that flowed Defense actually reduces a hit in the resolver's own armour math
            const double oneHit = 20.0;
            double platedTakes = GroundDamageMatrix.ArmourSoak(platedUnit.Defense, oneHit);
            double bareTakes = GroundDamageMatrix.ArmourSoak(bareUnit.Defense, oneHit);
            Assert.That(platedTakes, Is.LessThan(bareTakes), "the plated unit soaks part of each incoming volley; the bare one takes it full");
            Assert.That(bareTakes, Is.EqualTo(oneHit), "no armour → full damage");
            Log($"armour cradle-to-grave: plating Defense {platingDef:0} → unit → a {oneHit:0} hit lands as {platedTakes:0} (plated) vs {bareTakes:0} (bare)");
        }

        [Test]
        [Description("⚙3 Defense armour NATURE cradle-to-grave: the base-mod Ablative Laminate is tuned against a damage NATURE — its GroundArmorAtb binds VsEnergy>1 / VsKinetic<1 from JSON (the six-point / 7-arg-ctor sensor), the assembler flows that part → design → raised unit (ArmourVs*), and in the resolver's own soak an ENERGY hit lands LESS on the ablative unit than on an identical composite-plated unit of EQUAL Defense (it shrugs lasers), while a KINETIC slug lands MORE (the light laminate is thin vs a slug). A plain plate reads natureFactor 1.0 → byte-identical. This closes the armour-nature loop: a build decides 'armour against WHAT'.")]
        public void ArmourNature_AblativeLaminate_ShrugsEnergy_ThinsVsKinetic_CradleToGrave()
        {
            _s = TestScenario.CreateWithColony();
            var human = Part("default-design-human-frame");
            var rifle = Part("default-design-ground-rifle");
            var composite = Part("default-design-ground-plating");
            var ablative = Part("default-design-ablative-plating");

            // The ablative part's nature dials bound from JSON through the real 7-arg NCalc ctor (gotcha #10 sensor).
            var abAtb = ablative.GetAttribute<GroundArmorAtb>();
            Assert.That(abAtb.VsEnergy, Is.GreaterThan(1.0), "ablative is TUNED vs energy (binds from JSON)");
            Assert.That(abAtb.VsKinetic, Is.LessThan(1.0), "…and a POOR match vs a kinetic slug");
            Assert.That(abAtb.Defense, Is.EqualTo(composite.GetAttribute<GroundArmorAtb>().Defense),
                "same mitigation as composite plating — the NATURE is the only difference (a clean comparison)");

            var compDesign = GroundUnitAssembly.ToGroundUnitDesign("test-comp", "Composite", human,
                new List<(ComponentDesign, int)> { (rifle, 1), (composite, 1) });
            var ablDesign = GroundUnitAssembly.ToGroundUnitDesign("test-abl", "Ablative", human,
                new List<(ComponentDesign, int)> { (rifle, 1), (ablative, 1) });

            // The nature flowed part → design (a single plate → its own values; the Defense-weighted average).
            Assert.That(ablDesign.ArmourVsEnergy, Is.EqualTo(abAtb.VsEnergy).Within(1e-9), "the design carries the ablative energy tuning");
            Assert.That(ablDesign.ArmourVsKinetic, Is.EqualTo(abAtb.VsKinetic).Within(1e-9));
            Assert.That(compDesign.ArmourVsEnergy, Is.EqualTo(1.0), "composite is a plain plate — neutral vs every nature (byte-identical)");

            // …and onto the raised units the resolver reads.
            var abl = GroundForces.RaiseUnit(_s.StartingBody, ablDesign, _s.Faction.Id, 0, "Ablative");
            var comp = GroundForces.RaiseUnit(_s.StartingBody, compDesign, _s.Faction.Id, 1, "Composite");
            Assert.That(abl.ArmourResistFor(Pulsar4X.Combat.WeaponNature.Energy), Is.GreaterThan(1.0));
            Assert.That(comp.ArmourResistFor(Pulsar4X.Combat.WeaponNature.Energy), Is.EqualTo(1.0));

            // …and it BITES in the resolver's OWN soak: same Defense, same hit, only the nature factor differs.
            const double hit = 20.0;
            const int shots = 1; const double pen = 0;
            double ablVsEnergy = GroundDamageMatrix.ArmourSoak(abl.Defense, hit, shots, pen, abl.ArmourResistFor(Pulsar4X.Combat.WeaponNature.Energy));
            double compVsEnergy = GroundDamageMatrix.ArmourSoak(comp.Defense, hit, shots, pen, comp.ArmourResistFor(Pulsar4X.Combat.WeaponNature.Energy));
            double ablVsKinetic = GroundDamageMatrix.ArmourSoak(abl.Defense, hit, shots, pen, abl.ArmourResistFor(Pulsar4X.Combat.WeaponNature.Kinetic));
            double compVsKinetic = GroundDamageMatrix.ArmourSoak(comp.Defense, hit, shots, pen, comp.ArmourResistFor(Pulsar4X.Combat.WeaponNature.Kinetic));

            Assert.That(ablVsEnergy, Is.LessThan(compVsEnergy), "ablative shrugs off ENERGY — less lands than on identical plain plate");
            Assert.That(ablVsKinetic, Is.GreaterThan(compVsKinetic), "…but is thin vs a KINETIC slug — more lands than on plain plate");
            // The plain plate is byte-identical to the pre-nature soak (natureFactor 1.0).
            Assert.That(compVsEnergy, Is.EqualTo(GroundDamageMatrix.ArmourSoak(comp.Defense, hit)).Within(1e-12),
                "a plain plate is byte-identical to the old flat soak (natureFactor 1.0)");
            Log($"a {hit:0} hit → ablative lands {ablVsEnergy:0.0} (energy) / {ablVsKinetic:0.0} (kinetic); composite {compVsEnergy:0.0} / {compVsKinetic:0.0}");
        }

        [Test]
        [Description("⚙3 Defense armour ROCK-PAPER-SCISSORS: Reactive Plating is the KINETIC/EXPLOSIVE counterpart to ablative — its blocks defeat slugs and shaped charges (VsKinetic>1, VsExplosive>1) but a laser burns through the light casing (VsEnergy<1). So the armour CHOICE stacks against the THREAT: vs an energy weapon the ablative unit soaks best, vs a kinetic weapon the reactive unit soaks best — the matchup flips, making 'armour against WHAT' a real decision rather than a flat +. Same Defense on both, so nature is the only variable.")]
        public void ArmourNature_ReactiveVsAblative_IsAThreatMatchup()
        {
            _s = TestScenario.CreateWithColony();
            var human = Part("default-design-human-frame");
            var rifle = Part("default-design-ground-rifle");
            var ablative = Part("default-design-ablative-plating");
            var reactive = Part("default-design-reactive-plating");

            var reAtb = reactive.GetAttribute<GroundArmorAtb>();
            Assert.That(reAtb.VsKinetic, Is.GreaterThan(1.0), "reactive defeats a kinetic slug (binds from JSON)");
            Assert.That(reAtb.VsExplosive, Is.GreaterThan(1.0), "…and a shaped-charge / HE warhead");
            Assert.That(reAtb.VsEnergy, Is.LessThan(1.0), "…but a laser burns through the light casing");

            var ablDesign = GroundUnitAssembly.ToGroundUnitDesign("test-abl2", "Ablative", human,
                new List<(ComponentDesign, int)> { (rifle, 1), (ablative, 1) });
            var reDesign = GroundUnitAssembly.ToGroundUnitDesign("test-re", "Reactive", human,
                new List<(ComponentDesign, int)> { (rifle, 1), (reactive, 1) });
            var abl = GroundForces.RaiseUnit(_s.StartingBody, ablDesign, _s.Faction.Id, 0, "Ablative");
            var re = GroundForces.RaiseUnit(_s.StartingBody, reDesign, _s.Faction.Id, 1, "Reactive");

            const double hit = 20.0; const int shots = 1; const double pen = 0;
            double Lands(GroundUnit u, Pulsar4X.Combat.WeaponNature n)
                => GroundDamageMatrix.ArmourSoak(u.Defense, hit, shots, pen, u.ArmourResistFor(n));

            // The matchup FLIPS by threat — the whole point of the decision.
            Assert.That(Lands(abl, Pulsar4X.Combat.WeaponNature.Energy), Is.LessThan(Lands(re, Pulsar4X.Combat.WeaponNature.Energy)),
                "vs a laser, the ABLATIVE unit takes less — pick ablative against energy");
            Assert.That(Lands(re, Pulsar4X.Combat.WeaponNature.Kinetic), Is.LessThan(Lands(abl, Pulsar4X.Combat.WeaponNature.Kinetic)),
                "vs a slug, the REACTIVE unit takes less — pick reactive against kinetic");
            Log($"energy hit: abl {Lands(abl, Pulsar4X.Combat.WeaponNature.Energy):0.0} vs re {Lands(re, Pulsar4X.Combat.WeaponNature.Energy):0.0};  kinetic hit: abl {Lands(abl, Pulsar4X.Combat.WeaponNature.Kinetic):0.0} vs re {Lands(re, Pulsar4X.Combat.WeaponNature.Kinetic):0.0}");
        }

        [Test]
        [Description("The Clone-vs-Zergling proof: two units from the SAME parts bin that share almost no gameplay DNA. A Clone (human + plasma + plating + shield) is a ranged, durable, shielded soak-tank; a Zergling (swarm frame + claws + reflex) is a fragile, cheap, melee dodger. Both assemble legally; every System-① field is opposite.")]
        public void CloneTrooper_vs_Zergling_AreOppositeUnits_FromOneBin()
        {
            _s = TestScenario.CreateWithColony();

            var clone = GroundUnitAssembly.ToGroundUnitDesign("test-clone", "Clone Trooper",
                Part("default-design-human-frame"), new List<(ComponentDesign, int)>
            {
                (Part("default-design-energy-weapon"), 1),
                (Part("default-design-ground-plating"), 1),
                (Part("default-design-shield-generator"), 1),
            });
            var zergling = GroundUnitAssembly.ToGroundUnitDesign("test-zergling", "Zergling",
                Part("default-design-swarm-frame"), new List<(ComponentDesign, int)>
            {
                (Part("default-design-claw-weapon"), 1),
                (Part("default-design-reflex-booster"), 1),
            });
            Log($"Clone: hp={clone.HitPoints:0} atk={clone.Attack:0} range={clone.Range} shield={clone.Shield:0} dmg={clone.DamageType} | " +
                $"Zergling: hp={zergling.HitPoints:0} atk={zergling.Attack:0} range={zergling.Range} evasion={zergling.Evasion:0.##} dmg={zergling.DamageType}");

            // Clone — ranged, durable, shielded, energy
            Assert.That(clone.HitPoints, Is.EqualTo(350), "Clone: soak-tank HP (frame + plating)");
            Assert.That(clone.Range, Is.EqualTo(2), "Clone: fights at range");
            Assert.That(clone.Shield, Is.EqualTo(150), "Clone: survives by a shield pool");
            Assert.That(clone.DamageType, Is.EqualTo(GroundWeaponMode.Energy), "Clone: energy weapon");

            // Zergling — fragile, melee, dodgy, cheap
            Assert.That(zergling.HitPoints, Is.EqualTo(40), "Zergling: paper-thin");
            Assert.That(zergling.Range, Is.EqualTo(0), "Zergling: must close to melee");
            Assert.That(zergling.Evasion, Is.EqualTo(0.4), "Zergling: survives by dodging, not soaking");
            Assert.That(zergling.DamageType, Is.EqualTo(GroundWeaponMode.Melee), "Zergling: claws");

            // opposite on every survival + reach axis, and the Zergling costs a fraction of the Clone
            Assert.That(zergling.HitPoints, Is.LessThan(clone.HitPoints));
            Assert.That(zergling.Range, Is.LessThan(clone.Range));
            Assert.That(zergling.IndustryPointCosts, Is.LessThan(clone.IndustryPointCosts), "a Zergling is far cheaper — you field a thousand");
        }

        [Test]
        [Description("⚙3 Defense shield RECHARGE decision (capacity vs recharge — the ground twin of the ship shield's dial): the base-mod Ward Projector holds a SMALLER pool than the standard Shield Generator but recharges much faster (ShieldRegenFraction 1.0 vs the default 0.34), binding that dial from JSON through the real 6-arg ctor. It flows part → design → raised unit, so a warded unit recovers a bigger FRACTION of its shield per hour (back to full in ~1 hour) while the big generator recovers only ~34% — a real burst-durability-vs-one-hit-buffer choice. A standard-shield or shield-less unit reads 0.34 → byte-identical.")]
        public void ShieldRecharge_WardProjector_RechargesFasterThanTheBigGenerator()
        {
            _s = TestScenario.CreateWithColony();
            var human = Part("default-design-human-frame");
            var rifle = Part("default-design-ground-rifle");
            var bigShield = Part("default-design-shield-generator");
            var ward = Part("default-design-ward-projector");

            // The ward's recharge dial binds from JSON via the 6-arg ctor (gotcha #10 sensor); the standard generator
            // uses the 5-arg ctor so it defaults to 0.34 (byte-identical).
            var wardAtb = ward.GetAttribute<GroundAugmentAtb>();
            var bigAtb = bigShield.GetAttribute<GroundAugmentAtb>();
            Assert.That(wardAtb.ShieldRegenFraction, Is.GreaterThan(0.34), "the ward dials a fast recharge (binds from JSON)");
            Assert.That(bigAtb.ShieldRegenFraction, Is.EqualTo(0.34).Within(1e-9), "the standard generator keeps the old default (byte-identical)");
            Assert.That(wardAtb.Shield, Is.LessThan(bigAtb.Shield), "the ward's pool is smaller — the trade for fast recharge");

            var wardDesign = GroundUnitAssembly.ToGroundUnitDesign("test-ward", "Warded", human,
                new List<(ComponentDesign, int)> { (rifle, 1), (ward, 1) });
            var bigDesign = GroundUnitAssembly.ToGroundUnitDesign("test-big", "BigShield", human,
                new List<(ComponentDesign, int)> { (rifle, 1), (bigShield, 1) });
            var bareDesign = GroundUnitAssembly.ToGroundUnitDesign("test-bareS", "Bare", human,
                new List<(ComponentDesign, int)> { (rifle, 1) });

            Assert.That(wardDesign.ShieldRegenFraction, Is.EqualTo(wardAtb.ShieldRegenFraction).Within(1e-9), "recharge flows part → design");
            Assert.That(bigDesign.ShieldRegenFraction, Is.EqualTo(0.34).Within(1e-9));
            Assert.That(bareDesign.ShieldRegenFraction, Is.EqualTo(0.34).Within(1e-9), "a shield-less unit keeps the default (byte-identical, moot with no pool)");

            var wardUnit = GroundForces.RaiseUnit(_s.StartingBody, wardDesign, _s.Faction.Id, 0, "Warded");
            var bigUnit = GroundForces.RaiseUnit(_s.StartingBody, bigDesign, _s.Faction.Id, 1, "BigShield");
            Assert.That(wardUnit.ShieldRegenFraction, Is.EqualTo(wardAtb.ShieldRegenFraction).Within(1e-9), "…and onto the raised unit");

            // The resolver recovers Shield × ShieldRegenFraction × (dt/3600) per step. Over an hour, as a FRACTION of
            // each unit's OWN pool, the ward recovers more (it's the recharge that differs, not the capacity).
            const double oneHour = 3600.0;
            double wardFrac = wardUnit.ShieldRegenFraction * (oneHour / 3600.0); // = its regen fraction, capped at 1 by the pool
            double bigFrac = bigUnit.ShieldRegenFraction * (oneHour / 3600.0);
            Assert.That(wardFrac, Is.GreaterThan(bigFrac), "the ward recharges a bigger fraction of its pool per hour");
            Assert.That(System.Math.Min(1.0, wardFrac), Is.EqualTo(1.0).Within(1e-9), "the ward is back to full within the hour");
            Assert.That(bigFrac, Is.LessThan(1.0), "the big shield is still recharging (only ~34% back)");
            Log($"per hour: ward recovers {wardFrac:P0} of its {wardUnit.Shield:0} pool; big generator {bigFrac:P0} of its {bigUnit.Shield:0}");
        }
    }
}
