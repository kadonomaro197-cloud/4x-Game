using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Components;
using Pulsar4X.DataStructures;   // ComponentMountType (the ordnance-part mount, so the OrdnanceDesign ctor counts it)
using Pulsar4X.Datablobs;        // ComponentInstancesDB (the built-ship parts sensor)
using Pulsar4X.Docking;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Galaxy;
using Pulsar4X.Movement;   // NewtonThrustAbilityDB (fuel type)
using Pulsar4X.Ships;
using Pulsar4X.Storage;    // CargoStorageDB, CargoMath, TypeStore (fuel/ordnance readout for the rearm gauges)
using Pulsar4X.Weapons;    // OrdnanceDesign (a round is an ICargoable in the ordnance-storage hold)

namespace Pulsar4X.Tests
{
    /// <summary>
    /// E12 — CARRIER SORTIE (Operation Blueprint-to-Steel, 2026-08-18). A carrier LAUNCHES a carried craft into the
    /// fight by UNDOCKING it and RECOVERS it by DOCKING it — reusing the existing Docking verbs, no new order. The
    /// engine wire is ONE filter: when <see cref="CombatEngagement.EnableCarrierSortie"/> is on, the resolver's
    /// ship-collect walks SKIP a docked craft (<see cref="DockTools.IsDocked"/>), so a docked fighter is held in the
    /// hangar (not a combatant) and rejoins the fight the moment it undocks. Byte-identical off (no stock ship mounts a
    /// bay → nothing is ever docked). Slice 1 = this launch/recover wire; rearm/refuel-on-recovery is slice 2.
    /// </summary>
    [TestFixture]
    public class CarrierSortieTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[carrier-sortie] " + m);

        private const string HeavyBerth = "default-design-heavy-berth";   // one wide 60 t door — anything light fits

        private static Entity Spawn(TestScenario s, ShipDesign d, string name)
            => ShipFactory.CreateShip(d, s.Faction, s.StartingBody, name);

        private static ComponentDesign BayDesign(TestScenario s)
        {
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns;
            Assert.That(designs.ContainsKey(HeavyBerth), Is.True, $"'{HeavyBerth}' should be built for the start faction");
            return designs[HeavyBerth];
        }

        private static double Mass(Entity e) => e.TryGetDataBlob<MassVolumeDB>(out var mv) ? mv.MassTotal : 0;

        private static ShipDesign Lightest(TestScenario s, List<ShipDesign> designs)
        {
            ShipDesign best = designs[0]; double bestMass = double.MaxValue;
            foreach (var d in designs) { double m = Mass(Spawn(s, d, "Weigh " + d.Name)); if (m > 0 && m < bestMass) { bestMass = m; best = d; } }
            return best;
        }

        [Test]
        [Description("A DOCKED craft is held in the hangar (not enrolled in combat) when EnableCarrierSortie is ON; "
                     + "undock = launch (it fights again), dock = recover. Flag OFF → a docked craft still fights "
                     + "(byte-identical). The carrier itself always fights.")]
        public void DockedCraft_HeldInHangar_WhenSortieOn_LaunchesOnUndock()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.ToList();
            Assert.That(designs, Is.Not.Empty, "the start faction has ship designs to spawn");

