using System;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.Movement;
using Pulsar4X.Orbital;
using Pulsar4X.Ships;

namespace Pulsar4X.Sites
{
    /// <summary>
    /// Site Engine SE-1b — the DRIVER that turns "a ship parked at the anomaly" into banked progress
    /// (docs/SITE-ENGINE-DESIGN.md §4). A daily hotloop keyed to <see cref="FieldSiteDB"/>: for each site, if an
    /// eligible worker (SE-1b v1 = any faction ship) is present within <see cref="PresenceRadius_m"/>, it feeds one
    /// work step into the pure <see cref="SiteMachine"/> (which begins the study on first work and banks
    /// Progress + Understanding). No worker present → the site simply doesn't advance (no timer, agency-preserving).
    ///
    /// Byte-identical in the live game: no live entity carries a <see cref="FieldSiteDB"/> yet (nothing calls
    /// <see cref="FieldSiteFactory"/> in the New-Game path), so <see cref="ProcessManager"/> finds no sites and the
    /// processor sleeps. The worker's Role/Grade sourcing the work RATE is SE-2 (the Command Berth); for now the
    /// rate is a flat constant so the spine can be proven end to end.
    /// </summary>
    public class SiteWorkProcessor : IHotloopProcessor
    {
        /// <summary>How close a worker must be (metres) to count as "on-site" and work the anomaly. 1,000 km —
        /// the same close-parked scale a jump-point/geo survey uses.</summary>
        public static double PresenceRadius_m = 1_000_000.0;

        /// <summary>Yield magnitude banked per day a worker is on-site (SE-2 replaces the flat rate with the
        /// Command Berth's Role/Grade output).</summary>
        public static double WorkPerDay = 10.0;

        /// <summary>Understanding banked per day a worker is on-site — the gate that unlocks the resolve branch.</summary>
        public static double UnderstandingPerDay = 5.0;

        /// <summary>SE-3b — the flat work-rate multiplier for a GROUND-unit worker on a surface site (no berth to scale
        /// it). v1 = 1.0 (the design's "unit rate ≤ facility rate" — a berthed ship facility exceeds this via Grade).</summary>
        public static double SurfaceWorkMultiplier = 1.0;

        public TimeSpan RunFrequency => TimeSpan.FromDays(1);
        public TimeSpan FirstRunOffset => TimeSpan.FromSeconds(1);
        public Type GetParameterType => typeof(FieldSiteDB);

        public void Init(Game game) { }

        public int ProcessManager(EntityManager manager, int deltaSeconds)
        {
            var sites = manager.GetAllEntitiesWithDataBlob<FieldSiteDB>();
            foreach (var site in sites)
                ProcessEntity(site, deltaSeconds);
            return sites.Count;
        }

        public void ProcessEntity(Entity entity, int deltaSeconds)
        {
            if (!entity.TryGetDataBlob<FieldSiteDB>(out var site)) return;

            double days = deltaSeconds / 86400.0;

            // SE-5d: a resolved PERSISTENT site (a standing faucet) can RUPTURE into a crisis if it carries a rupture
            // chance — the reward carried the risk (docs/SITE-ENGINE-DESIGN.md §4). Handled BEFORE the no-work early
            // return below. Default RuptureChancePerDay 0 → never rolls → byte-identical (a persistent site otherwise
            // just sits here, taking no work).
            if (site.Status == SiteStatus.Persistent)
            {
                if (site.RuptureChancePerDay > 0) TryRupture(entity, site, days);
                return; // a persistent site takes no work either way
            }

            // A resolved site (Depleted/Ruptured) takes no more work — SiteMachine also guards this, but skip the
            // presence scan when there's nothing to accrue.
            if (site.Status != SiteStatus.Discovered && site.Status != SiteStatus.Worked) return;

            // SE-4c: a LIVE incident bleeds steady pressure into its region every tick — worker present or not. This is
            // the "stop-the-bleed" clock: it harms your holding force until you contain the site. Only fires for a
            // Shape.Incident site (SiteMachine.IsIncidentLive), so every OneShot/Persistent site is byte-identical.
            if (SiteMachine.IsIncidentLive(site))
                ApplyIncidentPressure(entity, site, days);

            // SE-3b: a surface site (on a planet's ground) is worked by a ground unit standing on it; a space anomaly
            // is worked by a ship parked at it. The two presence models are different, so branch here. Each path only
            // reaches its resolve/deliver after a real accrue, so the space path stays byte-identical.
            if (site.IsSurfaceSite)
                ProcessSurfaceWork(entity, site, days);
            else
                ProcessSpaceWork(entity, site, days);
        }

