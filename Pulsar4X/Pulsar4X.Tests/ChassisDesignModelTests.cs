using System;
using NUnit.Framework;
using Pulsar4X.Components.Designers;
using Pulsar4X.Ships;
using Pulsar4X.Stations;
using Pulsar4X.Colonies;
using Pulsar4X.GroundCombat;
using Pulsar4X.Interfaces;
using Pulsar4X.DataStructures;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL, Phase C — the CHASSIS door parametric designer, slice-1 gauge.
    ///
    /// Proves the pure <see cref="ChassisDesignModel"/> (the engine half of the "pick a host cell + slide a couple
    /// dials" frame form) REPRODUCES every shipped base-mod chassis — all NINE designs across all FOUR cells of the
    /// 2x2 (ship hull light/medium/heavy, station chassis, building foundation, and the human/vehicle/walker/swarm
    /// ground frames) — from its two choices (Environment x Kind) + dials. So every hand-authored frame falls out of
    /// the one parametric form (the DESIGNER-NORTH-STAR reproduction claim, made executable). Pure (no colony harness
    /// -> fast, not the slow CI shard); the reproduction VALUES are the ones the shipped templates carry (verified
    /// against installations.json / componentDesigns.json), so a drift in the model's per-cell mapping fails here.
    /// Byte-identical to the live game (nothing calls the model yet).
    ///
    /// Two layers of assertion per case: (1) the model's produced <see cref="ChassisProfile"/> — the frame atb TYPE,
    /// its ctor ARGS (in template order), the component Mass, and the cell-forced budget-currency + mount; and (2) a
    /// CROSS-CHECK that feeds those args straight into the EXISTING frame ctors (ShipHullAtb(double) / StationChassisAtb
    /// (double) / BuildingChassisAtb(double) / GroundChassisAtb(double x5)) and asserts the constructed atb's fields
    /// equal the args — proving the model drives the real save/binder ctor path with no new overload (no landmine L13).
    ///
    /// D10 honesty: reproduction is a clean PASS-THROUGH — the developer dropped the HTML's square-root "structural
    /// efficiency" slider (2026-08-17), so the medium hull is NOT recalibrated (90000 stays 90000, never 112800). The
    /// four cells carry DIFFERENT dial counts (ship 2 / station+building 1 [+footprint] / ground 5) — the model is an
    /// honest per-cell mapping, so these tests assert per cell, not one flat slider set.
    /// </summary>
    [TestFixture]
    public class ChassisDesignModelTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[chassis-model] " + m);

        /// <summary>Assert the model's profile matches the shipped frame's type/args/mass/currency/mount/substrate,
        /// then cross-check by constructing the REAL frame atb from those args and reading its fields back.</summary>
        private static void AssertProfile(string name, ChassisDesignModel m,
            Type atbType, double[] atbArgs, double mass,
            ChassisBudgetKind budgetKind, ComponentMountType mount, GroundSubstrate substrate,
            Type coMountType = null, double[] coMountArgs = null)
        {
            var p = m.BuildProfile();
            Log($"{name}: {p.AtbType.Name}({string.Join(",", p.AtbArgs)}) mass={p.Mass} kind={p.BudgetKind} mount={p.PartMount} substrate={p.Substrate}"
                + (p.CoMountAtbType != null ? $" + {p.CoMountAtbType.Name}({string.Join(",", p.CoMountAtbArgs)})" : ""));

            Assert.That(p.AtbType, Is.EqualTo(atbType), $"{name} AtbType");
            Assert.That(p.AtbArgs.Length, Is.EqualTo(atbArgs.Length), $"{name} AtbArgs arity");
            for (int i = 0; i < atbArgs.Length; i++)
                Assert.That(p.AtbArgs[i], Is.EqualTo(atbArgs[i]).Within(1e-6), $"{name} AtbArgs[{i}]");
            Assert.That(p.Mass, Is.EqualTo(mass).Within(1e-6), $"{name} Mass");
            Assert.That(p.BudgetKind, Is.EqualTo(budgetKind), $"{name} BudgetKind");
            Assert.That(p.PartMount, Is.EqualTo(mount), $"{name} PartMount");
            Assert.That(p.Substrate, Is.EqualTo(substrate), $"{name} Substrate");

            if (coMountType == null)
            {
                Assert.That(p.CoMountAtbType, Is.Null, $"{name} no co-mount");
                Assert.That(p.CoMountAtbArgs.Length, Is.EqualTo(0), $"{name} no co-mount args");
            }
            else
            {
                Assert.That(p.CoMountAtbType, Is.EqualTo(coMountType), $"{name} CoMountAtbType");
                Assert.That(p.CoMountAtbArgs.Length, Is.EqualTo(coMountArgs.Length), $"{name} co-mount arity");
                for (int i = 0; i < coMountArgs.Length; i++)
                    Assert.That(p.CoMountAtbArgs[i], Is.EqualTo(coMountArgs[i]).Within(1e-6), $"{name} CoMountAtbArgs[{i}]");
            }

            AssertReproducesRealAtb(name, p);
        }

        /// <summary>The can't-rot cross-check: feed the model's args into the EXISTING frame ctor and read the fields
        /// back, so a drift between the model and the real atb (or a ctor-arity break) fails CI right here.</summary>
        private static void AssertReproducesRealAtb(string name, ChassisProfile p)
        {
            if (p.AtbType == typeof(ShipHullAtb))
            {
                var atb = new ShipHullAtb(p.AtbArgs[0]);
                Assert.That(atb.MassBudget, Is.EqualTo(p.AtbArgs[0]).Within(1e-6), $"{name} real ShipHullAtb.MassBudget");
                Assert.That(atb.BudgetKind, Is.EqualTo(p.BudgetKind), $"{name} real ShipHullAtb.BudgetKind");
                Assert.That(atb.PartMount, Is.EqualTo(p.PartMount), $"{name} real ShipHullAtb.PartMount");
            }
            else if (p.AtbType == typeof(StationChassisAtb))
            {
                var atb = new StationChassisAtb(p.AtbArgs[0]);
                Assert.That(atb.StructuralAllowance, Is.EqualTo(p.AtbArgs[0]).Within(1e-6), $"{name} real StationChassisAtb.StructuralAllowance");
                Assert.That(atb.BudgetKind, Is.EqualTo(p.BudgetKind), $"{name} real StationChassisAtb.BudgetKind");
                Assert.That(atb.PartMount, Is.EqualTo(p.PartMount), $"{name} real StationChassisAtb.PartMount");
            }
            else if (p.AtbType == typeof(BuildingChassisAtb))
            {
                var atb = new BuildingChassisAtb(p.AtbArgs[0]);
                Assert.That(atb.FootprintAllowance, Is.EqualTo(p.AtbArgs[0]).Within(1e-6), $"{name} real BuildingChassisAtb.FootprintAllowance");
                Assert.That(atb.BudgetKind, Is.EqualTo(p.BudgetKind), $"{name} real BuildingChassisAtb.BudgetKind");
                Assert.That(atb.PartMount, Is.EqualTo(p.PartMount), $"{name} real BuildingChassisAtb.PartMount");
                // the co-mounted footprint part
                var fp = new GroundFootprintAtb(p.CoMountAtbArgs[0]);
                Assert.That(fp.TileFootprint, Is.EqualTo((int)p.CoMountAtbArgs[0]), $"{name} real GroundFootprintAtb.TileFootprint");
            }
            else if (p.AtbType == typeof(GroundChassisAtb))
            {
                var atb = new GroundChassisAtb(p.AtbArgs[0], p.AtbArgs[1], p.AtbArgs[2], p.AtbArgs[3], p.AtbArgs[4]);
                Assert.That(atb.BaseStrength, Is.EqualTo(p.AtbArgs[0]).Within(1e-6), $"{name} real GroundChassisAtb.BaseStrength");
                Assert.That(atb.BaseHP, Is.EqualTo(p.AtbArgs[1]).Within(1e-6), $"{name} real GroundChassisAtb.BaseHP");
                Assert.That(atb.Size, Is.EqualTo(p.AtbArgs[2]).Within(1e-6), $"{name} real GroundChassisAtb.Size");
                Assert.That((int)atb.Locomotion, Is.EqualTo((int)p.AtbArgs[3]), $"{name} real GroundChassisAtb.Locomotion");
                Assert.That((int)atb.CarryClass, Is.EqualTo((int)p.AtbArgs[4]), $"{name} real GroundChassisAtb.CarryClass");
                Assert.That(atb.BudgetKind, Is.EqualTo(p.BudgetKind), $"{name} real GroundChassisAtb.BudgetKind");
                Assert.That(atb.PartMount, Is.EqualTo(p.PartMount), $"{name} real GroundChassisAtb.PartMount");
            }
            else
            {
                Assert.Fail($"{name} produced an unexpected chassis atb type {p.AtbType}");
            }
        }

        [Test]
        [Description("Orbital x Unit: the three shipped ship-hull tiers (light/medium/heavy) fall out of ONE cell — only the frame-mass + mass-budget dials move; no square-root recalibration (D10), so the medium hull stays 90000.")]
        public void ShipHulls_ReproduceFromTheForm()
        {
            // default-design-ship-hull (Medium): Hull Mass 10000, Mass Budget 90000.
            AssertProfile("ship-hull-medium",
                new ChassisDesignModel(ChassisEnvironment.Orbital, ChassisKind.Unit, frameMass: 10000, structuralBudget: 90000),
                typeof(ShipHullAtb), new[] { 90000.0 }, 10000,
                ChassisBudgetKind.Mass, ComponentMountType.ShipComponent, GroundSubstrate.Mechanical);

            // default-design-ship-hull-light: Hull Mass 500, Mass Budget 25000.
            AssertProfile("ship-hull-light",
                new ChassisDesignModel(ChassisEnvironment.Orbital, ChassisKind.Unit, frameMass: 500, structuralBudget: 25000),
                typeof(ShipHullAtb), new[] { 25000.0 }, 500,
                ChassisBudgetKind.Mass, ComponentMountType.ShipComponent, GroundSubstrate.Mechanical);

            // default-design-ship-hull-heavy: Hull Mass 25000, Mass Budget 180000.
            AssertProfile("ship-hull-heavy",
                new ChassisDesignModel(ChassisEnvironment.Orbital, ChassisKind.Unit, frameMass: 25000, structuralBudget: 180000),
                typeof(ShipHullAtb), new[] { 180000.0 }, 25000,
                ChassisBudgetKind.Mass, ComponentMountType.ShipComponent, GroundSubstrate.Mechanical);
        }

        [Test]
        [Description("Orbital x Infrastructure: the station chassis — a constant 100000 kg frame with a Structure-currency budget of 2000, mounting Station parts.")]
        public void StationChassis_ReproducesFromTheForm()
        {
            // default-design-station-chassis: Mass constant 100000, Structural Budget 2000.
            AssertProfile("station-chassis",
                new ChassisDesignModel(ChassisEnvironment.Orbital, ChassisKind.Infrastructure, frameMass: 100000, structuralBudget: 2000),
                typeof(StationChassisAtb), new[] { 2000.0 }, 100000,
                ChassisBudgetKind.Structure, ComponentMountType.Station, GroundSubstrate.Mechanical);
        }

        [Test]
        [Description("Surface x Infrastructure: the building foundation — a constant 50000 kg frame with a Footprint budget of 2000, and the co-mounted GroundFootprintAtb(4) that gives the building its located presence on the ground map.")]
        public void BuildingFoundation_ReproducesFromTheForm_WithFootprintCoMount()
        {
            // default-design-building-foundation: Mass constant 50000, Footprint Budget 2000, TileFootprint 4.
            AssertProfile("building-foundation",
                new ChassisDesignModel(ChassisEnvironment.Surface, ChassisKind.Infrastructure, frameMass: 50000, structuralBudget: 2000, tileFootprint: 4),
                typeof(BuildingChassisAtb), new[] { 2000.0 }, 50000,
                ChassisBudgetKind.Footprint, ComponentMountType.PlanetInstallation, GroundSubstrate.Mechanical,
                coMountType: typeof(GroundFootprintAtb), coMountArgs: new[] { 4.0 });
        }

        [Test]
        [Description("Surface x Unit: the four shipped ground frames (human/vehicle/walker/swarm) fall out of ONE cell with the five GroundChassisAtb dials (strength/HP/size/locomotion/carry-class); each shipped frame is reproduced Mechanical for byte-identity (substrate is on the design, not the atb).")]
        public void GroundFrames_ReproduceFromTheForm()
        {
            // default-design-human-frame: Mass 20, GroundChassisAtb(100,200,1,Foot,Personnel).
            AssertProfile("human-frame",
                new ChassisDesignModel(ChassisEnvironment.Surface, ChassisKind.Unit, frameMass: 20, structuralBudget: 100,
                    hp: 200, size: 1, locomotion: GroundLocomotion.Foot, carryClass: GroundCarryClass.Personnel),
                typeof(GroundChassisAtb), new[] { 100.0, 200.0, 1.0, 0.0, 0.0 }, 20,
                ChassisBudgetKind.Carry, ComponentMountType.GroundUnit, GroundSubstrate.Mechanical);

            // default-design-vehicle-frame: Mass 4000, GroundChassisAtb(800,1500,6,Tracked,Vehicle).
            AssertProfile("vehicle-frame",
                new ChassisDesignModel(ChassisEnvironment.Surface, ChassisKind.Unit, frameMass: 4000, structuralBudget: 800,
                    hp: 1500, size: 6, locomotion: GroundLocomotion.Tracked, carryClass: GroundCarryClass.Vehicle),
                typeof(GroundChassisAtb), new[] { 800.0, 1500.0, 6.0, 1.0, 1.0 }, 4000,
                ChassisBudgetKind.Carry, ComponentMountType.GroundUnit, GroundSubstrate.Mechanical);

            // default-design-walker-frame: Mass 2500, GroundChassisAtb(400,1000,4,Walker,Vehicle).
            AssertProfile("walker-frame",
                new ChassisDesignModel(ChassisEnvironment.Surface, ChassisKind.Unit, frameMass: 2500, structuralBudget: 400,
                    hp: 1000, size: 4, locomotion: GroundLocomotion.Walker, carryClass: GroundCarryClass.Vehicle),
                typeof(GroundChassisAtb), new[] { 400.0, 1000.0, 4.0, 2.0, 1.0 }, 2500,
                ChassisBudgetKind.Carry, ComponentMountType.GroundUnit, GroundSubstrate.Mechanical);

            // default-design-swarm-frame: Mass 5, GroundChassisAtb(30,40,1,Foot,Personnel). Shipped as Mechanical
            // (the HTML's swarm=Organic is a new-reach proposal, NOT a shipped value — reproducing it Organic would
            // not be byte-identical, so slice-1 reproduces Mechanical).
            AssertProfile("swarm-frame",
                new ChassisDesignModel(ChassisEnvironment.Surface, ChassisKind.Unit, frameMass: 5, structuralBudget: 30,
                    hp: 40, size: 1, locomotion: GroundLocomotion.Foot, carryClass: GroundCarryClass.Personnel),
                typeof(GroundChassisAtb), new[] { 30.0, 40.0, 1.0, 0.0, 0.0 }, 5,
                ChassisBudgetKind.Carry, ComponentMountType.GroundUnit, GroundSubstrate.Mechanical);
        }

        [Test]
        [Description("The 2x2 host-cell pick maps to the four distinct frame classes with the right budget currency + mount — the door's whole structural claim in one place.")]
        public void HostCell_MapsToTheFourFrameClasses()
        {
            var ship = new ChassisDesignModel(ChassisEnvironment.Orbital, ChassisKind.Unit, 1, 1).BuildProfile();
            var station = new ChassisDesignModel(ChassisEnvironment.Orbital, ChassisKind.Infrastructure, 1, 1).BuildProfile();
            var ground = new ChassisDesignModel(ChassisEnvironment.Surface, ChassisKind.Unit, 1, 1).BuildProfile();
            var building = new ChassisDesignModel(ChassisEnvironment.Surface, ChassisKind.Infrastructure, 1, 1).BuildProfile();

            Assert.That(ship.AtbType, Is.EqualTo(typeof(ShipHullAtb)));
            Assert.That(ship.BudgetKind, Is.EqualTo(ChassisBudgetKind.Mass));
            Assert.That(ship.PartMount, Is.EqualTo(ComponentMountType.ShipComponent));

            Assert.That(station.AtbType, Is.EqualTo(typeof(StationChassisAtb)));
            Assert.That(station.BudgetKind, Is.EqualTo(ChassisBudgetKind.Structure));
            Assert.That(station.PartMount, Is.EqualTo(ComponentMountType.Station));

            Assert.That(ground.AtbType, Is.EqualTo(typeof(GroundChassisAtb)));
            Assert.That(ground.BudgetKind, Is.EqualTo(ChassisBudgetKind.Carry));
            Assert.That(ground.PartMount, Is.EqualTo(ComponentMountType.GroundUnit));

            Assert.That(building.AtbType, Is.EqualTo(typeof(BuildingChassisAtb)));
            Assert.That(building.BudgetKind, Is.EqualTo(ChassisBudgetKind.Footprint));
            Assert.That(building.PartMount, Is.EqualTo(ComponentMountType.PlanetInstallation));
            // only the building foundation co-mounts a footprint part
            Assert.That(building.CoMountAtbType, Is.EqualTo(typeof(GroundFootprintAtb)));
            Assert.That(ship.CoMountAtbType, Is.Null);
            Assert.That(station.CoMountAtbType, Is.Null);
            Assert.That(ground.CoMountAtbType, Is.Null);
        }

        [Test]
        [Description("CHOICE 2 substrate is a design-level dial the ground cell passes through unchanged (Mechanical default, and it accepts Organic/Synthetic for a future living/nanite hull) — it never becomes a chassis-atb ctor arg, which is what keeps it save-safe.")]
        public void Substrate_PassesThroughOnTheGroundCell()
        {
            var mech = new ChassisDesignModel(ChassisEnvironment.Surface, ChassisKind.Unit, 20, 100,
                hp: 200, size: 1, substrate: GroundSubstrate.Mechanical).BuildProfile();
            var organic = new ChassisDesignModel(ChassisEnvironment.Surface, ChassisKind.Unit, 20, 100,
                hp: 200, size: 1, substrate: GroundSubstrate.Organic).BuildProfile();

            Assert.That(mech.Substrate, Is.EqualTo(GroundSubstrate.Mechanical));
            Assert.That(organic.Substrate, Is.EqualTo(GroundSubstrate.Organic));
            // the substrate never touches the atb args (byte-identical ctor call regardless)
            Assert.That(organic.AtbArgs, Is.EqualTo(mech.AtbArgs).AsCollection);
        }
    }
}
