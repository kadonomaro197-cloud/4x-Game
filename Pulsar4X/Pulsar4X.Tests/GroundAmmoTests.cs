using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Galaxy;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Weapon-unification B (ammo depletion) — the DURABLE, resolver-agnostic ammo pool + operations. A raised unit
    /// snapshots its magazine capacity into an ammo pool; firing drains it; DRY silences its ammo weapons (checked here
    /// via <see cref="GroundAmmo.IsDry"/>); a resupply tops it back up. The in-combat drain call site + silence-when-dry
    /// read ride the resolver merge (next branch); this proves the pool math that both the current and merged resolvers
    /// use. Engine-only → runs in CI. Design: docs/WEAPON-UNIFICATION-DESIGN.md P2c.
    /// </summary>
    [TestFixture]
    public class GroundAmmoTests
    {
        private static GroundUnitDesign Design(string id, double ammoKg) => new GroundUnitDesign
        {
            UniqueID = id, Name = id, UnitType = GroundUnitType.Infantry,
            Attack = 100, HitPoints = 100, AmmoCapacity_kg = ammoKg,
            IndustryTypeID = "installation", ResourceCosts = new Dictionary<string, long>(),
        };

        [Test]
        [Description("B: a raised unit snapshots its magazine capacity into an ammo pool; consume drains, over-consume floors + goes DRY, refill tops it; a unit with no magazine never runs dry.")]
        public void AmmoPool_SnapshotsFromDesign_DrainsRefills_AndDryOnlyWithAMagazine()
        {
            var s = TestScenario.CreateWithColony();

            // a unit with a 500 kg magazine musters full
            var unit = GroundForces.RaiseUnit(s.StartingBody, Design("gunner", 500), s.Faction.Id, 0);
            Assert.That(unit.MaxAmmo_kg, Is.EqualTo(500), "pool sized from the design's magazine capacity");
            Assert.That(unit.CurrentAmmo_kg, Is.EqualTo(500), "musters full");
            Assert.That(GroundAmmo.CarriesAmmo(unit), Is.True);
            Assert.That(GroundAmmo.IsDry(unit), Is.False);

            // firing drains it
            Assert.That(GroundAmmo.Consume(unit, 300), Is.EqualTo(300));
            Assert.That(unit.CurrentAmmo_kg, Is.EqualTo(200));
            Assert.That(GroundAmmo.Fraction(unit), Is.EqualTo(0.4).Within(1e-9));

            // over-consuming floors at 0 and goes DRY
            Assert.That(GroundAmmo.Consume(unit, 999), Is.EqualTo(200), "only the remaining 200 was fed");
            Assert.That(unit.CurrentAmmo_kg, Is.EqualTo(0));
            Assert.That(GroundAmmo.IsDry(unit), Is.True, "dry → its ammo weapons go silent");

            // a dry unit feeds nothing more
            Assert.That(GroundAmmo.Consume(unit, 50), Is.EqualTo(0), "no ammo to feed");

            // resupply tops it back to full
            Assert.That(GroundAmmo.Refill(unit), Is.EqualTo(500), "500 kg added back");
            Assert.That(unit.CurrentAmmo_kg, Is.EqualTo(500));
            Assert.That(GroundAmmo.IsDry(unit), Is.False);

            // a unit with NO magazine never runs dry (energy/melee unit — nothing to deplete)
            var noMag = GroundForces.RaiseUnit(s.StartingBody, Design("rifleman", 0), s.Faction.Id, 0);
            Assert.That(noMag.MaxAmmo_kg, Is.EqualTo(0));
            Assert.That(GroundAmmo.CarriesAmmo(noMag), Is.False);
            Assert.That(GroundAmmo.IsDry(noMag), Is.False, "no magazine → never dry");
            Assert.That(GroundAmmo.Fraction(noMag), Is.EqualTo(1.0));
            Assert.That(GroundAmmo.Consume(noMag, 100), Is.EqualTo(0), "no pool to draw from");
        }

        [Test]
        [Description("B resupply (manual): a dry unit on FRIENDLY-held ground rearms to full; on enemy/neutral ground it can't (v1 source = your own territory).")]
        public void Resupply_RefillsOnFriendlyGround_NotOnEnemyGround()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.StartingBody;
            var regions = body.GetDataBlob<PlanetRegionsDB>().Regions;

            var unit = GroundForces.RaiseUnit(body, Design("gunner", 500), s.Faction.Id, 0);
            GroundAmmo.Consume(unit, 500);
            Assert.That(GroundAmmo.IsDry(unit), Is.True, "fired dry");

            // friendly-held region 0 -> a depot rearms it
            regions[0].OwnerFactionID = s.Faction.Id;
            Assert.That(GroundForces.ResupplyUnit(body, unit), Is.EqualTo(500), "rearmed to full on friendly ground");
            Assert.That(unit.CurrentAmmo_kg, Is.EqualTo(500));
            Assert.That(GroundAmmo.IsDry(unit), Is.False);

            // drain again, flip region to enemy/neutral -> no depot, can't resupply
            GroundAmmo.Consume(unit, 500);
            regions[0].OwnerFactionID = -1;
            Assert.That(GroundForces.ResupplyUnit(body, unit), Is.EqualTo(0), "no depot on enemy/neutral ground");
            Assert.That(GroundAmmo.IsDry(unit), Is.True, "still dry - no resupply");
        }
    }
}
