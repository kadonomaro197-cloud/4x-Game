using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Components;
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

        // (designId, expected UnitType, Attack, HitPoints, Range) — stats mirror GroundStartGarrison exactly.
        private static readonly (string id, GroundUnitType type, double attack, double hp, int range)[] Expected =
        {
            ("default-design-infantry",  GroundUnitType.Infantry,  100, 500, 1),
            ("default-design-armor",     GroundUnitType.Armor,     140, 700, 1),
            ("default-design-artillery", GroundUnitType.Artillery, 160, 400, 3),
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

                Assert.That(design.ComponentMountType.HasFlag(ComponentMountType.PlanetInstallation), Is.True,
                    $"{e.id}: mounts as a PlanetInstallation — so building it auto-installs on the colony and raises the unit");
            }
        }
    }
}
