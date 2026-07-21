using System;
using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Factions;   // FactionInfoDB (the authored garrison composition → the battalion cap)

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// FORM-UP — the ground echo of <c>Fleets.FleetAssembly</c> (assemble loose built warships into ONE fleet), applied
    /// to a planet's LOOSE ground units. Fixes R2 gap 3 (the AI NEVER forms): before this, only the player UI called
    /// <see cref="GroundForces.CreateFormation"/> — a raised garrison and a landed invader fielded as scattered,
    /// unformed units, so the ground tactical brain had nothing to command. <see cref="FormUpLoose"/> sweeps a faction's
    /// formation-less units on a body into one (or more) BATTALIONS, the way the fleet assembler folds loose warships
    /// into a task force.
    ///
    /// Pure engine helper — callable directly by the player UI, the AI's <c>Factions.ConquerResolver</c> (CORE wires it
    /// in at the invasion-arm kickoff — see the lane notes), and the two GROUND-owned sites it wires itself at:
    /// <see cref="GroundStartGarrison.RaiseHomeGarrison"/> (garrison raise) and <see cref="GroundTransport.TryLandUnit"/>
    /// (invasion landing). Those two AUTO sites are gated behind <see cref="AutoFormUp"/> (default OFF), so a default game
    /// is byte-identical (the <c>AutoRaiseHomeGarrison</c>/<c>InterruptTimeOnNewBattle</c> pattern) until the New-Game
    /// path / the ground-tactical-AI wiring flips it on. <see cref="FormUpLoose"/> ITSELF is always active — the gate is
    /// only on the auto-invocation, never on the helper.
    /// </summary>
    public static class GroundAssembly
    {
        /// <summary>Master gate for AUTO forming-up freshly-RAISED garrisons and freshly-LANDED invaders into battalions
        /// at the two GROUND-owned sites (<see cref="GroundStartGarrison.RaiseHomeGarrison"/> +
        /// <see cref="GroundTransport.TryLandUnit"/>). Default OFF → those two paths behave exactly as before
        /// (byte-identical); flipped ON by the New-Game path / the ground-tactical-AI wiring so the AI fields BATTALIONS
        /// rather than loose units. <see cref="FormUpLoose"/> called directly (player UI / resolver / tests) is unaffected
        /// by this flag.</summary>
        public static bool AutoFormUp = false;

        /// <summary>Fallback battalion size cap when a faction can't be resolved / authors no garrison mix — the engine
        /// default garrison size. FLAGGED balance value (a battalion holds one authored-garrison's worth of units).</summary>
        public const int DefaultBattalionCap = 6;   // FLAGGED balance value

        /// <summary>
        /// Sweep <paramref name="factionId"/>'s formation-less ground units standing on <paramref name="body"/> into one
        /// or more BATTALIONS (<see cref="GroundForces.CreateFormation"/> + <see cref="GroundForces.AssignUnit"/>). Splits
        /// into multiple battalions when the loose count exceeds the size cap (the faction's authored garrison size, off
        /// <see cref="FactionInfoDB.GarrisonComposition"/> via <c>GroundReinforcement.GarrisonTargetFor</c> — FLAGGED via
        /// <see cref="DefaultBattalionCap"/> when the faction authors none). Sweeps in a DETERMINISTIC order (ascending
        /// <see cref="GroundUnit.UnitId"/>) so a save reloads identically and a test is stable. Returns the battalion(s)
        /// created (empty when there are no loose units — a garrison-less/already-formed body is a no-op). Never throws
        /// (runs on the raise/land paths, L4).
        /// </summary>
        /// <param name="name">The battalion's base name; a single battalion takes it verbatim, multiple are numbered
        /// ("Home Guard 1", "Home Guard 2"). Null/blank → "Battalion".</param>
        public static List<GroundFormation> FormUpLoose(Entity body, int factionId, string name = null)
        {
            var formed = new List<GroundFormation>();
            if (body == null) return formed;
            try
            {
                if (!body.TryGetDataBlob<GroundForcesDB>(out var forces) || forces.Units == null) return formed;

                // Deterministic sweep: this faction's UNFORMED units, ordered by stable UnitId (the ground echo of a
                // ship's entity id — monotonic at raise, so the order is save-stable regardless of list mutation).
                var loose = new List<GroundUnit>();
                foreach (var u in forces.Units)
                    if (u != null && u.FactionOwnerID == factionId && u.FormationId < 0)
                        loose.Add(u);
                if (loose.Count == 0) return formed;   // nothing to form (already-formed / garrison-less) → no-op
                loose.Sort((a, b) => a.UnitId.CompareTo(b.UnitId));

                int cap = BattalionCapFor(body, factionId);
                if (cap < 1) cap = 1;

                int chunkCount = (loose.Count + cap - 1) / cap;   // ceil — how many battalions
                string baseName = string.IsNullOrWhiteSpace(name) ? "Battalion" : name.Trim();

                int battalion = 0;
                for (int i = 0; i < loose.Count; i += cap)
                {
                    battalion++;
                    string fname = chunkCount > 1 ? $"{baseName} {battalion}" : baseName;
                    var formation = GroundForces.CreateFormation(body, factionId, fname);
                    int end = Math.Min(i + cap, loose.Count);
                    for (int j = i; j < end; j++)
                        GroundForces.AssignUnit(formation, loose[j]);   // first unit in becomes the leader (flagship echo)
                    formed.Add(formation);
                }
            }
            catch { /* forming up is a convenience — never break the raise/land path over it (L4) */ }
            return formed;
        }

        /// <summary>The battalion size cap for a faction on this body — its authored garrison size (the same per-faction
        /// mix the start garrison raises), or <see cref="DefaultBattalionCap"/> when the faction can't be resolved or
        /// authors none. Reads only; never throws.</summary>
        private static int BattalionCapFor(Entity body, int factionId)
        {
            var fi = ResolveFactionInfo(body, factionId);
            if (fi != null)
            {
                int target = GroundReinforcement.GarrisonTargetFor(fi);   // Σ of the authored composition (else engine default)
                if (target > 0) return target;
            }
            return DefaultBattalionCap;
        }

        /// <summary>Resolve <paramref name="factionId"/> → its <see cref="FactionInfoDB"/> via the body's game, or null
        /// (a bare test body with no game link, or an unknown faction). Same lookup the radar reveal uses.</summary>
        private static FactionInfoDB ResolveFactionInfo(Entity body, int factionId)
        {
            var game = body?.Manager?.Game;
            if (game?.Factions != null && game.Factions.TryGetValue(factionId, out var facEnt)
                && facEnt != null && facEnt.TryGetDataBlob<FactionInfoDB>(out var fi))
                return fi;
            return null;
        }
    }
}
