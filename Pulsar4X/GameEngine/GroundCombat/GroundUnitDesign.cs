using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.DataStructures;
using Pulsar4X.Industry;
using Pulsar4X.Storage;
using Pulsar4X.Colonies;
using Pulsar4X.Galaxy;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// A buildable ground unit — the ground echo of a ship design. Implements <see cref="IConstructableDesign"/>, so
    /// it rides the EXISTING industry rails for free: you research it, queue it at a colony, it consumes materials,
    /// and when the build completes it is placed on that colony's planet (the cradle-to-grave chain, one axis over
    /// from a ship). The combat resolver (slice 5c) reads the stats a raised <see cref="GroundUnit"/> snapshots from
    /// here.
    ///
    /// v1 is a plain C# design (like the combat-test ship designs were, before their JSON registration) — a follow-up
    /// wires a base-mod JSON template so it's player-buildable in a New Game without the six-point registration
    /// crashing the start (gotcha #10). Design: docs/GROUND-COMBAT-MAP-DESIGN.md (slice 5a).
    /// </summary>
    public class GroundUnitDesign : IConstructableDesign
    {
        // A ground unit is neither installed on a ship nor launched — it's placed on the surface by our own hook.
        public ConstructableGuiHints GuiHints => ConstructableGuiHints.None;

        [JsonProperty] public string UniqueID { get; set; }
        [JsonProperty] public string Name { get; set; }
        public bool IsValid => !string.IsNullOrEmpty(UniqueID);

        [JsonProperty] public Dictionary<string, long> ResourceCosts { get; set; } = new Dictionary<string, long>();
        [JsonProperty] public long IndustryPointCosts { get; set; }
        [JsonProperty] public string IndustryTypeID { get; set; }
        public ushort OutputAmount => 1;

        // --- ground combat stats (snapshotted onto each raised unit) ---
        [JsonProperty] public GroundUnitType UnitType { get; set; } = GroundUnitType.Infantry;
        [JsonProperty] public double Attack { get; set; }
        [JsonProperty] public double Defense { get; set; }
        // ⚙3 Defense — armour NATURE tuning: how well this design's plating soaks each incoming damage nature (the
        // Defense-weighted combination of its GroundArmorAtb parts). 1.0 = a plain plate (every design until a
        // nature-tuned plating is fitted → byte-identical). Snapshotted onto each raised unit's ArmourVs* fields.
        [JsonProperty] public double ArmourVsKinetic { get; set; } = 1.0;
        [JsonProperty] public double ArmourVsEnergy { get; set; } = 1.0;
        [JsonProperty] public double ArmourVsExplosive { get; set; } = 1.0;
        [JsonProperty] public double ArmourVsExotic { get; set; } = 1.0;
        [JsonProperty] public double HitPoints { get; set; }
        /// <summary>Strike RANGE in HEXES (H3) — the max hex-distance at which this unit can hit an enemy (0 = same hex
        /// only, 1 = also the adjacent ring, 3 = out to three rings). The unit's operational REACH: a longer-ranged
        /// unit damages a shorter-ranged one as it closes, without being hit back until the enemy is in ITS range — the
        /// ground echo of the space first-strike ("clone trooper vs a zerg swarm has the advantage until they reach
        /// them"). Range is in HEXES, not real metres, because at operational scale a hex is continental and its real
        /// size differs body-to-body (Earth vs Io) — <see cref="GroundRangeTools.RealReachKm"/> converts it for the
        /// readout. 0/unset → a per-type default (<see cref="GroundRangeTools.DefaultRangeFor"/>: Infantry 1, Armor 1,
        /// Artillery 3). Moddable per design.</summary>
        [JsonProperty] public int Range { get; set; }
        /// <summary>REAL-DISTANCE FOUNDATION (Slice 1b) — this design's weapon reach in real METRES, the metric TRUTH
        /// alongside the display <see cref="Range"/> (hexes). Built by <c>GroundUnitAssembly.Compute</c> from the weapons'
        /// hex ranges × a fixed nominal reference pitch (a real per-body pitch is a later slice) and snapshotted onto
        /// <see cref="GroundUnit.Range_m"/>. 0 = unset (a code-built / garrison design leaves it 0 → <c>RaiseUnit</c>
        /// derives it from the hex range). <b>ADDITIVE + UNREAD by the resolver</b> → byte-identical.
        /// Design: docs/combat/REAL-DISTANCE-COMBAT-DESIGN.md.</summary>
        [JsonProperty] public double Range_m { get; set; }
        /// <summary>SYSTEM ① survivability-by-dodge (0..1) — Σ augment evasion; snapshotted onto each raised unit.</summary>
        [JsonProperty] public double Evasion { get; set; }
        /// <summary>SYSTEM ① survivability-by-shield — flat incoming-damage soak pool; Σ augment shield.</summary>
        [JsonProperty] public double Shield { get; set; }
        /// <summary>⚙3 Defense — shield RECHARGE: fraction of full shield capacity restored per hour between salvos
        /// (Shield-weighted combination of the mounted augments' dials). Default 0.34 (the old global constant) →
        /// byte-identical. Snapshotted onto each raised unit's <see cref="GroundUnit.ShieldRegenFraction"/>.</summary>
        [JsonProperty] public double ShieldRegenFraction { get; set; } = 0.34;
        /// <summary>AMMO magazine capacity (kg) — Σ mounted magazines (weapon-unification B). Snapshotted onto each
        /// raised unit's <see cref="GroundUnit.MaxAmmo_kg"/>. 0 = no magazine / no ammo weapons.</summary>
        [JsonProperty] public double AmmoCapacity_kg { get; set; }
        /// <summary>SYSTEM ① primary damage flavour (from the heaviest weapon), for the future damage×defence matrix.</summary>
        [JsonProperty] public GroundWeaponMode DamageType { get; set; } = GroundWeaponMode.Ballistic;
        /// <summary>ARMOUR PENETRATION — how much of an enemy's flat armour (Defense) this unit's weapon IGNORES before
        /// the per-source soak (Weapons pilot W1c; the ground echo of <see cref="Pulsar4X.Combat.WeaponProfile.Penetration"/>).
        /// 0 = a normal round (bounces off heavy plate); a high value is an AP/sabot cracker. Snapshotted onto each
        /// raised unit's <see cref="GroundUnit.Penetration"/>. Moddable per design (the base-mod Armor unit carries it —
        /// a tank's AP main gun).</summary>
        [JsonProperty] public double Penetration { get; set; }
        /// <summary>PER-SHOT ENERGY — how much of this design's Attack is one shot (Weapons pilot W2c; the ground echo of
        /// <see cref="Pulsar4X.Combat.WeaponProfile.PerShotEnergy"/>). 0 = a single lump; a cannon delivers a big alpha
        /// that punches flat armour, small arms chip and bounce. Snapshotted onto each raised unit's
        /// <see cref="GroundUnit.PerShotEnergy"/>. Moddable per design (the base-mod Armor unit's main gun is a big alpha).</summary>
        [JsonProperty] public double PerShotEnergy { get; set; }
        /// <summary>W1 — the per-weapon LOADOUT (one <see cref="GroundWeaponMount"/> per mounted weapon component). Built
        /// by <c>GroundUnitAssembly.Compute</c> from the design's weapon parts and snapshotted onto each raised
        /// <see cref="GroundUnit.WeaponLoadout"/>. Empty for a code-built / garrison design → the resolver falls back to
        /// the single collapsed weapon (byte-identical). ADDITIVE — carried now; the resolver reads it in W2.</summary>
        [JsonProperty] public List<GroundWeaponMount> WeaponLoadout { get; set; } = new List<GroundWeaponMount>();
        /// <summary>STANDING UPKEEP in credits/month this unit costs its owning faction simply by EXISTING — the ground
        /// echo of a station's operating cost, billed monthly by <see cref="GroundUpkeep"/>. 0 = free (byte-identical: no
        /// existing design sets it). Snapshotted onto <see cref="GroundUnit.UpkeepCredits"/> at raise.
        /// <para>This slice ships the MECHANISM + gauge only; the value's SOURCE is a deliberate follow-up (a balance
        /// decision — the developer's call to scale it off the unit's pay + build cost). To make a real garrison cost
        /// money, set this at one of the three design-builder sites: <c>GroundUnitAssembly.ToGroundUnitDesign</c> (the
        /// player-assembled 6-man-squad path — it does NOT set this today), <c>GroundStartGarrison</c>'s code-built
        /// designs, and/or a base-mod <c>GroundUnitAtb</c> ctor arg (note: adding a ctor arg is NOT template-free — the
        /// JSON binder is exact-arity, so all base-mod ground-unit templates must be updated in lockstep, gotcha #10).</para></summary>
        [JsonProperty] public double UpkeepCredits { get; set; }
        /// <summary>VETERANCY / TRAINING multiplier — how much better-than-green this unit is, the ground echo of a ship's
        /// <see cref="Pulsar4X.Combat.UnitCaliberAtb"/> elite stamp (Firepower×Toughness). At raise it multiplies the
        /// unit's <c>Attack</c> AND toughness (<c>MaxHealth</c>) — an elite unit hits harder and takes more — the
        /// developer's "training reflected in game." <b>1.0 = green/untrained → byte-identical</b> (every existing design
        /// keeps 1.0). The <c>= 1.0</c> initializer is LOAD-BEARING: it is the fallback for an old save whose JSON lacks
        /// this property (Newtonsoft keeps the ctor value for an absent field), so a pre-veterancy design never loads at
        /// 0× and zeroes its units. The multiplier is BAKED into the stats at raise, never re-read by the resolver (the
        /// developer's constraint). Its COST (elite units are dearer to build/train) rides the costed training-component
        /// slice (B); this slice (A) is the multiplier mechanism + gauge.</summary>
        [JsonProperty] public double TrainingMultiplier { get; set; } = 1.0;
        /// <summary>ENVIRONMENTAL GEAR (E4) — per-hazard protection this design's units carry, keyed by the shared
        /// <see cref="Pulsar4X.Hazards.HazardEffectType"/>. Value 0..1 = fraction of that hazard's attrition negated
        /// (a "heat-shielded" design has <c>{HeatDamage: 0.8}</c>). Snapshotted onto each raised <see cref="GroundUnit"/>.
        /// The cradle-to-grave counter: in the full model this is a researched/installed GEAR component (like a ship's
        /// <c>HazardResistanceAtb</c>); v1 folds it into the design (its cost already rides <see cref="ResourceCosts"/>),
        /// with gear-as-a-swappable-component the v2 promotion alongside units-as-entities.</summary>
        [JsonProperty] public Dictionary<Pulsar4X.Hazards.HazardEffectType, double> EnvironmentalResistance { get; set; }
            = new Dictionary<Pulsar4X.Hazards.HazardEffectType, double>();
        /// <summary>Which region a completed build musters into (v1: the capital region 0; a chosen muster point is a
        /// refinement). Clamped to the world's real region count at placement.</summary>
        [JsonProperty] public int DefaultRegionIndex { get; set; } = 0;

        /// <summary>SQUAD SIZE — how many <see cref="GroundUnit"/>s ONE build of this design raises (the assembler dial:
        /// "how many units are made when this is built"). A build job that completes raises this many units into the
        /// muster region, so a designer stamps out a squad/platoon in one order instead of queueing them one at a time.
        /// The design's build COST + build-TIME scale with it (set in <c>GroundUnitAssembly.ToGroundUnitDesign</c>), so
        /// a squad of 10 costs 10× a single unit — no free multiplication (CONVENTIONS §16). Composes with the industry
        /// batch (order N jobs → N × UnitsPerBuild units). <b>1 = byte-identical</b> (every code-built / garrison design,
        /// and an old save whose JSON lacks the field — the <c>= 1</c> initializer is the load-bearing fallback).</summary>
        [JsonProperty] public int UnitsPerBuild { get; set; } = 1;

        /// <summary>The COMPONENTS this unit is built from — the mounted component-design ids → count (frame + parts).
        /// KEEPING these (instead of only the flattened combat stats above) is the foundation of units-as-entities
        /// (Option A, docs/GROUND-UNITS-AS-ENTITIES-DESIGN.md): once a raised unit carries these as real
        /// <c>ComponentInstance</c>s, every ability (radar-reveal / speed / crew / weapons) falls out of the SAME
        /// component infrastructure a ship uses, with no per-ability special-casing. Populated by the assembler; the
        /// chassis is identified by its <see cref="GroundChassisAtb"/>, not a separate flag. Additive — the flat stats
        /// stay the combat read-model until every reader is migrated off them (slice 6).</summary>
        [JsonProperty] public Dictionary<string, int> ComponentDesignIds { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// The industry processor calls this when a build finishes. Mirrors <c>ComponentDesign.OnConstructionComplete</c>'s
        /// batch bookkeeping, but instead of installing a component it PLACES a ground unit on the colony's planet.
        /// Defensive: a colony with no planet / no region layer, or a missing production line, simply skips that part
        /// (never throws — it runs inside the daily industry hotloop).
        /// </summary>
        public void OnConstructionComplete(Entity industryEntity, CargoStorageDB storage, string productionLine, IndustryJob batchJob, IConstructableDesign designInfo)
        {
            batchJob.NumberCompleted++;
            batchJob.ResourcesRequiredRemaining = new Dictionary<string, long>(designInfo.ResourceCosts);
            batchJob.ProductionPointsLeft = designInfo.IndustryPointCosts;

            // Place the raised unit on the colony's planet surface (ground map).
            if (industryEntity.TryGetDataBlob<ColonyInfoDB>(out var colony)
                && colony.PlanetEntity != null && colony.PlanetEntity != Entity.InvalidEntity)
            {
                var body = colony.PlanetEntity;
                int region = DefaultRegionIndex;
                if (body.TryGetDataBlob<PlanetRegionsDB>(out var regions) && regions.Regions.Count > 0)
                    region = Math.Max(0, Math.Min(DefaultRegionIndex, regions.Regions.Count - 1));
                // SQUAD SIZE — one build raises UnitsPerBuild units into the muster region (the assembler dial). 1 →
                // exactly one raise, byte-identical. The cost/time already scaled by UnitsPerBuild at design time.
                int squad = Math.Max(1, UnitsPerBuild);
                for (int i = 0; i < squad; i++)
                    GroundForces.RaiseUnit(body, this, industryEntity.FactionOwnerID, region);
            }

            // Job lifecycle (same as ComponentDesign) — guarded so a test/host without a real production line is safe.
            if (batchJob.NumberCompleted >= batchJob.NumberOrdered
                && industryEntity.TryGetDataBlob<IndustryAbilityDB>(out var industryDB)
                && industryDB.ProductionLines.ContainsKey(productionLine))
            {
                industryDB.ProductionLines[productionLine].Jobs.Remove(batchJob);
                if (batchJob.Auto)
                {
                    batchJob.NumberCompleted = 0;
                    industryDB.ProductionLines[productionLine].Jobs.Add(batchJob);
                }
            }
        }
    }
}
