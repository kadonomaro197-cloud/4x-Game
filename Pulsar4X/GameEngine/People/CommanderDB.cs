using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Pulsar4X.Datablobs;
using Pulsar4X.DataStructures;
using Pulsar4X.Names;

namespace Pulsar4X.People
{
    public class CommanderDB : BaseDataBlob
    {
        public new static List<Type> GetDependencies() => new List<Type>() { typeof(NameDB) };

        [JsonProperty]
        public string Name { get; internal set; }
        [JsonProperty]
        public int Rank { get; internal set; }
        [JsonProperty]
        public CommanderTypes Type { get; internal set; }
        [JsonProperty]
        public int Experience { get; internal set; } = 0;
        [JsonProperty]
        public int ExperienceCap { get; internal set; } = 0;
        [JsonProperty]
        public DateTime CommissionedOn { get; internal set; }
        [JsonProperty]
        public DateTime RankedOn { get; internal set; }
        [JsonProperty]
        public int AssignedTo { get; internal set; } = -1;

        /// <summary>
        /// Phase-2.7-attach: this officer's OWN character — the same 12-trait model a faction carries
        /// (<see cref="Pulsar4X.Factions.PersonalityDB"/>). A GREEN officer defers to the faction's doctrine; only as
        /// they gain tenure (<see cref="Experience"/> toward <see cref="ExperienceCap"/>) does their own leaning start
        /// to override it — see <see cref="Pulsar4X.People.OfficerCharacter.Blend"/>. DEFAULTS TO ALL-NEUTRAL (an empty
        /// trait set → every trait reads <see cref="Pulsar4X.Factions.PersonalityDB.Neutral"/> 0.5), so an officer with
        /// no authored character is indistinguishable from today (byte-identical). Deep-copied on clone so it survives
        /// save/load and moving the commander between managers.
        /// </summary>
        [JsonProperty]
        public Pulsar4X.Factions.PersonalityDB Personality { get; internal set; } = new Pulsar4X.Factions.PersonalityDB();


        public CommanderDB() { }

        public CommanderDB(string name, int rank, CommanderTypes type)
        {
            Name = name;
            Rank = rank;
            Type = type;
        }

        public CommanderDB(CommanderDB commanderDB)
        {
            //Should we create new commander? I think no but we have rank in there and same commander with different ranks is not good.
            Name = commanderDB.Name;
            Rank = commanderDB.Rank;
            Type = commanderDB.Type;
            Experience = commanderDB.Experience;
            ExperienceCap = commanderDB.ExperienceCap;
            CommissionedOn = commanderDB.CommissionedOn;
            RankedOn = commanderDB.RankedOn;
            Personality = commanderDB.Personality == null
                ? new Pulsar4X.Factions.PersonalityDB()
                : (Pulsar4X.Factions.PersonalityDB)commanderDB.Personality.Clone();
        }

        public override object Clone()
        {
            return new CommanderDB(this);
        }

        public override string ToString()
        {
            switch(Type)
            {
                // FIXME: need to get rid of staticreflib references
                // case CommanderTypes.Navy:
                //     return StaticRefLib.StaticData.Themes[StaticRefLib.GameSettings.CurrentTheme].NavyRanksAbbreviations[Rank] + " " + Name;
                default:
                    return Name;
            }
        }
    }
}
