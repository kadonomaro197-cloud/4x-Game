using System;
using Newtonsoft.Json;
using Pulsar4X.DataStructures;
using Pulsar4X.Engine;
using Pulsar4X.Storage;

namespace Pulsar4X.Technology
{
    public class TeamObject : ICargoable
    {
        public int ID { get; private set; } = Game.GetEntityID();
        public TeamTypes TeamType { get; protected set; }
        public string LeaderName;
        public int Age;

        [JsonProperty]
        private int _teamSize;
        [JsonProperty]
        private object? _teamTask;

        /// <summary>
        /// Determines how many Labs this team can manage
        /// </summary>
        public int TeamSize
        {
            get { return _teamSize; }
            internal set { _teamSize = value; }
        }

        /// <summary>
        /// not sure if this should be a blob, entity or guid. and maybe a queue as well.
        /// </summary>
        /// TODO: Communications Review
        /// Detemine team orders system
        [PublicAPI]
        public object? TeamTask
        {
            get { return _teamTask; }
            internal set { _teamTask = value; }
        }

        public TeamObject() { }

        public TeamObject(int teamSize = 0, object? initialTask = null)
        {
            TeamSize = teamSize;
            TeamTask = initialTask;
        }

        public TeamObject(TeamObject teamsdb)
        {
            TeamSize = teamsdb.TeamSize;
            TeamTask = teamsdb.TeamTask;
        }

        public  object Clone()
        {
            return new TeamObject(this);
        }

        public string UniqueID { get; } = Guid.NewGuid().ToString();
        public string Name
        {
            get { return LeaderName; }
        }
        public string CargoTypeID { get; set; } = PassengerPacking.PassengerStorage;
        public long MassPerUnit
        {
            get { return Convert.ToInt64(_teamSize * (long)PassengerPacking.MassPerPerson_kg); }
        }

        /// <summary>
        /// Room this team needs, which depends on HOW you are carrying them — a berth in a passenger cabin, or a
        /// cryogenic pod (a fifth of the room, and they arrive asleep). See <see cref="PassengerPacking"/>.
        ///
        /// <para>⚠ This used to read <c>0.065 × teamSize</c> — <b>65 litres a head, the volume of a human body</b>,
        /// which would berth seven thousand people in a 500 m³ cabin. It had never been wrong in play because it had
        /// never been READ: no component in the game provided <c>passenger-storage</c>, so
        /// <c>CargoMath.GetFreeVolume</c> returned a silent 0 for every team and no team could be loaded onto
        /// anything. Fixing it is byte-identical for exactly that reason.</para>
        /// </summary>
        public double VolumePerUnit
        {
            get { return PassengerPacking.VolumePerPerson(CargoTypeID) * _teamSize; }
        }

        public double Density
        {
            get { return _teamSize * 985.0; } //avg density of a human.
        }
    }
}