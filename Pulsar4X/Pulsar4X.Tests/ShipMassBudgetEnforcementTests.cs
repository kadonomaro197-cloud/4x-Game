using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Chassis ⚙11 ▸ HULL — the mass-budget cap now ENFORCES. Slice D1 computed <see cref="ShipDesign.MassBudget"/> /
    /// <see cref="ShipDesign.OverMassBudget"/> from a mounted hull but left them read-only ("a later slice … lets
    /// IsValid bite"). This is that slice: when <see cref="ShipDesign.EnforceMassBudget"/> is on, a design whose mass
    /// exceeds its hull's Mass Budget is marked <c>IsValid = false</c>, so the client production list refuses it — the
    /// §0b "the physical budget forces the build" gate made to BITE (mount an over-spec load on an under-spec hull and
    /// it won't build → find a bigger hull).
    ///
    /// Byte-identical by construction: the flag defaults OFF and every base-mod ship sits under its hull budget, so
    /// flipping it on changes nothing for real designs. The gate only bites a deliberately-overloaded design.
    /// Engine-only → CI.
    /// </summary>
    [TestFixture]
    public class ShipMassBudgetEnforcementTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[mass-budget-enforce] " + m);

        [Test]
        [Description("Byte-identical: with enforcement OFF (the default) every base-mod ship design is IsValid, AND with it ON they all stay IsValid — because every base-mod ship is under its hull's Mass Budget (OverMassBudget false).")]
        public void EveryBaseModShip_StaysValid_EnforcementOnOrOff()
        {
            var s = TestScenario.CreateWithColony();
            var ships = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.ToList();

            foreach (var d in ships)
                Assert.That(d.IsValid, Is.True, $"'{d.Name}' should be valid with enforcement off (default)");

            bool saved = ShipDesign.EnforceMassBudget;
            try
            {
                ShipDesign.EnforceMassBudget = true;
                foreach (var d in ships)
                {
                    d.Recalculate(s.Faction.GetDataBlob<FactionInfoDB>());
                    Log($"{d.Name,-34} mass {d.MassPerUnit,12:N0}  budget {d.MassBudget,12:N0}  over? {d.OverMassBudget}  valid? {d.IsValid}");
                    Assert.That(d.OverMassBudget, Is.False, $"'{d.Name}' must sit under its hull budget (byte-identical guarantee)");
                    Assert.That(d.IsValid, Is.True, $"'{d.Name}' stays valid under enforcement — it's within budget");
                }
            }
            finally { ShipDesign.EnforceMassBudget = saved; }
        }

        [Test]
        [Description("The gate BITES: an overloaded design (a LIGHT hull — 25 t budget — carrying six lasers, ~60 t) reads OverMassBudget, and with enforcement on it is marked IsValid = false, so the client production list refuses it. With enforcement off it stays valid (only the client gate cares).")]
        public void AnOverloadedDesign_IsRefused_WhenEnforced()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            ComponentDesign D(string id) => (ComponentDesign)faction.IndustryDesigns[id];
            var armor = faction.Data.Armor["plastic-armor"];

            var overloaded = new List<(ComponentDesign, int)>
            {
                (D("default-design-ship-hull-light"), 1),   // Mass Budget 25,000 kg
                (D("default-design-laser-weapon"), 6),       // ~6 × 10 t = far over the light-hull budget
            };

            bool saved = ShipDesign.EnforceMassBudget;
            try
            {
                ShipDesign.EnforceMassBudget = false;
                var offDesign = new ShipDesign(faction, "Overloaded (off)", overloaded, (armor, 1f));
                Log($"enforce OFF: mass {offDesign.MassPerUnit:N0} budget {offDesign.MassBudget:N0} over? {offDesign.OverMassBudget} valid? {offDesign.IsValid}");
                Assert.That(offDesign.OverMassBudget, Is.True, "six lasers on a light hull must exceed its 25 t budget");
                Assert.That(offDesign.IsValid, Is.True, "with enforcement off, an over-budget design is still 'valid' (only the client gate reads it)");

                ShipDesign.EnforceMassBudget = true;
                var onDesign = new ShipDesign(faction, "Overloaded (on)", overloaded, (armor, 1f));
                Log($"enforce ON:  mass {onDesign.MassPerUnit:N0} budget {onDesign.MassBudget:N0} over? {onDesign.OverMassBudget} valid? {onDesign.IsValid}");
                Assert.That(onDesign.OverMassBudget, Is.True, "same overload, still over budget");
                Assert.That(onDesign.IsValid, Is.False, "with enforcement on, an over-budget design is refused (IsValid = false) — the §0b gate bites");
            }
            finally { ShipDesign.EnforceMassBudget = saved; }
        }
    }
}
