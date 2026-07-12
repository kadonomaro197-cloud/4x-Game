using Newtonsoft.Json;
using System;
using Pulsar4X.Engine;
using Pulsar4X.Components;
using Pulsar4X.Interfaces;
using Pulsar4X.DataStructures;
using System.Diagnostics.CodeAnalysis;
using Pulsar4X.Orbital;
using Pulsar4X.Datablobs;
using Pulsar4X.Movement;

namespace Pulsar4X.Weapons
{
    public class GenericBeamWeaponAtb : IComponentDesignAttribute, IFireWeaponInstr
    {
        [JsonProperty]
        public double MaxRange { get; internal set; }

        [JsonProperty]
        public double WaveLength { get; internal set; } = 700;

        // Pulse energy in joules. DOUBLE, not int: a superlaser-scale design exceeds int.MaxValue (~2.1 GJ) and the
        // whole downstream chain (FireBeamWeapon, ShipCombatValueDB firepower, the power-draw check) is already double —
        // the old int silently overflowed (wrapped negative) at the top of the beam scale.
        [JsonProperty]
        public double Energy { get; internal set; }

        // Distance at which beam is at peak focus and full energy.
        // Beyond this the beam spreads; damage scales inverse-square out to MaxRange.
        // Defaults to half MaxRange if not supplied.
        [JsonProperty]
        public double OptimalRange_m { get; internal set; }

        // Seconds between shots (charge period drives magazine fill rate via JSON formula).
        [JsonProperty]
        public double ChargePeriod { get; internal set; } = 1.0;

        // Waste heat dumped per second of operation (Watts). Used for thermal suppression.
        [JsonProperty]
        public double ThermalOutput_W { get; internal set; }

        // When true the weapon design can be pushed past thermal limit at the player's risk.
        [JsonProperty]
        public bool AllowThermalOverride { get; internal set; }

        // COMBAT waste heat (kJ/s) fed to the AUTO-RESOLVE heat model (Weapons pilot W5) — distinct from ThermalOutput_W
        // (which drives the parked per-pixel firing sim). Flows into WeaponProfile.HeatPerSecond, so a HOT beam builds
        // the fleet's heat pool and NEEDS radiators to sustain fire. 0 = a "cool" beam (every base-mod beam today →
        // byte-identical); a pulse/heavy beam (W5c) dials it up.
        [JsonProperty]
        public double CombatHeat_kJps { get; internal set; }

        public double LenPerPulseInSeconds = 1;

        public double BeamSpeed { get; internal set; } = UniversalConstants.Units.SpeedOfLightInMetresPerSecond;
        public float BaseHitChance { get; internal set; } = 0.95f;

        public GenericBeamWeaponAtb() { }

        // The ORIGINAL 7-arg JSON ctor. The component binder (Activator.CreateInstance) matches by EXACT ARITY, so this
        // must stay for the existing beam templates' 7-value AtbConstrArgs to bind (Weapons/CLAUDE.md gotcha #0); it
        // delegates to the 8-arg ctor with CombatHeat 0 → existing beams generate no combat heat (byte-identical).
        public GenericBeamWeaponAtb(double maxRange, double waveLen, double jules,
            double focalLength = 0, double chargePeriod = 1.0, double thermalOutput_W = 0,
            double allowThermalOverride = 0)
            : this(maxRange, waveLen, jules, focalLength, chargePeriod, thermalOutput_W, allowThermalOverride, 0) { }

        // The 8-arg ctor WITH combat heat (Weapons pilot W5). A hot-beam template passes an 8th value.
        public GenericBeamWeaponAtb(double maxRange, double waveLen, double jules,
            double focalLength, double chargePeriod, double thermalOutput_W,
            double allowThermalOverride, double combatHeat_kJps)
        {
            MaxRange = maxRange;
            WaveLength = waveLen;
            Energy = jules;
            OptimalRange_m = focalLength > 0 ? focalLength : maxRange * 0.5;
            // INVARIANT (the established physics): MaxRange is ALWAYS the outer bound. Full damage at/inside
            // OptimalRange; degraded (inverse-square) from OptimalRange out to MaxRange; no fire beyond MaxRange.
            // A design or a stale debug value that sets focal length past MaxRange must NOT be able to invert that
            // (it silently disabled the falloff before) — clamp optimal to MaxRange so the degraded band can never
            // vanish above the range. MaxRange == 0 is the legacy "unlimited" sentinel; leave optimal alone there.
            if (MaxRange > 0 && OptimalRange_m > MaxRange)
                OptimalRange_m = MaxRange;
            ChargePeriod = chargePeriod;
            ThermalOutput_W = thermalOutput_W;
            AllowThermalOverride = allowThermalOverride > 0.5;
            CombatHeat_kJps = combatHeat_kJps < 0 ? 0 : combatHeat_kJps;
        }

