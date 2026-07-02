using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;
using Pulsar4X.Names;

namespace Pulsar4X.Stations
{
    /// <summary>
    /// Core state for a space station — the deliberate PARALLEL to <see cref="Pulsar4X.Colonies.ColonyInfoDB"/>.
    ///
    /// A station is the CHEAP, FAST, FLEXIBLE, FRAGILE alternative to planetary colonization: it does the
    /// same off-world jobs (mine, refine, research, trade, house people) by carrying the SAME component
    /// equipment a colony does, but it is its own chassis so it can own its own cost curve, durability,
    /// and invasion math (see docs/SPACE-STATIONS-DESIGN.md — "Why PARALLEL, not generalized").
    ///
    /// Why this is its own DataBlob and not just a planet-less ColonyInfoDB: the two trade-offs that make
    /// stations interesting (cheap-while-focused / expensive-as-a-planet-replacement, and a fraction of the
    /// effort to destroy vs. a planet's long ground war) only exist if stations and planets are mechanically
    /// distinct hosts. The economy processors (mining/industry/research) discover their work by the ability
    /// component on the entity, NOT by host type, so a station gets that economy "for free" — only the
    /// host-keyed loops (population) need a station-aware counterpart.
    /// </summary>
    public class StationInfoDB : BaseDataBlob
    {
        /// <summary>
        /// Species Entity ID and amount housed on this station. A manned station has population the same way
        /// a colony does; an unmanned automated platform leaves this empty.
        /// </summary>
        [JsonProperty]
        public Dictionary<int, long> Population { get; internal set; } = new ();

        /// <summary>
        /// Constructed parts stockpile. Construction pulls and pushes from here (mirrors ColonyInfoDB).
        /// </summary>
        [JsonProperty]
        public Dictionary<string, int> ComponentStockpile { get; internal set; } = new ();

        /// <summary>
        /// The body, belt point, or anomaly this station orbits / is parked at — the station's equivalent of a
        /// colony's parent planet (<see cref="Pulsar4X.Colonies.ColonyInfoDB.PlanetEntity"/>). What the station
        /// orbits decides what it can mine and (for a research station) what research flavor it yields.
        /// </summary>
        [JsonProperty]
        public Entity HostingBodyEntity { get; internal set; } = Entity.InvalidEntity;

        public StationInfoDB() { }

        /// <param name="hostingBody">the body / belt / anomaly this station orbits</param>
        public StationInfoDB(Entity hostingBody)
        {
            HostingBodyEntity = hostingBody;
            Population = new Dictionary<int, long>();
            ComponentStockpile = new Dictionary<string, int>();
        }

        public StationInfoDB(Entity species, long populationCount, Entity hostingBody) : this(hostingBody)
        {
            Population = new Dictionary<int, long> { { species.Id, populationCount } };
        }

        public StationInfoDB(StationInfoDB other)
        {
            Population = new Dictionary<int, long>(other.Population);
            ComponentStockpile = new Dictionary<string, int>(other.ComponentStockpile);
            HostingBodyEntity = other.HostingBodyEntity;
        }

        public override object Clone()
        {
            return new StationInfoDB(this);
        }

        public new static List<Type> GetDependencies() => new List<Type>() { typeof(NameDB) };
    }
}
