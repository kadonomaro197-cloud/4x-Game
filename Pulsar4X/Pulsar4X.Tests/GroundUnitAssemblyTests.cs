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
    /// Design: docs/GROUND-UNIT-DESIGNER-DESIGN.md → §2 (emergence) + §4 (the gate).
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
    }
}