        public GenericBeamWeaponAtb(GenericBeamWeaponAtb db)
        {
            MaxRange = db.MaxRange;
            WaveLength = db.WaveLength;
            Energy = db.Energy;
            OptimalRange_m = db.OptimalRange_m;
            ChargePeriod = db.ChargePeriod;
            ThermalOutput_W = db.ThermalOutput_W;
            AllowThermalOverride = db.AllowThermalOverride;
            CombatHeat_kJps = db.CombatHeat_kJps;
        }

        /*
        public override object Clone()
        {
            return new GenericBeamWeaponAtb(this);
        }*/

        public string WeaponType => "Beam";

        public void SetWeaponState(WeaponState state)
        {
            state.WeaponType = WeaponType;
            state.WeaponStats = new (string name, double value, ValueTypeStruct valueType)[3];
            state.WeaponStats[0] = ("Max Range:", MaxRange, new ValueTypeStruct(ValueTypeStruct.ValueTypes.Distance, ValueTypeStruct.ValueSizes.BaseUnit));
            state.WeaponStats[1] = ("Wavelength:", WaveLength, new ValueTypeStruct(ValueTypeStruct.ValueTypes.Distance, ValueTypeStruct.ValueSizes.BaseUnit));
            state.WeaponStats[2] = ("Power:", Energy, new ValueTypeStruct(ValueTypeStruct.ValueTypes.Power, ValueTypeStruct.ValueSizes.BaseUnit));
        }

        public bool CanLoadOrdnance(OrdnanceDesign ordnanceDesign)
        {
            return false;
        }

        public bool AssignOrdnance(OrdnanceDesign ordnanceDesign)
        {
            return false;
        }

        public bool TryGetOrdnance([NotNullWhen(true)] out OrdnanceDesign? ordnanceDesign)
        {
            ordnanceDesign = null;
            return false;
        }

        public bool IsInRange(Entity launchingEntity, Entity tgtEntity)
        {
            // MaxRange == 0 means no range limit configured (legacy designs).
            if (MaxRange <= 0) return true;
            var launchPos = MoveMath.GetAbsolutePosition(launchingEntity);
            var tgtPos = MoveMath.GetAbsolutePosition(tgtEntity);
            return (launchPos - tgtPos).Length() <= MaxRange;
        }

        public void FireWeapon(Entity launchingEntity, Entity tgtEntity, int count)
        {
            var beamLen = Math.Min(1, count * LenPerPulseInSeconds);
            BeamWeaponProcessor.FireBeamWeapon(launchingEntity, tgtEntity, true, Energy, WaveLength, BeamSpeed, beamLen, BaseHitChance, OptimalRange_m);
        }

        public float ToHitChance(Entity launchingEntity, Entity tgtEntity)
        {
            var launchPos = MoveMath.GetAbsolutePosition(launchingEntity);
            var tgtPos = MoveMath.GetAbsolutePosition(tgtEntity);
            double range = Math.Abs((launchPos - tgtPos).Length());
            double ttt = range / BeamSpeed;
            double missChance = ttt * (1 - BaseHitChance);
            return (float)(1 - missChance);
        }

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance)
        {
            var instancesDB = parentEntity.GetDataBlob<ComponentInstancesDB>();
            if (!parentEntity.HasDataBlob<FireControlAbilityDB>())
            {
                var fcdb = new FireControlAbilityDB();
                parentEntity.SetDataBlob(fcdb);
            }

            if (!componentInstance.HasAblity<WeaponState>())
            {
                var wpnState = new WeaponState(componentInstance, this);
                SetWeaponState(wpnState);
                // Heat capacity: headroom for 2 full charge cycles before thermal suppression.
                wpnState.HeatCapacity_kJ = (float)(ThermalOutput_W * ChargePeriod * 2.0 / 1000.0);
                wpnState.AllowThermalOverride = AllowThermalOverride;
                componentInstance.SetAbilityState<WeaponState>(wpnState);
            }
        }

        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance)
        {

        }

        public string AtbName()
        {
            return "Generic Beam Weapon";
        }

        public string AtbDescription()
        {
            return "";
        }

    }
}
