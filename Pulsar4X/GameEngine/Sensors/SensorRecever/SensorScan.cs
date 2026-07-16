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

        /// <summary>Floor for the scan-reschedule interval. A sensor whose <c>ScanTime</c> is &lt;= 0 (a data/clamp slip,
        /// or a fractional value truncated to 0 by the int cast) would otherwise reschedule at the SAME game-instant —
        /// turning the instance queue into a busy-loop that spins on that one entity forever, freezing game-time while
        /// <see cref="ScanCount"/> explodes (a SIM-STALL "SensorScan STILL CLIMBING" freeze). Mirrors the guard the
        /// install-kick already applies (<c>SensorReceiverAtb.OnComponentInstallation</c>).</summary>
        internal const int DefaultScanSeconds = 3600;
        private static bool _zeroScanTimeWarned;

        /// <summary>Diagnostic scan ATTRIBUTION — OFF by default → zero overhead, byte-identical. When a test sets
        /// <see cref="AttributeScans"/> true, every <see cref="ProcessEntity"/> invocation bumps the per-entity count in
        /// <see cref="ScansByEntity"/>, so a scan STORM can be pinned on the offending entity/design instead of only the
        /// global <see cref="ScanCount"/>. Cleared by the test before it measures.</summary>
        public static bool AttributeScans = false;
        public static readonly System.Collections.Concurrent.ConcurrentDictionary<int, long> ScansByEntity
            = new System.Collections.Concurrent.ConcurrentDictionary<int, long>();

        internal override void ProcessEntity(Entity entity, DateTime atDateTime)
        {
            System.Threading.Interlocked.Increment(ref ScanCount);
            if (AttributeScans) ScansByEntity.AddOrUpdate(entity.Id, 1, (_, n) => n + 1);   // diagnostic: which entity storms
            if(entity.Manager == null) throw new NullReferenceException("entity.Manager cannot be null");

            EntityManager manager = entity.Manager;
            // Defensive (twin of EntityManager.GetSensorContacts): Game.Factions is a plain Dictionary whose indexer
            // throws on a missing key. This scan runs on the parallel sim thread (an unobserved throw silently FREEZES
            // the clock — landmine L4), thousands of times a session. A scanning entity whose owning faction isn't
            // registered (orphaned, or a neutral -99 that isn't in the faction table) can't populate a faction contact
            // store — skip it rather than wedge the whole sim.
            if (!entity.Manager.Game.Factions.TryGetValue(entity.FactionOwnerID, out var faction))
                return;

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

                                // Event Logger (2026-07-16): a newly-detected ENEMY foreign SHIP raises an
                                // "Enemy Fleet detected at [nearest body]" event. The player's FactionEventLog halts
                                // on it → pause + reset to 1-hour steps. No-op for own/neutral/non-hostile targets.
                                SensorEvents.OnNewShipContact(faction, detectableEntity, manager, atDateTime);
                            }

                        }
                        else if (sensorMgr.SensorContactExists(detectableEntity.Id) && sensorAbl.CurrentContacts.ContainsKey(detectableEntity.Id))
                        {
                            sensorAbl.CurrentContacts.Remove(detectableEntity.Id);
                            sensorAbl.OldContacts[detectableEntity.Id] = detectionValues;
                            sensorMgr.RemoveContact(detectableEntity.Id);
                        }
                    }

                    // Reschedule the next scan, FLOORING the interval so a non-positive ScanTime can never reschedule at
                    // the same game-instant and busy-loop the instance queue (the SensorScan freeze) — the same guard the
                    // install-kick applies. A one-time warning names the offending design so it can be fixed at the data.
                    int scanSecs = sensorAtb.ScanTime > 0 ? sensorAtb.ScanTime : DefaultScanSeconds;
                    if (sensorAtb.ScanTime <= 0 && !_zeroScanTimeWarned)
                    {
                        _zeroScanTimeWarned = true;
                        Console.WriteLine($"[SensorScan] WARNING: a sensor on entity {entity.Id} (faction {entity.FactionOwnerID}) "
                            + $"has ScanTime={sensorAtb.ScanTime} <= 0 — floored to {DefaultScanSeconds}s to avoid a same-instant reschedule busy-loop. Fix the sensor design's Scan Time.");
                    }
                    manager.ManagerSubpulses.AddEntityInterupt(atDateTime + TimeSpan.FromSeconds(scanSecs), this.TypeName, entity);
                }
            }
        }
    }
}
