using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar4X.Datablobs;
using Pulsar4X.Interfaces;
using Pulsar4X.Extensions;
using Pulsar4X.DataStructures;
using Pulsar4X.Factions;
using Pulsar4X.Engine;
using Pulsar4X.Storage;

namespace Pulsar4X.Industry
{
    public static class IndustryTools
    {
        /// <summary>
        /// TIER 2.6 workforce→production STAFFING throttle (developer ruling 2026-08-10,
        /// docs/assembler/ENGINE-WIRING-BACKLOG-2026-08-06.md). Default OFF → the engine test suite is byte-identical;
        /// <c>NewGameMenu</c> flips it ON for a menu game (the same default-off/menu-on pattern as
        /// <c>PopulationProcessor.EnableEmploymentMorale</c>). When on, a colony whose available workforce cannot cover
        /// its facilities' total job demand builds proportionally slower — population PACES production instead of only
        /// gating it.
        /// </summary>
        public static bool EnableWorkforceStaffing = false;

        /// <summary>
        /// The workforce-staffing multiplier on a host's production rate: <c>min(1, availableWorkforce ÷ Σ facility
        /// CrewReq)</c> — a second factor on the rate, exactly parallel to infrastructure efficiency. Returns 1.0 (no
        /// throttle) when the flag is OFF, when the host has no manpower pool (a station — inert, exactly as the crew gate
        /// is), or when it has no job demand. Shares ONE producer with the employment→morale term
        /// (<see cref="Pulsar4X.Extensions.ComponentInstancesDBExtensions.GetTotalJobs"/>) — the same jobs total feeds both
        /// consumers, per the backlog's "one producer, two consumers." Public so the gauge can measure it without driving
        /// a full production run.
        /// </summary>
        public static double StaffingEfficiency(Entity industryEntity)
        {
            if (!EnableWorkforceStaffing || industryEntity == null) return 1.0;
            long available = Pulsar4X.Colonies.ManpowerTools.AvailableWorkforce(industryEntity);
            if (available < 0) return 1.0;   // -1 sentinel = no manpower pool (a station) → unenforced, full rate
            if (!industryEntity.TryGetDataBlob<ComponentInstancesDB>(out var instances)) return 1.0;
            long jobsDemand = instances.GetTotalJobs();
            if (jobsDemand <= 0) return 1.0; // nothing to staff → no throttle
            double eff = available / (double)jobsDemand;
            if (eff < 0.0) eff = 0.0;
            if (eff > 1.0) eff = 1.0;
            return eff;
        }

        public static void AddJob(Entity industryEntity, string plineID, IndustryJob job)
        {
            var industryDB = industryEntity.GetDataBlob<IndustryAbilityDB>();
            AddJob(industryDB, plineID, job);
        }

        public static void AddJob(IndustryAbilityDB industryDB, string plineID, IndustryJob job)
        {
            lock(industryDB.ProductionLines[plineID])
            {
                var pline = industryDB.ProductionLines[plineID];
                pline.Jobs.Add(job);
            }
        }

        public static void ChangeJobPriority(Entity industryEntity, string prodLine, string jobID, int delta)
        {
            var industryDB = industryEntity.GetDataBlob<IndustryAbilityDB>();
            var jobList = industryDB.ProductionLines[prodLine].Jobs;
            //first check that the job does still exsist in the list.
            var job = jobList.Find((obj) => obj.JobID == jobID);
            if (job != null)
            {
                var currentIndex = jobList.IndexOf(job);
                var newIndex = currentIndex + delta;
                if (newIndex <= 0)
                {
                    jobList.RemoveAt(currentIndex);
                    jobList.Insert(0, job);
                }
                else if (newIndex >= jobList.Count - 1)
                {
                    jobList.RemoveAt(currentIndex);
                    jobList.Add(job);
                }
                else
                {
                    jobList.RemoveAt(currentIndex);
                    jobList.Insert(newIndex, job);
                }
            }
        }