        /// <summary>
        /// SE-4c — bleed a live incident's steady pressure into its region for a step of <paramref name="days"/>: every
        /// NON-menace unit standing in the site's region loses <c>CurrentPressure × days</c> health (the menace is the
        /// SOURCE, so it's spared). The drain reuses the same Health axis the ground layer's attrition uses. v1 is
        /// surface-only (a space incident's pressure is a later concern). Pure over game state (no RNG).
        /// </summary>
        private static void ApplyIncidentPressure(Entity entity, FieldSiteDB site, double days)
        {
            if (!site.IsSurfaceSite) return;
            double drain = SiteMachine.CurrentPressure(site) * days;
            if (drain <= 0) return;

            if (entity.Manager == null || !entity.Manager.TryGetEntityById(site.SurfaceBodyEntityId, out var body)) return;
            if (!body.TryGetDataBlob<Pulsar4X.GroundCombat.GroundForcesDB>(out var forces)) return;

            foreach (var unit in forces.Units)
            {
                if (unit.Health <= 0) continue;
                if (unit.RegionIndex != site.SurfaceRegionIndex) continue;
                if (unit.FactionOwnerID == site.MenaceFactionId) continue; // the menace causes the bleed; it doesn't suffer it

                unit.Health -= drain;
                if (unit.Health < 0) unit.Health = 0;
            }
        }

        /// <summary>
        /// SE-5d — roll the RUPTURE of a live persistent site for a step of <paramref name="days"/>. On a hit the faucet
        /// turns into a crisis: the site's Status flips to <see cref="SiteStatus.Ruptured"/> (it stops producing) and a
        /// NEW crisis site is spawned via <see cref="SpawnCrisis"/>. Uses the SEEDED system RNG (deterministic replay).
        /// Wrapped so a hotloop never throws (L4). A zero-chance site never reaches here (guarded in ProcessEntity), so
        /// an ordinary persistent stream is byte-identical.
        /// </summary>
        private static void TryRupture(Entity entity, FieldSiteDB site, double days)
        {
            try
            {
                double chance = Math.Min(1.0, site.RuptureChancePerDay * days);
                if (chance <= 0.0) return;

                var rng = entity.Manager?.RNG;
                if (rng == null) return;
                if (rng.NextDouble() >= chance) return; // survived this step

                site.Status = SiteStatus.Ruptured; // the faucet ruptured — it stops being a safe stream
                SpawnCrisis(entity, site);
            }
            catch
            {
                // A rupture must never crash the sim (L4). If the crisis spawn fails, the site is left Ruptured
                // (inert) rather than propagating the throw out of the daily hotloop.
            }
        }

