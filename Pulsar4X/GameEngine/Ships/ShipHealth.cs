using Pulsar4X.Engine;
using Pulsar4X.Datablobs;      // ComponentInstancesDB

namespace Pulsar4X.Ships
{
    /// <summary>
    /// The missing ship <b>Health</b> gauge (FORCES-WINDOW-DESIGN §S8) — the aggregate accessor the Force-Management
    /// roster's Health column needs. Ground formations already compute a health (<c>GroundFormationTools.FormationHealth</c>)
    /// and a ground unit carries <c>Health</c>/<c>MaxHealth</c> directly, but a SHIP's health was never aggregated
    /// anywhere the UI could read — the damage system tracks it per-<c>ComponentInstance</c> (<c>HealthPercent</c>), and
    /// nothing summed it. This is the gauge-before-UI job that decision #4 of §7 asked for: build the number engine-side
    /// (so it's CI-tested), then the client just reads it.
    ///
    /// Pure / read-only / defensive — safe to call every frame from the UI and never throws.
    /// </summary>
    public static class ShipHealth
    {
        /// <summary>
        /// A ship's structural health as a fraction <c>0..1</c> — the summed <see cref="Datablobs.ComponentInstancesDB"/>
        /// <c>HealthPercent</c> of its LIVING components divided by the count the ship's <see cref="ShipDesign"/> was
        /// built with. Dividing by the ORIGINAL design count (not the surviving count) is the honest choice: a component
        /// destroyed in battle is REMOVED from <c>AllComponents</c> (<c>DamageProcessor</c> → <c>RemoveComponentInstance</c>),
        /// so a plain mean-of-survivors would read a half-wrecked ship as pristine; here a lost component correctly
        /// counts as 0. <c>1.0</c> = pristine; <c>1.0</c> for an entity with no components (defensive — a bare hull is
        /// "undamaged", not "0 health"). The result is clamped to <c>[0,1]</c>.
        ///
        /// v1 weights every component equally (a lost sensor and a lost reactor count the same). A hit-point-weighted
        /// health (closer to <c>ShipCombatValueDB.Toughness</c> current-vs-max) is a flagged refinement.
        /// </summary>
        public static double HealthFraction(Entity ship)
        {
            // null OR an UNMANAGED entity (no Manager — e.g. a bare Entity.Create()) has no reachable components → 1.0.
            // The Manager guard is what makes "never throws" true: Entity.TryGetDataBlob delegates to Manager, so a
            // Manager-less entity would NRE without it (the roster only ever passes valid managed ships, but the
            // accessor honours its defensive contract for any caller).
            if (ship == null || ship.Manager == null || !ship.TryGetDataBlob<ComponentInstancesDB>(out var comps)) return 1.0;

            double sum = 0;
            int live = 0;
            foreach (var c in comps.AllComponents.Values)
            {
                sum += c.HealthPercent;
                live++;
            }

            // Denominator = the ORIGINAL design component count, so destroyed (removed) components count as 0 health.
            // Fall back to the live count when the design isn't reachable (a component host that isn't a designed ship).
            int total = live;
            if (ship.TryGetDataBlob<ShipInfoDB>(out var info) && info.Design?.Components != null)
            {
                int designCount = 0;
                foreach (var (_, count) in info.Design.Components)
                    designCount += count;
                if (designCount > 0) total = designCount;
            }

            if (total <= 0) return 1.0;
            double frac = sum / total;
            return frac < 0 ? 0 : (frac > 1 ? 1 : frac);
        }
    }
}
