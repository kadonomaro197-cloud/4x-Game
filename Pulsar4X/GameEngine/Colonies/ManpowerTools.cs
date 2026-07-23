using System.Linq;
using Pulsar4X.Engine;

namespace Pulsar4X.Colonies
{
    /// <summary>
    /// The M3-2b crew ENFORCEMENT bridge (docs/MORALE-AND-POPULATION-DESIGN.md): the small set of calls that
    /// turn the <see cref="ColonyManpowerDB"/> pool math into a real gate on ship construction — "you cannot
    /// build a ship you can't crew." Kept in one place so the three wiring sites (the build gate in
    /// <c>IndustryTools.ConstructStuff</c>, the commit in <c>ShipDesign.OnConstructionComplete</c>, and the
    /// release in <c>ShipFactory.DestroyShip</c>) all read the same rule.
    ///
    /// INERT WHEN ABSENT — every call no-ops (allows the build, commits nothing) if the building host has no
    /// <see cref="ColonyManpowerDB"/> (e.g. a station, which carries none). That, plus the start fleet bypassing
    /// the construction queue entirely (it spawns via <c>ShipFactory.CreateShip</c> directly) and the huge
    /// starting population, is why this gate cannot break New Game. The FEEL (how often a real economy runs
    /// short of crew) is a local PC-test — see TESTING-TRACKER.
    /// </summary>
    public static class ManpowerTools
    {
        /// <summary>Total population on a host — a planet COLONY or a manned STATION (0 if it isn't a population host).</summary>
        private static long PopulationOf(Entity host)
        {
            if (host.TryGetDataBlob<ColonyInfoDB>(out var info)) return info.Population.Values.Sum();
            // A station is a population host too: a station-dwelling faction (the Kithrin live on Titan, not a
            // planet) crews its builds from the residents ON the station. ManpowerTools is otherwise fully
            // host-agnostic, so reading StationInfoDB.Population here is all it takes for the crew gate to work
            // off-world exactly as it does on a colony.
            if (host.TryGetDataBlob<Pulsar4X.Stations.StationInfoDB>(out var station)) return station.Population.Values.Sum();
            return 0L;
        }

        /// <summary>
        /// Decide whether <paramref name="host"/> may build a unit needing <paramref name="crewRequired"/> bulk
        /// manpower right now, applying the owning government's <see cref="CrewShortagePolicy"/> (Block by
        /// default; a high-authority regime conscripts = BuildUnderstaffed). Returns a proceed-decision with
        /// zero commitment if the host has no manpower pool (unenforced host).
        /// </summary>
        public static BuildCrewDecision ResolveBuild(Entity host, long crewRequired)
        {
            if (host == null || !host.TryGetDataBlob<ColonyManpowerDB>(out var manpower))
                return new BuildCrewDecision(true, 0, false, 0); // unenforced host → always allowed

            long available = manpower.AvailableBulk(PopulationOf(host));
            var policy = Pulsar4X.Factions.GovernmentTools.OwnerOf(host).CrewPolicy();
            return ColonyManpowerDB.ResolveConstructionCrew(available, crewRequired, policy);
        }

        /// <summary>Commit crew from the host's pool (no-op if the host has no pool or crew ≤ 0).</summary>
        public static void CommitCrew(Entity host, long crew)
        {
            if (crew > 0 && host != null && host.TryGetDataBlob<ColonyManpowerDB>(out var manpower))
                manpower.CommitBulk(crew);
        }

        /// <summary>
        /// Release committed crew back to the source colony's pool (no-op if unset/absent). Called when a ship
        /// that drew from a pool is destroyed or disbanded — the crew are freed to build again. (The harsher
        /// "casualties permanently shrink the source population" sting is parked for local calibration.)
        /// </summary>
        public static void ReleaseCrew(Entity sourceColony, long crew)
        {
            if (crew > 0 && sourceColony != null && sourceColony.TryGetDataBlob<ColonyManpowerDB>(out var manpower))
                manpower.ReleaseBulk(crew);
        }

        // ── TALENT (the scarce half) — the anti-dominance handle for Enhancers ⚙6.2 Unit Caliber ──
        // A veteran cadre is drawn from a colony's SCARCE TALENT pool (officers/specialists), not bulk workforce.
        // So an elite hull can't be spammed: talent is ~0.5% of population, and an elite ship ties some up until it
        // dies. Mirrors the crew half exactly, INERT WHEN ABSENT (a talent-less host — or a ship with no caliber
        // module, talentRequired 0 — always proceeds and commits nothing → byte-identical).

        /// <summary>Whether <paramref name="host"/> has <paramref name="talentRequired"/> free talent to commit right
        /// now. True when the host has no manpower pool (unenforced) or the requirement is ≤ 0. Unlike the bulk crew
        /// gate this is a HARD wall (no conscript-understaffed policy — you can't fake a veteran crew).</summary>
        public static bool HasTalentToBuild(Entity host, long talentRequired)
        {
            if (talentRequired <= 0) return true;
            if (host == null || !host.TryGetDataBlob<ColonyManpowerDB>(out var manpower)) return true;
            return manpower.CanCommitTalent(PopulationOf(host), talentRequired);
        }

        /// <summary>Commit scarce talent from the host's pool (no-op if the host has no pool or talent ≤ 0).</summary>
        public static void CommitTalent(Entity host, long talent)
        {
            if (talent > 0 && host != null && host.TryGetDataBlob<ColonyManpowerDB>(out var manpower))
                manpower.CommitTalent(talent);
        }

        /// <summary>Release committed talent back to the source colony's pool when the ship that drew it is destroyed.</summary>
        public static void ReleaseTalent(Entity sourceColony, long talent)
        {
            if (talent > 0 && sourceColony != null && sourceColony.TryGetDataBlob<ColonyManpowerDB>(out var manpower))
                manpower.ReleaseTalent(talent);
        }
    }
}
