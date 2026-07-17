using System;
using Pulsar4X.Colonies;      // ColonyInfoDB (colony → planet body)
using Pulsar4X.Components;    // ComponentDesign (a ground-unit design mounting GroundUnitAtb)
using Pulsar4X.Engine;        // Entity
using Pulsar4X.Factions;      // FactionInfoDB
using Pulsar4X.Interfaces;    // IConstructableDesign

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// GROUND REINFORCEMENT — the ground echo of the fleet "size-to-resources + keep-a-reserve" ladder
    /// (<c>Fleets.FleetAssembly.AspirationFor</c> / <c>Factions.ConquerResolver.HasHomeReserve</c>), applied to a
    /// planet's GARRISON. The developer's call: a planetary garrison shouldn't ship its WHOLE defense off on an
    /// invasion (keep a home RESERVE), and a garrison ground below its authored target should be REBUILT — exactly
    /// what the AI already does for its fleets.
    ///
    /// This is the pure DECISION half (READS ONLY, no side effects), the ground twin of the fleet ladder so it reads
    /// like it:
    ///   • <see cref="GarrisonTargetFor"/> — the faction's authored garrison size (off
    ///     <c>GroundStartGarrison.CompositionFor</c> → <see cref="FactionInfoDB.GarrisonComposition"/>; needs NO new
    ///     data — it's the same per-faction mix the start garrison raises).
    ///   • <see cref="GarrisonReserveFor"/> — the minimum a world keeps home (a fraction of the target; the ground
    ///     echo of the fleet's MinToDeploy core).
    ///   • <see cref="CurrentGarrison"/> — the friendly units actually standing on a world (off <c>GroundForcesDB</c>).
    ///   • <see cref="WouldStripReserve"/> — the LOAD-rung guard: loading this unit would drop the world under reserve.
    ///   • <see cref="NeedsReinforcement"/> — the REBUILD-rung trigger: a garrisoned world below its target.
    ///
    /// Pure / deterministic / defensive (never throws) — safe to call from <c>ConquerResolver</c>, which runs inside
    /// the monthly Tick. A faction with no <see cref="FactionInfoDB.GarrisonComposition"/> falls back to the engine
    /// default target; a world with NO friendly garrison is a no-op (the default player, a station-only faction, a
    /// bare test colony) — so the resolver stays byte-identical for them.
    /// </summary>
    public static class GroundReinforcement
    {
        /// <summary>Fraction of the authored garrison TARGET a world keeps home as a minimum RESERVE — the ground echo
        /// of the fleet MinToDeploy core. Loading a garrison unit off for an invasion is refused when it would drop the
        /// world below this floor. FLAGGED tunable (0.5 = keep half at home).</summary>
        public const double ReserveFraction = 0.5;

        /// <summary>The faction's total authored garrison TARGET — the SUM of its <see cref="FactionInfoDB.GarrisonComposition"/>
        /// counts (its own mix if authored, else the engine default via <c>GroundStartGarrison.CompositionFor</c>).
        /// 0 for a null faction (defensive → every predicate below becomes a no-op).</summary>
        public static int GarrisonTargetFor(FactionInfoDB faction)
        {
            if (faction == null) return 0;
            int total = 0;
            foreach (var (_, count) in GroundStartGarrison.CompositionFor(faction))
                if (count > 0) total += count;
            return total;
        }

        /// <summary>The minimum home garrison a world keeps back as a RESERVE — a fraction of the target, floored at 1
        /// whenever there is any target (a garrisoned world always keeps at least one defender). 0 for a null faction.
        /// <para>POSTURE (military-command layer): when a <paramref name="factionEntity"/> is supplied, the reserve is
        /// scaled by that faction's commit-vs-reserve posture (<see cref="Factions.MilitaryCommand.GroundReserveFactor(Entity)"/>)
        /// — an aggressive/high-risk faction keeps a SMALLER garrison reserve (ships more off on an invasion), a cautious
        /// one keeps a BIGGER one. A null entity or a faction with no <c>PersonalityDB</c> → ×1.0 → the reserve is the
        /// unscaled fraction exactly (byte-identical), so an unposture-aware caller behaves exactly as before.</para></summary>
        public static int GarrisonReserveFor(FactionInfoDB faction, Entity factionEntity = null)
        {
            int target = GarrisonTargetFor(faction);
            if (target <= 0) return 0;
            int reserve = (int)Math.Ceiling(target * ReserveFraction);
            if (reserve < 1) reserve = 1;
            return Pulsar4X.Factions.MilitaryCommand.ScaleReserve(
                reserve, Pulsar4X.Factions.MilitaryCommand.GroundReserveFactor(factionEntity));
        }

        /// <summary>Count of <paramref name="factionId"/>'s ground units on a roster (0 for a null roster).</summary>
        public static int CurrentGarrison(GroundForcesDB forces, int factionId)
        {
            if (forces?.Units == null) return 0;
            int n = 0;
            foreach (var u in forces.Units)
                if (u != null && u.FactionOwnerID == factionId) n++;
            return n;
        }

        /// <summary>Count of <paramref name="factionId"/>'s ground units standing on <paramref name="body"/>
        /// (0 if the body has no <see cref="GroundForcesDB"/> roster).</summary>
        public static int CurrentGarrison(Entity body, int factionId)
            => body != null && body.TryGetDataBlob<GroundForcesDB>(out var forces) ? CurrentGarrison(forces, factionId) : 0;

        /// <summary>TRUE when this roster holds a garrison of the faction that is BELOW its authored target — a depleted
        /// world the REBUILD rung should top up. Requires ≥1 standing friendly unit (a garrison the faction actually
        /// maintains), so a roster with none is a no-op.</summary>
        public static bool NeedsReinforcement(GroundForcesDB forces, FactionInfoDB faction, int factionId)
        {
            int current = CurrentGarrison(forces, factionId);
            return current > 0 && current < GarrisonTargetFor(faction);   // a maintained garrison below strength
        }

        /// <summary>TRUE when <paramref name="body"/> holds a below-target garrison of the faction (else FALSE — incl.
        /// a body with no roster / no friendly units).</summary>
        public static bool NeedsReinforcement(Entity body, FactionInfoDB faction, int factionId)
            => body != null && body.TryGetDataBlob<GroundForcesDB>(out var forces) && NeedsReinforcement(forces, faction, factionId);

        /// <summary>TRUE when shipping ONE more unit off this roster (loading it for an invasion) would drop the
        /// garrison below the RESERVE floor — i.e. the world is already AT or UNDER its reserve. FALSE while a SURPLUS
        /// above the reserve remains. The developer's "don't ship your whole defense off." The optional
        /// <paramref name="factionEntity"/> lets the faction's posture size the reserve (see
        /// <see cref="GarrisonReserveFor"/>); null → the unscaled reserve (byte-identical).</summary>
        public static bool WouldStripReserve(GroundForcesDB forces, FactionInfoDB faction, int factionId, Entity factionEntity = null)
            => CurrentGarrison(forces, factionId) <= GarrisonReserveFor(faction, factionEntity);

        /// <summary>TRUE when loading a unit off <paramref name="body"/> would breach the home reserve (a body with no
        /// roster has nothing to ship → treated as "at reserve" = TRUE; the LOAD rung already finds no unit there). The
        /// optional <paramref name="factionEntity"/> lets the faction's posture size the reserve; null → byte-identical.</summary>
        public static bool WouldStripReserve(Entity body, FactionInfoDB faction, int factionId, Entity factionEntity = null)
            => CurrentGarrison(body, factionId) <= GarrisonReserveFor(faction, factionEntity);

        /// <summary>The planet BODY a colony sits on (its <see cref="ColonyInfoDB.PlanetEntity"/> — the
        /// <see cref="GroundForcesDB"/> host), or null.</summary>
        public static Entity GarrisonBodyOf(Entity colony)
            => colony != null && colony.TryGetDataBlob<ColonyInfoDB>(out var ci) && ci.PlanetEntity != null && ci.PlanetEntity.IsValid
               ? ci.PlanetEntity : null;

        /// <summary>A buildable GROUND UNIT design: a <see cref="GroundUnitDesign"/> (assembler-registered) OR a
        /// <see cref="ComponentDesign"/> carrying a <see cref="GroundUnitAtb"/> (the base-mod infantry/armor/artillery,
        /// which mount PlanetInstallation → build → auto-install raises a fielded unit). NOT a ground PART
        /// (locomotion/radar/magazine carry their own atbs, not <see cref="GroundUnitAtb"/>).</summary>
        public static bool IsBuildableGroundUnit(IConstructableDesign design)
        {
            if (design is GroundUnitDesign) return true;
            if (design is ComponentDesign cd && cd.HasAttribute<GroundUnitAtb>()) return true;
            return false;
        }
    }
}
