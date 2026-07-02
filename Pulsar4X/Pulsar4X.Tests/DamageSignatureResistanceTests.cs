using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;
using Pulsar4X.Damage;   // DamageSignature(s), DamageFragment, DamageProcessor, DamageTools
using Pulsar4X.Orbital;  // Vector2

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge on the keystone's PAYOFF half: a hit now carries a <see cref="DamageSignature"/> (the shared
    /// hazard↔weapon damage flavour), and armour rated against that flavour takes LESS damage from it — proven
    /// through the REAL per-pixel sim (<see cref="DamageTools.DealDamageEnergyBeamSim"/>) on a REAL ship, the same
    /// path a hazard tick or a beam hit takes. This is "the armour material IS the counter" made literal.
    ///
    /// Sequential-suite note: the resistance test temporarily sets <c>SignatureResistance</c> on the shared
    /// material blueprints and restores them in a finally. NUnit runs fixtures sequentially here (no
    /// [Parallelizable] anywhere in the project), so the global touch is safe.
    /// </summary>
    [TestFixture]
    public class DamageSignatureResistanceTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[sig-resist] " + m);

        [Test]
        [Description("Wavelength → signature: 0 = Kinetic, UV (<400nm) = HardRadiation, visible/IR = Thermal — the " +
                     "classifier a beam uses to label its hit, aligned with the armour band cut at 400 nm.")]
        public void FromWavelength_ClassifiesIntoSignature()
        {
            Assert.That(DamageSignatures.FromWavelength_nm(0), Is.EqualTo(DamageSignature.Kinetic));
            Assert.That(DamageSignatures.FromWavelength_nm(150), Is.EqualTo(DamageSignature.HardRadiation));
            Assert.That(DamageSignatures.FromWavelength_nm(550), Is.EqualTo(DamageSignature.Thermal));
            Assert.That(DamageSignatures.FromWavelength_nm(10000), Is.EqualTo(DamageSignature.Thermal));
        }

        [Test]
        [Description("Armour rated against a damage flavour takes LESS damage from a hit of that flavour, through " +
                     "the real per-pixel sim on a real ship. The keystone payoff: signature-tuned armour is the counter.")]
        public void SignatureRatedArmour_TakesLessDamage_FromThatFlavour()
        {
            var s = TestScenario.CreateWithColony();
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();

            // A thermal (far-IR) hit, energy well above the per-pixel damage floor so the resistance scaling shows.
            // The sim is pure geometry (no RNG), so two fresh ships from the same design differ only by resistance.
            DamageFragment ThermalHit() => new DamageFragment
            {
                Velocity   = new Vector2(1, 1),
                Position   = (0, 0),
                Energy     = 1e6,
                Wavelength = 10000,                  // far-IR
                Signature  = DamageSignature.Thermal,
            };

            // Unrated: a fresh ship, default materials (SignatureResistance all 0).
            var shipA = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "Unrated");
            int dmgUnrated = DamageProcessor.OnTakingDamage(shipA, ThermalHit()).Damage;

            // Rated: harden EVERY material strongly against Thermal, fire the same hit at a fresh ship, then restore.
            var saved = DamageTools.DamageResistsLookupTable.Values
                .ToDictionary(m => m.IDCode, m => m.SignatureResistance);
            try
            {
                foreach (var m in DamageTools.DamageResistsLookupTable.Values)
                {
                    var arr = new float[6];
                    arr[(int)DamageSignature.Thermal] = 0.9f;   // 90% damage resistance to thermal
                    m.SignatureResistance = arr;
                }

                var shipB = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "ThermalRated");
                int dmgRated = DamageProcessor.OnTakingDamage(shipB, ThermalHit()).Damage;

                Log($"thermal hit: unrated armour took {dmgUnrated} dmg, thermal-rated took {dmgRated}");
                Assert.That(dmgUnrated, Is.GreaterThan(0),
                    "the unrated ship must take measurable damage, or the comparison proves nothing");
                Assert.That(dmgRated, Is.LessThan(dmgUnrated),
                    "thermal-rated armour must take less damage from a thermal hit than unrated armour");
            }
            finally
            {
                foreach (var kv in saved)
                    DamageTools.DamageResistsLookupTable[kv.Key].SignatureResistance = kv.Value;
            }
        }

        [Test]
        [Description("The NON-WAVELENGTH damage SITE: a Corrosive hit (no wavelength — can't use the per-pixel sim) is " +
                     "applied FLAT to the hull and reduced by the ship's armour-material resistance. Proves the trio's " +
                     "(Corrosive/EMStorm/Gravimetric) armour is the counter, through the real DamageProcessor path.")]
        public void NonWavelengthHit_FlatSite_RatedArmourTakesLess()
        {
            var s = TestScenario.CreateWithColony();
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();

            // A corrosive hit with NO wavelength — this routes through the flat non-wavelength site, not the per-pixel sim.
            DamageFragment CorrosiveHit() => new DamageFragment
            {
                Velocity  = new Vector2(1, 1),
                Position  = (0, 0),
                Energy    = 1e6,
                Signature = DamageSignature.Corrosive,
            };

            var shipA = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "Unrated");
            int dmgUnrated = DamageProcessor.OnTakingDamage(shipA, CorrosiveHit()).Damage;

            var saved = DamageTools.DamageResistsLookupTable.Values
                .ToDictionary(m => m.IDCode, m => m.SignatureResistance);
            try
            {
                foreach (var m in DamageTools.DamageResistsLookupTable.Values)
                {
                    var arr = new float[6];
                    arr[(int)DamageSignature.Corrosive] = 0.9f;   // 90% resistance to corrosive
                    m.SignatureResistance = arr;
                }

                var shipB = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "CorrosionRated");
                int dmgRated = DamageProcessor.OnTakingDamage(shipB, CorrosiveHit()).Damage;

                Log($"corrosive (non-wavelength) hit: unrated armour took {dmgUnrated}, corrosion-rated took {dmgRated}");
                Assert.That(dmgUnrated, Is.GreaterThan(0),
                    "the flat non-wavelength site must deposit measurable damage, or the comparison proves nothing");
                Assert.That(dmgRated, Is.LessThan(dmgUnrated),
                    "corrosion-rated armour must take less from a corrosive hit — the trio's armour is the counter");
            }
            finally
            {
                foreach (var kv in saved)
                    DamageTools.DamageResistsLookupTable[kv.Key].SignatureResistance = kv.Value;
            }
        }
    }
}
