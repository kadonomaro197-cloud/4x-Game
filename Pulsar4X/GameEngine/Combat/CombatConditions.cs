using Pulsar4X.Engine;
using Pulsar4X.Hazards;
using Pulsar4X.Orbital;

namespace Pulsar4X.Combat
{
    /// <summary>
    /// THE ENVIRONMENT A BATTLE IS FOUGHT IN — a small bundle of multipliers (+ an ambient damage rate) the combat
    /// resolver can read so a fight is shaped by WHERE it happens, not fought in a featureless void.
    ///
    /// Plain English: a fleet brawling inside a nebula can't see far, its beams scatter, and the gas corrodes hulls;
    /// a clean patch of deep space does none of that. This struct is that difference expressed as numbers the shared
    /// combat math already speaks — each field is a ×multiplier (1.0 = "no change / clean space") except the ambient
    /// damage-over-time, which is joules/second. It is the space-side mirror of what GROUND combat already reads off
    /// its terrain + <c>PlanetEnvironmentsDB</c> (see <c>GroundForcesProcessor</c>); the design is
    /// docs/combat/ENVIRONMENT-CONDITIONS-DESIGN.md ("an environment is a named bundle of multipliers").
    ///
    /// <b>Wiring status — SLICE 1 (the reader, this file): NO combat caller yet, so it is byte-identical.</b> This
    /// slice builds the value object + the translation from the live space-hazard query (<see cref="SpaceHazardTools"/>).
    /// A later slice stores it on <c>FleetCombatStateDB</c> and reads the accuracy/closing/DoT coefficients into the
    /// shared kernel + the space resolver (the design's Part 4 steps 2-3). Kept a plain value struct (no
    /// <c>[JsonProperty]</c>, no <c>Clone</c>) because it is DERIVED per battle from the system + position, not saved.
    /// </summary>
    public struct CombatConditions
    {
        /// <summary>True when the point is inside at least one hazard/environment; false = clean space (identity).</summary>
        public bool InHazard;

        /// <summary>True when sensors are fully blinded here (a solar flare) — you fight what closes to point blank.</summary>
        public bool Blind;

        /// <summary>× on how far you can DETECT / engage (a jam cuts it; 1.0 = full reach, 0 = blind).</summary>
        public double Detection;

        /// <summary>× on how well fire LANDS — the "visibility/accuracy" coefficient the shared kernel's HitFraction
        /// will read (you hit worse what you can barely see; 1.0 = unaffected, 0 = blind).</summary>
        public double Accuracy;

        /// <summary>× on CLOSING speed — a dense medium (gas, debris) drags thrust, so fleets close slower (1.0 = free).</summary>
        public double Closing;

        /// <summary>× on FIREPOWER dealt (a hot corona chokes beams). Identity (1.0) until the authored-environment
        /// reads land (design Part 4 step 3+); the raw hazard query doesn't provide a firepower cut today.</summary>
        public double Firepower;

        /// <summary>× on SHIELD regen (an ion storm won't let shields recharge). Identity (1.0) until the
        /// authored-environment reads land; the raw hazard query doesn't provide a regen cut today.</summary>
        public double ShieldRegen;

        /// <summary>Additive EVASION/cover the terrain grants (debris, a planet's limb). 0 = none. Identity until the
        /// authored-environment/body reads land.</summary>
        public double Cover;

        /// <summary>Ambient DAMAGE the surroundings deal per second (heat, radiation, corrosion), armour-resisted the
        /// same as a weapon hit. 0 = none.</summary>
        public double AmbientDoT_Jps;

        /// <summary>The featureless-void baseline: every multiplier at 1.0, no cover, no ambient damage. What a fight
        /// in clean deep space reads — so an environment-blind resolver is byte-identical to reading this.</summary>
        public static CombatConditions Clean => new CombatConditions
        {
            InHazard = false,
            Blind = false,
            Detection = 1.0,
            Accuracy = 1.0,
            Closing = 1.0,
            Firepower = 1.0,
            ShieldRegen = 1.0,
            Cover = 0.0,
            AmbientDoT_Jps = 0.0,
        };

        /// <summary>
        /// Translate the live space-hazard query (<see cref="HazardModifiers"/>) into combat conditions. This is the
        /// pure, deterministic mapping — no entity, no clock — so it is unit-testable on its own. Only the effects the
        /// hazard system actually provides today (🟢 LIVE in the design) are mapped: a sensor jam cuts BOTH detection
        /// and accuracy (you can't hit what you can't see), movement drag cuts closing, and the summed hazard damage
        /// becomes the ambient DoT. Firepower/ShieldRegen/Cover stay at identity here (a later authored-environment
        /// read fills them). Not-in-any-hazard → <see cref="Clean"/>.
        /// </summary>
        public static CombatConditions FromHazard(HazardModifiers mods)
        {
            if (!mods.InAnyHazard)
                return Clean;

            // A blind flare already reports SensorRangeMultiplier 0; guard anyway so "blind" always reads 0 detection.
            double sensors = mods.BlindsSensors ? 0.0 : mods.SensorRangeMultiplier;

            return new CombatConditions
            {
                InHazard = true,
                Blind = mods.BlindsSensors,
                Detection = sensors,
                Accuracy = sensors,
                Closing = mods.MoveSpeedMultiplier,
                Firepower = 1.0,
                ShieldRegen = 1.0,
                Cover = 0.0,
                AmbientDoT_Jps = mods.DamagePerSecond,
            };
        }

        /// <summary>
        /// Read the combat conditions at a point in a star system — the whole "wire" the design's Part 2 names as the
        /// gap: ask the live hazard system what overlaps this point, then translate. A null system (or a point in no
        /// hazard) reads <see cref="Clean"/>. RAW (pre-ship-resistance): a specific ship's
        /// <see cref="HazardResistanceAtb"/> shrinks these cuts at the point of use, exactly as the sensor/warp code
        /// already applies <see cref="SpaceHazardTools.ApplyResistance"/>.
        /// </summary>
        public static CombatConditions ReadAt(StarSystem system, Vector3 position)
            => FromHazard(SpaceHazardTools.CombinedAt(system, position));
    }
}
