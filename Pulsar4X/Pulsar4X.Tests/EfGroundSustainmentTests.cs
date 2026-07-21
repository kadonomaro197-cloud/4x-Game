using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.Galaxy;        // PlanetRegionsDB
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Operation Earthfall G2.3 — the SUSTAINMENT loop (ammo drain + resupply + standing upkeep). Wires the ammo pool,
    /// the depot resupply, and the upkeep bill into the live ground tick, so a garrison finally costs money and an
    /// ammo-fed force runs dry, resupplies at a depot, and dries again. Engine-only → CI (`rest` shard). Byte-identical
    /// for every existing unit (no default/garrison unit carries a magazine, so the ammo drain/resupply are no-ops;
    /// only the NEW upkeep VALUES change the economy — a deliberate change per the GroundUpkeep design).
    /// </summary>
    [TestFixture]
    public class EfGroundSustainmentTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[sustain] " + m);

        private static GroundUnitDesign Gunner() => new GroundUnitDesign
        { UniqueID = "efs-gunner", Name = "Gunner", UnitType = GroundUnitType.Infantry, Attack = 100, Defense = 10, HitPoints = 500, Range = 1 };

        private static GroundUnitDesign PassiveTarget() => new GroundUnitDesign
        { UniqueID = "efs-target", Name = "Target", UnitType = GroundUnitType.Infantry, Attack = 0, Defense = 0, HitPoints = 100000, Range = 1 };

        // ───────────────────────── (a) ammo drain + silence-when-dry ─────────────────────────

        [Test]
        [Description("G2.3a: a magazine-fed unit BURNS ammo as it fires in combat, and once DRY its ammo weapons go SILENT (the target takes no more damage) — a silent gun line doesn't charge. A unit with no magazine is unaffected.")]
        public void Ammo_DrainsWhenFiring_AndSilencesWhenDry()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            int fid = s.Faction.Id;
            const int enemyFid = 700123;
            var regions = body.GetDataBlob<PlanetRegionsDB>();
            regions.Regions[0].OwnerFactionID = -1;   // contested → no fortification bias, clean fire math

            var a = GroundForces.RaiseUnit(body, Gunner(), fid, 0);
            a.MaxAmmo_kg = 10; a.CurrentAmmo_kg = 10;                  // give it a magazine (an ammo-fed unit)
            var d = GroundForces.RaiseUnit(body, PassiveTarget(), enemyFid, 0);   // a co-located enemy target (fires nothing back)

            var proc = new GroundForcesProcessor();
            proc.ProcessEntity(body, 3600);                           // one salvo

            Assert.That(a.CurrentAmmo_kg, Is.LessThan(10.0), "the firing unit burned a salvo of ammo");
            Assert.That(d.Health, Is.LessThan(d.MaxHealth), "and it dealt damage while it had ammo");
            Log($"wet salvo: ammo {a.CurrentAmmo_kg:0.0}/10, target {d.Health:0}/{d.MaxHealth:0}");

            // Run the magazine dry and reset the target — a DRY unit is silent.
            a.CurrentAmmo_kg = 0;
            d.Health = d.MaxHealth;
            proc.ProcessEntity(body, 3600);
            Assert.That(d.Health, Is.EqualTo(d.MaxHealth), "a DRY ammo unit fires nothing — the target takes no damage");
            Log("dry salvo: target untouched (the gun line is silent)");
        }

        // ───────────────────────── (b) resupply at a depot ─────────────────────────

        [Test]
        [Description("G2.3b: a magazine-fed unit standing at a DEPOT (a friendly-held region that holds a base) auto-rearms to full each tick; the same unit in the open friendly field (no base) is NOT resupplied.")]
        public void Resupply_RefillsAtADepot_NotInTheOpenField()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            int fid = s.Faction.Id;
            var regions = body.GetDataBlob<PlanetRegionsDB>();
            Assert.That(regions.Regions.Count, Is.GreaterThanOrEqualTo(2), "the world has a depot region and a field region");

            regions.Regions[0].OwnerFactionID = fid;
            regions.Regions[0].InstallationIds.Add(999);   // a base sits here → region 0 is a depot
            regions.Regions[1].OwnerFactionID = fid;
            regions.Regions[1].InstallationIds.Clear();     // friendly-held but bare → the open field, no depot

            var atDepot = GroundForces.RaiseUnit(body, Gunner(), fid, 0);
            atDepot.MaxAmmo_kg = 10; atDepot.CurrentAmmo_kg = 2;
            var inField = GroundForces.RaiseUnit(body, Gunner(), fid, 1);
            inField.MaxAmmo_kg = 10; inField.CurrentAmmo_kg = 2;

            new GroundForcesProcessor().ProcessEntity(body, 3600);   // no enemy → only the resupply step runs

            Assert.That(atDepot.CurrentAmmo_kg, Is.EqualTo(10.0), "a unit at a depot rearms to full");
            Assert.That(inField.CurrentAmmo_kg, Is.EqualTo(2.0), "a unit in the open field (no base) is not resupplied");
            Log($"resupply: depot 2→{atDepot.CurrentAmmo_kg:0}, field stays {inField.CurrentAmmo_kg:0}");
        }

        // ───────────────────────── (c) upkeep values are now set ─────────────────────────

        [Test]
        [Description("G2.3c: the standing-army biller finally has a value to bill — a START-GARRISON unit carries HitPoint-scaled upkeep and an ASSEMBLED unit carries mass-scaled upkeep, snapshotted onto the raised unit.")]
        public void Upkeep_IsSetOnGarrisonAndAssembledUnits_SoTheBillerBills()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            var body = s.StartingBody;
            ComponentDesign Part(string id) => (ComponentDesign)faction.IndustryDesigns[id];

            // (c1) START GARRISON — raise it FIRST (RaiseForFactionColonies is idempotent: it skips a body that already
            //      has this faction's units). An Infantry garrison (500 HP) now bills 500 × the flagged per-HP rate.
            int raised = GroundStartGarrison.RaiseForFactionColonies(s.Game, s.Faction);
            Assert.That(raised, Is.GreaterThan(0), "the home garrison was raised");
            var forces = body.GetDataBlob<GroundForcesDB>();
            var inf = forces.Units.First(x => x.UnitType == GroundUnitType.Infantry);
            Assert.That(inf.UpkeepCredits, Is.EqualTo(500 * GroundStartGarrison.GarrisonUpkeepPerHitPoint).Within(1e-9),
                "a garrison unit now carries a standing upkeep (the biller was billing nobody before)");

            // (c2) ASSEMBLED unit — a player-designed unit now carries mass-scaled upkeep, snapshotted at raise.
            var design = GroundUnitAssembly.RegisterAssembledDesign(
                faction, "efs-upkeep-unit", "Upkeep Test Unit",
                Part("default-design-human-frame"),
                new List<(ComponentDesign, int)> { (Part("default-design-ground-constructor"), 1) });
            Assert.That(design.UpkeepCredits, Is.GreaterThan(0.0), "an assembled design now carries a standing upkeep");
            var u = GroundForces.RaiseUnit(body, design, s.Faction.Id, 0);
            Assert.That(u.UpkeepCredits, Is.EqualTo(design.UpkeepCredits).Within(1e-9),
                "the raised unit snapshots the design's upkeep (so GroundUpkeep bills it)");
            Log($"upkeep: garrison Infantry {inf.UpkeepCredits:0.0}/mo, assembled {u.UpkeepCredits:0.0}/mo");
        }
    }
}
