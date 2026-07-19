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
    /// Default composition is a modest combined-arms garrison (<see cref="Composition"/>); units are C# designs (like the
    /// DevTools raise button). Idempotent: a body that already holds this faction's ground units is skipped, so it never
    /// double-garrisons. **PER-FACTION (2026-07-16):** a faction can author its OWN garrison mix via a "garrison" JSON
    /// node (→ <see cref="FactionInfoDB.GarrisonComposition"/>) — the ground echo of the per-faction fleet ladder — so the
    /// militarist UMF garrisons a heavier Martian legion (4/3/2) than the default light watch (3/2/1). Absent → the
    /// default stands → byte-identical.
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
                var composition = CompositionFor(fi);   // this faction's authored garrison, else the engine default
                foreach (var colony in fi.Colonies)
                {
                    if (colony == null || !colony.TryGetDataBlob<ColonyInfoDB>(out var ci)) continue;
                    var body = ci.PlanetEntity;
                    if (body == null || !body.IsValid || !body.HasDataBlob<PlanetRegionsDB>()) continue;
                    raised += RaiseHomeGarrison(body, faction.Id, composition);
                }
            }
            catch { /* a bad colony is skipped — a New Game must never break over the garrison */ }
            return raised;
        }

        /// <summary>Raise the garrison on one body in its capital region (0), owned by <paramref name="factionId"/>.
        /// Idempotent: skips if this faction already has ground units on the body. <paramref name="composition"/> is the
        /// faction's authored mix; null → the engine default (so existing callers are byte-identical).</summary>
        public static int RaiseHomeGarrison(Entity body, int factionId, (GroundUnitType type, int count)[] composition = null)
        {
            if (body == null || !body.HasDataBlob<PlanetRegionsDB>()) return 0;
            if (body.TryGetDataBlob<GroundForcesDB>(out var existing) && existing.Units.Any(u => u.FactionOwnerID == factionId))
                return 0;   // already garrisoned — don't double up

            int raised = 0;
            foreach (var (type, count) in (composition ?? Composition))
            {
                var design = MakeGarrisonDesign(type);
                for (int i = 0; i < count; i++)
                {
                    GroundForces.RaiseUnit(body, design, factionId, 0, $"{type} Garrison");
                    raised++;
                }
            }
            // AI formation parity (R2 gap 3): a raised garrison fields as a BATTALION, not scattered loose units, so the
            // ground tactical brain has a formation to command. Gated OFF by default → byte-identical (the AutoRaise/
            // InterruptTimeOnNewBattle pattern); the New-Game path / tactical-AI wiring flips GroundAssembly.AutoFormUp on.
            if (raised > 0 && GroundAssembly.AutoFormUp)
                GroundAssembly.FormUpLoose(body, factionId, "Home Guard");
            return raised;
        }

        /// <summary>A throwaway C# design per unit type (the base-mod JSON ground-unit template is deferred, gotcha #10).
        /// Modest per-type differentiation on top of the triangle: armour hits hard + tough, artillery hits hardest but
        /// fragile, infantry the baseline.</summary>
        /// <summary>Monthly UPKEEP a start-garrison unit costs, per hit-point (a size proxy — the code-built garrison
        /// designs carry no assembler mass) (G2.3c). So the standing home garrison finally costs its owner money ("an
        /// army costs money as it stands"), the <c>GroundUpkeep</c> biller has a non-zero value to bill. FLAGGED.</summary>
        public const double GarrisonUpkeepPerHitPoint = 0.02;   // FLAGGED balance value

        private static GroundUnitDesign MakeGarrisonDesign(GroundUnitType type)
        {
            double hp = type == GroundUnitType.Armor ? 700 : (type == GroundUnitType.Artillery ? 400 : 500);
            return new GroundUnitDesign
            {
                UniqueID = "start-garrison-" + type,
                Name = type + " Garrison",
                UnitType = type,
                Attack = type == GroundUnitType.Artillery ? 160 : (type == GroundUnitType.Armor ? 140 : 100),
                Defense = type == GroundUnitType.Armor ? 15 : (type == GroundUnitType.Artillery ? 5 : 10),
                HitPoints = hp,
                UpkeepCredits = hp * GarrisonUpkeepPerHitPoint,   // G2.3c — the standing garrison finally costs money (FLAGGED)
            };
        }

        /// <summary>This faction's authored garrison mix (from its <see cref="FactionInfoDB.GarrisonComposition"/> — a
        /// scenario's "garrison" JSON node), parsed to the (type, count) shape; falls back to the engine
        /// <see cref="Composition"/> for a faction that authored none (→ byte-identical). Unknown type names / non-positive
        /// counts are skipped; an all-invalid map also falls back.</summary>
        internal static (GroundUnitType type, int count)[] CompositionFor(FactionInfoDB fi)
        {
            if (fi?.GarrisonComposition == null || fi.GarrisonComposition.Count == 0) return Composition;
            var list = new System.Collections.Generic.List<(GroundUnitType, int)>();
            foreach (var kv in fi.GarrisonComposition)
                if (Enum.TryParse<GroundUnitType>(kv.Key, ignoreCase: true, out var t) && kv.Value > 0)
                    list.Add((t, kv.Value));
            return list.Count > 0 ? list.ToArray() : Composition;
        }
    }
}
