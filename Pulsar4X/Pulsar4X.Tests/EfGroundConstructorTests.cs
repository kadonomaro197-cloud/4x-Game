using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.Storage;
using Pulsar4X.Ships;
using Pulsar4X.DataStructures;   // ComponentMountType
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION EARTHFALL — G1.1: the COMBAT-ENGINEER beachhead foundation. Two halves, both gauged here:
    ///
    ///   (a) <see cref="GroundConstructorAtb"/> — a ground-mountable field-constructor COMPONENT (a build-rate dial), so
    ///       a combat engineer is "a chassis carrying a constructor part," NOT a bespoke unit type (GroundCombat/CLAUDE.md
    ///       LOCKED PRINCIPLE). Test: the base-mod `ground-constructor` binds its atb from JSON with the template default
    ///       and mounts on a ground unit — the six-point / gotcha-10 sensor (a template or ctor-arity drift reds CI here
    ///       instead of crashing a player's New Game).
    ///
    ///   (b) <see cref="GroundParts"/> — SURFACE PARTS HAULAGE: crated component parts land onto a body's per-region
    ///       surface pool (<see cref="GroundForcesDB.SurfaceParts"/>) so an engineer can later assemble a footprint
    ///       building on site with no colony present. Tests: the parts land and are readable (the primitive), the pool is
    ///       save-safe (deep-copied), and the full haul draws crated parts from a ship's cargo and lands them (gated on
    ///       orbital control, short pool lands nothing).
    ///
    /// Engine-only → runs in CI. Byte-identical: nothing fields the constructor by default and the surface pool starts
    /// empty + is read/written only by the new helpers, so a stock game is unchanged.
    /// </summary>
    [TestFixture]
    public class EfGroundConstructorTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[ground-constructor] " + m);

        private static ComponentDesign Design(TestScenario s, string id)
            => (ComponentDesign)s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns[id];

        // ── (a) the atb, from JSON ────────────────────────────────────────────────────────────────────────────────

        [Test]
        [Description("The base-mod ground-constructor loads onto the start faction, binds a GroundConstructorAtb from JSON with the template's default build rate, and mounts on a ground unit — the six-point / gotcha-10 sensor.")]
        public void GroundConstructor_LoadsFromJson_BindsItsAtb_AndMountsOnGroundUnits()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns;

            Assert.That(designs.ContainsKey("default-design-ground-constructor"), Is.True,
                "the ground constructor loads (template + component design + earth.json StartingItems + ComponentDesigns wired up)");

            var ctor = (ComponentDesign)designs["default-design-ground-constructor"];
            Assert.That(ctor.HasAttribute<GroundConstructorAtb>(), Is.True,
                "the JSON groundConstructorArgs bound a GroundConstructorAtb (template→atb arity path works)");

            var atb = ctor.GetAttribute<GroundConstructorAtb>();
            Log($"constructor: build rate {atb.BuildRate:0} bp/day, mount {ctor.ComponentMountType}");
            Assert.That(atb.BuildRate, Is.EqualTo(100).Within(1e-9), "template default BuildRate bound through");
            Assert.That(ctor.ComponentMountType.HasFlag(ComponentMountType.GroundUnit), Is.True,
                "a field constructor mounts on a ground unit (the combat engineer is a chassis + this part, not a unit type)");
        }

        // ── (b) surface parts haulage — land + read + save-safe ───────────────────────────────────────────────────

        [Test]
        [Description("The AddParts primitive lands crated component parts onto a body's per-region surface pool, they read back per region + per design, and the pool is deep-copied (save-safe) — parts land and are readable.")]
        public void SurfaceParts_LandAndAreReadable_AndThePoolIsDeepCopied()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;

            // land two crates in region 0 and one (different design) in region 1
            Assert.That(GroundParts.AddParts(body, 0, "default-design-building-foundation", 2), Is.EqualTo(2));
            Assert.That(GroundParts.AddParts(body, 0, "default-design-building-foundation", 3), Is.EqualTo(5),
                "a second drop of the same design in the same region MERGES into the existing crate");
            Assert.That(GroundParts.AddParts(body, 1, "default-design-bunker", 1), Is.EqualTo(1));

            // readable per design + per region
            Assert.That(GroundParts.PartCount(body, 0, "default-design-building-foundation"), Is.EqualTo(5));
            Assert.That(GroundParts.PartCount(body, 1, "default-design-bunker"), Is.EqualTo(1));
            Assert.That(GroundParts.PartCount(body, 0, "default-design-bunker"), Is.EqualTo(0),
                "a design not landed in a region reads 0 (region-scoped)");

            var r0 = GroundParts.PartsInRegion(body, 0);
            Assert.That(r0.Count, Is.EqualTo(1));
            Assert.That(r0["default-design-building-foundation"], Is.EqualTo(5));
            Log($"region 0 holds: {string.Join(", ", r0.Select(kv => $"{kv.Value}x {kv.Key}"))}");

            // bad args add nothing (defensive)
            Assert.That(GroundParts.AddParts(body, 0, "default-design-building-foundation", 0), Is.EqualTo(0));
            Assert.That(GroundParts.AddParts(body, -1, "default-design-building-foundation", 1), Is.EqualTo(0));
            Assert.That(GroundParts.PartCount(body, 0, "default-design-building-foundation"), Is.EqualTo(5),
                "the bad-arg calls didn't mutate the pool");

            // SAVE-SAFE: the deep-copy clone carries the crates and is INDEPENDENT of the original
            Assert.That(body.TryGetDataBlob<GroundForcesDB>(out var forces), Is.True);
            var clone = (GroundForcesDB)forces.Clone();
            Assert.That(clone.SurfaceParts.Count, Is.EqualTo(2), "the clone carries both region crates");
            var clonedR0 = clone.SurfaceParts.First(p => p.RegionIndex == 0);
            Assert.That(clonedR0.Count, Is.EqualTo(5), "the clone's crate has the right count");

            GroundParts.AddParts(body, 0, "default-design-building-foundation", 10);   // mutate the ORIGINAL after cloning
            Assert.That(clonedR0.Count, Is.EqualTo(5), "the clone is a DEEP copy — mutating the original doesn't touch it");
            Assert.That(GroundParts.PartCount(body, 0, "default-design-building-foundation"), Is.EqualTo(15));
        }

        [Test]
        [Description("The full haul: a cargo ship parked at the body carrying crated parts lands them onto the surface (drawn from its hold, gated on orbital control); a short pool lands nothing and a bad ship is refused.")]
        public void LandPartsFromShip_HaulsCratedPartsOntoTheSurface_AndGuardsRefuse()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var part = Design(s, "default-design-building-foundation");   // haulable (general-storage + ShipCargo mount)

            // a cargo ship parked AT the body, carrying 3 crated foundations
            var ship = CargoShip(s, body, "Beachhead Lighter");
            SeedComponentCargo(s, ship, part, 3);
            var hold = ship.GetDataBlob<CargoStorageDB>();
            Assert.That(hold.GetUnitsStored(part, false), Is.EqualTo(3), "precondition: the ship carries 3 foundations");
            Assert.That(GroundTransport.ShipIsAtBody(ship, body), Is.True, "precondition: the ship is at the body");
            Assert.That(GroundTransport.HasOrbitalControl(ship, body), Is.True, "precondition: single faction → it holds the orbit");

            // land 2 of them into region 0
            Assert.That(GroundParts.LandPartsFromShip(ship, body, 0, "default-design-building-foundation", 2), Is.True,
                "the haul succeeds — the ship is at the body, holds the orbit, and carries enough parts");
            Assert.That(hold.GetUnitsStored(part, false), Is.EqualTo(1), "2 foundations were drawn from the hold");
            Assert.That(GroundParts.PartCount(body, 0, "default-design-building-foundation"), Is.EqualTo(2),
                "and 2 landed on the surface, readable per region");
            int landed = GroundParts.PartCount(body, 0, "default-design-building-foundation");
            Log($"hauled 2 foundations; ship hold now {hold.GetUnitsStored(part, false)}, surface region 0 now {landed}");

            // CHECK-THEN-CONSUME: asking for more than the hold carries lands NOTHING and drains NOTHING
            Assert.That(GroundParts.LandPartsFromShip(ship, body, 0, "default-design-building-foundation", 5), Is.False,
                "a short pool refuses the haul");
            Assert.That(hold.GetUnitsStored(part, false), Is.EqualTo(1), "the refused haul drained nothing (check-then-consume)");
            Assert.That(GroundParts.PartCount(body, 0, "default-design-building-foundation"), Is.EqualTo(2),
                "the refused haul landed nothing");

            // defensive guards
            Assert.That(GroundParts.LandPartsFromShip(null, body, 0, "default-design-building-foundation", 1), Is.False,
                "a null ship is refused, never throws");
            Assert.That(GroundParts.LandPartsFromShip(ship, body, 0, "not-a-real-design", 1), Is.False,
                "an unknown design id is refused");
        }

        // ── local fixture helpers (mirroring the CI-proven ConstructorTests pattern) ──────────────────────────────

        /// <summary>A start-faction ship that carries a cargo hold, parked at <paramref name="parent"/>.</summary>
        private static Entity CargoShip(TestScenario s, Entity parent, string name)
        {
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();
            foreach (var kv in factionInfo.ShipDesigns)
            {
                var candidate = ShipFactory.CreateShip(kv.Value, s.Faction, parent, name);
                if (candidate.HasDataBlob<CargoStorageDB>()) return candidate;
                candidate.Destroy();
            }
            return ShipFactory.CreateShip(factionInfo.ShipDesigns.Values.First(), s.Faction, parent, name);
        }

        /// <summary>Put <paramref name="count"/> units of a built component into the ship's hold, mounting warehouses
        /// until the general-storage store has room (so a test never fails on hold size rather than the logic).</summary>
        private static void SeedComponentCargo(TestScenario s, Entity ship, ComponentDesign comp, long count)
        {
            var warehouse = Design(s, "default-design-warehouse");
            var hold = ship.GetDataBlob<CargoStorageDB>();
            int guard = 0;
            while (hold.GetFreeUnitSpace(comp, true) < count && guard++ < 100)
                ship.AddComponent(warehouse);
            hold = ship.GetDataBlob<CargoStorageDB>();
            hold.AddCargoByUnit(comp, count);
        }
    }
}