        /// <summary>
        /// SE-5d — birth the crisis a ruptured site spawns. v1: a ruptured SURFACE site spawns a fresh Shape.Incident
        /// crisis at its own body+region (reusing the SE-4 menace machinery via <see cref="IncidentScenario.SpawnIncidentAt"/>) —
        /// a hostile force + steady pressure + spread. A space anomaly just goes Ruptured (a space-site crisis is a
        /// later refinement — flagged). The child is Incident-shaped, so it can't itself rupture → no cascade. Defensive:
        /// any missing game/system/body → no spawn.
        /// </summary>
        private static void SpawnCrisis(Entity entity, FieldSiteDB site)
        {
            if (!site.IsSurfaceSite) return; // space-site crisis is a follow-up
            var game = entity.Manager?.Game;
            if (game == null) return;
            if (entity.Manager is not StarSystem sys) return;
            if (!entity.Manager.TryGetEntityById(site.SurfaceBodyEntityId, out var body)) return;

            IncidentScenario.SpawnIncidentAt(game, sys, body, site.SurfaceRegionIndex, name: "Rupture Crisis");
        }

        /// <summary>SE-1b/2b/2c — the SPACE-anomaly work path: a ship parked within <see cref="PresenceRadius_m"/>
        /// works it, its manned Command Berth scales the rate, and a dangerous site rolls the posting incident.</summary>
        private void ProcessSpaceWork(Entity entity, FieldSiteDB site, double days)
        {
            if (!entity.TryGetDataBlob<PositionDB>(out var sitePos)) return;
            if (!TryFindWorker(entity.Manager, sitePos.AbsolutePosition, out var worker)) return;

            // SE-2b: a MANNED Command Berth on the worker whose Role matches the site works it FASTER — scaled by the
            // berth's Grade and the seated leader's competence (+ the berth's Support). No matching manned berth (or no
            // berth at all) → the multiplier is 1.0, i.e. the SE-1b flat rate — additive / byte-identical.
            var berth = GetWorkingBerth(worker, site.Role);
            double multiplier = MultiplierOf(berth, worker.Manager);

            site.WorkedByFactionId = worker.FactionOwnerID;
            SiteMachine.Accrue(site, WorkPerDay * multiplier * days, UnderstandingPerDay * multiplier * days);

            // SE-2c: a leader posted to a DANGEROUS site can be lost. A Benign site has zero danger → no roll.
            if (berth != null) RollPostingIncident(entity, worker, berth, site, days);

            TryResolveAndDeliver(entity, site);
        }

        /// <summary>
        /// SE-3b — the SURFACE-site work path: a friendly ground unit standing in the site's region works it. No berth
        /// yet (a ground unit carries none), so the rate is the flat <see cref="SurfaceWorkMultiplier"/> (v1 = 1.0, the
        /// "unit rate ≤ facility rate" of the design). The guardian gate (refuse work until the region is cleared) is
        /// SE-3d.
        /// </summary>
        private static void ProcessSurfaceWork(Entity entity, FieldSiteDB site, double days)
        {
            if (!TryFindGroundWorker(entity.Manager, site, out int workerFactionId)) return;

            // SE-3d — the GUARDIAN gate: a foreign unit (a neutral/menace guardian, or a rival) standing in the site's
            // region blocks work until it is cleared. You beat the defender FIRST (the region combat + capture the
            // ground layer already runs), THEN work the site. A region with only your own units is clear → work
            // proceeds. A Benign surface site with no guardian is never gated → byte-identical.
            if (!RegionIsClearFor(entity.Manager, site, workerFactionId)) return;

            site.WorkedByFactionId = workerFactionId;
            SiteMachine.Accrue(site, WorkPerDay * SurfaceWorkMultiplier * days, UnderstandingPerDay * SurfaceWorkMultiplier * days);

            TryResolveAndDeliver(entity, site);
        }

        /// <summary>
        /// SE-3d — is the site's region CLEAR for the worker faction? True unless a live unit of another faction (a
        /// neutral/menace guardian or a rival) stands in the region. Pure read; the actual clearing is the ground
        /// layer's region combat + capture (`GroundForcesProcessor`).
        /// </summary>
        public static bool RegionIsClearFor(EntityManager manager, FieldSiteDB site, int workerFactionId)
        {
            if (manager == null || site == null || site.SurfaceBodyEntityId < 0) return false;
            if (!manager.TryGetEntityById(site.SurfaceBodyEntityId, out var body)) return false;
            if (!body.TryGetDataBlob<Pulsar4X.GroundCombat.GroundForcesDB>(out var forces)) return true;

            foreach (var unit in forces.Units)
            {
                if (unit.Health <= 0) continue;
                if (unit.RegionIndex != site.SurfaceRegionIndex) continue;
                if (unit.FactionOwnerID != workerFactionId) return false; // a foreign unit holds the region
            }
            return true;
        }

