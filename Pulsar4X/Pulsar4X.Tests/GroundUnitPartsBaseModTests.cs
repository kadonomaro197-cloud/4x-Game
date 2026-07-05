using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.DataStructures;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// GROUND-UNIT DESIGNER track, slice G-D2 — the base-mod parts bin, through the REAL data path. Four starter parts
    /// (a frame, a weapon, plating, an augment), one of each attribute type, load from JSON and bind their
    /// Ground*Atb via the ComponentDesigner (template → NCalc → atb, gotcha #10). This is the sensor that a
    /// mis-ordered/mis-counted AtbConstrArgs or a bad ctor fails HERE, not in a player's New Game — the
    /// <see cref="RailgunWeaponTests"/> equivalent for ground parts. They mount as the new GroundUnit type so they stay
    /// out of the ship/colony build lists and the ground designer (G-D4) can filter to them. Assembly + the carry gate
    /// ride on top in G-D3. Design: docs/GROUND-COMBAT-MAP-DESIGN.md → unit designer.
    /// </summary>
    [TestFixture]
    public class GroundUnitPartsBaseModTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[gd-parts] " + m);

        [Test]
        [Description("G-D2: the four base-mod parts load onto the start faction, bind their Ground*Atb from JSON with the flagged stats, and mount as GroundUnit parts.")]
        public void GroundParts_LoadFromJson_BindTheirAtbs_AsGroundUnitParts()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns;

            ComponentDesign Design(string id)
            {
                Assert.That(designs.ContainsKey(id), Is.True, $"{id} loads onto the faction (six-point registration wired up)");
                var d = designs[id] as ComponentDesign;
                Assert.That(d, Is.Not.Null, $"{id} is a ComponentDesign");
                Assert.That(d.ComponentMountType.HasFlag(ComponentMountType.GroundUnit), Is.True, $"{id} mounts as a GroundUnit part");
                return d;
            }

            // CHASSIS — Human Frame
            var frame = Design("default-design-human-frame");
            Assert.That(frame.HasAttribute<GroundChassisAtb>(), Is.True, "human frame binds a GroundChassisAtb");
            var ch = frame.GetAttribute<GroundChassisAtb>();
            Log($"frame: str={ch.BaseStrength:0} hp={ch.BaseHP:0} loco={ch.Locomotion} carry={ch.CarryClass}");
            Assert.That(ch.BaseStrength, Is.EqualTo(100), "frame carry-strength from JSON");
            Assert.That(ch.Locomotion, Is.EqualTo(GroundLocomotion.Foot), "locomotion enum bound");
            Assert.That(ch.CarryClass, Is.EqualTo(GroundCarryClass.Personnel), "carry-class enum bound");

            // WEAPON — Service Rifle
            var rifle = Design("default-design-ground-rifle");
            Assert.That(rifle.HasAttribute<GroundWeaponAtb>(), Is.True, "rifle binds a GroundWeaponAtb");
            var w = rifle.GetAttribute<GroundWeaponAtb>();
            Log($"rifle: mass={w.Mass:0} atk={w.Attack:0} range={w.Range} mode={w.Mode}");
            Assert.That(w.Attack, Is.EqualTo(40), "rifle attack from JSON");
            Assert.That(w.Range, Is.EqualTo(1), "rifle reach from JSON");
            Assert.That(w.Mode, Is.EqualTo(GroundWeaponMode.Ballistic), "weapon mode enum bound");

            // ARMOUR — Composite Plating
            var plating = Design("default-design-ground-plating");
            Assert.That(plating.HasAttribute<GroundArmorAtb>(), Is.True, "plating binds a GroundArmorAtb");
            var ar = plating.GetAttribute<GroundArmorAtb>();
            Assert.That(ar.HP, Is.EqualTo(150), "plating HP from JSON");
            Assert.That(ar.Defense, Is.EqualTo(5), "plating defence from JSON");

            // AUGMENT — Power Armour (the strength-unlock part)
            var pa = Design("default-design-power-armor");
            Assert.That(pa.HasAttribute<GroundAugmentAtb>(), Is.True, "power armour binds a GroundAugmentAtb");
            var au = pa.GetAttribute<GroundAugmentAtb>();
            Log($"power-armor: +str={au.StrengthBonus:0} tough={au.ToughnessBonus:0.##}");
            Assert.That(au.StrengthBonus, Is.EqualTo(300), "power-armour strength boost from JSON (lets a frame carry heavier gear)");
            Assert.That(au.ToughnessBonus, Is.EqualTo(0.2), "toughness from JSON");
        }

        [Test]
        [Description("Weapon-designer scale span (slice 1): the ground-weapon knobs are now DIALABLE, not baked. The SAME rifle template builds at siege scale — Attack dials from its default 40 up to a battle-cannon, Range past 1 hex — proving GroundWeaponAtb accepts the full continuous span from the base-mod template. This is the ground echo of dialing a beam from blaster pistol to phaser array. The DEFAULT (40/1) is unchanged — GroundParts_LoadFromJson gauges that; this dials it UP.")]
        public void GroundWeapon_KnobsAreDialable_SameTemplateBuildsAtSiegeScale()
        {
            var s = TestScenario.CreateWithColony();
            var dataStore = s.Faction.GetDataBlob<FactionInfoDB>().Data;
            Assert.That(dataStore.ComponentTemplates.ContainsKey("ground-rifle"), Is.True, "the ground-weapon template is loaded");

            var tmpl = dataStore.ComponentTemplates["ground-rifle"];
            var designer = new ComponentDesigner(tmpl, dataStore, s.Faction.GetDataBlob<FactionTechDB>());
            foreach (var attr in designer.ComponentDesignAttributes.Values) attr.SetValue();   // start from the template defaults

            // dial the weapon WAY up — a battle-cannon, not a service rifle (the knobs are live now, not read-only)
            designer.ComponentDesignAttributes["Attack"].SetValueFromInput(3000);
            designer.ComponentDesignAttributes["Range"].SetValueFromInput(8);
            var big = designer.CreateDesign(s.Faction);
            var w = big.GetAttribute<GroundWeaponAtb>();
            Log($"dialed rifle → attack {w.Attack:0}, range {w.Range} (the template default is 40 / 1 hex)");

            Assert.That(w.Attack, Is.EqualTo(3000), "the Attack knob spans to siege scale — NOT clamped to the rifle's default 40");
            Assert.That(w.Range, Is.EqualTo(8), "and Range dials past 1 — the knob is a live slider, not a baked readout");
            Assert.That(w.Attack, Is.GreaterThan(40 * 10), "same template, an order of magnitude bigger — the pistol→cannon span is real on the engine side");
        }
    }
}
