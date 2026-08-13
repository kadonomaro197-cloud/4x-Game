using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;    // FactionInfoDB, ConquerResolver (the AI warship classifier that now delegates here)
using Pulsar4X.Ships;       // ShipRoleTools, ShipRole, ShipDesign, ShipFactory

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The ship ROLE classifier (<see cref="ShipRoleTools"/>) — the space twin of the ground
    /// <c>GroundRoleComposer.ClassifyRole</c>, and the ONE place a warship is told from a freighter. It exists to satisfy
    /// the studio law "one verb, both seats": the Forces window shows a ship's Class from it, and the faction AI's
    /// <see cref="ConquerResolver.IsWarship"/> / <c>DefendResolver.IsWarship</c> now DELEGATE to it — so the window and the
    /// AI can never classify a warship two different ways.
    ///
    /// These gauges prove: (1) every design the AI calls a warship classifies as <see cref="ShipRole.Warship"/> and reads
    /// Military, and every non-warship design reads a civilian role — the delegation is consistent (byte-identity tripwire
    /// for the two AI helpers that used to hold their own copy of the predicate); (2) a BUILT ship classifies the same way
    /// its DESIGN does (the window's built-ship path agrees with the AI's design-time path); (3) the Mil/Civ filter maps
    /// only Warship to Military. Runs on the real base-mod start faction (no hard-coded design ids).
    /// </summary>
    [TestFixture]
    public class ShipRoleToolsTests
    {
        private static IEnumerable<ShipDesign> Designs(Entity faction)
            => faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values;

        [Test]
        [Description("Every AI-warship design classifies as Warship+Military and every other design as a civilian role — "
                   + "the shared classifier agrees with the AI helpers that now delegate to it (byte-identity tripwire).")]
        public void ClassifyRole_AgreesWithTheAIWarshipTest_AcrossEveryStartDesign()
        {
            var s = TestScenario.CreateWithColony();
            int warships = 0, civilians = 0;

            foreach (var d in Designs(s.Faction))
            {
                var role = ShipRoleTools.ClassifyRole(d);
                bool aiSaysWarship = ConquerResolver.IsWarship(d);   // internal AI helper — now delegates to ShipRoleTools
                TestContext.WriteLine($"[ship-role] {d.Name,-32} -> {role}  (mil={ShipRoleTools.IsMilitary(role)})");

                if (aiSaysWarship)
                {
                    Assert.That(role, Is.EqualTo(ShipRole.Warship),
                        $"{d.Name}: the AI calls it a warship, so the shared classifier must too");
                    Assert.That(ShipRoleTools.IsMilitary(role), Is.True, $"{d.Name}: a warship is Military");
                    warships++;
                }
                else
                {
                    Assert.That(role, Is.Not.EqualTo(ShipRole.Warship),
                        $"{d.Name}: the AI does NOT call it a warship, so it must NOT classify as Warship");
                    Assert.That(ShipRoleTools.IsMilitary(role), Is.False, $"{d.Name}: a non-warship is Civilian");
                    civilians++;
                }
            }

            Assert.That(warships, Is.GreaterThan(0), "the start faction fields at least one warship design");
            Assert.That(civilians, Is.GreaterThan(0), "the start faction fields at least one civilian design");
        }

        [Test]
        [Description("A BUILT ship classifies the same role as its DESIGN — the window's built-ship path (which reads the "
                   + "installed parts + firepower) agrees with the AI's design-time path.")]
        public void ClassifyRole_BuiltShip_MatchesItsDesign()
        {
            var s = TestScenario.CreateWithColony();

            ShipDesign warship = null, civilian = null;
            foreach (var d in Designs(s.Faction))
            {
                if (ShipRoleTools.ClassifyRole(d) == ShipRole.Warship) { warship ??= d; }
                else { civilian ??= d; }
            }
            Assert.That(warship, Is.Not.Null, "there is an armed design to build");
            Assert.That(civilian, Is.Not.Null, "there is a civilian design to build");

            var warshipEntity = ShipFactory.CreateShip(warship, s.Faction, s.StartingBody);
            var civilianEntity = ShipFactory.CreateShip(civilian, s.Faction, s.StartingBody);

            Assert.That(ShipRoleTools.ClassifyRole(warshipEntity), Is.EqualTo(ShipRole.Warship),
                "a built armed hull reads as a Warship (firepower > 0)");
            Assert.That(ShipRoleTools.ClassifyRole(warshipEntity), Is.EqualTo(ShipRoleTools.ClassifyRole(warship)),
                "the built warship classifies the same role as its design");
            Assert.That(ShipRoleTools.ClassifyRole(civilianEntity), Is.EqualTo(ShipRoleTools.ClassifyRole(civilian)),
                "the built civilian classifies the same role as its design (installed-parts path == design path)");
            Assert.That(ShipRoleTools.IsMilitary(civilianEntity), Is.False, "the civilian hull is not Military");
        }

        [Test]
        [Description("The Mil/Civ filter maps ONLY Warship to Military — every other role is civilian (a troop transport is "
                   + "a civilian hull carrying military cargo, the Forces-window §7 default).")]
        public void IsMilitary_OnlyWarshipIsMilitary()
        {
            Assert.That(ShipRoleTools.IsMilitary(ShipRole.Warship), Is.True);
            foreach (var role in new[] { ShipRole.Survey, ShipRole.Freighter, ShipRole.Transport,
                                         ShipRole.Tender, ShipRole.Hauler, ShipRole.Utility })
                Assert.That(ShipRoleTools.IsMilitary(role), Is.False, $"{role} is civilian");
        }

        [Test]
        [Description("Defensive: a null design and a null entity both classify as Utility and never throw.")]
        public void ClassifyRole_NullInputs_AreUtility_NoThrow()
        {
            Assert.That(ShipRoleTools.ClassifyRole((ShipDesign)null), Is.EqualTo(ShipRole.Utility));
            Assert.That(ShipRoleTools.ClassifyRole((Entity)null), Is.EqualTo(ShipRole.Utility));
            Assert.That(ShipRoleTools.IsMilitary((ShipDesign)null), Is.False);
        }
    }
}