        /// <summary>SE-1c — once understanding fills, the single-branch site RESOLVES and pays its yield out ONCE into
        /// the consumer system its Yield names. Shared by both work paths; only called after a real accrue.
        /// SE-5c: a BRANCHED site (one offering a choice) does NOT auto-resolve here — it keeps accruing understanding
        /// (so more branches unlock) and WAITS for the player to commit a branch via <see cref="CommitSiteBranchOrder"/>.
        /// A branchless site (every existing site — nothing authors branches yet) is unchanged → byte-identical.</summary>
        private static void TryResolveAndDeliver(Entity entity, FieldSiteDB site)
        {
            if (site.HasBranches) return; // a branched site waits for a committed choice (SE-5c), no auto-resolve

            if (!site.YieldDelivered && SiteMachine.BranchUnlocked(site) && SiteMachine.Resolve(site))
            {
                DeliverYield(entity, site);
                site.YieldDelivered = true;
            }
        }

        /// <summary>
        /// SE-3b — find a ground unit working a surface site: the first ALIVE, non-neutral unit on the site's body that
        /// stands in the site's region. Returns its faction (the site's yield routes there). v1 matches by region (the
        /// guardian gate + hex-exact standing are later refinements). Public + static so the presence rule is testable.
        /// </summary>
        public static bool TryFindGroundWorker(EntityManager manager, FieldSiteDB site, out int workerFactionId)
        {
            workerFactionId = Game.NeutralFactionId;
            if (manager == null || site == null || site.SurfaceBodyEntityId < 0) return false;
            if (!manager.TryGetEntityById(site.SurfaceBodyEntityId, out var body)) return false;
            if (!body.TryGetDataBlob<Pulsar4X.GroundCombat.GroundForcesDB>(out var forces)) return false;

            foreach (var unit in forces.Units)
            {
                if (unit.FactionOwnerID == Game.NeutralFactionId) continue;
                if (unit.Health <= 0) continue;
                if (unit.RegionIndex != site.SurfaceRegionIndex) continue;

                workerFactionId = unit.FactionOwnerID;
                return true;
            }
            return false;
        }

        /// <summary>The worker's best MANNED, Role-matching Command Berth (or null) — the berth whose seated leader
        /// actually works the site. SE-2b/2c read it for the work rate and the posting-danger roll.</summary>
        public static CommandBerth GetWorkingBerth(Entity worker, SiteRole role)
        {
            if (worker == null || !worker.TryGetDataBlob<CommandBerthDB>(out var roster)) return null;
            return roster.BestOccupiedBerthFor(role);
        }

        /// <summary>The work-rate multiplier a manned berth grants: <c>Grade × (1 + leaderSkill + Support/100)</c>.
        /// A null berth (none manned/matching) → 1.0 (the SE-1b flat rate). Grade floors at 1 so a manned berth is
        /// never a penalty.</summary>
        public static double MultiplierOf(CommandBerth berth, EntityManager manager)
        {
            if (berth == null) return 1.0;
            double grade = Math.Max(1, berth.Grade);
            double skill = BerthOps.LeaderSkill01(manager, berth.CommanderID) + berth.Support / 100.0;
            return grade * (1.0 + skill);
        }

        /// <summary>SE-2b convenience (used by gauges): the multiplier from the worker's best manned matching berth.</summary>
        public static double BerthWorkMultiplier(Entity worker, SiteRole role)
            => MultiplierOf(GetWorkingBerth(worker, role), worker?.Manager);