        public static void EditExsistingJob(Entity industryEntity, string prodLine, string jobID, bool RepeatJob = false, ushort NumberOrderd = 1, bool autoInstall = false)
        {
            var industryDB = industryEntity.GetDataBlob<IndustryAbilityDB>();
            var jobList = industryDB.ProductionLines[prodLine].Jobs;
            //first check that the job does still exsist in the list.
            var job = jobList.Find((obj) => obj.JobID == jobID);
            if (job != null)
            {
                job.Auto = RepeatJob;
                job.NumberOrdered = NumberOrderd;
                /*if (job is ConstructJob)
                {
                    var cj = (ConstructJob)job;
                    cj.InstallOn = industryEntity;
                }*/

            }
        }

        public static void CancelExsistingJob(Entity industryEntity, string prodLine, string jobID)
        {
            var industryDB = industryEntity.GetDataBlob<IndustryAbilityDB>();
            var jobList = industryDB.ProductionLines[prodLine].Jobs;
            //first check that the job does still exsist in the list.
            var job = jobList.Find((obj) => obj.JobID == jobID);
            if (job != null)
            {
                jobList.Remove(job);
            }
        }

        internal static void ConstructStuff(Entity industryEntity)
        {
            if(!industryEntity.TryGetDataBlob<CargoStorageDB>(out var stockpile))
            {
                throw new Exception("Tried to ConstructStuff on an entity with no CargoStorageDB");
            }

            if(!industryEntity.Manager.Game.Factions.ContainsKey(industryEntity.FactionOwnerID))
            {
                throw new Exception("Unable to find the faction entity");
            }
            var faction = industryEntity.Manager.Game.Factions[industryEntity.FactionOwnerID];

            if(!faction.TryGetDataBlob<FactionInfoDB>(out var factionInfo))
            {
                throw new Exception("Unable to find FactionInfoDB");
            }

            if(!industryEntity.TryGetDataBlob<IndustryAbilityDB>(out var industryDB))
            {
                throw new Exception("Unable to find IndustryAbilityDB");
            }

            // Infrastructure is the limiting factor on a colony's output: when the colony's
            // buildings exceed its infrastructure capacity, every production rate is scaled down.
            double infraEfficiency = InfrastructureProcessor.GetEfficiency(industryEntity);

            // TIER 2.6 (flag-gated; 1.0 when off → byte-identical): the workforce-staffing throttle — a colony that can't
            // man its facilities builds proportionally slower. A SECOND factor on the rate, exactly parallel to infra.
            double staffingEfficiency = StaffingEfficiency(industryEntity);

            foreach (var (prodLineID, prodLine) in industryDB.ProductionLines.ToArray())
            {
                var industryPointsRemaining = new Dictionary<string, int>();
                foreach (var rate in prodLine.IndustryTypeRates)
                    industryPointsRemaining[rate.Key] = (int)(rate.Value * infraEfficiency * staffingEfficiency);

                foreach(var batchJob in prodLine.Jobs.ToArray())
                {
                    // Defensive (L4 / the mining time-stall class): a job can reference a design the CURRENT owner's
                    // store lacks — most notably after a COLONY CAPTURE flips FactionOwnerID while the old owner's
                    // queued jobs remain. A hard index here throws on the parallel sim thread (unobserved -> the clock
                    // freezes). Skip such a job rather than crash. Same for a job whose industry type this line can't
                    // produce (a mismatched AddJob).
                    if (!factionInfo.IndustryDesigns.TryGetValue(batchJob.ItemGuid, out var designInfo))
                    {
                        batchJob.Status = IndustryJobStatus.MissingResources;
                        continue;
                    }
                    if (!industryPointsRemaining.TryGetValue(designInfo.IndustryTypeID, out var industryPointsAvailable))
                        continue;// this line can't produce this job's industry type
                    float industryPointsToUse = industryPointsAvailable;

                    if(batchJob.Status != IndustryJobStatus.Completed)
                    {
                        batchJob.Status = IndustryJobStatus.Queued;
                    }

                    if(industryPointsToUse < 1) continue;

                    // M3-2b crew GATE (docs/society/MORALE-AND-POPULATION-DESIGN.md): you cannot build a ship you can't
                    // crew. Checked BEFORE any resources are consumed below, and only for ship hulls with a real
                    // crew requirement. Inert on a host with no manpower pool (a station) and at the all-Mid
                    // government default the policy is Block; a high-authority regime conscripts instead
                    // (BuildUnderstaffed) via GovernmentDB.CrewPolicy. A blocked job waits — same as waiting on
                    // materials — until crew frees up (a destroyed ship returns its crew to the pool).
                    if (designInfo is Pulsar4X.Ships.ShipDesign shipToCrew && shipToCrew.CrewReq > 0)
                    {
                        // Enhancers ⚙6.2: a caliber ship's crew splits — the veteran-cadre slice (TalentReq) is
                        // gated against the SCARCE talent pool, the rest against bulk workforce. TalentReq is 0 for
                        // every non-caliber ship, so bulk is gated on the full CrewReq and the talent wall passes —
                        // byte-identical to the old single-pool gate for the entire base-mod fleet.
                        var crewDecision = Pulsar4X.Colonies.ManpowerTools.ResolveBuild(industryEntity, shipToCrew.CrewReq - shipToCrew.TalentReq);
                        bool haveTalent = Pulsar4X.Colonies.ManpowerTools.HasTalentToBuild(industryEntity, shipToCrew.TalentReq);
                        if (!crewDecision.CanBuild || !haveTalent)
                        {
                            batchJob.Status = IndustryJobStatus.MissingResources; // short on crew or veteran talent — hold the job
                            continue;
                        }
                    }

                    //total number of resources requred for a single job in this batch
                    var resourceSum = batchJob.ResourcesCosts.Sum(item => item.Value);
                    //how many construction points each resourcepoint is worth.
                    if (resourceSum == 0)
                        throw new Exception("resources can't cost 0");

                    float pointPerResource = (float)designInfo.IndustryPointCosts / (float)resourceSum;
                    float startingPointsLeft = batchJob.ProductionPointsLeft;
                    float startingPointsToUse = industryPointsToUse;

                    while (
                        batchJob.NumberCompleted < batchJob.NumberOrdered &&
                        industryPointsToUse >= 1)
                    {
                        //gather availible resorces for this job.
                        //right now we take all the resources we can, for an individual item in the batch.
                        //even if we're taking more than we can use in this turn, we're using/storing it.
                        IDictionary<string, long> resourceCosts = batchJob.ResourcesRequiredRemaining;

                        var totalResourceReq = resourceCosts.Sum(item => item.Value);

                        //Note: this is editing batchjob.ResourcesRequired variable (as ref resourceCosts).
                        ConsumeResources(stockpile, ref resourceCosts);
                        //we calculate the difference between the design resources and the amount of resources we've squirreled away.

                        // this is the total of the resources that we don't have access to for this item.
                        var totalResourceStillReq = resourceCosts.Sum(item => item.Value);

                        // this is the total resources that can be used on this item.
                        var totalResourcesUsed = totalResourceReq - totalResourceStillReq;
                        // the industry Points equivelent of total used resources.
                        var totalIPEquvelent = totalResourcesUsed * pointPerResource;

                        int pointsToUse = 0;
                        industryPointsToUse = Math.Min(industryPointsRemaining[designInfo.IndustryTypeID], batchJob.ProductionPointsLeft);
                        if (totalResourceStillReq == 0)
                        {
                            pointsToUse = Math.Max((int)industryPointsToUse, 1);
                        }
                        else
                        {
                            industryPointsToUse = Math.Min(industryPointsToUse, totalIPEquvelent);
                            pointsToUse = (int)Math.Floor(industryPointsToUse);
                        }

                        //construct only enough for the amount of resources we have.
                        batchJob.ProductionPointsLeft -= pointsToUse;
                        industryPointsRemaining[designInfo.IndustryTypeID] -= pointsToUse;

                        if(startingPointsLeft == batchJob.ProductionPointsLeft
                            && batchJob.ProductionPointsCost > startingPointsToUse)
                        {
                            // Didn't make any progress mark as missing resources
                            batchJob.Status = IndustryJobStatus.MissingResources;
                        }
                        else if(pointsToUse >= 1 || totalResourcesUsed > 0)
                        {
                            batchJob.Status = IndustryJobStatus.Processing;
                        }

                        if (batchJob.ProductionPointsLeft == 0 && totalResourceStillReq == 0)
                        {
                            batchJob.Status = IndustryJobStatus.Completed;
                            designInfo.OnConstructionComplete(industryEntity, stockpile, prodLineID, batchJob, designInfo);
                        }
                    }
                }
            }
        }