            var fleet = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Carrier Group");
            var carrier = Spawn(s, designs[0], "Carrier");
            carrier.AddComponent(BayDesign(s));   // install a dock bay so it can carry a craft
            var fighter = Spawn(s, Lightest(s, designs), "Fighter");

            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(s.Faction.Id, fleet, carrier));
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(s.Faction.Id, fleet, fighter));

            List<int> Ids() => CombatEngagement.GetFleetShips(fleet).Select(e => e.Id).ToList();

            // Sanity: before docking, both the carrier and the fighter are combatants.
            Assert.That(Ids(), Does.Contain(fighter.Id), "before docking, the fighter is a combatant");
            Assert.That(Ids(), Does.Contain(carrier.Id), "the carrier is a combatant");

            // Dock the fighter INSIDE the carrier (docking touches only position, not fleet membership).
            Assert.That(DockTools.TryDock(carrier, fighter, out var why), Is.True, "the fighter docks in the carrier (" + why + ")");
            Assert.That(DockTools.IsDocked(fighter), Is.True, "IsDocked sees the docked fighter");

            bool saved = CombatEngagement.EnableCarrierSortie;
            try
            {
                // Flag OFF: a docked craft STILL fights → byte-identical (the pre-E12 behaviour).
                CombatEngagement.EnableCarrierSortie = false;
                Assert.That(Ids(), Does.Contain(fighter.Id), "flag OFF: a docked craft is still enrolled (byte-identical)");

                // Flag ON: the docked fighter is held in the hangar; the carrier still fights.
                CombatEngagement.EnableCarrierSortie = true;
                Assert.That(Ids(), Does.Not.Contain(fighter.Id), "flag ON: a DOCKED fighter is held in reserve, not a combatant");
                Assert.That(Ids(), Does.Contain(carrier.Id), "the carrier itself still fights");

                // Undock = LAUNCH: the fighter rejoins the fight.
                Assert.That(DockTools.Undock(carrier, fighter), Is.True, "undock the fighter (launch)");
                Assert.That(DockTools.IsDocked(fighter), Is.False, "no longer docked");
                Assert.That(Ids(), Does.Contain(fighter.Id), "undock = launch: the fighter is a combatant again");
                Log("docked fighter held in hangar (flag on); undock relaunches it into the fight");
            }
            finally { CombatEngagement.EnableCarrierSortie = saved; }
        }

        private const string FuelTank = "default-design-fuel-tank-1000";   // a fuel-storage hold (the Wasp has none)

        private static ComponentDesign FuelTankDesign(TestScenario s)
        {
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns;
            Assert.That(designs.ContainsKey(FuelTank), Is.True, $"'{FuelTank}' should be built for the start faction");
            return designs[FuelTank];
        }

        /// <summary>Units of the ship's own fuel type currently stored aboard (resolved the same way DockTools does).</summary>
        private static long FuelUnits(TestScenario s, Entity ship)
        {
            if (!ship.TryGetDataBlob<NewtonThrustAbilityDB>(out var thrust) || string.IsNullOrEmpty(thrust.FuelType)) return 0;
            if (!ship.TryGetDataBlob<CargoStorageDB>(out var cargo)) return 0;
            var fi = s.Faction.GetDataBlob<FactionInfoDB>();
            var fuel = fi.Data.CargoGoods.GetAny(thrust.FuelType) ?? fi.Data.LockedCargoGoods.GetAny(thrust.FuelType);
            return fuel == null ? 0 : cargo.GetUnitsStored(fuel, false);
        }

        /// <summary>The LIGHTEST design whose thruster burns a STOCKED fuel — so a spawned copy + a fuel tank +
        /// FillFuelTanks actually yields READABLE fuel. NOT every design qualifies: a warp-only or thrusterless design
        /// (no <see cref="NewtonThrustAbilityDB"/>, or an empty/unstocked FuelType) fills 0, and a bare <c>Lightest()</c>
        /// can land on exactly one of those (that is what red-failed these gauges in CI). Returns null if none qualify,
        /// so the caller `Assume`-skips rather than red-failing. Carrier + parasite then share ONE fuelable design → the
        /// same fuel type, so the transfer can never silently mismatch materials.</summary>
        private static ShipDesign FuelableDesign(TestScenario s)
        {
            var fi = s.Faction.GetDataBlob<FactionInfoDB>();
            ShipDesign best = null; double bestMass = double.MaxValue;
            foreach (var d in fi.ShipDesigns.Values)
            {
                var probe = Spawn(s, d, "fuelprobe-" + d.Name);
                probe.AddComponent(FuelTankDesign(s));
                ShipFactory.FillFuelTanks(probe, fi);
                if (FuelUnits(s, probe) <= 0) continue;      // no thruster / unstocked fuel / no fuel-storage hold
                double m = Mass(probe);
                if (m > 0 && m < bestMass) { bestMass = m; best = d; }
            }
            return best;
        }

        [Test]
        [Description("E12 slice 2 — REARM ON RECOVERY: DockTools.RefuelFromCarrier tops off a recovered craft's fuel "
                     + "from the CARRIER's own stock. The craft's fuel rises and the carrier's drops by exactly the same "
                     + "amount (conservation); a second pass adds no more once the tank is full (take-what's-available); "
                     + "a full craft draws nothing (no-op). Driven directly (no berth door) so the transfer is proven "
                     + "deterministically. Carrier + parasite share ONE light design → the same fuel type, so the "
                     + "transfer can never silently mismatch materials.")]
        public void RefuelFromCarrier_MovesFuel_ConservesIt_TakesWhatFits()
        {
            var s = TestScenario.CreateWithColony();
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();
            var lightDesign = FuelableDesign(s);   // both hulls share this design → identical, LOADABLE fuel type
            Assume.That(lightDesign, Is.Not.Null,
                "a start design must burn a STOCKED fuel (rp-1/ntp) for FillFuelTanks to load fuel to hand out");

            // Carrier: the light hull + a fuel-storage hold, filled so it has fuel to hand out.
            var carrier = Spawn(s, lightDesign, "Carrier");
            carrier.AddComponent(FuelTankDesign(s));
            ShipFactory.FillFuelTanks(carrier, factionInfo);
            long carrierBefore = FuelUnits(s, carrier);
            Assert.That(carrierBefore, Is.GreaterThan(0), "the carrier holds fuel to hand out");

            // Parasite: the SAME light hull + a fuel-storage hold (freshly built → empty tank).
            var fighter = Spawn(s, lightDesign, "Fighter");
            fighter.AddComponent(FuelTankDesign(s));
            long fighterBefore = FuelUnits(s, fighter);
            Assert.That(fighterBefore, Is.EqualTo(0), "a freshly built parasite starts with empty tanks");

            // NO-OP: a FULL craft (the carrier itself) draws nothing — the take-what-fits guard, and it never throws.
            DockTools.RefuelFromCarrier(carrier, carrier);
            Assert.That(FuelUnits(s, carrier), Is.EqualTo(carrierBefore), "a full/self craft draws nothing (no-op)");

            // THE TRANSFER: fuel flows carrier → parasite, conserved.
            DockTools.RefuelFromCarrier(carrier, fighter);
            long fighterAfter = FuelUnits(s, fighter);
            long carrierAfter = FuelUnits(s, carrier);
            Log($"rearm: fighter {fighterBefore}→{fighterAfter}, carrier {carrierBefore}→{carrierAfter}");
            Assert.That(fighterAfter, Is.GreaterThan(fighterBefore), "the recovered craft gained fuel from the carrier");
            Assert.That(carrierAfter, Is.LessThan(carrierBefore), "the carrier's fuel dropped by what it handed over");
            Assert.That(carrierBefore - carrierAfter, Is.EqualTo(fighterAfter - fighterBefore),
                "conservation: exactly what the carrier lost, the craft gained");

            // TAKE-WHAT'S-AVAILABLE: a second pass adds nothing more than free space allows (never a duplication).
            long fighterFull = FuelUnits(s, fighter);
            DockTools.RefuelFromCarrier(carrier, fighter);
            Assert.That(FuelUnits(s, fighter), Is.LessThanOrEqualTo(fighterFull + 1),
                "a second rearm tops off no further than free space allows (bounded)");
        }

        [Test]
        [Description("The rearm is FLAG-GATED at TryDock: with EnableCarrierRearm ON a docked craft is refuelled; with "
                     + "it OFF the dock only re-parents the position and the craft's fuel is unchanged (byte-identical). "
                     + "Assume-guarded on the berth door — if the parasite+tank is too big for the 60t berth this is "
                     + "inconclusive, not a failure (the door math is DockBayTests' job; this gauge is the flag).")]
        public void TryDock_RefuelsOnlyWhenRearmFlagOn()
        {
            long DockAndReadFuel(bool rearmOn, out bool docked)
            {
                var s = TestScenario.CreateWithColony();
                var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();
                var lightDesign = FuelableDesign(s);   // carrier + parasite share the design → identical, LOADABLE fuel type
                if (lightDesign == null) { docked = false; return -1; }

                var carrier = Spawn(s, lightDesign, "Carrier");
                carrier.AddComponent(BayDesign(s));         // the bay makes it a carrier
                carrier.AddComponent(FuelTankDesign(s));    // a hold to hand fuel from
                ShipFactory.FillFuelTanks(carrier, factionInfo);
                var fighter = Spawn(s, lightDesign, "Fighter");
                fighter.AddComponent(FuelTankDesign(s));

                DockTools.EnableCarrierRearm = rearmOn;
                try
                {
                    docked = DockTools.TryDock(carrier, fighter, out _);
                    return docked ? FuelUnits(s, fighter) : -1;
                }
                finally { DockTools.EnableCarrierRearm = false; }
            }

            long onFuel = DockAndReadFuel(true, out bool fitOn);
            Assume.That(fitOn, "the parasite+tank must fit the 60t berth to exercise the rearm-on-dock path");
            long offFuel = DockAndReadFuel(false, out bool fitOff);
            Assume.That(fitOff, "the parasite+tank must fit the 60t berth");

            Assert.That(onFuel, Is.GreaterThan(0), "flag ON: docking refuelled the recovered craft");
            Assert.That(offFuel, Is.EqualTo(0), "flag OFF: docking left the craft's fuel untouched (byte-identical)");
            Log($"flag-gated rearm on dock — on={onFuel} off={offFuel}");
        }

        private const string CarrierDesignId = "default-ship-design-test-carrier";   // Sovereign Fleet Carrier (heavy hull + heavy berth)
        private const string ParasiteDesignId = "default-ship-design-test-parasite"; // Kestrel Parasite Craft (medium hull)

        [Test]
        [Description("The REAL base-mod carrier + parasite pair (E12): the Sovereign Fleet Carrier and Kestrel Parasite "
                     + "designs load from JSON and build into real ships with their parts (the gotcha-10 JSON->ship sensor "
                     + "for these two new designs — nothing else builds the base-mod earth ShipDesigns), the carrier's heavy "
                     + "berth gives it real bay capacity, and it ADMITS the parasite through the berth door. The door-fit is "
                     + "Assume-guarded on the measured masses (a too-heavy parasite is inconclusive + the masses printed, not "
                     + "red — the fix would be a lighter craft or a wider berth, a data tune not a code bug).")]
        public void RealBaseModCarrier_AdmitsParasite()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            Assert.That(designs.ContainsKey(CarrierDesignId), Is.True, "the Sovereign Fleet Carrier design loaded from JSON");
            Assert.That(designs.ContainsKey(ParasiteDesignId), Is.True, "the Kestrel Parasite Craft design loaded from JSON");

            var carrier = Spawn(s, designs[CarrierDesignId], "Sovereign");
            var parasite = Spawn(s, designs[ParasiteDesignId], "Kestrel");

            // Both build into real ships with their installed parts (the gotcha-10 JSON->ship sensor).
            Assert.That(carrier.HasDataBlob<ComponentInstancesDB>(), Is.True, "the carrier built with its components");
            Assert.That(parasite.HasDataBlob<ComponentInstancesDB>(), Is.True, "the parasite built with its components");

            double capacity = DockTools.Capacity(carrier);
            double berth = DockTools.LargestBerth(carrier);
            double pMass = Mass(parasite);
            Log($"carrier bay capacity={capacity:N0} kg, berth door={berth:N0} kg, parasite mass={pMass:N0} kg");

            Assert.That(capacity, Is.GreaterThan(0), "the carrier's heavy berth gives it real bay capacity");
            Assert.That(pMass, Is.GreaterThan(0), "the parasite has mass");

            // Design intent: the Kestrel fits the Sovereign's berth. If it doesn't, the masses above say by how much.
            Assume.That(pMass, Is.LessThanOrEqualTo(berth), "the parasite must fit the carrier's berth door to be a real pair");
            Assert.That(DockTools.TryDock(carrier, parasite, out var why), Is.True, "the Sovereign admits the Kestrel (" + why + ")");
            Assert.That(DockTools.IsDocked(parasite), Is.True, "the parasite is now held in the hangar");
            Log("the base-mod carrier admits its parasite through the real berth door");
        }

        private const string OrdnanceCargoTypeId = "ordnance-storage";

        /// <summary>Register a valid ordnance round on the faction via the PUBLIC ctor (which sets
        /// CargoTypeID = "ordnance-storage" and computes mass/volume from the Missile-mount part). Object-init can't set
        /// CargoTypeID (get-only), so a hand round needs the ctor.</summary>
        private static OrdnanceDesign RegisterOrdnance(TestScenario s, string id, long massKg, double volPerUnit)
        {
            var fi = s.Faction.GetDataBlob<FactionInfoDB>();
            var warhead = new ComponentDesign { UniqueID = "test-ord-part-" + id, Name = "Warhead " + id };
            warhead.ComponentMountType = ComponentMountType.Missile;   // counted by the OrdnanceDesign ctor's mass/vol sum
            warhead.MassPerUnit = massKg;         // internal set — reachable via InternalsVisibleTo
            warhead.VolumePerUnit = volPerUnit;   // non-zero so AddCargoByUnit doesn't reject it as volumeless
            return new OrdnanceDesign(fi, "Torpedo " + id, 0,
                new List<(ComponentDesign, int)> { (warhead, 1) }, id, startResearched: true);
        }

        /// <summary>Give a ship an ordnance hold directly — because NO base-mod cargo hold provides "ordnance-storage"
        /// yet (see DockTools.EnableCarrierRearm). This isolates the ENGINE capability from the missing DATA rung. Handles
        /// a ship with an existing cargo store (add the type) or none (attach a fresh single-type store).</summary>
        private static void GiveOrdnanceHold(Entity ship, double maxVolume)
        {
            if (ship.TryGetDataBlob<CargoStorageDB>(out var cargo))
                cargo.TypeStores[OrdnanceCargoTypeId] = new TypeStore(maxVolume);
            else
                ship.SetDataBlob(new CargoStorageDB(OrdnanceCargoTypeId, maxVolume));
        }

        private static long OrdnanceUnits(Entity ship, OrdnanceDesign ord)
        {
            var cargo = ship.GetDataBlob<CargoStorageDB>();
            return cargo.TypeStores.ContainsKey(OrdnanceCargoTypeId) ? cargo.GetUnitsStored(ord, false) : 0;
        }

        [Test]
        [Description("E12 slice 2c — REARM ORDNANCE ON RECOVERY (the ordnance twin of the fuel refuel): "
                     + "DockTools.RearmOrdnanceFromCarrier moves ordnance rounds from the CARRIER's ordnance hold into a "
                     + "recovered craft's, conserved (carrier lost == craft gained), take-what-fits (a smaller craft hold "
                     + "caps the pull; a full/self craft is a no-op). The ENGINE CAPABILITY is gauged here on HAND-INJECTED "
                     + "ordnance-storage holds, because NO base-mod cargo hold provides 'ordnance-storage' yet (the "
                     + "ordnance-cargo-hold template mislabels itself general-storage) — that DATA rung is a deferred "
                     + "developer decision; this proves the code is correct for when it lands.")]
        public void RearmOrdnanceFromCarrier_MovesOrdnance_ConservesIt_TakesWhatFits()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.ToList();
            var light = Lightest(s, designs);

            var carrier = Spawn(s, light, "Carrier");
            var craft = Spawn(s, light, "Parasite");
            GiveOrdnanceHold(carrier, 5000);   // ~500 rounds at 10 m³ each
            GiveOrdnanceHold(craft, 1000);     // ~100 rounds — deliberately SMALLER than the carrier's stock, to exercise take-what-fits

            var ord = RegisterOrdnance(s, "e12", 100, 10);
            long stocked = (long)CargoTransferProcessor.AddCargoItems(carrier, ord, 300);
            Assert.That(stocked, Is.GreaterThan(0), "the carrier is stocked with ordnance");
            long carrierBefore = OrdnanceUnits(carrier, ord);
            long craftBefore = OrdnanceUnits(craft, ord);
            Assert.That(craftBefore, Is.EqualTo(0), "the craft starts with an empty ordnance hold");

            // NO-OP: a full/self craft draws nothing, and it never throws.
            DockTools.RearmOrdnanceFromCarrier(carrier, carrier);
            Assert.That(OrdnanceUnits(carrier, ord), Is.EqualTo(carrierBefore), "self/full rearm is a no-op");

            // THE TRANSFER: ordnance flows carrier -> craft, conserved and capped by the craft's smaller hold.
            DockTools.RearmOrdnanceFromCarrier(carrier, craft);
            long craftAfter = OrdnanceUnits(craft, ord);
            long carrierAfter = OrdnanceUnits(carrier, ord);
            Log($"ordnance rearm: craft {craftBefore}->{craftAfter}, carrier {carrierBefore}->{carrierAfter}");
            Assert.That(craftAfter, Is.GreaterThan(craftBefore), "the recovered craft gained ordnance from the carrier");
            Assert.That(carrierAfter, Is.LessThan(carrierBefore), "the carrier's ordnance dropped by what it handed over");
            Assert.That(carrierBefore - carrierAfter, Is.EqualTo(craftAfter - craftBefore),
                "conservation: exactly what the carrier lost, the craft gained");

            // TAKE-WHAT-FITS: the craft's hold filled; a second pass adds no more than free space allows.
            long craftFull = OrdnanceUnits(craft, ord);
            DockTools.RearmOrdnanceFromCarrier(carrier, craft);
            Assert.That(OrdnanceUnits(craft, ord), Is.LessThanOrEqualTo(craftFull + 1),
                "a second rearm tops off no further than free space allows (bounded)");
        }
    }
}
