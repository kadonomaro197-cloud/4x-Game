using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Galaxy;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// DS-AI-HOOK — the AI rung that keeps ore FLOWING on the ground (OPERATION BLUEPRINT-TO-STEEL tail DS-AI-hook).
    /// The standing haul route (DS-T1, <see cref="GroundHaulRoutes"/>) is a player button; this makes the NPC brain
    /// press the SAME button. When per-hex mining (R1b) has piled ore on a mining hex, that ore SITS there until it is
    /// hauled — so a faction with a mine on one hex and its factories on another needs a convoy. This rung notices a
    /// hex holding a real surplus of a good and sets a STANDING haul route consolidating it toward the faction's most
    /// built-up ("depot") hex, via the EXACT SAME <see cref="GroundHaulRoutes.AddRoute"/> a player would click — One
    /// Verb, Both Seats.
    ///
    /// <para><b>Honest framing (no over-claim).</b> There is NO per-hex CONSUMER model in the engine yet (a hex has a
    /// stockpile but nothing on it "runs low"), so this is not literally "feed a starving factory" — it is
    /// <b>surplus consolidation</b>: move mined ore off the scattered mining hexes to where the infrastructure is.
    /// That is the honest v1 of the same intent, and it is INERT in a stock game — nothing fills a hex bucket until the
    /// flag-gated per-hex mining (R1b) is on, so with no ore on any hex this rung returns null and a default game is
    /// byte-identical.</para>
    ///
    /// <para><b>Byte-identical + safe.</b> <see cref="EnableGroundHaulAI"/> defaults OFF (the resolver rung is skipped),
    /// so the existing <c>GrowEconomyResolverTests</c> are unchanged; the whole decision is pure/read-only (the
    /// <see cref="PlannerAction"/> closure is what mutates, and only when the processor runs it — same contract as
    /// every other resolver rung); and it is <b>idempotent</b> — it never stacks a route that already exists
    /// (<see cref="GroundHaulRoutes.AddRoute"/> appends unconditionally, so the caller MUST check first). Defensive
    /// throughout — a null grid / off-grid hex / missing blob is a quiet skip, never a throw (this runs inside the
    /// monthly Tick, landmine L4). Mirrors the read-only-decision style of <see cref="GroundReinforcement"/>.
    /// Design: docs/ground/UNITS-ON-THE-MAP-DESIGN.md §3.</para>
    /// </summary>
    public static class GroundHaulAI
    {
        /// <summary>Master gate. Default OFF → the resolver rung is skipped → byte-identical. Flipped ON by
        /// <c>NewGameMenu</c> for a menu game (both start paths), the A1/EnableOrderEmission pattern.</summary>
        public static bool EnableGroundHaulAI = false;

        /// <summary>Minimum units of a good on a hex before the AI bothers routing it — skip dribbles so a single tick's
        /// mining output doesn't spawn a route. FLAGGED tunable.</summary>
        public const long MinHaulSurplus = 100;

        /// <summary>
        /// Try to set ONE standing haul route consolidating a surplus of some good toward the faction's most built-up
        /// hex, on the first of its colony bodies that has one to move. Returns the <see cref="PlannerAction"/> the
        /// resolver runs (its closure calls <see cref="GroundHaulRoutes.AddRoute"/>), or null when there is nothing to
        /// do (flag off, no colony body, no infrastructure hex, no surplus, or every candidate route already exists —
        /// the idempotency guard). Pure read-only decision; never throws.
        /// </summary>
        public static PlannerAction TryConsolidateOre(FactionState state)
        {
            if (!EnableGroundHaulAI || state == null) return null;
            int factionId = state.FactionId;

            foreach (var colony in state.Colonies)
            {
                var body = GroundReinforcement.GarrisonBodyOf(colony?.Colony);
                if (body == null) continue;   // a station (no ColonyInfoDB) or a colony off a body → skip

                SurfaceGrid grid;
                try { grid = PlanetGridFactory.EnsureGridForBody(body); }
                catch { continue; }
                if (grid?.Hexes == null || grid.Hexes.Count == 0) continue;

                // The DEPOT: this faction's most built-up hex on the body (most footprint buildings) — where mined ore
                // wants to end up. Skip any hex a DIFFERENT faction holds (don't consolidate onto contested ground).
                GroundHex depot = null;
                int bestBuildings = 0;
                foreach (var hex in grid.Hexes)
                {
                    if (hex == null || IsEnemyHeld(hex, factionId)) continue;
                    int buildings = hex.InstallationIds?.Count ?? 0;
                    if (buildings > bestBuildings) { bestBuildings = buildings; depot = hex; }
                }
                if (depot == null) continue;   // no infrastructure hex to consolidate toward → nothing to do here

                // A SOURCE: any OTHER non-enemy hex holding a real surplus of a good not already routed to the depot.
                foreach (var hex in grid.Hexes)
                {
                    if (hex == null || ReferenceEquals(hex, depot)) continue;
                    if (hex.Q == depot.Q && hex.R == depot.R) continue;   // same location (defensive)
                    if (IsEnemyHeld(hex, factionId)) continue;
                    if (hex.Stockpile == null || hex.Stockpile.Count == 0) continue;

                    foreach (var kv in hex.Stockpile)
                    {
                        if (kv.Value < MinHaulSurplus) continue;
                        int goodId = kv.Key;
                        if (RouteExists(body, hex.Q, hex.R, depot.Q, depot.R, goodId)) continue;   // idempotent

                        // Capture for the closure (the processor runs it; the resolver stays a pure decision).
                        int srcQ = hex.Q, srcR = hex.R, dstQ = depot.Q, dstR = depot.R;
                        var haulBody = body;
                        return new PlannerAction(
                            "HaulOre",
                            $"set standing haul route ({srcQ},{srcR})->({dstQ},{dstR}) of good {goodId} on body {haulBody.Id}",
                            () => GroundHaulRoutes.AddRoute(haulBody, factionId, srcQ, srcR, dstQ, dstR, goodId, 0));
                    }
                }
            }

            return null;
        }

        /// <summary>A hex a DIFFERENT (real, non-neutral) faction holds. An uncontested hex (<c>OwnerFactionID</c> &lt; 0)
        /// or our own is NOT enemy-held. (Uncontested is -1 and neutral is negative, so the <c>&gt;= 0</c> test excludes
        /// both — no explicit neutral-id check needed.)</summary>
        private static bool IsEnemyHeld(GroundHex hex, int factionId)
            => hex.OwnerFactionID >= 0 && hex.OwnerFactionID != factionId;

        /// <summary>TRUE when <paramref name="body"/> already carries a standing route with the same source, destination
        /// and good — the idempotency guard that stops this rung stacking duplicate routes every cycle
        /// (<see cref="GroundHaulRoutes.AddRoute"/> appends unconditionally). False when the body has no route roster.</summary>
        private static bool RouteExists(Entity body, int srcQ, int srcR, int dstQ, int dstR, int goodId)
        {
            if (!body.TryGetDataBlob<GroundHaulRouteDB>(out var db) || db.Routes == null) return false;
            foreach (var r in db.Routes)
                if (r.SrcQ == srcQ && r.SrcR == srcR && r.DstQ == dstQ && r.DstR == dstR && r.CargoableId == goodId)
                    return true;
            return false;
        }
    }
}
