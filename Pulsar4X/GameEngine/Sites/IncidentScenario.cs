using System.Linq;
using Pulsar4X.Colonies;
using Pulsar4X.Engine;
using Pulsar4X.Factions;

namespace Pulsar4X.Sites
{
    /// <summary>
    /// Site Engine SE-4e — the DISCOVERY / authoring wire: the first slice that can actually put a live incident into a
    /// game. It composes the SE-4 parts into one call — author a Shape.Incident surface <see cref="FieldSiteDB"/>
    /// (<see cref="FieldSiteFactory.CreateSurfaceSite"/>), stand up its menace (<see cref="MenaceFactory.RaiseMenaceAt"/>),
    /// set the incident dials, and arm the spread (<see cref="SiteIncidentProcessor.Schedule"/>). From then on it bleeds
    /// (SE-4c) and grows (SE-4d) until you clear the menace and contain it.
    ///
    /// The New-Game auto-spawn is behind the default-OFF <see cref="AutoSpawnIncident"/> flag (the
    /// <c>AutoRaiseHomeGarrison</c>/<c>SpawnMarsBeachhead</c> pattern), so a normal New Game is byte-identical until it
    /// is flipped on — the switch that makes this whole SE-4 build visible/playable.
    /// </summary>
    public static class IncidentScenario
    {
        /// <summary>Default OFF → New Game spawns no incident (byte-identical). Flip on to seed a home-world outbreak.</summary>
        public static bool AutoSpawnIncident = false;

        /// <summary>
        /// Author a LIVE incident at <paramref name="body"/>'s region <paramref name="regionIndex"/>: a Shape.Incident
        /// surface site + a menace force holding the region + the pressure/spread dials + the armed spread timer.
        /// Returns the site entity. Defensive on null args.
        /// </summary>
        public static Entity SpawnIncidentAt(Game game, StarSystem system, Entity body, int regionIndex,
            double pressurePerDay = 20.0, double spawnIntervalDays = 60.0, int menaceUnits = 3, string name = "Outbreak")
        {
            if (game == null || system == null || body == null || !body.IsValid) return Entity.InvalidEntity;

            var site = FieldSiteFactory.CreateSurfaceSite(system, body, regionIndex, 0, 0, name,
                role: SiteRole.Tactical, shape: SiteShape.Incident, hook: SiteHook.Contested, yield: SiteYield.Nothing);
            var db = site.GetDataBlob<FieldSiteDB>();

            var menace = MenaceFactory.RaiseMenaceAt(game, body, regionIndex, name + " Menace", menaceUnits);
            db.MenaceFactionId = menace.Id;
            db.PressurePerDay = pressurePerDay;
            db.SpawnIntervalDays = spawnIntervalDays;

            SiteIncidentProcessor.Schedule(site);
            return site;
        }

        /// <summary>
        /// New-Game hook (flag-gated): when <see cref="AutoSpawnIncident"/> is on, seed one incident on each player
        /// faction's home world (its first colony's planet, capital region 0). Returns how many were spawned. A no-op
        /// (returns 0) when the flag is off — so New Game stays byte-identical.
        /// </summary>
        public static int MaybeSpawnForNewGame(Game game)
        {
            if (!AutoSpawnIncident || game == null) return 0;

            int spawned = 0;
            // Snapshot: SpawnIncidentAt adds the menace faction to game.Factions, so don't iterate it live.
            foreach (var faction in game.Factions.Values.ToList())
            {
                if (!faction.TryGetDataBlob<FactionInfoDB>(out var fi) || fi.IsNPC) continue;

                foreach (var colony in fi.Colonies)
                {
                    if (colony == null || !colony.TryGetDataBlob<ColonyInfoDB>(out var ci)) continue;
                    var body = ci.PlanetEntity;
                    if (body == null || !body.IsValid || body.Manager is not StarSystem sys) continue;

                    SpawnIncidentAt(game, sys, body, 0);
                    spawned++;
                    break; // one home-world incident per faction
                }
            }
            return spawned;
        }
    }
}
