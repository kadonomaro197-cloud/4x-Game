using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.Datablobs;   // ComponentInstancesDB (namespace ≠ folder)
using Pulsar4X.DataStructures;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// SHARED-DESIGNER track, slice A2 — the three base-mod ground-unit designs (infantry / armor / artillery) load
    /// from JSON through the REAL data path (template → NCalc → <see cref="GroundUnitAtb"/> via reflection), the same
    /// gotcha-10 sensor <see cref="RailgunWeaponTests"/> is for weapons. If the template args or the atb constructor
    /// drift, this (and <see cref="Pulsar4X.Tests.Modding.BaseModIntegrityTests"/>) go red in CI instead of crashing
    /// the developer's New Game. It proves the whole cradle rung: a ground unit is just a component with a
    /// <see cref="GroundUnitAtb"/> — so it rides the shared designer, is unlocked on the start colony, and (via the
    /// PlanetInstallation mount) auto-installs on the colony, which is what fires the raise-a-unit hook (slice A1).
    ///
    /// The combat stats asserted here are REUSED verbatim from the already-shipped start garrison
    /// (<see cref="GroundStartGarrison"/>) — no new balance numbers. Only the build COSTS are new (flagged for the
    /// developer). Engine-only → runs in CI. Design: docs/GROUND-COMBAT-MAP-DESIGN.md.
    /// </summary>
    [TestFixture]
    public class GroundUnitBaseModTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[ground-unit] " + m);

        // (designId, expected UnitType, Attack, HitPoints, Range, Penetration) — combat stats mirror GroundStartGarrison
        // exactly; Penetration (W1c) is the new armour-crack dial: infantry small arms 0, a tank's AP gun 20, artillery
        // HE 8 (flagged balance numbers).
        private static readonly (string id, GroundUnitType type, double attack, double hp, int range, double pen)[] Expected =
        {
            ("default-design-infantry",  GroundUnitType.Infantry,  100, 500, 1, 0),
            ("default-design-armor",     GroundUnitType.Armor,     140, 700, 1, 20),
            ("default-design-artillery", GroundUnitType.Artillery, 160, 400, 3, 8),
        };

        [Test]
        [Description("A2: the base-mod infantry/armor/artillery designs load onto the start colony's faction, each binds a GroundUnitAtb from JSON with the garrison's stats, and each mounts as a PlanetInstallation (so building one auto-installs on the colony and fires the raise-a-unit hook).")]
        public void GroundUnitDesigns_LoadFromJson_BindTheAtb_WithGarrisonStats()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns;

            foreach (var e in Expected)
            {
                Assert.That(designs.ContainsKey(e.id), Is.True,
                    $"{e.id} loads onto the faction (the JSON template + component design + earth.json entry wired up)");
                var design = designs[e.id] as ComponentDesign;
                Assert.That(design, Is.Not.Null, $"{e.id} is a ComponentDesign (rides the shared designer)");

                Assert.That(design.HasAttribute<GroundUnitAtb>(), Is.True,
                    $"{e.id}: the JSON groundUnitAtbArgs bound a GroundUnitAtb (gotcha-10 template→atb path works)");
                var atb = design.GetAttribute<GroundUnitAtb>();
                Log($"{e.id}: type={atb.UnitType} atk={atb.Attack:0} def={atb.Defense:0} hp={atb.HitPoints:0} rng={atb.Range} mount={design.ComponentMountType}");

                Assert.That(atb.UnitType, Is.EqualTo(e.type), $"{e.id}: unit type bound from the template");
                Assert.That(atb.Attack, Is.EqualTo(e.attack), $"{e.id}: attack matches the garrison stat");
                Assert.That(atb.HitPoints, Is.EqualTo(e.hp), $"{e.id}: HP matches the garrison stat");
                Assert.That(atb.Range, Is.EqualTo(e.range), $"{e.id}: strike range matches the garrison default");
                Assert.That(atb.Penetration, Is.EqualTo(e.pen), $"{e.id}: armour penetration bound from the template (W1c dial)");

                Assert.That(design.ComponentMountType.HasFlag(ComponentMountType.PlanetInstallation), Is.True,
                    $"{e.id}: mounts as a PlanetInstallation — so building it auto-installs on the colony and raises the unit");
            }
        }

        [Test]
        [Description("A1×A2 cradle-to-grave, through the REAL path: installing a real base-mod infantry component on the colony via Entity.AddComponent (the exact call ComponentDesign.OnConstructionComplete makes when a built PlanetInstallation auto-installs) raises an Infantry unit on the planet and leaves NO lingering component. Proves the raise+remove hook is safe when driven through AddComponent's attribute loop, not just called directly.")]
        public void BuildingAnInfantryComponent_ThroughAddComponent_RaisesAUnit_AndLeavesNoInstallation()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var infantry = (ComponentDesign)s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns["default-design-infantry"];
            var comps = s.Colony.GetDataBlob<ComponentInstancesDB>();

            int unitsBefore = body.TryGetDataBlob<GroundForcesDB>(out var pre) ? pre.Units.Count : 0;

            // the exact entry point OnConstructionComplete uses for an auto-installed PlanetInstallation
            var instance = new ComponentInstance(infantry);
            s.Colony.AddComponent(instance);

            Assert.That(body.TryGetDataBlob<GroundForcesDB>(out var forces), Is.True, "a roster exists on the body");
            Assert.That(forces.Units.Count, Is.EqualTo(unitsBefore + 1), "exactly one Infantry unit was raised by the build→deploy hook");
            var raised = forces.Units[forces.Units.Count - 1];
            Assert.That(raised.UnitType, Is.EqualTo(GroundUnitType.Infantry), "an Infantry unit (the design's type carried through)");
            Assert.That(raised.Attack, Is.EqualTo(100), "with the design's attack stat");

            Assert.That(comps.AllComponents.ContainsKey(instance.UniqueID), Is.False,
                "no lingering infantry INSTALLATION — the component deployed as a ground force and removed itself (safe mid-AddComponent-loop)");
        }

        [Test]
        [Description("W1c cradle-to-grave: a player-built ARMOR unit (the base-mod design, raised through the real AddComponent hook) carries the template's Penetration onto its GroundUnit, and that penetration actually CRACKS armour — vs a Defense-15 defender it lands in full where a normal (penetration-0) round is soaked. So the whole rung is real: designed → built → deployed → the AP unit beats plate that stops infantry.")]
        public void BuildingAnArmorUnit_CarriesPenetration_ThatCracksArmour()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var armor = (ComponentDesign)s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns["default-design-armor"];

            s.Colony.AddComponent(new ComponentInstance(armor));

            Assert.That(body.TryGetDataBlob<GroundForcesDB>(out var forces), Is.True, "a roster exists on the body");
            var tank = forces.Units[forces.Units.Count - 1];
            Assert.That(tank.UnitType, Is.EqualTo(GroundUnitType.Armor), "an Armor unit was raised");
            Assert.That(tank.Penetration, Is.EqualTo(20), "the built armor unit carries the template's AP penetration (design → GroundUnit)");

            // The penetration cracks plate a normal round bounces off — through the shared ground armour soak.
            const double defense = 15, oneHit = 100;
            double apLands = GroundDamageMatrix.ArmourSoak(defense, oneHit, tank.Penetration);
            double normalLands = GroundDamageMatrix.ArmourSoak(defense, oneHit, 0);
            Log($"vs Defense {defense}: the built AP tank (pen {tank.Penetration}) lands {apLands}, a normal round lands {normalLands}");
            Assert.That(apLands, Is.GreaterThan(normalLands),
                "the player-built AP unit cracks armour a normal round is soaked by — the cradle-to-grave payoff");
        }
    }
}
