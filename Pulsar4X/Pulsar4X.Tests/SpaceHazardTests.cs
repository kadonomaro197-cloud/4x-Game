using System;
using System.Linq;
using NUnit.Framework;
using System.Collections.Generic;
using Pulsar4X.Components;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Galaxy;
using Pulsar4X.Hazards;
using Pulsar4X.Movement;
using Pulsar4X.Orbital;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauges the space-hazard system (gas cloud + solar flare) — the "region of space that affects ships inside
    /// it" feature. Engine-only, so CI verifies the logic (the on-map visuals are client-side and CI-blind).
    /// </summary>
    [TestFixture]
    public class SpaceHazardTests
    {
        private static Entity FirstStar(TestScenario s)
            => s.StartingSystem.GetAllDataBlobsOfType<StarInfoDB>().First().OwningEntity;

        [Test]
        [Description("A gas cloud applies its effects to a point INSIDE its radius and nothing outside.")]
        public void GasCloud_AffectsInsideOnly()
        {
            var s = TestScenario.CreateWithColony();
            var star = FirstStar(s);
            var starPos = star.GetDataBlob<PositionDB>().AbsolutePosition;

            var offset = new Vector3(Distance.AuToMt(5), 0, 0);
            double radius = Distance.AuToMt(1.0);
            SpaceHazardFactory.CreateGasCloud(s.StartingSystem, star, offset, radius);

            var center = starPos + offset; // the cloud sits at star + offset

            var inside = SpaceHazardTools.CombinedAt(s.StartingSystem, center);
            Assert.IsTrue(inside.InAnyHazard, "Point at the cloud centre should be inside it.");
            Assert.Less(inside.SensorRangeMultiplier, 1.0, "Sensors should be cut inside the cloud.");
            Assert.Less(inside.MoveSpeedMultiplier, 1.0, "Movement should be slowed inside the cloud.");
            Assert.Less(inside.WarpSpeedMultiplier, 1.0, "Warp should be slowed inside the cloud.");
            Assert.Greater(inside.DamagePerSecond, 0.0, "The cloud should damage over time.");

            var outside = SpaceHazardTools.CombinedAt(s.StartingSystem, center + new Vector3(Distance.AuToMt(5), 0, 0));
            Assert.IsFalse(outside.InAnyHazard, "A point well outside the cloud should be unaffected.");
            Assert.AreEqual(1.0, outside.SensorRangeMultiplier, 1e-9);
            Assert.AreEqual(1.0, outside.MoveSpeedMultiplier, 1e-9);
        }

        [Test]
        [Description("A solar flare's radius grows from a point to its max at the halfway peak, then fades.")]
        public void FlareRadius_GrowsToPeakThenFades()
        {
            var haz = new SpaceHazardDB
            {
                IsTransient = true,
                MaxRadius_m = 1000.0,
                StartedAt = new DateTime(2050, 1, 1, 0, 0, 0),
                ExpiresAt = new DateTime(2050, 1, 1, 12, 0, 0),
            };

            double rStart = SpaceHazardProcessor.FlareRadiusAt(haz, haz.StartedAt);
            double rQuarter = SpaceHazardProcessor.FlareRadiusAt(haz, haz.StartedAt + TimeSpan.FromHours(3));
            double rPeak = SpaceHazardProcessor.FlareRadiusAt(haz, haz.StartedAt + TimeSpan.FromHours(6));
            double rThreeQ = SpaceHazardProcessor.FlareRadiusAt(haz, haz.StartedAt + TimeSpan.FromHours(9));
            double rEnd = SpaceHazardProcessor.FlareRadiusAt(haz, haz.ExpiresAt);

            Assert.AreEqual(1000.0, rPeak, 1.0, "Flare should reach its max radius at the halfway peak.");
            Assert.Greater(rPeak, rQuarter, "It should be growing before the peak.");
            Assert.Greater(rPeak, rThreeQ, "It should be fading after the peak.");
            Assert.Greater(rQuarter, rStart, "Start should be near a point.");
            Assert.LessOrEqual(rEnd, 1.0001, "It should be gone by the end.");
        }

        [Test]
        [Description("Placement: the home star is given recurring-flare weather on load.")]
        public void HomeStar_HasFlareWeather()
        {
            var s = TestScenario.CreateWithColony();
            bool hasFlareSource = s.StartingSystem.GetAllDataBlobsOfType<StarFlareSourceDB>().Any();
            Assert.IsTrue(hasFlareSource, "The home star should carry a StarFlareSourceDB (recurring solar flares).");
        }

        [Test]
        [Description("A solar flare blinds sensors in its area, and is removed from the game once it expires.")]
        public void Flare_BlindsThenExpires()
        {
            var s = TestScenario.CreateWithColony();
            var star = FirstStar(s);
            DateTime now = star.StarSysDateTime;

            // An already-expired flare (started 2h ago, lasted 1h) — so processing it should remove it.
            var flare = SpaceHazardFactory.CreateSolarFlare(
                s.StartingSystem, star, now - TimeSpan.FromHours(2), TimeSpan.FromHours(1), Distance.AuToMt(0.1));

            var starPos = star.GetDataBlob<PositionDB>().AbsolutePosition;
            var atStar = SpaceHazardTools.CombinedAt(s.StartingSystem, starPos);
            Assert.IsTrue(atStar.BlindsSensors, "A flare at the star should blind sensors there.");
            Assert.AreEqual(0.0, atStar.SensorRangeMultiplier, 1e-9, "Blinding means zero effective sensor range.");

            // Processing past its expiry removes it.
            new SpaceHazardProcessor().ProcessEntity(flare, 5);
            Assert.IsFalse(flare.IsValid, "An expired flare should be destroyed when processed.");
        }

        [Test]
        [Description("Every star gets a permanent corona danger zone (proximity-scaled heat damage); it bites near the star and not far out.")]
        public void StarCorona_DamagesNearTheStar()
        {
            var s = TestScenario.CreateWithColony();
            var star = FirstStar(s);
            var starPos = star.GetDataBlob<PositionDB>().AbsolutePosition;

            var coronas = s.StartingSystem.GetAllDataBlobsOfType<SpaceHazardDB>()
                .Where(h => h.HazardType == SpaceHazardType.StarCorona).ToList();
            Assert.IsNotEmpty(coronas, "The home star should have a corona danger zone.");
            var coronaHeat = coronas[0].Effects.First(e => e.Type == HazardEffectType.HeatDamage);
            Assert.IsTrue(coronaHeat.ScalesWithProximity, "Corona damage should scale with proximity.");
            Assert.Greater(coronaHeat.Magnitude, 0.0);

            var atStar = SpaceHazardTools.CombinedAt(s.StartingSystem, starPos);
            Assert.IsTrue(atStar.InAnyHazard, "Right at the star you should be inside the corona.");
            Assert.Greater(atStar.DamagePerSecond, 0.0, "The corona should be flagged as damaging.");

            // 10 AU out is far beyond the corona — no corona there (and Sol has no other load-time hazards).
            var farOut = SpaceHazardTools.CombinedAt(s.StartingSystem, starPos + new Vector3(Distance.AuToMt(10), 0, 0));
            Assert.IsFalse(farOut.InAnyHazard, "Far from the star you should be clear of the corona.");
        }

        [Test]
        [Description("Hazard damage carries a wavelength so the ARMOUR a ship is clad in is its defence — the player's agency. Corona heat is IR, flare radiation is UV (different bands → different shielding).")]
        public void HazardDamage_CarriesAnArmourResistableWavelength()
        {
            var s = TestScenario.CreateWithColony();
            var star = FirstStar(s);

            var corona = s.StartingSystem.GetAllDataBlobsOfType<SpaceHazardDB>()
                .First(h => h.HazardType == SpaceHazardType.StarCorona);
            var coronaHeat = corona.Effects.First(e => e.IsDamage);
            Assert.Greater(coronaHeat.Wavelength_nm, 0.0,
                "Corona damage must carry a wavelength so heat-reflective armour can resist it (else there's no counterplay).");

            var flare = SpaceHazardFactory.CreateSolarFlare(
                s.StartingSystem, star, star.StarSysDateTime, TimeSpan.FromHours(1), Distance.AuToMt(0.1))
                .GetDataBlob<SpaceHazardDB>();
            var flareDmg = flare.Effects.First(e => e.IsDamage);
            Assert.Greater(flareDmg.Wavelength_nm, 0.0, "Flare damage must also carry a wavelength.");

            // The corona (heat) lives in the infrared; the flare (radiation) in the ultraviolet — different bands,
            // so the armour material that beats one isn't automatically the one that beats the other.
            Assert.Less(flareDmg.Wavelength_nm, coronaHeat.Wavelength_nm,
                "Flare (UV/radiation) should be a shorter wavelength than corona (heat/IR).");
        }

        [Test]
        [Description("Resistance shrinks a hazard's stat-cut: no resistance leaves it unchanged, full resistance nearly negates it.")]
        public void ApplyResistance_ShrinksTheCut()
        {
            // A hazard cuts the stat to 0.35 (35%). With no resistance it stays 0.35.
            Assert.AreEqual(0.35, SpaceHazardTools.ApplyResistance(0.35, 0.0), 1e-9);
            // With 0.9 resistance only a tenth of the cut remains → ~0.935.
            Assert.AreEqual(0.935, SpaceHazardTools.ApplyResistance(0.35, 0.9), 1e-9);
            // A full blind (mult 0) with 0.9 resistance is reduced to a 0.9 multiplier — you can still see.
            Assert.AreEqual(0.9, SpaceHazardTools.ApplyResistance(0.0, 0.9), 1e-9);
            // No cut to begin with stays no cut.
            Assert.AreEqual(1.0, SpaceHazardTools.ApplyResistance(1.0, 0.5), 1e-9);
        }

        [Test]
        [Description("A hazard authored purely as JSON (Alpha Centauri's gas cloud) loads with its typed effects.")]
        public void JsonAuthoredHazard_LoadsFromSystemBlueprint()
        {
            var s = TestScenario.CreateWithColony();
            var blueprint = s.Game.StartingGameData.Systems["system-alpha-centauri"];
            var ac = StarSystemFactory.LoadFromBlueprint(s.Game, blueprint);

            var clouds = ac.GetAllDataBlobsOfType<SpaceHazardDB>()
                .Where(h => h.HazardType == SpaceHazardType.GasCloud).ToList();
            Assert.IsNotEmpty(clouds, "The JSON-authored gas cloud should load in Alpha Centauri.");

            var cloud = clouds[0];
            Assert.Less(cloud.MultiplierFor(HazardEffectType.SensorJam), 1.0, "Cloud should jam sensors per its JSON.");
            Assert.Less(cloud.MultiplierFor(HazardEffectType.WarpInhibit), 1.0, "Cloud should inhibit warp per its JSON.");
            Assert.IsTrue(cloud.Effects.Any(e => e.Type == HazardEffectType.HeatDamage),
                "Cloud should carry its heat-damage effect from JSON.");
        }

        [Test]
        [Description("Cradle-to-grave: the buildable Sensor Hardening Module unlocks on the faction and its JSON template binds to the generic HazardResistanceAtb resisting SensorJam.")]
        public void SensorHardeningModule_BuildsAndCarriesHazardResistance()
        {
            var s = TestScenario.CreateWithColony();
            var facData = s.Faction.GetDataBlob<FactionInfoDB>();

            Assert.IsTrue(facData.ComponentDesigns.ContainsKey("default-design-sensor-hardening-module"),
                "The Sensor Hardening Module design should be unlocked on the faction (the six-point JSON chain).");

            var design = facData.ComponentDesigns["default-design-sensor-hardening-module"];
            Assert.IsTrue(design.TryGetAttribute<HazardResistanceAtb>(out var atb),
                "The module must carry a HazardResistanceAtb (the JSON template -> Atb binding, gotcha-10).");
            Assert.AreEqual(HazardEffectType.SensorJam, atb.ResistedEffectType, "It should resist SensorJam.");
            Assert.Greater(atb.ResistanceFraction, 0.0, "It should provide some resistance.");
        }

        [Test]
        [Description("Install → read → grave: an INSTALLED hardening module gives resistance to its kind only, and a DESTROYED one gives none.")]
        public void InstalledHardeningModule_Resists_AndLosesItWhenDestroyed()
        {
            var s = TestScenario.CreateWithColony();
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns["default-design-sensor-hardening-module"];

            // A bare ship-like entity carrying a ComponentInstancesDB, with the module installed.
            var ship = Entity.Create();
            var cidb = new ComponentInstancesDB();
            s.StartingSystem.AddEntity(ship, new List<BaseDataBlob> { cidb });
            var module = new ComponentInstance(design);
            cidb.AddComponentInstance(module);

            Assert.Greater(SpaceHazardTools.ResistanceFraction(ship, HazardEffectType.SensorJam), 0.0,
                "An installed Sensor Hardening Module should give sensor-jam resistance.");
            Assert.AreEqual(0.0, SpaceHazardTools.ResistanceFraction(ship, HazardEffectType.MovementDrag), 1e-9,
                "It should NOT resist a different effect kind (drag) — resistance is per-kind.");

            // Grave rung: destroy the module → its resistance is gone (read live from installed components).
            cidb.RemoveComponentInstance(module);
            Assert.AreEqual(0.0, SpaceHazardTools.ResistanceFraction(ship, HazardEffectType.SensorJam), 1e-9,
                "Destroying the module removes the resistance (the grave rung).");
        }
    }
}
