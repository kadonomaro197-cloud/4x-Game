using System.Collections.Generic;
using Newtonsoft.Json;
using Pulsar4X.DataStructures;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Interfaces;

namespace Pulsar4X.Industry
{
    public class IndustryJob : JobBase
    {
        internal string TypeID;
        public IndustryJobStatus Status { get; internal set; } = IndustryJobStatus.Queued;

        /// <summary>
        /// Deserialization constructor. WITHOUT this, Newtonsoft used the <c>(FactionInfoDB, string)</c> ctor below
        /// on load — but it can't supply a FactionInfoDB, so it passed null and <c>factionInfo.IndustryDesigns[...]</c>
        /// threw NullReferenceException, breaking LOAD of ANY save that has a queued production job (the "save didn't
        /// work" bug, 2026-07-03). This parameterless ctor is what Json.NET now uses; it then populates the serialized
        /// fields (ItemGuid/TypeID/Name/costs/points/Status/…) directly. Gauge: SaveLoadWithJobTests.
        /// </summary>
        [JsonConstructor]
        private IndustryJob() { }

        public IndustryJob(FactionInfoDB factionInfo, string itemID)
        {
            ItemGuid = itemID;
            var design = factionInfo.IndustryDesigns[itemID];
            TypeID = design.IndustryTypeID;
            Name = design.Name;
            if(design.ResourceCosts != null)
            {
                ResourcesRequiredRemaining = new Dictionary<string, long>(design.ResourceCosts);
            }
            else
            {
                ResourcesRequiredRemaining = new ();
            }
            ResourcesCosts = design.ResourceCosts;
            ProductionPointsLeft = design.IndustryPointCosts;
            ProductionPointsCost = design.IndustryPointCosts;
            NumberOrdered = 1;
        }

        internal IndustryJob(IConstructableDesign design)
        {
            ItemGuid = design.UniqueID;
            TypeID = design.IndustryTypeID;
            Name = design.Name;
            ResourcesRequiredRemaining = new Dictionary<string, long>(design.ResourceCosts);
            ResourcesCosts = design.ResourceCosts;
            ProductionPointsLeft = design.IndustryPointCosts;
            ProductionPointsCost = design.IndustryPointCosts;
            NumberOrdered = 1;
        }

        public Entity? InstallOn { get; set; } = null;

        public override void InitialiseJob(ushort numberOrderd, bool auto)
        {
            NumberOrdered = numberOrderd;
            NumberCompleted = 0;
            Auto = auto;
        }
    }
}