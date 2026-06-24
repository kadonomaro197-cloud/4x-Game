using Newtonsoft.Json;
using System;
using System.Runtime.Serialization;
using Pulsar4X.Components;
using Pulsar4X.Interfaces;
using Pulsar4X.DataStructures;

namespace Pulsar4X.Weapons
{
    public class WeaponState : ComponentTreeHeirarchyAbilityState
    {
        
        [JsonProperty]
        public ComponentInstance WeaponComponentInstance { get; set; }
        [JsonProperty]
        public IFireWeaponInstr FireWeaponInstructions;
        
        [JsonProperty]
        public DateTime CoolDown { get; internal set; }
        [JsonProperty]
        public bool ReadyToFire { get; internal set; }
        
        
        [JsonProperty]
        public string WeaponType = "";
        [JsonIgnore]
        public (string name, double value, ValueTypeStruct valueType)[] WeaponStats;
        [JsonProperty]
        public int InternalMagCurAmount = 0;

        // Thermal management for beam weapons.
        // CurrentHeat_kJ rises each time the weapon fires; falls by passive radiation each tick.
        // When CurrentHeat_kJ >= HeatCapacity_kJ the weapon is suppressed (cannot fire).
        [JsonProperty]
        public float CurrentHeat_kJ = 0f;
        [JsonProperty]
        public float HeatCapacity_kJ = 1f;
        // AllowThermalOverride: weapon design permits firing past thermal limit (risks weapon damage).
        // ThermalOverrideActive: player has turned override on for this instance.
        [JsonProperty]
        public bool AllowThermalOverride = false;
        [JsonProperty]
        public bool ThermalOverrideActive = false;

        [JsonConstructor]
        private WeaponState(){}
        
        public WeaponState(ComponentInstance componentInstance, IFireWeaponInstr weaponInstr) : base(componentInstance)
        {
            FireWeaponInstructions = weaponInstr;
            //weapon starts loaded, max value from component design.
            InternalMagCurAmount = componentInstance.Design.GetAttribute<GenericWeaponAtb>().InternalMagSize;
        }
        

        public WeaponState(WeaponState db): base(db.ComponentInstance)
        {
            CoolDown = db.CoolDown;
            ReadyToFire = db.ReadyToFire;
            WeaponComponentInstance = db.WeaponComponentInstance;
            WeaponStats = db.WeaponStats;
            InternalMagCurAmount = db.InternalMagCurAmount;
            CurrentHeat_kJ = db.CurrentHeat_kJ;
            HeatCapacity_kJ = db.HeatCapacity_kJ;
            AllowThermalOverride = db.AllowThermalOverride;
            ThermalOverrideActive = db.ThermalOverrideActive;
        }
        
        // JSON deserialization callback.
        [OnDeserialized]
        private void Deserialized(StreamingContext context)
        {
            FireWeaponInstructions.SetWeaponState(this);
        }

    }
}
