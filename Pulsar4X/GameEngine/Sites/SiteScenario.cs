using System.Linq;
using Pulsar4X.Colonies;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Galaxy;    // PlanetRegionsDB
using Pulsar4X.Movement;  // PositionDB

namespace Pulsar4X.Sites
{
    /// <summary>
    /// Site Engine SE-6a — the "MAKE IT PLAYABLE" wire. The Site Engine (SE-1..SE-5) is built and CI-green but left
    /// unwired, so a normal game never sees a site. This is the switch that puts a couple of real, workable DEMO sites
    /// into a New Game so the engine can be experienced hands-on — the sibling of <see cref="IncidentScenario"/>.
    ///
    /// Both demo sites are SINGLE-PATH (no branches), so they auto-resolve the moment they're worked — no site UI needed
    /// yet (that's a later slice). You experience them today:
    ///   • the SURFACE RUIN — raise a ground unit in its region (DevTools "Raise Ground Unit") → it works the ruin →
    ///     research lands when it resolves. The readily-workable demo.
    ///   • the SPACE ANOMALY — a neutral marker at the home world; a ship parked ON it works it → research. (Parking a
    ///     ship within the on-site radius is fiddly today; the ruin is the easy one to watch resolve.)
    ///
    /// Gated behind the default-OFF <see cref="AutoSpawnSites"/> flag (the <c>AutoRaiseHomeGarrison</c> pattern), so a
    /// normal New Game is byte-identical until it is flipped on (a DevTools checkbox / the flag).
    /// </summary>
    public static class SiteScenario
    {
        /// <summary>Default OFF → New Game spawns no demo sites (byte-identical). Flip on to seed the playable demo.</summary>
        public static bool AutoSpawnSites = false;

        /// <summary>
        /// New-Game hook (flag-gated): when <see cref="AutoSpawnSites"/> is on, seed the demo sites at each player
        /// faction's home world. Returns how many sites were spawned. A no-op (returns 0) when the flag is off — so New
        /// Game stays byte-identical.
        /// </summary>
        public static int MaybeSpawnForNewGame(Game game)
        {
            if (!AutoSpawnSites || game == null) return 0;

            int spawned = 0;
            foreach (var faction in game.Factions.Values.ToList()) // snapshot: factory calls can add entities
            {
                if (!faction.TryGetDataBlob<FactionInfoDB>(out var fi) || fi.IsNPC) continue;

                foreach (var colony in fi.Colonies)
                {
                    if (colony == null || !colony.TryGetDataBlob<ColonyInfoDB>(out var ci)) continue;
                    var body = ci.PlanetEntity;
                    if (body == null || !body.IsValid || body.Manager is not StarSystem sys) continue;

                    spawned += SpawnDemoSitesAt(game, sys, body);
                    break; // one home world per faction
                }
            }
            return spawned;
        }

        /// <summary>
        /// Author the demo sites at <paramref name="body"/>: a space Science anomaly co-located with the world, and a
        /// surface Science ruin on the world's region 1. Both are single-path Research one-shots (auto-resolve when
        /// worked). Returns how many were created. Defensive on missing blobs — spawns whichever it can.
        /// </summary>
        public static int SpawnDemoSitesAt(Game game, StarSystem system, Entity body)
        {
            if (game == null || system == null || body == null || !body.IsValid) return 0;
            int n = 0;

            // (1) A space Science anomaly co-located with the home world (a ship parked on it works it → research).
            if (body.TryGetDataBlob<PositionDB>(out var bodyPos))
            {
                FieldSiteFactory.CreateAnomalySite(system, bodyPos.AbsolutePosition, "Derelict Probe",
                    role: SiteRole.Science, shape: SiteShape.OneShot, hook: SiteHook.Benign, yield: SiteYield.Research,
                    understandingToResolve: 50.0);
                n++;
            }

            // (2) A surface Science ruin on region 1 (a ground unit standing there works it → research — the easy demo).
            if (body.TryGetDataBlob<PlanetRegionsDB>(out var regions) && regions.Regions.Count > 1)
            {
                FieldSiteFactory.CreateSurfaceSite(system, body, 1, 0, 0, "Ancient Ruin",
                    role: SiteRole.Science, shape: SiteShape.OneShot, hook: SiteHook.Benign, yield: SiteYield.Research,
                    understandingToResolve: 50.0);
                n++;
            }

            return n;
        }
    }
}
