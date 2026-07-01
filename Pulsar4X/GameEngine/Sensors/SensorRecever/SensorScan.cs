using System;
using System.Collections.Generic;
using Pulsar4X.Datablobs;
using Pulsar4X.Interfaces;
using Pulsar4X.Colonies;
using Pulsar4X.Energy;
using Pulsar4X.Engine;
using Pulsar4X.Movement;

namespace Pulsar4X.Sensors
{
    public class SensorScan : IInstanceProcessor
    {

        public void TriggerProcess(Entity entity, DateTime atDateTime)
        {
            ProcessEntity(entity, atDateTime);
        }
        //TODO: ReWrite this, instead of each component trying to do a scan,
        //multiple components should mix together to form a single suite and the ship itself should scan.
        //maybe the scan freqency /attribute.scanTime should just effect the chance of a detection.
        /// <summary>Liveness counter (diagnostic only): how many times the scan processor has been invoked across
        /// the whole game. The client logs this each heartbeat so a remote review can tell "detecting nothing
        /// because there's nothing in range" apart from "the scan never fires" — the latter is a real risk, since
        /// the scan is only auto-scheduled by <c>Game.PostNewGameInitialization</c> (the test harness skips it).
        /// Interlocked because systems process in parallel; exactness doesn't matter, only that it climbs.</summary>
        public static long ScanCount;

        internal override void ProcessEntity(Entity entity, DateTime atDateTime)
        {
            System.Threading.Interlocked.Increment(ref ScanCount);
            if(entity.Manager == null) throw new NullReferenceException("entity.Manager cannot be null");

            EntityManager manager = entity.Manager;
            Entity faction = entity.Manager.Game.Factions[entity.FactionOwnerID];

            var position = entity.GetDataBlob<PositionDB>();//recever is a componentDB. not a shipDB
            if (position == null) //then it's probilby a colony
                position = entity.GetDataBlob<ColonyInfoDB>().PlanetEntity.GetDataBlob<PositionDB>();

            // Hazards at the OBSERVER's position degrade what it can see: a gas cloud cuts the effective sensor
            // range, a solar flare blinds entirely. Computed once per scan; applied per contact below. With no
            // hazard present this is the identity (InAnyHazard == false), so normal detection is unchanged.
            var hazardMods = Pulsar4X.Hazards.SpaceHazardTools.CombinedAt(manager as StarSystem, position.AbsolutePosition);

            if( entity.TryGetDataBlob<SensorAbilityDB>(out var sensorAbility))
            {
                var detectableEntitys = manager.GetAllEntitiesWithDataBlob<SensorProfileDB>();
                sensorAbility.CurrentContacts = new List<(Entity, SensorReturnValues)>();
                for(int i = 0; i < sensorAbility.InstanceStates.Count; i++)
                {
                    var sensorAbl = sensorAbility.InstanceStates[i];
                    var sensorAtb = sensorAbility.InstanceAtributes[i];
                    var sensorMgr = manager.GetSensorContacts(entity.FactionOwnerID);
                    var detections = SensorTools.GetDetectedEntites(sensorAtb, position.AbsolutePosition, detectableEntitys, atDateTime, faction.Id, true);
                    
                    SensorInfoDB sensorInfo;
                    for (int j = 0; j < detections.Length; j++)
                    {
                        var detectionValues = detections[j];
                        var detectableEntity = detectableEntitys[j];
                        sensorAbility.CurrentContacts.Add((detectableEntity, detectionValues));

                        // A hazard the observer sits in can hide this contact: blinded (flare) drops everything;
                        // a sensor-cut (gas cloud) drops contacts beyond the observer's reduced reach to the target.
                        bool hazardHides = false;
                        if (hazardMods.InAnyHazard && detectionValues.SignalStrength_kW > 0.0)
                        {
                            // The ship's SensorJam resistance (a hardening component) shrinks the jam: a fully
                            // hardened ship can see through a cloud / flare that blinds an unhardened one.
                            double sensorResist = Pulsar4X.Hazards.SpaceHazardTools.ResistanceFraction(entity, Pulsar4X.Hazards.HazardEffectType.SensorJam);
                            double effSensorMult = Pulsar4X.Hazards.SpaceHazardTools.ApplyResistance(hazardMods.SensorRangeMultiplier, sensorResist);

                            if (effSensorMult <= 0.0)
                            {
                                hazardHides = true;
                            }
                            else if (effSensorMult < 1.0
                                     && detectableEntity.TryGetDataBlob<SensorProfileDB>(out var hzProfile)
                                     && detectableEntity.TryGetDataBlob<PositionDB>(out var hzTgtPos))
                            {
                                double reach = SensorTools.DetectionRange_m(sensorAtb, hzProfile);
                                double dist = (hzTgtPos.AbsolutePosition - position.AbsolutePosition).Length();
                                if (dist > reach * effSensorMult)
                                    hazardHides = true;
                            }
                        }

                        if (detectionValues.SignalStrength_kW > 0.0 && !hazardHides)
                        {
                            
                            if (sensorAtb.IsEnergyGen)//if solar array not sensor
                            {
                                var genAbil = entity.GetDataBlob<EnergyGenAbilityDB>();
                                genAbil.LocalFuel = genAbil.TotalFuelUseAtMax.maxUse * sensorAtb.ScanTime;
                            }
                            else if (sensorMgr.SensorContactExists(detectableEntity.Id))
                            {
                                //sensorInfo = knownContacts[detectableEntity.ID].GetDataBlob<SensorInfoDB>();
                                sensorInfo = sensorMgr.GetSensorContact(detectableEntity.Id).SensorInfo;
                                sensorInfo.LatestDetectionQuality = detectionValues;
                                sensorInfo.LastDetection = atDateTime;
                                if (sensorInfo.HighestDetectionQuality.SignalQuality < detectionValues.SignalQuality)
                                    sensorInfo.HighestDetectionQuality.SignalQuality = detectionValues.SignalQuality;

                                if (sensorInfo.HighestDetectionQuality.SignalStrength_kW < detectionValues.SignalStrength_kW)
                                    sensorInfo.HighestDetectionQuality.SignalStrength_kW = detectionValues.SignalStrength_kW;
                                SensorEntityFactory.UpdateSensorContact(faction, sensorInfo);
                            }
                            else
                            {
                                var contact = new SensorContact(faction, detectableEntity, atDateTime);
                                sensorMgr.AddContact(contact);
                                sensorAbl.CurrentContacts[detectableEntity.Id] = detectionValues;

                                // First contact between factions — the front door to external politics. The first
                                // foreign entity this faction ever detects is a NEW sensor contact, so this branch
                                // is where two factions first "meet". The HasMet guard inside makes it fire once
                                // per pair; a neutral (planet/asteroid) or own-faction target is a no-op.
                                Pulsar4X.Factions.FirstContact.OnDetection(faction, detectableEntity, atDateTime);
                            }

                        }
                        else if (sensorMgr.SensorContactExists(detectableEntity.Id) && sensorAbl.CurrentContacts.ContainsKey(detectableEntity.Id))
                        {
                            sensorAbl.CurrentContacts.Remove(detectableEntity.Id);
                            sensorAbl.OldContacts[detectableEntity.Id] = detectionValues;
                            sensorMgr.RemoveContact(detectableEntity.Id);
                        }
                    }

                    manager.ManagerSubpulses.AddEntityInterupt(atDateTime + TimeSpan.FromSeconds(sensorAtb.ScanTime), this.TypeName, entity);
                }
            }
        }
    }
}
