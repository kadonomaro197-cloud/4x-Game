using System;
using Pulsar4X.Engine;
using Pulsar4X.Engine.Orders;
using Pulsar4X.Energy;
using Pulsar4X.Interfaces;

namespace Pulsar4X.Weapons;

public class GenericFiringWeaponsProcessor : IHotloopProcessor
{
    public void Init(Game game)
    {
        //do nothing
    }

    public void ProcessEntity(Entity entity, int deltaSeconds)
    {
        if(entity.TryGetDataBlob<GenericFiringWeaponsDB>(out var db))
            UpdateWeapons(db);
    }

    public int ProcessManager(EntityManager manager, int deltaSeconds)
    {
        var list = manager.GetAllDataBlobsOfType<GenericFiringWeaponsDB>();
        foreach(GenericFiringWeaponsDB db in list)
            UpdateWeapons(db);
        return list.Count;
    }

    private void UpdateWeapons(GenericFiringWeaponsDB db)
    {
        // Resolve the ship's power system once. May be null for entities without power plants.
        EnergyGenAbilityDB energyDB = null;
        if (db.OwningEntity != null)
            db.OwningEntity.TryGetDataBlob<EnergyGenAbilityDB>(out energyDB);

        for (int i = 0; i < db.WpnIDs.Length; i++)
        {
            var ws = db.WeaponStates[i];

            // Beam weapons: passive thermal cooling every tick (half a second of radiator output).
            // Non-beam weapons skip this block entirely.
            GenericBeamWeaponAtb beamAtb = db.FireInstructions[i] as GenericBeamWeaponAtb;
            if (beamAtb != null)
            {
                float cooling = (float)(beamAtb.ThermalOutput_W / 1000.0 * 0.5);
                ws.CurrentHeat_kJ = Math.Max(0f, ws.CurrentHeat_kJ - cooling);
            }

            int shots = (int)(db.InternalMagQty[i] / db.AmountPerShot[i]);
            if (shots >= db.MinShotsPerfire[i] && db.OwningEntity != null)
            {
                var tgt = db.FireControlStates[i].Target;
                if (tgt.IsValid && db.FireInstructions[i].IsInRange(db.OwningEntity, tgt))
                {
                    bool canFire = true;

                    if (beamAtb != null)
                    {
                        // Thermal suppression: blocked if heat at capacity and override not active.
                        if (ws.CurrentHeat_kJ >= ws.HeatCapacity_kJ && !ws.ThermalOverrideActive)
                            canFire = false;

                        // Power grid check: deduct shot cost from ship's stored energy.
                        if (canFire && energyDB != null && energyDB.EnergyType != null)
                        {
                            string etype = energyDB.EnergyType.UniqueID;
                            double costKJ = beamAtb.Energy / 1000.0;
                            if (energyDB.EnergyStored.TryGetValue(etype, out double stored) && stored >= costKJ)
                                energyDB.EnergyStored[etype] = stored - costKJ;
                            else
                                canFire = false;
                        }
                    }

                    if (canFire)
                    {
                        db.ShotsFiredThisTick[i] = shots;
                        db.FireInstructions[i].FireWeapon(db.OwningEntity, tgt, shots);
                        db.InternalMagQty[i] -= shots * db.AmountPerShot[i];
                        db.WeaponStates[i].InternalMagCurAmount = db.InternalMagQty[i];

                        // Beam weapons accumulate waste heat from firing.
                        if (beamAtb != null)
                            ws.CurrentHeat_kJ += (float)(beamAtb.ThermalOutput_W * beamAtb.ChargePeriod / 1000.0);
                    }
                }
                else
                {
                    // If we encounter an invalid target check to see if any valid targets exist
                    ValidateTargetExists(db, db.FireControlStates);
                }
            }
        }

        // Reload all internal magazines (Math.Min caps at magSize — was incorrectly Math.Max).
        for (int i = 0; i < db.WpnIDs.Length; i++)
        {
            var tickReloadAmount = db.ReloadAmountsPerSec[i];
            var magQty = Math.Min(db.InternalMagQty[i] + tickReloadAmount, db.InternalMagSizes[i]);
            db.InternalMagQty[i] = magQty;
            db.WeaponStates[i].InternalMagCurAmount = magQty;
        }
    }

    /// <summary>
    /// Check if each of the FireControlAbilityStates have a valid target,
    /// if not issue the cease firing command for that fire control.
    /// </summary>
    ///<param name="genericFiringWeaponsDB"></param>
    /// <param name="fireControlAbilityStates"></param>
    private void ValidateTargetExists(GenericFiringWeaponsDB genericFiringWeaponsDB, FireControlAbilityState[] fireControlAbilityStates)
    {
        for(int i  = 0; i < fireControlAbilityStates.Length; i++)
        {
            if(!fireControlAbilityStates[i].Target.IsValid)
            {
                SetOpenFireControlOrder.CreateCmd(
                    genericFiringWeaponsDB.OwningEntity.Manager.Game,
                    genericFiringWeaponsDB.OwningEntity.FactionOwnerID,
                    genericFiringWeaponsDB.OwningEntity.Id,
                    fireControlAbilityStates[i].ID,
                    SetOpenFireControlOrder.FireModes.CeaseFire);
                return; // We only need to send the CeaseFire command once
            }
        }
    }

    public TimeSpan RunFrequency { get; } = TimeSpan.FromSeconds(1);
    public TimeSpan FirstRunOffset { get; } = TimeSpan.Zero;
    public Type GetParameterType { get; } = typeof(GenericFiringWeaponsDB);
}