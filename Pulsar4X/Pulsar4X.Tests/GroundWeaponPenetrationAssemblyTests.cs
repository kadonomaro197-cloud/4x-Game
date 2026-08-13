using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;        // WeaponProfile — the resolver's per-weapon view (reads Penetration/PerShotEnergy)
using Pulsar4X.Components;    // ComponentDesign
using Pulsar4X.Factions;     // FactionInfoDB
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL A2 (ENGINE-WIRING-BACKLOG TIER 1 #1) — carry the armour-crack (Penetration) and
    /// alpha-vs-chip (PerShotEnergy) dials THROUGH THE ASSEMBLER PATH.
    ///
    /// The gap this closes: the MONOLITHIC base-mod ground units (`GroundUnitAtb`) already carried both dials, but the
    /// weapon PART the Entity Assembler mounts (`GroundWeaponAtb`) had no such fields — so a *player-designed* AP weapon
    /// came out with Penetration 0 / PerShotEnergy 0 and bounced off plate a monolithic tank cracked. A2 adds the two
    /// dials to `GroundWeaponAtb` (its 6th/7th ctor args, the K1 Range_m pattern), reads them onto each
    /// `GroundWeaponMount` + the `GroundUnitDesign`, and repoints `GroundCombatant.ToWeaponProfile(unit, mount)` to read
    /// the MOUNT's own values — so a unit with a rifle AND a cannon cracks plate only with the cannon.
    ///
    /// These gauges prove the carry-through end to end on the REAL base-mod weapons (the JSON→atb bind is the gotcha-10
    /// sensor for the new 7-arg ctor): a cannon (authored 20/140) assembles a unit whose design + mount + resolver
    /// profile carry 20/140; a rifle (0/10) stays 0/10; on a mixed unit each mount keeps its OWN pen; and penetration
    /// actually cracks armour (the resolver's `GroundDamageMatrix.ArmourSoak` lands more with pen than without at equal
    /// damage). Assembler idioms mirror the green `GroundWeaponLoadoutTests`.
    /// </summary>
    [TestFixture]
    public class GroundWeaponPenetrationAssemblyTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[a2-pen] " + m);

        private const string Frame  = "default-design-human-frame";
        private const string Rifle  = "default-design-ground-rifle";   // Attack 40,  Range 1, Penetration 0,  PerShotEnergy 10
        private const string Cannon = "default-design-ground-cannon";  // Attack 220, Range 3, Penetration 20, PerShotEnergy 140

        // The authored (FLAGGED) values in installations.json — the parity target with the monolithic Armor unit's gun.
        private const double CannonPen = 20, CannonPerShot = 140, RiflePen = 0, RiflePerShot = 10;

        private static ComponentDesign Part(FactionInfoDB faction, string id) => (ComponentDesign)faction.IndustryDesigns[id];

        [Test]
        [Description("A cannon (authored 20/140) assembled onto a frame yields a design AND a mount AND a resolver profile "
                   + "carrying Penetration 20 / PerShotEnergy 140; a rifle (0/10) stays 0/10 — the JSON→atb→assembler→"
                   + "design→mount→profile carry-through (the gotcha-10 bind sensor for the new 7-arg GroundWeaponAtb ctor).")]
        public void AssembledWeapon_CarriesPenetrationAndPerShotEnergy_ThroughToTheProfile()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();

            void CheckOneWeapon(string weaponId, double expectPen, double expectPerShot)
            {
                var design = GroundUnitAssembly.RegisterAssembledDesign(
                    faction, "a2-" + weaponId, "A2 " + weaponId,
                    Part(faction, Frame), new List<(ComponentDesign, int)> { (Part(faction, weaponId), 1) });

                // design-level (the collapsed fallback + monolithic-parity value = the single weapon's)
                Assert.That(design.Penetration, Is.EqualTo(expectPen).Within(1e-6), $"{weaponId}: design.Penetration");
                Assert.That(design.PerShotEnergy, Is.EqualTo(expectPerShot).Within(1e-6), $"{weaponId}: design.PerShotEnergy");

                // per-mount (the honest home the resolver reads)
                Assert.That(design.WeaponLoadout.Count, Is.EqualTo(1), $"{weaponId}: one mount");
                var mount = design.WeaponLoadout[0];
                Assert.That(mount.Penetration, Is.EqualTo(expectPen).Within(1e-6), $"{weaponId}: mount.Penetration");
                Assert.That(mount.PerShotEnergy, Is.EqualTo(expectPerShot).Within(1e-6), $"{weaponId}: mount.PerShotEnergy");

                // raise: the snapshot deep-copies onto the unit (the copy-ctor must carry the new fields — L12)
                var unit = GroundForces.RaiseUnit(s.StartingBody, design, s.Faction.Id, 0);
                var raised = unit.WeaponLoadout[0];
                Assert.That(raised.Penetration, Is.EqualTo(expectPen).Within(1e-6), $"{weaponId}: raised mount.Penetration");
                Assert.That(raised.PerShotEnergy, Is.EqualTo(expectPerShot).Within(1e-6), $"{weaponId}: raised mount.PerShotEnergy");

                // the resolver's per-weapon profile reads the MOUNT's pen/per-shot (the A2 behaviour edit)
                WeaponProfile prof = GroundCombatant.ToWeaponProfile(unit, raised);
                Assert.That(prof.Penetration, Is.EqualTo(expectPen).Within(1e-6), $"{weaponId}: profile.Penetration == mount");
                Assert.That(prof.PerShotEnergy, Is.EqualTo(expectPerShot).Within(1e-6), $"{weaponId}: profile.PerShotEnergy == mount");
                Log($"{weaponId}: pen {prof.Penetration} perShot {prof.PerShotEnergy}");
            }

            CheckOneWeapon(Cannon, CannonPen, CannonPerShot);
            CheckOneWeapon(Rifle, RiflePen, RiflePerShot);
        }

        [Test]
        [Description("PER-MOUNT honesty: on ONE unit carrying a rifle (pen 0) AND a cannon (pen 20), each mount keeps its "
                   + "OWN penetration through the resolver profile — the cannon cracks plate, the rifle does not (the "
                   + "'rifle+railgun cracks plate only with the railgun' rule the assembler backlog names).")]
        public void MixedUnit_EachMountKeepsItsOwnPenetration()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();

            var design = GroundUnitAssembly.RegisterAssembledDesign(
                faction, "a2-mixed", "A2 Rifle+Cannon",
                Part(faction, Frame),
                new List<(ComponentDesign, int)> { (Part(faction, Rifle), 1), (Part(faction, Cannon), 1) });

            var unit = GroundForces.RaiseUnit(s.StartingBody, design, s.Faction.Id, 0);
            Assert.That(unit.WeaponLoadout.Count, Is.EqualTo(2), "two mounts");

            var cannonMount = unit.WeaponLoadout.Single(m => m.RangeHexes == 3);   // the cannon (range 3)
            var rifleMount  = unit.WeaponLoadout.Single(m => m.RangeHexes == 1);   // the rifle  (range 1)

            Assert.That(GroundCombatant.ToWeaponProfile(unit, cannonMount).Penetration, Is.EqualTo(CannonPen).Within(1e-6),
                "the cannon mount's profile carries the cannon's penetration");
            Assert.That(GroundCombatant.ToWeaponProfile(unit, rifleMount).Penetration, Is.EqualTo(RiflePen).Within(1e-6),
                "the rifle mount's profile carries the rifle's (zero) penetration — same unit, different mounts");

            // design-level pen tracks the PRIMARY weapon (the cannon, Attack 220 > rifle 40) — the collapsed fallback.
            Assert.That(design.Penetration, Is.EqualTo(CannonPen).Within(1e-6),
                "the unit-level design pen tracks the heaviest hitter (the cannon)");
        }

        [Test]
        [Description("The bite: penetration cracks armour — at EQUAL incoming damage a pen-20 shot loses less to a plated "
                   + "target's flat armour (lands more) than a pen-0 shot, via the resolver's own GroundDamageMatrix.ArmourSoak. "
                   + "Combined with the carry-through above, an assembled AP cannon unit cracks plate a rifle unit bounces off.")]
        public void Penetration_CracksPlate_ViaTheResolverArmourSoak()
        {
            const double defense = 30.0;      // a plated target
            const double incoming = 100.0;    // the same source damage for both

            double landedWithAp   = GroundDamageMatrix.ArmourSoak(defense, incoming, CannonPen);   // pen 20 — cracks
            double landedNoAp     = GroundDamageMatrix.ArmourSoak(defense, incoming, RiflePen);     // pen 0  — bounces
            Log($"armour soak: AP(pen {CannonPen}) lands {landedWithAp:0.###}  vs  no-AP(pen {RiflePen}) lands {landedNoAp:0.###} of {incoming}");

            Assert.That(landedWithAp, Is.GreaterThan(landedNoAp),
                "an AP shot penetrates the target's flat armour and lands more damage than a no-pen shot of equal power");
        }
    }
}
