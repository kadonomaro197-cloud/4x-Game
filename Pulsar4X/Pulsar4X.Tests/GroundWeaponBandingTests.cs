using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Components;    // ComponentDesign
using Pulsar4X.Factions;      // FactionInfoDB
using Pulsar4X.Galaxy;        // PlanetRegionsFactory, PlanetRegionsDB, RegionFeature, RegionFeatureType
using Pulsar4X.GroundCombat;  // GroundForces, GroundUnitDesign, GroundUnitAssembly, GroundForcesProcessor, PlanetEnvironmentsDB

namespace Pulsar4X.Tests
{
    /// <summary>
    /// W-TRACK W2 — PER-WEAPON RANGE BANDING (docs/combat/GROUND-CLOSING-FIGHT-W-TRACK.md §W2). The payoff of W1's
    /// per-weapon loadout: the resolver now fires EACH weapon in ITS OWN hex range band, so a long-range cannon
    /// (range 3, undodgeable Artillery) reaches a CLOSING enemy while a short-range rifle (range 1, Ballistic) is still
    /// silent — then the rifle bands in once the enemy is close. The ground echo of a ship's per-weapon range bands
    /// (each ship weapon fires when its own <c>Range_m</c> reaches the closing gap): "a lascannon reaches before a
    /// chainsword."
    ///
    /// Byte-identity guard (verified across the whole suite): a monolithic / garrison unit (empty loadout) AND a
    /// SINGLE-weapon assembled unit both keep the collapsed fire — a one-mount loadout reproduces the old Attack/Range/
    /// Mode bit-for-bit — so every pre-W2 ground-combat gauge is unchanged. Engine-only → CI (`rest` shard). Mirrors the
    /// closing-fight idiom of <c>GroundForcesTests.RangeCombat_OutRangerHitsCloserUnitFirst_CloneVsZerg</c>.
    /// </summary>
    [TestFixture]
    public class GroundWeaponBandingTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[w2-banding] " + m);
        private const int Enemy = 990201;

        private const string Frame = "default-design-human-frame";
        private const string Rifle = "default-design-ground-rifle";   // range 1, Ballistic
        private const string Cannon = "default-design-ground-cannon";  // range 3, Artillery

        // A punching-bag target: fires nothing back (Attack 0) and can't die in one salvo (huge HP), so the target's
        // health drop reads the attacker's per-salvo output. Raw design → empty loadout (collapsed path — it fires 0).
        private static GroundUnitDesign PunchingBag() => new GroundUnitDesign
        {
            UniqueID = "w2-bag", Name = "Bag", UnitType = GroundUnitType.Infantry,
            Attack = 0, Defense = 0, HitPoints = 1_000_000, Range = 1,
            IndustryTypeID = "installation", ResourceCosts = new Dictionary<string, long>(),
        };

        [Test]
        [Description("A rifle(range 1)+cannon(range 3) unit deals damage to an enemy 2 hexes away (only the cannon reaches) "
                   + "and MORE when co-located (the rifle bands in too) — the resolver fires each weapon in its own range "
                   + "band, so the long gun reaches while the short gun is still silent.")]
        public void MultiWeaponUnit_LongGunReachesFar_ShortGunOnlyWhenClose()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.StartingBody;
            if (body.HasDataBlob<PlanetEnvironmentsDB>()) body.RemoveDataBlob<PlanetEnvironmentsDB>();   // isolate combat from attrition
            var regions = body.GetDataBlob<PlanetRegionsDB>().Regions;
            Assert.That(regions.Count, Is.GreaterThanOrEqualTo(2), "need two regions to run the far/near fights independently");
            // Neutral ground (no defender cover/fort bias) + identical open terrain in BOTH regions → the only difference
            // between the two fights is the hex distance (2 vs 0).
            regions[0].OwnerFactionID = -1; regions[1].OwnerFactionID = -1;
            regions[0].Features.Clear(); regions[0].Features.Add(new RegionFeature(RegionFeatureType.Plains, 1.0));
            regions[1].Features.Clear(); regions[1].Features.Add(new RegionFeature(RegionFeatureType.Plains, 1.0));

            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            ComponentDesign Part(string p) => (ComponentDesign)faction.IndustryDesigns[p];

            // an assembled TWO-weapon unit: rifle (range 1) + cannon (range 3) → a per-weapon loadout (W1).
            var design = GroundUnitAssembly.RegisterAssembledDesign(
                faction, "w2-riflecannon", "Rifle+Cannon",
                Part(Frame),
                new List<(ComponentDesign, int)> { (Part(Rifle), 1), (Part(Cannon), 1) });
            Assert.That(design.WeaponLoadout.Count, Is.EqualTo(2), "precondition: the attacker carries two weapons");

            // region 0 — FAR (distance 2): only the cannon (range 3) reaches; the rifle (range 1) is out of its band.
            var attFar = GroundForces.RaiseUnit(body, design, s.Faction.Id, 0); attFar.HexQ = 0; attFar.HexR = 0;
            var bagFar = GroundForces.RaiseUnit(body, PunchingBag(), Enemy, 0); bagFar.HexQ = 2; bagFar.HexR = 0;

            // region 1 — NEAR (co-located): BOTH the cannon and the rifle reach.
            var attNear = GroundForces.RaiseUnit(body, design, s.Faction.Id, 1); attNear.HexQ = 0; attNear.HexR = 0;
            var bagNear = GroundForces.RaiseUnit(body, PunchingBag(), Enemy, 1); bagNear.HexQ = 0; bagNear.HexR = 0;

            // one salvo — both region fights resolve in the same tick.
            new GroundForcesProcessor().ProcessEntity(body, 3600);

            double dmgFar = bagFar.MaxHealth - bagFar.Health;    // cannon only (rifle silent at dist 2)
            double dmgNear = bagNear.MaxHealth - bagNear.Health; // cannon + rifle (both in range at dist 0)

            Assert.That(dmgFar, Is.GreaterThan(0),
                "the cannon (range 3) reaches the enemy 2 hexes away — the long gun fires while the short rifle is still silent");
            Assert.That(dmgNear, Is.GreaterThan(dmgFar),
                "co-located, the rifle (range 1) bands in too, so the enemy takes MORE than from the cannon alone");
            Log($"banding: far(d2, cannon only) {dmgFar:0} < near(d0, cannon+rifle) {dmgNear:0}");
        }
    }
}
