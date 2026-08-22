using System;
using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Components.Designers;
using Pulsar4X.Interfaces;
using Pulsar4X.Storage;
using Pulsar4X.Combat;
using Pulsar4X.Docking;
using Pulsar4X.GroundCombat;
using Pulsar4X.Logistics;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL, Phase C — the LOGISTICAL door parametric designer, slice-1 gauge.
    ///
    /// Proves the pure <see cref="LogisticalDesignModel"/> (the engine half of the one parametric logistical form)
    /// REPRODUCES every base-mod logistical component — cargo holds, warehouses, fuel tanks, passenger/cryo/refrigerated/
    /// containment compartments, movers, the ordnance rack, troop bay, ship + ground magazines, the docking bay and the
    /// logistics office — from its <see cref="ContainerKind"/> + dials. Each hand-authored template falls out of the one
    /// form (the DESIGNER-NORTH-STAR reproduction claim, made executable).
    ///
    /// PURE (no colony harness → fast, not the slow CI shard). The reproduction VALUES are the ones each base-mod
    /// TEMPLATE's JSON Formulas compute (verified against the templates in <c>GameData/basemod/TemplateFiles/</c> +
    /// the design overrides in <c>componentDesigns.json</c>), so a drift in the model's per-kind arithmetic fails here.
    /// Byte-identical to the live game (nothing calls the model yet).
    ///
    /// The load-bearing assertions are (1) the *ATB* the model builds — its type AND its resulting field values, which
    /// include the behaviours the atb ctors apply for free (the int-truncated <see cref="CargoTransferAtb"/> rate, the
    /// clamped <see cref="DockBayAtb"/> door) — and (2) the emergent mass/crew/volume/cost the template's Formulas
    /// produce. Where the current game is BUGGY (the ordnance rack's near-negative storage, the fuel-cargo-hold's
    /// ~65-billion crew) the gauge pins the BUG, because byte-identity means reproducing the game as it is today.
    ///
    /// ⚠ TWO honest limits, recorded as risks (a PURE gauge cannot reach them): the merged "spaceport" template is
    /// defined twice across two files and the running game MERGES them property-by-property — this gauge asserts the
    /// storage.json arithmetic the door-spec concluded wins, but the actual merge winner needs the harness/real-load
    /// path to confirm; and the fuel-shell masses are transcendental, asserted against exact double values with a
    /// magnitude-relative tolerance so a 1-ULP libm difference between platforms can't red the run.
    /// </summary>
    [TestFixture]
    public class LogisticalDesignModelTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[logistical-model] " + m);

        // magnitude-aware tolerance: 1e-6 for ordinary values, a few ULP for the ~1e6..1e11 fuel/crew numbers.
        private static double Tol(double expected) => Math.Max(1e-6, Math.Abs(expected) * 1e-9);

        private static void AssertScalars(string name, LogisticalProfile p,
            double mass, double crew, double volume, double credit, double buildPoint, double research)
        {
            Log($"{name}: mass={p.MassPerUnit} crew={p.CrewReq} vol={p.Volume} credit={p.CreditCost} bp={p.BuildPointCost} rc={p.ResearchCost}");
            Assert.That(p.MassPerUnit, Is.EqualTo(mass).Within(Tol(mass)), $"{name} MassPerUnit");
            Assert.That(p.CrewReq, Is.EqualTo(crew).Within(Tol(crew)), $"{name} CrewReq");
            Assert.That(p.Volume, Is.EqualTo(volume).Within(Tol(volume)), $"{name} Volume");
            Assert.That(p.CreditCost, Is.EqualTo(credit).Within(Tol(credit)), $"{name} CreditCost");
            Assert.That(p.BuildPointCost, Is.EqualTo(buildPoint).Within(Tol(buildPoint)), $"{name} BuildPointCost");
            Assert.That(p.ResearchCost, Is.EqualTo(research).Within(Tol(research)), $"{name} ResearchCost");
        }

        private static T Single<T>(LogisticalProfile p) where T : class
        {
            Assert.That(p.Attributes.Count, Is.EqualTo(1), "expected exactly one attribute");
            Assert.That(p.Attributes[0], Is.InstanceOf<T>(), "attribute type");
            return (T)p.Attributes[0];
        }

        private static T At<T>(LogisticalProfile p, int i) where T : class
        {
            Assert.That(p.Attributes[i], Is.InstanceOf<T>(), $"attribute[{i}] type");
            return (T)p.Attributes[i];
        }

        private static void AssertStore(IComponentDesignAttribute a, string storeType, double maxVolume)
        {
            var s = (CargoStorageAtb)a;
            Assert.That(s.StoreTypeID, Is.EqualTo(storeType), "CargoStorageAtb.StoreTypeID");
            Assert.That(s.MaxVolume, Is.EqualTo(maxVolume).Within(Tol(maxVolume)), "CargoStorageAtb.MaxVolume");
        }

        private static void AssertTransfer(IComponentDesignAttribute a, int rateInt, double range)
        {
            var t = (CargoTransferAtb)a;
            Assert.That(t.TransferRate_kgs, Is.EqualTo(rateInt), "CargoTransferAtb.TransferRate_kgs (int-truncated)");
            Assert.That(t.TransferRange_ms, Is.EqualTo(range).Within(Tol(range)), "CargoTransferAtb.TransferRange_ms");
        }

        // ------------------------------------------------------------------------------------------------------------
        [Test]
        [Description("Every base-mod CargoStorageAtb hold (general 1t/5t, colony warehouse w/ its 10,000 m³ clamp, passenger, cryo, refrigerated + cold-store, containment + vault) falls out of the form: one store atb with the right class + capacity, and the per-class mass/volume/crew/cost coefficients.")]
        public void Holds_ReproduceFromTheForm()
        {
            // cargo-hold-1t: Storage Volume 1000 → sizeEff 10, mass 1000, crew 1, vol 1010.
            var h1 = new LogisticalDesignModel(ContainerKind.GeneralCargoHold, size: 1000).Compute();
            AssertStore(Single<CargoStorageAtb>(h1), "general-storage", 1000);
            AssertScalars("cargo-hold-1t", h1, mass: 1000, crew: 1, volume: 1010, credit: 120, buildPoint: 4500, research: 0);

            // cargo-hold-5t: Storage Volume 5000 → sizeEff 50, mass 5000, crew 5, vol 5050.
            var h5 = new LogisticalDesignModel(ContainerKind.GeneralCargoHold, size: 5000).Compute();
            AssertStore(Single<CargoStorageAtb>(h5), "general-storage", 5000);
            AssertScalars("cargo-hold-5t", h5, mass: 5000, crew: 5, volume: 5050, credit: 600, buildPoint: 22500, research: 0);

            // warehouse: Storage Volume 1,000,000 CLAMPS to 10,000 → sizeEff 100, mass 15,000, crew 10, vol 10,100.
            var wh = new LogisticalDesignModel(ContainerKind.Warehouse, size: 1_000_000).Compute();
            AssertStore(Single<CargoStorageAtb>(wh), "general-storage", 10_000);   // the clamp reproduced
            AssertScalars("warehouse", wh, mass: 15_000, crew: 10, volume: 10_100, credit: 1800, buildPoint: 67_500, research: 0);

            // passenger-cabin: Storage Volume 500 → sizeEff 5, mass 2000, crew 2, vol 510.
            var pc = new LogisticalDesignModel(ContainerKind.PassengerCabin, size: 500).Compute();
            AssertStore(Single<CargoStorageAtb>(pc), "passenger-storage", 500);
            AssertScalars("passenger-cabin", pc, mass: 2000, crew: 2, volume: 510, credit: 600, buildPoint: 12000, research: 0);

            // cryo-bay: Storage Volume 500 → sizeEff 5, mass 1500, crew Max(1,0.25)=1, vol 510.
            var cb = new LogisticalDesignModel(ContainerKind.CryoBay, size: 500).Compute();
            AssertStore(Single<CargoStorageAtb>(cb), "cryogenic-storage", 500);
            AssertScalars("cryo-bay", cb, mass: 1500, crew: 1, volume: 510, credit: 525, buildPoint: 9750, research: 0);

            // refrigerated-hold: Storage Volume 500 → sizeEff 5, mass 1000, crew Max(1,0.5)=1, vol 507.5.
            var rh = new LogisticalDesignModel(ContainerKind.RefrigeratedHold, size: 500).Compute();
            AssertStore(Single<CargoStorageAtb>(rh), "perishable-storage", 500);
            AssertScalars("refrigerated-hold", rh, mass: 1000, crew: 1, volume: 507.5, credit: 200, buildPoint: 5000, research: 0);

            // cold-store: Storage Volume 10,000 → sizeEff 100, mass 20,000, crew 10, vol 10,150.
            var cs = new LogisticalDesignModel(ContainerKind.RefrigeratedHold, size: 10_000).Compute();
            AssertStore(Single<CargoStorageAtb>(cs), "perishable-storage", 10_000);
            AssertScalars("cold-store", cs, mass: 20_000, crew: 10, volume: 10_150, credit: 4000, buildPoint: 100_000, research: 0);

            // containment-hold: Storage Volume 500 → sizeEff 5, mass 3000, crew Max(2,1)=2, vol 515.
            var ch = new LogisticalDesignModel(ContainerKind.ContainmentHold, size: 500).Compute();
            AssertStore(Single<CargoStorageAtb>(ch), "contained-storage", 500);
            AssertScalars("containment-hold", ch, mass: 3000, crew: 2, volume: 515, credit: 1350, buildPoint: 21000, research: 0);

            // containment-vault: Storage Volume 10,000 → sizeEff 100, mass 60,000, crew 20, vol 10,300.
            var cv = new LogisticalDesignModel(ContainerKind.ContainmentHold, size: 10_000).Compute();
            AssertStore(Single<CargoStorageAtb>(cv), "contained-storage", 10_000);
            AssertScalars("containment-vault", cv, mass: 60_000, crew: 20, volume: 10_300, credit: 27000, buildPoint: 420_000, research: 0);
        }

        // ------------------------------------------------------------------------------------------------------------
        [Test]
        [Description("The fuel containers: the steel fuel tanks (size = tank VOLUME → a real spherical-shell mass, with the fuel-farm's 1,000,000 m³ clamp) and the buggy fuel-cargo-hold (size = RADIUS, crew = tank volume ≈ 65 billion) — reproduced AS-IS to prove byte-identity even on the broken twin.")]
        public void FuelContainers_ReproduceFromTheForm()
        {
            // Steel-shell masses computed from the template's real geometry (r = (3V/4π)^(1/3);
            // DryWeight = 1.333·π·(r³−(r−0.004)³)·8000) — exact double values, magnitude-relative tolerance.
            var ft1000 = new LogisticalDesignModel(ContainerKind.FuelTank, size: 1000).Compute();
            AssertStore(Single<CargoStorageAtb>(ft1000), "fuel-storage", 1000);
            AssertScalars("fuel-tank-1000", ft1000, mass: 15461.280307219, crew: 0, volume: 1000,
                credit: 15461.280307219, buildPoint: 1546.1280307219, research: 0);

            var ft1500 = new LogisticalDesignModel(ContainerKind.FuelTank, size: 1500).Compute();
            AssertStore(Single<CargoStorageAtb>(ft1500), "fuel-storage", 1500);
            AssertScalars("fuel-tank-1500", ft1500, mass: 20261.660546930623, crew: 0, volume: 1500,
                credit: 20261.660546930623, buildPoint: 2026.1660546930623, research: 0);

            var ft3000 = new LogisticalDesignModel(ContainerKind.FuelTank, size: 3000).Compute();
            AssertStore(Single<CargoStorageAtb>(ft3000), "fuel-storage", 3000);
            AssertScalars("fuel-tank-3000", ft3000, mass: 32167.119651243007, crew: 0, volume: 3000,
                credit: 32167.119651243007, buildPoint: 3216.7119651243007, research: 0);

            // fuel-farm: Tank Volume 5,000,000 CLAMPS to 1,000,000 → CargoStorageAtb('fuel-storage', 1e6).
            var farm = new LogisticalDesignModel(ContainerKind.FuelTank, size: 5_000_000).Compute();
            AssertStore(Single<CargoStorageAtb>(farm), "fuel-storage", 1_000_000);   // clamp reproduced
            AssertScalars("fuel-farm-5000k", farm, mass: 1547025.6417820295, crew: 0, volume: 1_000_000,
                credit: 1547025.6417820295, buildPoint: 154702.56417820295, research: 0);

            // fuel-cargo-hold (template-only): default radius 2500 → TankVolume = (4/3)π·2500³ ≈ 6.545e10;
            // Mass = radius = 2500; Crew = TankVolume (the ~65-billion-crew bug); store capacity = TankVolume.
            var fch = new LogisticalDesignModel(ContainerKind.FuelCargoHold, size: 2500).Compute();
            AssertStore(Single<CargoStorageAtb>(fch), "fuel-storage", 65449846949.78735);
            AssertScalars("fuel-cargo-hold", fch, mass: 2500, crew: 65449846949.78735, volume: 2500,
                credit: 2500, buildPoint: 2500, research: 0);
        }

        // ------------------------------------------------------------------------------------------------------------
        [Test]
        [Description("The pure movers: the ship cargo shuttlebay and the surface spaceport (one CargoTransferAtb each, rate int-truncated, range derived from size and the Rate↔Range split) and the standalone space-port facility (flat mass, the 1,000,000-crew bug, rate/range set directly).")]
        public void Movers_ReproduceFromTheForm()
        {
            // shuttlebay: Size 5000, Rate-vs-Range 2 → rate 0.001·(2500+2500·2·0.1)=3.0→3(int); range 1.5·(2500−500)=3000.
            var sb = new LogisticalDesignModel(ContainerKind.Shuttlebay, size: 5000, split: 2).Compute();
            AssertTransfer(Single<CargoTransferAtb>(sb), rateInt: 3, range: 3000);
            AssertScalars("shuttlebay", sb, mass: 7500, crew: 5, volume: 5000, credit: 900, buildPoint: 33750, research: 0);

            // spaceport (storage.json): Size 12000, RvR 5 → rate 0.00075·(6000+6000·5·0.1)=6.75→6(int); range 1.0·(6000−3000)=3000.
            // RISK: 'spaceport' is defined in BOTH storage.json and installations.json and the running game MERGES them;
            // this asserts the storage.json arithmetic the door-spec concluded wins the merge — the actual merge winner
            // needs the harness/real-load path to confirm (recorded in the fixture's risk note).
            var sp = new LogisticalDesignModel(ContainerKind.Spaceport, size: 12000, split: 5).Compute();
            AssertTransfer(Single<CargoTransferAtb>(sp), rateInt: 6, range: 3000);
            AssertScalars("spaceport", sp, mass: 12000, crew: 1200, volume: 12000, credit: 1440, buildPoint: 54000, research: 0);

            // space-port (installations.json, standalone): defaults rate 5, range 50000 → CargoTransferAtb(5, 50000);
            // Mass 500,000 flat; Crew 1,000,000 flat (the third crew bug).
            var stand = new LogisticalDesignModel(ContainerKind.SpacePortStandalone, size: 0, split: 5, range: 50000).Compute();
            AssertTransfer(Single<CargoTransferAtb>(stand), rateInt: 5, range: 50000);
            AssertScalars("space-port-standalone", stand, mass: 500000, crew: 1000000, volume: 500000, credit: 120, buildPoint: 500000, research: 0);
        }

        // ------------------------------------------------------------------------------------------------------------
        [Test]
        [Description("The ordnance rack — the one 'both' kind: it carries a CargoStorageAtb ('ordnance-storage') AND a CargoTransferAtb, and reproduces the near-negative storage formula (Total = Rack − racking − rate) AS-IS.")]
        public void OrdnanceRack_ReproducesBothAtbsAndTheNegativeStorageFormula()
        {
            // ordnance-rack-2.5t: Rack 2627, Cargo Transfer Rate 100, Transfer Range 100
            //   → sizeEff 26.27; Total Stored = 2627 − 26.27 − 100 = 2500.73.
            var rack = new LogisticalDesignModel(ContainerKind.OrdnanceRack, size: 2627, split: 100, range: 100).Compute();
            Assert.That(rack.Attributes.Count, Is.EqualTo(2), "ordnance rack carries a store AND a mover");
            AssertStore(At<CargoStorageAtb>(rack, 0), "ordnance-storage", 2500.73);
            AssertTransfer(At<CargoTransferAtb>(rack, 1), rateInt: 100, range: 100);
            AssertScalars("ordnance-rack-2.5t", rack, mass: 2627, crew: 2.627, volume: 2627, credit: 120, buildPoint: 2627, research: 0);
        }

        // ------------------------------------------------------------------------------------------------------------
        [Test]
        [Description("The dedicated-attribute kinds: the troop bay (GroundBayAtb, carry class = Personnel), ship + ground magazines (kg of ammo, the ship magazine being the one logistical template with a non-zero research cost), the docking bay + heavy berth (DockBayAtb — the same tonnage split into 4 doors vs 1, each door's MaxHullMass emergent), and the logistics office (LogiBaseAtb).")]
        public void BaysMagazinesDockAndHub_ReproduceFromTheForm()
        {
            // troop-bay: defaults Capacity 6, CarryClass 0 (Personnel) → GroundBayAtb(6, Personnel); Mass 5000 const, Crew 10.
            var tb = new LogisticalDesignModel(ContainerKind.TroopBay, size: 6, split: 0).Compute();
            var bay = Single<GroundBayAtb>(tb);
            Assert.That(bay.Capacity, Is.EqualTo(6).Within(1e-6), "GroundBayAtb.Capacity");
            Assert.That(bay.CarryClass, Is.EqualTo(GroundCarryClass.Personnel), "GroundBayAtb.CarryClass");
            AssertScalars("troop-bay", tb, mass: 5000, crew: 10, volume: 50, credit: 600, buildPoint: 22500, research: 0);

            // ship-magazine: default Ammo Capacity 5000 → ShipMagazineAtb(5000); Mass 6000, Crew 120, ResearchCost = Mass.
            var sm = new LogisticalDesignModel(ContainerKind.ShipMagazine, size: 5000).Compute();
            Assert.That(Single<ShipMagazineAtb>(sm).Capacity_kg, Is.EqualTo(5000).Within(1e-6), "ShipMagazineAtb.Capacity_kg");
            AssertScalars("ship-magazine", sm, mass: 6000, crew: 120, volume: 3000, credit: 6000, buildPoint: 6000, research: 6000);

            // ground-magazine: default Capacity 500 → GroundMagazineAtb(500); Mass 1000, Crew 5.
            var gm = new LogisticalDesignModel(ContainerKind.GroundMagazine, size: 500).Compute();
            Assert.That(Single<GroundMagazineAtb>(gm).Capacity_kg, Is.EqualTo(500).Within(1e-6), "GroundMagazineAtb.Capacity_kg");
            AssertScalars("ground-magazine", gm, mass: 1000, crew: 5, volume: 10, credit: 200, buildPoint: 1000, research: 0);

            // docking-bay: Berth Tonnage 60000, Berths 4 → Max Hull Mass 15000; Mass 5000, Crew 8.
            var db = new LogisticalDesignModel(ContainerKind.DockingBay, size: 60000, split: 4).Compute();
            var dock = Single<DockBayAtb>(db);
            Assert.That(dock.BerthTonnage, Is.EqualTo(60000).Within(1e-6), "DockBayAtb.BerthTonnage");
            Assert.That(dock.MaxHullMass, Is.EqualTo(15000).Within(1e-6), "DockBayAtb.MaxHullMass (tonnage/berths)");
            AssertScalars("docking-bay", db, mass: 5000, crew: 8, volume: 120, credit: 1250, buildPoint: 25000, research: 2500);

            // heavy-berth: same 60000 tonnage, 1 berth → one wide door (MaxHullMass 60000); Mass 3500, Crew 2.
            var hb = new LogisticalDesignModel(ContainerKind.DockingBay, size: 60000, split: 1).Compute();
            var heavy = Single<DockBayAtb>(hb);
            Assert.That(heavy.BerthTonnage, Is.EqualTo(60000).Within(1e-6), "heavy-berth BerthTonnage");
            Assert.That(heavy.MaxHullMass, Is.EqualTo(60000).Within(1e-6), "heavy-berth MaxHullMass (one door = full tonnage)");
            AssertScalars("heavy-berth", hb, mass: 3500, crew: 2, volume: 120, credit: 875, buildPoint: 17500, research: 1750);

            // logistics-office: default Logistic Capacity 5 → LogiBaseAtb(5); Mass 5000 const, Crew 10.
            var lo = new LogisticalDesignModel(ContainerKind.LogisticsHub, size: 5).Compute();
            Assert.That(Single<LogiBaseAtb>(lo).LogisicCapacity, Is.EqualTo(5), "LogiBaseAtb.LogisicCapacity (int)");
            AssertScalars("logistics-office", lo, mass: 5000, crew: 10, volume: 5000, credit: 120, buildPoint: 5000, research: 0);
        }
    }
}
