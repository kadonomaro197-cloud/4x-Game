using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;
using Pulsar4X.Damage;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge that a ship is hit AS THE ARMOUR MATERIAL IT'S CLAD IN — the pipe that makes "the armour material is
    /// the counter" real per-design (and the foundation a researched, signature-rated armour rides). Before the fix,
    /// <c>ComponentPlacement</c> painted armour pixels with a density-derived byte (only coincidentally 255 for
    /// stainless), so a ship's actual armour material did NOT reliably drive its damage resistance. Now the armour
    /// pixels carry the material's real <see cref="DamageResistBlueprint"/> IDCode.
    /// </summary>
    [TestFixture]
    public class ArmorMaterialWiringTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[armour-wiring] " + m);

        [Test]
        [Description("IDCodeForMaterial maps an armour material id to its damage-resistance IDCode (the bitmap R-channel " +
                     "key); an unknown/empty id falls back to 255 (stainless), matching the old default.")]
        public void IDCodeForMaterial_MapsMaterialToResistanceBlueprint()
        {
            Assert.That(DamageTools.IDCodeForMaterial("stainless-steel"), Is.EqualTo((byte)255));
            Assert.That(DamageTools.IDCodeForMaterial("aluminium"), Is.EqualTo((byte)150));
            Assert.That(DamageTools.IDCodeForMaterial("plastic"), Is.EqualTo((byte)100));
            Assert.That(DamageTools.IDCodeForMaterial("does-not-exist"), Is.EqualTo((byte)255), "unknown material falls back to stainless");
            Assert.That(DamageTools.IDCodeForMaterial(""), Is.EqualTo((byte)255), "empty falls back to stainless");
        }

        [Test]
        [Description("A ship's damage-profile bitmap paints its armour pixels with its ACTUAL armour-material IDCode — " +
                     "so when hit, the sim looks up that material's wavelength absorption + signature resistance. This " +
                     "is the wiring that makes the armour material the player chose actually matter.")]
        public void DamageProfile_PaintsArmour_WithItsActualMaterialId()
        {
            var s = TestScenario.CreateWithColony();
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();

            var profile = new EntityDamageProfileDB(design);   // builds the damage-profile bitmap
            string mat = profile.Armor.armorType.ResourceID;
            byte ownId = DamageTools.IDCodeForMaterial(mat);

            var bmp = profile.DamageProfile;
            int ownPixels = 0, occupied = 0;
            for (int x = 0; x < bmp.Width; x++)
            for (int y = 0; y < bmp.Height; y++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.a > 0)
                {
                    occupied++;
                    if (px.r == ownId) ownPixels++;
                }
            }

            Log($"armour material='{mat}' → IDCode {ownId}; armour-material pixels = {ownPixels} of {occupied} occupied");
            Assert.That(ownId, Is.GreaterThan((byte)0), "the armour material must resolve to a real resistance IDCode");
            Assert.That(ownPixels, Is.GreaterThan(0),
                "the damage profile must paint armour pixels with the ship's actual armour-material IDCode — the wiring that makes armour material matter");
        }
    }
}
