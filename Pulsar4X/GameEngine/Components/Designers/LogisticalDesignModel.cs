using System;
using System.Collections.Generic;
using Pulsar4X.Interfaces;
using Pulsar4X.Storage;
using Pulsar4X.Combat;
using Pulsar4X.Docking;
using Pulsar4X.GroundCombat;
using Pulsar4X.Logistics;

namespace Pulsar4X.Components.Designers
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL, Phase C — the LOGISTICAL door parametric designer, slice 1 (ENGINE MODEL).
    ///
    /// WHAT IT IS, in plain English: the "logistical" door is every compartment that HOLDS or MOVES something — a
    /// cargo hold, a fuel tank, a warehouse, an ordnance rack, a troop bay, a ship or ground ammo magazine, a docking
    /// bay for whole vessels, a logistics office. Today the in-game designer picks one of ~17 hand-authored templates
    /// off a menu. This class is the ENGINE HALF of replacing that menu with ONE parametric form: you pick WHAT KIND of
    /// container it is and slide a couple of dials, and the exact component the base mod ships falls out of the one
    /// form. The ImGui screen that drives it is slice 2 (client, verified on the developer's machine — CI can compile
    /// the client but can't run it).
    ///
    /// WHY IT'S SHAPED THIS WAY (the derivation, per <c>docs/economy/DESIGNER-NORTH-STAR.md</c>): a "door" is DERIVED
    /// from the numbers the simulation actually reads. Reading the source (not the menu), a logistical component's real
    /// output is (a) one or two component-design *ATTRIBUTES* the sim reads — a <see cref="CargoStorageAtb"/> (how many
    /// cubic metres of a named cargo class it holds), a <see cref="CargoTransferAtb"/> (a loading rate + a shuttle Δv
    /// range), a <see cref="GroundBayAtb"/> (troop/vehicle carry-room), a <see cref="ShipMagazineAtb"/> /
    /// <see cref="GroundMagazineAtb"/> (kg of ammo), a <see cref="DockBayAtb"/> (berth tonnage split into doors), or a
    /// <see cref="LogiBaseAtb"/> (how many goods a trade office handles) — plus (b) the emergent build numbers every
    /// component has: mass, crew, volume, and the three costs. Group those by the question each answers and the FORCED
    /// half (which attribute the thing gets) becomes the CHOICE while the FREE half (how big) becomes the sliders:
    ///   • CHOICE — <see cref="ContainerKind"/>: the FORCED job. It decides which *Atb(s) the component carries AND the
    ///     mass/crew/cost coefficient family, so it decides which slider is even active. (A troop bay's carry class —
    ///     Personnel vs Vehicle, the engine's <see cref="GroundCarryClass"/> — rides on <see cref="Split"/> for that one
    ///     kind, exactly as the atb's own ctor reads a 0/1 double.)
    ///   • SLIDER 1 — <see cref="Size"/>: the ONE dial on most templates — cubic metres of hold / kg of ammo / carry-room
    ///     / berth tonnage / a logistics office's item count / a fuel tank's volume. It drives the primary atb's capacity
    ///     AND (through the template's Mass formula) the whole mass→crew→credit→build-point→research cascade.
    ///   • SLIDER 2 — <see cref="Split"/> (and, for the two-mover-dial cases, <see cref="Range"/>): the SECOND dial that
    ///     only some kinds use — a mover's RATE↔RANGE trade, a docking bay's BERTHS count (which carves the berth tonnage
    ///     into that many doors → the per-door <c>MaxHullMass</c>), or the troop bay's carry class.
    ///
    /// THE HONEST CAVEAT (the load-bearing finding, flagged for the developer): the design-north-star's clean ideal is
    /// "two attributes (a store + a mover) and one cargo-CLASS dial." The AS-BUILT engine does NOT match that — it uses
    /// FIVE dedicated attributes beyond the store/mover pair (troops, ship ammo, ground ammo, docks, and the logistics
    /// office are each their OWN atb, not a cargo TYPE), and the "holds" themselves are ~ten separate templates with
    /// DIFFERENT mass/volume/crew coefficients (a general hold masses 100×its racking, a warehouse 150×, a passenger
    /// cabin 400×, a containment hold 600×; a steel fuel tank computes a real spherical-shell weight). A literal
    /// 2-choice/2-slider form therefore CANNOT reproduce every shipped component — so for a byte-identical slice-1 the
    /// CHOICE is a granular <see cref="ContainerKind"/> that fuses "which kind" with "which class/coefficient family."
    /// Collapsing those parallel stores into one cargo-CLASS dial is a real unification, but it is a BEHAVIOUR CHANGE
    /// for a later slice; this model reproduces the parallel templates AS-BUILT (including their bugs — see below) so the
    /// gauge can prove byte-identity first.
    ///
    /// REPRODUCES THE CURRENT (buggy) GAME, NOT A FIXED ONE. Byte-identity means matching what the running game builds
    /// today, warts included: the ordnance rack's "Total Stored = Rack − racking − rate" formula that would go NEGATIVE
    /// on a small rack; the <see cref="ContainerKind.FuelCargoHold"/> whose crew requirement is literally its tank VOLUME
    /// (~65 billion crew at the default radius — an unbuildable twin); the standalone space-port's flat 1,000,000-crew
    /// constant. Those are reproduced on purpose; the "apply the proposed fixes" pass is a separate, live-data-touching
    /// slice, deliberately kept OUT of this additive one.
    ///
    /// BYTE-IDENTICAL / SAFE (slice-1 rules): this is a NEW file, a pure value type with no serialized state, no DataBlob,
    /// no <c>*Atb</c> ctor change — nothing in the live game calls it yet, so the whole engine is unchanged. It does not
    /// re-implement the *Atb ctors' truncation/clamp behaviour by hand — it CONSTRUCTS the real <see cref="CargoTransferAtb"/>
    /// (which truncates its rate to an int), <see cref="DockBayAtb"/> (which clamps a door bigger than the whole bay), and
    /// the rest, so those behaviours are reproduced for free by the SAME ctors the game calls. What it DOES re-implement
    /// is each template's JSON <c>PropertyFormula</c> arithmetic (mass/crew/volume/cost + the Min/Max clamp on the size
    /// dial) so the gauge <c>LogisticalDesignModelTests</c> can prove it reproduces every base-mod logistical component.
    /// </summary>
    public readonly struct LogisticalDesignModel
    {
        /// <summary>CHOICE — the FORCED job: which attribute(s) the component carries and which mass/crew/cost family it
        /// uses. Fuses "kind" with "cargo-class/coefficient variant" (see the honest caveat above).</summary>
        public ContainerKind Kind { get; }

        /// <summary>SLIDER 1 — the primary dial (cubic metres of hold / kg of ammo / carry-room / berth tonnage / a fuel
        /// tank's volume / a fuel-cargo-hold's radius / a logistics office's item count). Clamped to the kind's Min/Max,
        /// so a warehouse dialed past its 10,000 m³ ceiling reproduces the game's clamp to 10,000.</summary>
        public double Size { get; }

        /// <summary>SLIDER 2 — the SECOND dial, meaning set by <see cref="Kind"/>: a mover's RATE↔RANGE trade (1..9), a
        /// docking bay's BERTHS count (1..20), the troop bay's carry class (0 = Personnel / 1 = Vehicle), or — for the
        /// standalone space-port and the ordnance rack — the transfer RATE. 0 for kinds that don't use it.</summary>
        public double Split { get; }

        /// <summary>Third dial — used only by the two kinds whose mover takes an explicit rate AND range
        /// (<see cref="ContainerKind.OrdnanceRack"/> and <see cref="ContainerKind.SpacePortStandalone"/>): the shuttle
        /// transfer RANGE (Δv). 0 otherwise.</summary>
        public double Range { get; }

        public LogisticalDesignModel(ContainerKind kind, double size, double split = 0, double range = 0)
        {
            Kind = kind;
            Size = size;
            Split = split;
            Range = range;
        }

        /// <summary>
        /// Build the <see cref="LogisticalProfile"/> — the real <c>*Atb</c> instance(s) the sim reads PLUS the emergent
        /// build numbers (mass/crew/volume/credit/build-point/research) — matching what the base-mod template produces
        /// for the matching hand-authored component, so a design made through this model builds identically.
        /// </summary>
        public LogisticalProfile Compute()
        {
            switch (Kind)
            {
                // ---- CargoStorageAtb "holds" — sizeEff-based mass/crew, one general-storage-class store ---------------
                case ContainerKind.GeneralCargoHold:
                    return Hold("general-storage", massK: 100, volOverhead: 1.0, crewFloor: 1, crewCoeff: 0.1, creditK: 0.12, bpK: 4.5);
                case ContainerKind.Warehouse:
                    return Hold("general-storage", massK: 150, volOverhead: 1.0, crewFloor: 1, crewCoeff: 0.1, creditK: 0.12, bpK: 4.5);
                case ContainerKind.PassengerCabin:
                    return Hold("passenger-storage", massK: 400, volOverhead: 2.0, crewFloor: 2, crewCoeff: 0.4, creditK: 0.3, bpK: 6.0);
                case ContainerKind.CryoBay:
                    return Hold("cryogenic-storage", massK: 300, volOverhead: 2.0, crewFloor: 1, crewCoeff: 0.05, creditK: 0.35, bpK: 6.5);
                case ContainerKind.RefrigeratedHold:
                    return Hold("perishable-storage", massK: 200, volOverhead: 1.5, crewFloor: 1, crewCoeff: 0.1, creditK: 0.2, bpK: 5.0);
                case ContainerKind.ContainmentHold:
                    return Hold("contained-storage", massK: 600, volOverhead: 3.0, crewFloor: 2, crewCoeff: 0.2, creditK: 0.45, bpK: 7.0);

                case ContainerKind.FuelTank:        return FuelTank();
                case ContainerKind.FuelCargoHold:   return FuelCargoHold();

                case ContainerKind.Shuttlebay:      return Mover(minSize: 2000, maxSize: 200000, massK: 1.5, crewFloor: 5, crewCoeff: 0.001, rateK: 0.001, rangeK: 1.5);
                case ContainerKind.Spaceport:       return Mover(minSize: 2000, maxSize: 2000000, massK: 1.0, crewFloor: 1, crewCoeff: 0.1, rateK: 0.00075, rangeK: 1.0);
                case ContainerKind.SpacePortStandalone: return SpacePortStandalone();

                case ContainerKind.OrdnanceRack:    return OrdnanceRack();
                case ContainerKind.TroopBay:        return TroopBay();
                case ContainerKind.ShipMagazine:    return ShipMagazine();
                case ContainerKind.GroundMagazine:  return GroundMagazine();
                case ContainerKind.DockingBay:      return DockingBay();
                case ContainerKind.LogisticsHub:    return LogisticsHub();

                default:
                    throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unknown logistical container kind");
            }
        }

        // ---- helpers: each mirrors one template's JSON PropertyFormula arithmetic exactly -----------------------------

        private static double Clamp(double v, double min, double max) => v < min ? min : (v > max ? max : v);

        /// <summary>The <c>Size Efficiency</c> property shared by every CargoStorageAtb hold: Max(1, volume * 0.01).</summary>
        private static double SizeEff(double volume) => Math.Max(1.0, volume * 0.01);

        /// <summary>General/warehouse/passenger/cryo/refrigerated/containment holds — all share the same shape, only the
        /// coefficients differ (the honest caveat's "parallel stores"). Mass = sizeEff × massK; Volume = volume +
        /// sizeEff × volOverhead; Crew = Max(crewFloor, sizeEff × crewCoeff); costs off Mass.</summary>
        private LogisticalProfile Hold(string storeType, double massK, double volOverhead, double crewFloor, double crewCoeff, double creditK, double bpK)
        {
            double volume = Clamp(Size, 10, 10000);
            double sizeEff = SizeEff(volume);
            double mass = sizeEff * massK;
            double vol = volume + sizeEff * volOverhead;
            double crew = Math.Max(crewFloor, sizeEff * crewCoeff);
            var atbs = new List<IComponentDesignAttribute> { new CargoStorageAtb(storeType, volume) };
            return new LogisticalProfile(atbs, mass, crew, vol, mass * creditK, mass * bpK, 0);
        }

        /// <summary>Stainless-steel fuel tank — size dial is the tank VOLUME; the mass is a real spherical-shell weight.
        /// radius = (3V / 4π)^(1/3); DryWeight = 1.333·π·(r³ − (r−0.004)³)·8000. (The template literals are 1/3 and
        /// 1.333; NCalc's division converts the numerator to double so 1/3 = 0.333…, and 1.333 is the truncated 4/3 the
        /// template actually writes — reproduced verbatim.)</summary>
        private LogisticalProfile FuelTank()
        {
            double volume = Clamp(Size, 1, 1000000);
            double r = Math.Pow(3.0 * (volume / (4.0 * Math.PI)), 1.0 / 3.0);
            double mass = 1.333 * Math.PI * (Math.Pow(r, 3) - Math.Pow(r - 0.004, 3)) * 8000;
            var atbs = new List<IComponentDesignAttribute> { new CargoStorageAtb("fuel-storage", volume) };
            // Volume = tank volume; Crew = 0; CreditCost = [Mass]; BuildPointCost = [Mass] * 0.1; ResearchCost = 0.
            return new LogisticalProfile(atbs, mass, 0, volume, mass, mass * 0.1, 0);
        }

        /// <summary>The buggy fuel-cargo-hold twin — size dial is the tank RADIUS; TankVolume = (4/3)·π·r³ (the store's
        /// capacity), Mass = the radius itself, and CrewReq = the tank VOLUME (the ~65-billion-crew bug, reproduced
        /// AS-IS). Template-only (no shipped standalone design); covered at the default radius.</summary>
        private LogisticalProfile FuelCargoHold()
        {
            double radius = Clamp(Size, 1, 1000000);
            double tankVolume = (4.0 / 3.0) * Math.PI * Math.Pow(radius, 3);
            double mass = radius;                 // Mass = PropertyValue('Tank Radius')
            var atbs = new List<IComponentDesignAttribute> { new CargoStorageAtb("fuel-storage", tankVolume) };
            // Volume = [Mass] = radius; Crew = Tank Volume (the bug); CreditCost = [Mass]; BuildPointCost = [Mass].
            return new LogisticalProfile(atbs, mass, tankVolume, mass, mass, mass, 0);
        }

        /// <summary>A shuttlebay/spaceport mover — one CargoTransferAtb, rate/range derived from Size and the Rate↔Range
        /// split. rate = rateK·(S·0.5 + S·0.5·RvR·0.1); range = rangeK·(S·0.5 − S·0.5·RvR·0.1). The atb's own ctor
        /// truncates the rate to an int, so we let it.</summary>
        private LogisticalProfile Mover(double minSize, double maxSize, double massK, double crewFloor, double crewCoeff, double rateK, double rangeK)
        {
            double size = Clamp(Size, minSize, maxSize);
            double rvr = Clamp(Split, 1, 9);
            double rate = rateK * (size * 0.5 + size * 0.5 * rvr * 0.1);
            double range = rangeK * (size * 0.5 - size * 0.5 * rvr * 0.1);
            double mass = size * massK;
            double crew = Math.Max(crewFloor, size * crewCoeff);
            var atbs = new List<IComponentDesignAttribute> { new CargoTransferAtb(rate, range) };
            return new LogisticalProfile(atbs, mass, crew, size, mass * 0.12, mass * 4.5, 0);
        }

        /// <summary>The standalone "space-port" facility (installations.json) — a mover with a FLAT mass and a FLAT
        /// 1,000,000 crew (the third crew bug), and rate/range set directly by two dials (rate on <see cref="Split"/>,
        /// range on <see cref="Range"/>). Template-only; covered at defaults (rate 5, range 50000).</summary>
        private LogisticalProfile SpacePortStandalone()
        {
            double rate = Clamp(Split, 1, 10000);
            double range = Clamp(Range, 100, 1000000);
            const double mass = 500000;           // flat
            var atbs = new List<IComponentDesignAttribute> { new CargoTransferAtb(rate, range) };
            // CrewReq = 1000000 const; Volume = [Mass]; CreditCost = 120 const; BuildPointCost = [Mass]; ResearchCost = 0.
            return new LogisticalProfile(atbs, mass, 1000000, mass, 120, mass, 0);
        }

        /// <summary>The ordnance rack — the one "both" kind: a CargoStorageAtb ('ordnance-storage') AND a
        /// CargoTransferAtb. Size = Rack Size, Split = the cargo transfer rate, Range = the transfer range. Reproduces
        /// the negative-storage formula AS-IS: TotalStored = Rack − (Rack·0.01) − rate.</summary>
        private LogisticalProfile OrdnanceRack()
        {
            double rack = Clamp(Size, 100, 1000000);
            double rate = Clamp(Split, 100, 1000000);
            double range = Clamp(Range, 100, 1000000);
            double sizeEff = rack * 0.01;
            double totalStored = rack - sizeEff - rate;   // the negative-storage formula, verbatim
            double mass = rack;                           // Mass = PropertyValue('Rack Size')
            var atbs = new List<IComponentDesignAttribute>
            {
                new CargoStorageAtb("ordnance-storage", totalStored),
                new CargoTransferAtb(rate, range)
            };
            // Volume = [Mass]; Crew = sizeEff * 0.1; CreditCost = 120 const; BuildPointCost = [Mass]; ResearchCost = 0.
            return new LogisticalProfile(atbs, mass, sizeEff * 0.1, mass, 120, mass, 0);
        }

        /// <summary>Troop/vehicle bay — a GroundBayAtb. Size = carry-room capacity, Split = carry class
        /// (0 = <see cref="GroundCarryClass.Personnel"/> / 1 = <see cref="GroundCarryClass.Vehicle"/>). Mass/crew are
        /// flat constants.</summary>
        private LogisticalProfile TroopBay()
        {
            double capacity = Clamp(Size, 1, 60);
            double carryClass = Clamp(Split, 0, 1);
            const double mass = 5000;             // flat
            var atbs = new List<IComponentDesignAttribute> { new GroundBayAtb(capacity, carryClass) };
            // Volume = [Mass] * 0.01; Crew = 10 const; CreditCost = [Mass]*0.12; BuildPointCost = [Mass]*4.5; RC = 0.
            return new LogisticalProfile(atbs, mass, 10, mass * 0.01, mass * 0.12, mass * 4.5, 0);
        }

        /// <summary>Ship ammo magazine — a ShipMagazineAtb (kg of ammo). The one logistical template with a non-zero
        /// research cost (= its mass).</summary>
        private LogisticalProfile ShipMagazine()
        {
            double capacity = Clamp(Size, 100, 100000);
            double mass = capacity * 1.2;
            var atbs = new List<IComponentDesignAttribute> { new ShipMagazineAtb(capacity) };
            // Volume = [Mass]*0.5; Crew = [Mass]*0.02; ResearchCost = [Mass]; CreditCost = [Mass]; BuildPointCost = [Mass].
            return new LogisticalProfile(atbs, mass, mass * 0.02, mass * 0.5, mass, mass, mass);
        }

        /// <summary>Ground ammo magazine — a GroundMagazineAtb (kg of ammo).</summary>
        private LogisticalProfile GroundMagazine()
        {
            double capacity = Clamp(Size, 100, 5000);
            double mass = capacity * 2;
            var atbs = new List<IComponentDesignAttribute> { new GroundMagazineAtb(capacity) };
            // Volume = [Mass]*0.01; Crew = 5 const; CreditCost = [Mass]*0.2; BuildPointCost = [Mass]; ResearchCost = 0.
            return new LogisticalProfile(atbs, mass, 5, mass * 0.01, mass * 0.2, mass, 0);
        }

        /// <summary>Docking bay — a DockBayAtb: Size = berth tonnage (the budget), Split = the number of berths (doors).
        /// The per-door MaxHullMass = tonnage / berths; the atb's own ctor clamps a door bigger than the whole bay.</summary>
        private LogisticalProfile DockingBay()
        {
            double berthTonnage = Clamp(Size, 1000, 5000000);
            double berths = Clamp(Split, 1, 20);
            double maxHullMass = berthTonnage / berths;
            double mass = berthTonnage * 0.05 + berths * 500;
            double crew = Math.Max(2, berths * 2);
            var atbs = new List<IComponentDesignAttribute> { new DockBayAtb(berthTonnage, maxHullMass) };
            // Volume = tonnage*0.002; ResearchCost = [Mass]*0.5; CreditCost = [Mass]*0.25; BuildPointCost = [Mass]*5.0.
            return new LogisticalProfile(atbs, mass, crew, berthTonnage * 0.002, mass * 0.25, mass * 5.0, mass * 0.5);
        }

        /// <summary>Logistics office — a LogiBaseAtb (how many distinct goods a trade hub handles). Flat mass/crew.</summary>
        private LogisticalProfile LogisticsHub()
        {
            double capacity = Clamp(Size, 5, 100);
            const double mass = 5000;             // flat
            var atbs = new List<IComponentDesignAttribute> { new LogiBaseAtb(capacity) };
            // Volume = [Mass]; Crew = 10 const; CreditCost = 120 const; BuildPointCost = [Mass]; ResearchCost = 0.
            return new LogisticalProfile(atbs, mass, 10, mass, 120, mass, 0);
        }
    }

    /// <summary>
    /// The FORCED job of a logistical container — the CHOICE the parametric form is built around. Each value maps to one
    /// base-mod template family: it decides which <c>*Atb</c>(s) the component carries AND its mass/crew/cost coefficient
    /// set (see <see cref="LogisticalDesignModel"/>'s honest caveat for why "kind" and "cargo class" are fused here rather
    /// than being two independent choices).
    /// </summary>
    public enum ContainerKind
    {
        /// <summary>general-cargo-hold — bulk general-storage on a ship (mass = racking × 100).</summary>
        GeneralCargoHold,
        /// <summary>warehouse-facility — bulk general-storage on a colony (mass = racking × 150), capped at 10,000 m³.</summary>
        Warehouse,
        /// <summary>stainless-steel-fuel-tank — fuel-storage; size dial is the tank VOLUME; a real spherical-shell mass.</summary>
        FuelTank,
        /// <summary>fuel-cargo-hold — fuel-storage; size dial is the tank RADIUS; the buggy ~65-billion-crew twin.</summary>
        FuelCargoHold,
        /// <summary>passenger-cabin — passenger-storage for living people (mass × 400, needs stewards).</summary>
        PassengerCabin,
        /// <summary>cryo-bay — cryogenic-storage (mass × 300, cheapest way to move a population).</summary>
        CryoBay,
        /// <summary>refrigerated-hold — perishable-storage for food and anything alive (mass × 200).</summary>
        RefrigeratedHold,
        /// <summary>containment-hold — contained-storage for cargo dangerous to its own carrier (mass × 600).</summary>
        ContainmentHold,
        /// <summary>cargo-Shuttlebay — a ship's cargo mover (CargoTransferAtb; rate coeff 0.001, range coeff 1.5).</summary>
        Shuttlebay,
        /// <summary>spaceport (storage.json) — a surface cargo mover (CargoTransferAtb; rate coeff 0.00075, range coeff 1.0).</summary>
        Spaceport,
        /// <summary>space-port (installations.json) — a standalone mover with a FLAT mass and 1,000,000-crew bug; rate/range set directly.</summary>
        SpacePortStandalone,
        /// <summary>ordnance-cargo-hold — the "both" kind: an ordnance-storage store AND a mover; the negative-storage formula.</summary>
        OrdnanceRack,
        /// <summary>troop-bay — a GroundBayAtb; Split picks the carry class (Personnel/Vehicle).</summary>
        TroopBay,
        /// <summary>ship-magazine — a ShipMagazineAtb (kg of ship ammo).</summary>
        ShipMagazine,
        /// <summary>ground-magazine — a GroundMagazineAtb (kg of ground ammo).</summary>
        GroundMagazine,
        /// <summary>docking-bay — a DockBayAtb; Split carves the berth tonnage into that many doors.</summary>
        DockingBay,
        /// <summary>logistics-office — a LogiBaseAtb (how many goods a trade hub handles).</summary>
        LogisticsHub
    }

    /// <summary>
    /// What a <see cref="LogisticalDesignModel"/> produces: the real <c>*Atb</c> instance(s) the simulation reads off the
    /// component, PLUS the emergent build numbers every component has (the same six the base-mod template's Formulas
    /// compute). The atbs are the ACTUAL engine objects (constructed with the same ctors the JSON binder uses), so their
    /// int-truncation / clamp behaviour is reproduced for free — the gauge reads their public fields to prove byte-identity.
    /// </summary>
    public readonly struct LogisticalProfile
    {
        /// <summary>The component-design attribute(s) the sim reads — one for a plain hold/mover/bay/magazine, two for the
        /// ordnance rack (a store + a mover). Real engine instances, not a description.</summary>
        public IReadOnlyList<IComponentDesignAttribute> Attributes { get; }
        /// <summary>Component mass, kg (the base-mod "Mass" formula) — the currency the whole cost cascade rides on.</summary>
        public double MassPerUnit { get; }
        /// <summary>Operating crew the component demands.</summary>
        public double CrewReq { get; }
        /// <summary>Component volume, m³.</summary>
        public double Volume { get; }
        /// <summary>Credit build cost.</summary>
        public double CreditCost { get; }
        /// <summary>Industry build-point cost.</summary>
        public double BuildPointCost { get; }
        /// <summary>Research point cost (zero for all but the ship magazine among the logistical templates).</summary>
        public double ResearchCost { get; }

        public LogisticalProfile(IReadOnlyList<IComponentDesignAttribute> attributes, double massPerUnit, double crewReq,
            double volume, double creditCost, double buildPointCost, double researchCost)
        {
            Attributes = attributes;
            MassPerUnit = massPerUnit;
            CrewReq = crewReq;
            Volume = volume;
            CreditCost = creditCost;
            BuildPointCost = buildPointCost;
            ResearchCost = researchCost;
        }
    }
}
