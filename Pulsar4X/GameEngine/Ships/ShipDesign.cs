using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Pulsar4X.Blueprints;
using Pulsar4X.Components;
using Pulsar4X.DataStructures;
using Pulsar4X.Industry;
using Pulsar4X.Interfaces;
using Pulsar4X.Extensions;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Damage;
using Pulsar4X.Engine;
using Pulsar4X.Names;
using Pulsar4X.Storage;

namespace Pulsar4X.Ships
{
    [JsonObject]
    public class ShipDesign : ICargoable, IConstructableDesign, ISerializable
    {
        public ConstructableGuiHints GuiHints { get; } = ConstructableGuiHints.CanBeLaunched;
        public int ID { get; private set; } = Game.GetEntityID();
        public string UniqueID { get; private set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string CargoTypeID { get; }
        public int DesignVersion { get; set; }= 0;
        public bool IsObsolete { get; set; } = false;
        public bool IsValid { get; set; } = true; // Used by ship designer & production
        public long MassPerUnit { get; private set; }
        public double VolumePerUnit { get; private set; }
        public double Density { get; }

        // ── §0b mass-budget cap (dossier ⚙11) — Slice A: COMPUTED & EXPOSED, NOT yet enforced ──────────
        // The "structural budget" a ship's mounted mass must fit within — the space echo of the ground
        // GroundUnitAssembly carry cap (which reads GroundChassisAtb.BaseStrength, sums part mass, and marks
        // an over-budget design invalid). Ships have no hull-chassis component yet, so Slice A sources the
        // budget from the design's OWN mass at 1.0 headroom: MassBudget == MassPerUnit, so OverMassBudget is
        // always false here — byte-identical to pre-cap. This lands the machinery + the calibration readout
        // (ShipMassBudgetTests prints every design's MassPerUnit). A later slice replaces this source with a
        // real hull ceiling (sized above the heaviest base-mod ship) and lets IsValid bite. `MassPerUnit`
        // above IS the "mass used"; no separate field needed.
        private const double MassBudgetHeadroom = 1.0;
        public long MassBudget { get; private set; }
        public bool OverMassBudget { get; private set; }

        /// <summary>When true, an over-budget design (mass exceeds its mounted hull's Mass Budget) is marked
        /// <see cref="IsValid"/> = false, so the client production list refuses to build it — the §0b "the physical
        /// budget forces the build" gate made to BITE. Default OFF (every base-mod ship sits under its hull budget,
        /// so flipping it changes nothing → byte-identical); the client turns it on. The enforcement slice the D1
        /// comment above anticipated ("a later slice … lets IsValid bite").</summary>
        public static bool EnforceMassBudget = false;

        private int _factionId;

        /// <summary>
        /// m^3
        /// </summary>
        //public double Volume;

        /// <summary>
        /// This lists all the components in order for the design, from front to back, and how many "wide".
        /// note that component types can be split/arranged ie:
        /// (bridge,1), (fueltank,2), (cargo,1)(fueltank,1)(engine,3) would have a bridge at teh front,
        /// then two fueltanks behind, one cargo, another single fueltank, then finaly three engines.
        /// </summary>
        public List<(ComponentDesign design, int count)> Components;
        public (ArmorBlueprint type, float thickness) Armor;
        public Dictionary<string, long> ResourceCosts { get; internal set; } = new Dictionary<string, long>();
        public Dictionary<string, long> MineralCosts = new Dictionary<string, long>();
        public Dictionary<string, long> MaterialCosts = new Dictionary<string, long>();
        public Dictionary<string, long> ComponentCosts = new Dictionary<string, long>();
        public Dictionary<string, long> ShipInstanceCost = new Dictionary<string, long>();
        public int CrewReq;
        /// <summary>
        /// The slice of <see cref="CrewReq"/> that must be drawn from the building colony's SCARCE talent pool
        /// (veteran cadre), not bulk workforce — the anti-dominance handle for Enhancers ⚙6.2 Unit Caliber. It is
        /// the summed crew of the design's caliber modules (a <see cref="Pulsar4X.Combat.UnitCaliberAtb"/>-carrying
        /// component). ZERO for every ship with no caliber module → byte-identical: bulk crew commit stays CrewReq,
        /// talent commit is nothing. When a caliber module IS fitted, that crew comes out of talent instead of bulk,
        /// so an elite hull ties up scarce officers and can't be spammed. Computed in <see cref="Recalculate"/>.
        /// </summary>
        public int TalentReq;
        public long IndustryPointCosts { get; private set; }

        //TODO: this is one of those places where moddata has bled into hardcode...
        //the guid here is from IndustryTypeData.json "Ship Assembly"
        public string IndustryTypeID { get; } = "ship-assembly"; //new Guid("91823C5B-A71A-4364-A62C-489F0183EFB5");
        public ushort OutputAmount { get; } = 1;

        // ── P4.2 (Operation Earthfall) — built-ship charge/fuel provisioning policy ──────────────────────
        // A production-built hull boots with a DEAD reactor and EMPTY tanks by design: ShipFactory.CreateShip
        // leaves EnergyStored = 0 and tanks empty on purpose, so a ship earns its charge/fuel at a colony over
        // game-time (the fuel economy depends on it). That deliberate rule STRANDS the NPC invasion chain — a
        // freshly-built transport/warship can't spin a warp bubble (paid out of STORED electricity) the instant
        // it exists, so the massed sealift never sails (findings/A4-sealift.md cause 3). The fix charges + fuels
        // a built hull at build-complete — but split by owner so the developer's "a production ship earns its
        // charge over game-time" stays TRUE for the human player. Two independent policy flags (both FLAGGED —
        // the developer owns balance/policy):
        //   ChargeBuiltNpcShips    default ON  — the AI's production sealift boots ready to fly.
        //   ChargeBuiltPlayerShips default ON  — developer decision (2026-07-21): the player's production ships
        //     also boot charged + fuelled (frictionless — no waiting on a reactor to charge). Flip to false to
        //     restore "a production ship earns its charge over game-time."
        public static bool ChargeBuiltNpcShips = true;      // FLAGGED balance value (built-ship provisioning policy)
        public static bool ChargeBuiltPlayerShips = true;   // FLAGGED — dev decision 2026-07-21 (was false)

        /// <summary>
        /// Apply the built-ship charge/fuel provisioning policy to a freshly production-built ship. Called at
        /// BOTH engine build paths — the direct <see cref="OnConstructionComplete"/> CreateShip branch and
        /// <see cref="LaunchComplexProcessor.TryLaunchShip"/> — so a built hull is provisioned identically
        /// however it's produced. Charges the reactor + fills the fuel tanks (via
        /// <see cref="ShipFactory.ChargeReactors"/> / <see cref="ShipFactory.FillFuelTanks"/>) when the owning
        /// faction's policy flag is set: <see cref="ChargeBuiltNpcShips"/> for an NPC owner (default ON — so the
        /// AI sealift can warp the instant a hull exists), <see cref="ChargeBuiltPlayerShips"/> for a player
        /// owner (default ON as of the 2026-07-21 developer decision — the player's production ships also boot
        /// ready to fly). Defensive / no-throw. The engine test suite is unaffected: CI builds player ships via
        /// <see cref="ShipFactory.CreateShip"/> directly (which never calls this), and the one test that asserts
        /// the OFF branch (NpcFleetReadyToSailTests) forces the flag false in its own try/finally.
        /// </summary>
        public static void ProvisionBuiltShip(Entity ship, Entity ownerFaction)
        {
            if (ship == null || ownerFaction == null) return;
            if (!ownerFaction.TryGetDataBlob<FactionInfoDB>(out var factionInfo)) return;
            bool provision = factionInfo.IsNPC ? ChargeBuiltNpcShips : ChargeBuiltPlayerShips;
            if (!provision) return;
            ShipFactory.ChargeReactors(ship);
            ShipFactory.FillFuelTanks(ship, factionInfo);
        }

        public void OnConstructionComplete(Entity industryEntity, CargoStorageDB storage, string productionLine, IndustryJob batchJob, IConstructableDesign designInfo)
        {
            var industryDB = industryEntity.GetDataBlob<IndustryAbilityDB>();
            batchJob.NumberCompleted++;
            batchJob.ResourcesRequiredRemaining = new Dictionary<string, long>(designInfo.ResourceCosts);
            batchJob.ProductionPointsLeft = designInfo.IndustryPointCosts;

            var faction = industryEntity.GetFactionOwner;

            if (industryEntity.TryGetDataBlob<LaunchComplexDB>(out var launchDB))
            {
                var shipName = NameFactory.GetShipName(industryEntity.Manager.Game);
                launchDB.LaunchQueue.Add(new LaunchQueueEntry
                {
                    DesignId = designInfo.UniqueID,
                    ShipName = shipName
                });
            }
            else
            {
                var industryParent = industryEntity.GetSOIParentEntity();
                if(industryParent == null) throw new NullReferenceException("industryParent cannot be null");

                var ship = ShipFactory.CreateShip((ShipDesign)designInfo, faction, industryParent);
                if(faction.TryGetDataBlob<FleetDB>(out var fleetDB))
                {
                    fleetDB.AddChild(ship);
                }

                // M3-2b: stamp WHICH colony's pool crewed this ship, so a later destroy/disband releases the
                // right pool. (The commit itself is below, so it runs for the launch-complex path too — where
                // the ship isn't born here but queued; LaunchComplexProcessor.TryLaunchShip stamps it then.)
                if (CrewReq > 0 && ship.TryGetDataBlob<ShipInfoDB>(out var builtInfo))
                    builtInfo.CrewSourceColonyId = industryEntity.Id;

                // P4.2 (Operation Earthfall) — provision the freshly-built hull per the built-ship charge/fuel
                // policy: an NPC-owned ship boots charged + fuelled so the AI's production sealift can warp the
                // instant it exists (findings/A4-sealift.md cause 3). Player ships ALSO boot charged + fuelled as of
                // developer decision 2026-07-21 (ChargeBuiltPlayerShips flipped false→true — frictionless; flip back
                // to restore "a production ship earns its charge over game-time"). See the policy doc above.
                ProvisionBuiltShip(ship, faction);
            }

            // M3-2b: commit the crew from the building colony's manpower pool at build-complete — for BOTH the
            // direct-launch path (above) and the launch-complex queue path (the ship launches later). Inert if
            // the host has no pool (a station) — CommitCrew no-ops.
            // Enhancers ⚙6.2: the veteran-cadre slice (TalentReq) comes out of the SCARCE talent pool, the rest out
            // of bulk workforce. TalentReq is 0 for every non-caliber ship, so this is byte-identical to the old
            // single CommitCrew(CrewReq) for the whole base-mod fleet.
            Pulsar4X.Colonies.ManpowerTools.CommitCrew(industryEntity, CrewReq - TalentReq);
            Pulsar4X.Colonies.ManpowerTools.CommitTalent(industryEntity, TalentReq);

            if (batchJob.NumberCompleted == batchJob.NumberOrdered)
            {
                industryDB.ProductionLines[productionLine].Jobs.Remove(batchJob);
                if (batchJob.Auto)
                {
                    batchJob.NumberCompleted = 0;
                    industryDB.ProductionLines[productionLine].Jobs.Add(batchJob);
                }
            }
        }

        public int CreditCost;
        public EntityDamageProfileDB DamageProfileDB;

        [JsonConstructor]
        internal ShipDesign()
        {
        }

        public ShipDesign(FactionInfoDB faction, string name, List<(ComponentDesign design, int count)> components, (ArmorBlueprint armorType, float thickness) armor, string? id = null)
        {
            if(id != null) UniqueID = id;
            _factionId = faction.OwningEntity.Id;
            Name = name;
            Components = components;
            Armor = armor;
            Recalculate(faction);
        }

        /// <summary>
        /// Recalculates all derived properties (costs, mass, crew, etc.) from the current Components and Armor.
        /// </summary>
        public void Recalculate(FactionInfoDB faction)
        {
            MassPerUnit = 0;
            CrewReq = 0;
            TalentReq = 0;
            CreditCost = 0;
            VolumePerUnit = 0;
            ResourceCosts.Clear();
            MineralCosts.Clear();
            MaterialCosts.Clear();
            ComponentCosts.Clear();
            ShipInstanceCost.Clear();

            int crewAutomation = 0; // Enhancers ⚙6.3: bulk crew replaced by automation/AI suites (0 = no module).
            foreach (var component in Components)
            {
                MassPerUnit += component.design.MassPerUnit * component.count;
                CrewReq += component.design.CrewReq;
                // Enhancers ⚙6.2: a caliber module's crew is a veteran CADRE — it draws from the scarce talent pool,
                // not bulk workforce. Track that slice so the commit at build-complete splits crew vs talent. Zero
                // for every non-caliber component → byte-identical for the entire base-mod fleet.
                if (component.design.TryGetAttribute<Pulsar4X.Combat.UnitCaliberAtb>(out _))
                    TalentReq += component.design.CrewReq;
                // Enhancers ⚙6.3 Systems ▸ Automation: an AI/automation suite runs part of the ship, so it needs
                // fewer BULK crew. Accumulate the reduction (applied to the bulk workforce after the loop, never the
                // veteran cadre). 0 for every non-automation component → byte-identical.
                if (component.design.TryGetAttribute<Pulsar4X.Combat.CrewAutomationAtb>(out var autom))
                    crewAutomation += (int)Math.Round(autom.CrewReduction);
                CreditCost += component.design.CreditCost;
                VolumePerUnit += component.design.VolumePerUnit * component.count;
                if (ComponentCosts.ContainsKey(component.design.UniqueID))
                {
                    ComponentCosts[component.design.UniqueID] = ComponentCosts[component.design.UniqueID] + component.count;
                }
                else
                {
                    ComponentCosts.Add(component.design.UniqueID, component.count);
                }

            }
            // Enhancers ⚙6.3 Systems ▸ Automation: subtract the automation reduction from the BULK crew only — the
            // total never drops below the veteran-cadre talent (you can't automate away the officers). No module →
            // crewAutomation 0 → CrewReq = TalentReq + (CrewReq - TalentReq) = CrewReq, byte-identical.
            if (crewAutomation > 0)
                CrewReq = TalentReq + Math.Max(0, (CrewReq - TalentReq) - crewAutomation);
            DamageProfileDB = new EntityDamageProfileDB(Components, Armor);
            var armorMass = GetArmorMass(DamageProfileDB, faction.Data.CargoGoods);
            MassPerUnit += (long)Math.Round(armorMass);
            MineralCosts.ToList().ForEach(x => ResourceCosts[x.Key] = x.Value);
            MaterialCosts.ToList().ForEach(x => ResourceCosts[x.Key] = x.Value);
            ComponentCosts.ToList().ForEach(x => ResourceCosts[x.Key] = x.Value);
            IndustryPointCosts = (long)(MassPerUnit * 0.1);

            // §0b mass-budget (dossier ⚙11), Slice D1 — a mounted HULL (ShipHullAtb) now sets the mass budget
            // (the space echo of the ground GroundChassisAtb carry cap). A hull-less design falls back to the
            // generous self-derived figure, so a design with no hull is byte-identical (nothing over budget) until
            // a real hull is fitted. Still compute & EXPOSE only — IsValid is untouched and no engine construction
            // path is gated (the client production list reads IsValid; the engine does not yet). Generous hull
            // budgets keep OverMassBudget false even once ships mount one, per the developer's call.
            double hullBudget = 0;
            bool hasHull = false;
            foreach (var component in Components)
            {
                if (component.design.TryGetAttribute<ShipHullAtb>(out var hull))
                {
                    hullBudget += hull.MassBudget * component.count;
                    hasHull = true;
                }
            }
            MassBudget = hasHull ? (long)hullBudget : (long)(MassPerUnit * MassBudgetHeadroom);
            OverMassBudget = MassPerUnit > MassBudget;
            // §0b enforcement (dossier ⚙11, the slice D1 anticipated). Over its hull budget → the design won't build.
            // Flag OFF by default and every base-mod ship is under budget, so this is byte-identical; the client turns
            // it on. Only ever marks invalid — it never re-validates a design flagged invalid for another reason.
            if (EnforceMassBudget && OverMassBudget)
                IsValid = false;
        }

        /// <summary>
        /// Recalculates derived properties and stores the design in the factionInfo.
        /// </summary>
        /// <param name="faction"></param>
        public void Initialise(FactionInfoDB faction)
        {
            Recalculate(faction);
            faction.ShipDesigns[UniqueID] = this;
            faction.IndustryDesigns[UniqueID] = this;
        }

        public static double GetArmorMass(EntityDamageProfileDB damageProfile, CargoDefinitionsLibrary cargoLibrary)
        {
            if (damageProfile.ArmorVertex.Count == 0)
                return 0;
            var armor = damageProfile.Armor;
            double surfaceArea = 0;
            (int x, int y) v1 = damageProfile.ArmorVertex[0];
            for (int index = 1; index < damageProfile.ArmorVertex.Count; index++)
            {
                (int x, int y) v2 = damageProfile.ArmorVertex[index];

                var r1 = v1.y; //radius of top
                var r2 = v2.y; //radius of bottom
                var h = v2.x - v1.x; //height
                var c1 = 2* Math.PI * r1; //circumference of top
                var c2 = 2 * Math.PI * r2; //circumference of bottom
                var sl = Math.Sqrt(h * h + (r1 - r2) * (r1 - r2)); //slope of side

                surfaceArea += 0.5 * sl * (c1 + c2);

                v1 = v2;
            }

            var aresource = cargoLibrary.GetAny(armor.armorType.ResourceID);
            if (aresource == null)
                return 0; // Armor material isn't in this faction's cargo library (not unlocked/defined).
                          // Return 0 mass rather than NRE-crashing the whole app from the ship-design UI,
                          // which recomputes stats every frame a design is selected.
            var amass = aresource.MassPerUnit;
            var avol = aresource.VolumePerUnit;
            var aden = amass / avol;
            var armorVolume = surfaceArea * armor.thickness * 0.001;
            var armorMass = armorVolume * aden;
            return armorMass;
        }

        /// <summary>
        /// Note: this itterates through the design list, so it's not particuarly efficent for per frame use.
        /// </summary>
        /// <param name="components">out list of components that have the given attribute</param>
        /// <typeparam name="T">attribute type</typeparam>
        /// <returns>true if design has componenst with this attribute</returns>
        public bool TryGetComponentsByAttribute<T>(out List<(ComponentDesign design, int count)> components)
            where T : IComponentDesignAttribute
        {
            bool hasComponents = false;
             components = new ();
            foreach (var component in Components)
            {
                if (component.design.HasAttribute<T>())
                {
                    hasComponents = true;
                    components.Add(component);
                }
            }
            return hasComponents;
        }

        /// <summary>
        /// Returns a dictionary of component designs by attribute type.
        /// Note that there will be doubleups of components where a component has multiple attributes.
        /// </summary>
        /// <returns></returns>
        public Dictionary<Type, List<(ComponentDesign design, int count)>> GetComponentsByAttribute()
        {
            Dictionary<Type, List<(ComponentDesign design, int count)>> dict = new();
            foreach (var component in Components)
            {
                foreach (var kvp in component.design.AttributesByType)
                {
                    if (!dict.ContainsKey(kvp.Key))
                        dict.Add(kvp.Key, new List<(ComponentDesign design, int count)>());
                    dict[kvp.Key].Add(component);
                }
            }
            return dict;
        }


        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(UniqueID), UniqueID);
            info.AddValue(nameof(Name), Name);
            info.AddValue(nameof(_factionId), _factionId);
            info.AddValue(nameof(Armor), Armor);
            info.AddValue(nameof(Components), Components);
        }

        /// <summary>
        /// creates a clone of this object
        /// </summary>
        /// <returns></returns>
        public ShipDesign Clone(FactionInfoDB faction)
        {
            var components = new List<(ComponentDesign design, int count)>(Components);
            var armor = Armor;
            var newDesign = new ShipDesign(faction, Name, components, armor);

            return newDesign;

        }
    }
}