        /// <summary>
        /// SE-2c — roll the posting-danger incident on the berth's seated leader for a work step of
        /// <paramref name="days"/>. On a hit the leader is LOST: the berth is vacated (freeing the seat + clearing the
        /// leader's back-reference) and the commander is destroyed (the grave rung, which also fires the standard crew-
        /// loss event). Uses the SEEDED system RNG (deterministic replay). A zero-danger site (Benign) never rolls, so
        /// the RNG is untouched and the base anomaly stays byte-identical.
        /// </summary>
        private static void RollPostingIncident(Entity siteEntity, Entity worker, CommandBerth berth, FieldSiteDB site, double days)
        {
            double chance = SiteHazard.IncidentChance(site.Hook, berth.Survivability, days);
            if (chance <= 0.0) return;

            var rng = siteEntity.Manager?.RNG;
            if (rng == null) return;
            if (rng.NextDouble() >= chance) return; // survived this step

            int leaderId = berth.CommanderID;
            BerthOps.VacateBerth(worker, leaderId); // frees the seat + clears AssignedTo (the leader is alive here)

            if (worker.Manager != null && worker.Manager.TryGetGlobalEntityById(leaderId, out var leader))
                Pulsar4X.People.CommanderFactory.DestroyCommander(leader);
        }

        /// <summary>Route a resolved single-path site's banked Progress into the consumer system its Yield dial names.</summary>
        private static void DeliverYield(Entity siteEntity, FieldSiteDB site)
            => DeliverSiteYield(siteEntity, site, site.Yield, site.Progress);

        /// <summary>
        /// SE-5c — route a CHOSEN yield of a given magnitude into its consumer system. The one yield router, shared by
        /// the single-path resolve (<see cref="DeliverYield"/> passes the site's own Yield × Progress) and the branch
        /// commit (<see cref="CommitSiteBranchOrder"/> passes the committed branch's Yield × Progress×YieldScale). Pays
        /// the site's <see cref="FieldSiteDB.WorkedByFactionId"/>. Public + static so the order can pay a branch.
        /// SE-1c wires Research; the other yields route into their own systems in later slices (SE-5e diplomacy/intel).
        /// Defensive (no game / no manager → no-op) so a resolve never throws.
        /// </summary>
        public static void DeliverSiteYield(Entity siteEntity, FieldSiteDB site, SiteYield yield, double magnitude)
        {
            var game = siteEntity?.Manager?.Game;
            if (game == null || site == null) return;

            switch (yield)
            {
                case SiteYield.Research:
                    SiteYields.DeliverResearch(game, site.WorkedByFactionId, (int)magnitude);
                    break;
                // SiteYield.Blueprint / Resource / Population / Leader / StrategicAsset / NetworkRoute / Nothing
                // route into their own consumer systems in later slices.
                default:
                    break;
            }
        }

        /// <summary>
        /// Find the nearest eligible worker to a site position. SE-1b v1: any faction (non-neutral) ship within
        /// <see cref="PresenceRadius_m"/>. SE-2 will require a Command Berth of the site's Role. Public + static so
        /// the presence rule is testable in isolation.
        /// </summary>
        public static bool TryFindWorker(EntityManager manager, Vector3 sitePosition, out Entity worker)
        {
            worker = Entity.InvalidEntity;
            double bestDistance = PresenceRadius_m;
            bool found = false;

            foreach (var candidate in manager.GetAllEntitiesWithDataBlob<ShipInfoDB>())
            {
                if (candidate.FactionOwnerID == Game.NeutralFactionId) continue;
                if (!candidate.TryGetDataBlob<PositionDB>(out var candPos)) continue;

                double distance = Vector3.Distance(sitePosition, candPos.AbsolutePosition);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    worker = candidate;
                    found = true;
                }
            }

            return found;
        }
    }
}
