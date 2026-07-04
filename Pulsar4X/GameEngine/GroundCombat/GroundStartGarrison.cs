using System;
using System.Linq;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Colonies;
using Pulsar4X.Galaxy;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// Raises a default HOME GARRISON on a faction's colonies — so a fresh New Game already has ground units on the
    /// map to command (the ground echo of the auto-spawned space combat scenario). Run from the New-Game path
    /// (<c>NewGameMenu.CreateGameCore</c>), NOT from <see cref="ColonyFactory"/>, so the CI test harness
    /// (<c>TestScenario.CreateWithColony</c>) stays a clean slate — the same split the combat scenario uses.
    ///
    /// v1 composition is a modest combined-arms garrison (tunable — see <see cref="Composition"/>); units are C#
    /// designs (like the DevTools raise button) since the base-mod JSON ground-unit template is deferred. Idempotent:
    /// a body that already holds this faction's ground units is skipped, so it never double-garrisons.
    /// </summary>
    public static class GroundStartGarrison
    {
        /// <summary>The starting garrison per home colony (type → count). Tunable; the triangle/terrain differentiate.</summary>
        public static readonly (GroundUnitType type, int count)[] Composition =
        {
            (GroundUnitType.Infantry, 3),
            (GroundUnitType.Armor, 2),
            (GroundUnitType.Artillery, 1),
        };

        /// <summary>Raise the home garrison on every colony of <paramref name="faction"/> that has a ground map.
        /// Returns the total units raised. Defensive — never throws (runs on the New-Game path).</summary>
        public static int RaiseForFactionColonies(Game game, Entity faction)
        {
            if (game == null || faction == null) return 0;
            int raised = 0;
            try
            {
                if (!faction.TryGetDataBlob<FactionInfoDB>(out var fi)) return 0;
                foreach (var colony in fi.Colonies)
                {
                    if (colony == null || !colony.TryGetDataBlob<ColonyInfoDB>(out var ci)) continue;
                    var body = ci.PlanetEntity;
                    if (body == null || !body.IsValid || !body.HasDataBlob<PlanetRegionsDB>()) continue;
                    raised += RaiseHomeGarrison(body, faction.Id);
                }
            }
            catch { /* a bad colony is skipped — a New Game must never break over the garrison */ }
            return raised;
        }

        /// <summary>Raise the garrison on one body in its capital region (0), owned by <paramref name="factionId"/>.
        /// Idempotent: skips if this faction already has ground units on the body.</summary>
        public static int RaiseHomeGarrison(Entity body, int factionId)
        {
            if (body == null || !body.HasDataBlob<PlanetRegionsDB>()) return 0;
            if (body.TryGetDataBlob<GroundForcesDB>(out var existing) && existing.Units.Any(u => u.FactionOwnerID == factionId))
                return 0;   // already garrisoned — don't double up

            int raised = 0;
            foreach (var (type, count) in Composition)
            {
                var design = MakeGarrisonDesign(type);
                for (int i = 0; i < count; i++)
                {
                    GroundForces.RaiseUnit(body, design, factionId, 0, $"{type} Garrison");
                    raised++;
                }
            }
            return raised;
        }

        /// <summary>A throwaway C# design per unit type (the base-mod JSON ground-unit template is deferred, gotcha #10).
        /// Modest per-type differentiation on top of the triangle: armour hits hard + tough, artillery hits hardest but
        /// fragile, infantry the baseline.</summary>
        private static GroundUnitDesign MakeGarrisonDesign(GroundUnitType type) => new GroundUnitDesign
        {
            UniqueID = "start-garrison-" + type,
            Name = type + " Garrison",
            UnitType = type,
            Attack = type == GroundUnitType.Artillery ? 160 : (type == GroundUnitType.Armor ? 140 : 100),
            Defense = type == GroundUnitType.Armor ? 15 : (type == GroundUnitType.Artillery ? 5 : 10),
            HitPoints = type == GroundUnitType.Armor ? 700 : (type == GroundUnitType.Artillery ? 400 : 500),
            // V1: speed multiplier on the base march pace — armour is fast, artillery a touch faster than foot (moddable).
            MovementSpeed = type == GroundUnitType.Armor ? 2.0 : (type == GroundUnitType.Artillery ? 1.2 : 1.0),
        };
    }
}