        internal static void ConsumeResources(CargoStorageDB fromCargo, ref IDictionary<string, long> toUse)
        {
            foreach (var kvp in toUse.ToArray())
            {
                ICargoable? cargoItem = fromCargo.OwningEntity.GetFactionOwner.GetDataBlob<FactionInfoDB>().Data.CargoGoods.GetAny(kvp.Key);//fromCargo.OwningEntity.Manager.Game.StaticData.GetICargoable(kvp.Key);
                if (cargoItem is null)
                {
                    if (fromCargo.OwningEntity.GetFactionOwner.GetDataBlob<FactionInfoDB>().InternalComponentDesigns.TryGetValue(kvp.Key, out var design))
                    {
                        if (design != null)
                            cargoItem = (ICargoable)design;
                    }
                    else
                    {
                        throw new Exception("Cant build from non ICargoable Items");
                    }
                }
                string cargoTypeID = cargoItem.CargoTypeID;
                long amountUsedThisTick = 0;
                if (fromCargo.TypeStores.ContainsKey(cargoTypeID))
                {
                    if (fromCargo.TypeStores[cargoTypeID].CurrentStoreInUnits.ContainsKey(cargoItem.ID))
                    {
                        amountUsedThisTick = Math.Min(fromCargo.TypeStores[cargoTypeID].CurrentStoreInUnits[cargoItem.ID], kvp.Value);
                    }
                }

                if (amountUsedThisTick > 0)
                {
                    long used = fromCargo.RemoveCargoByUnit(cargoItem, amountUsedThisTick);
                    toUse[kvp.Key] -= used;
                }
            }
        }

        public static void AutoAddSubJobs(Entity industryEntity, IndustryJob job)
        {
            if(!industryEntity.TryGetDataBlob<CargoStorageDB>(out var stockpile))
            {
                throw new Exception("Tried to ConstructStuff on an entity with no CargoStorageDB");
            }
            if(!industryEntity.TryGetDataBlob<IndustryAbilityDB>(out var industryDB))
            {
                throw new Exception("Unable to find IndustryAbilityDB");
            }

            var resReq = job.ResourcesRequiredRemaining;
            foreach (var kvp in resReq)
            {
                ICargoable? cargoItem = industryEntity.GetFactionOwner.GetDataBlob<FactionInfoDB>().Data.CargoGoods.GetAny(kvp.Key);
                if (cargoItem is null)
                {
                    if (industryEntity.GetFactionOwner.GetDataBlob<FactionInfoDB>().IndustryDesigns.TryGetValue(kvp.Key, out var design)
                        && design != null)
                    {
                        cargoItem = (ICargoable)design;
                    }
                    else
                    {
                        continue;
                    }
                }
                var numStored = stockpile.GetUnitsStored(cargoItem, false);
                var numReq = kvp.Value - numStored;
                if (numReq > 0)
                {
                    if (cargoItem is IConstructableDesign)
                    {
                        IConstructableDesign des = (IConstructableDesign)cargoItem;
                        IndustryJob newjob = new IndustryJob(des);
                        newjob.InitialiseJob((ushort)numReq, false);
                        SetJobToFastest(industryDB, newjob);
                        AutoAddSubJobs(industryEntity, newjob); //recursivly add jobs.
                    }
                }
            }


        }
        internal static void SetJobToFastest(IndustryAbilityDB industrydb, IndustryJob job)
        {
            var typID = job.TypeID;
            (string lineID, int rate) bestLine = (String.Empty, 0);
            var plines = industrydb.ProductionLines;
            foreach (var line in plines)
            {
                if (!line.Value.IndustryTypeRates.TryGetValue(typID, out int rate))
                    rate = -1;
                if (rate > bestLine.rate)
                    bestLine = (line.Key, rate);
            }
            if(bestLine.lineID != String.Empty)
                AddJob(industrydb, bestLine.lineID, job);
        }
    }
}