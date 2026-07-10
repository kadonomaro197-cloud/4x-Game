using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Combat
{
    /// <summary>
    /// Marks a fleet as ENGAGED in an auto-resolved battle, and carries that battle's running state for this
    /// fleet. Present on EVERY fleet taking part in an engagement; removed from a fleet when it leaves the fight
    /// (wiped, breaks off, or no enemy remains).
    ///
    /// Its presence is the "in combat" signal: while a fleet has this blob it is locked into the fight. The
    /// engagement lock (combat spine step 11) keys on it to block the fleet's regular orders — only doctrine
    /// changes apply. The battle trigger steps the fight a little each game-tick, so battles play out over
    /// time rather than instantly.
    ///
    /// MULTI-PARTY: an engagement can hold any number of fleets on either side, and fleets can join one in
    /// progress (see <see cref="Combat.CombatEngagement"/>). State here is PER-FLEET — each fleet carries its own
    /// damage pool, step count, and starting ship count; the side it fights on is its faction. There is no single
    /// "opponent": <see cref="OpponentFleetId"/> is only a representative enemy for the readout.
    /// </summary>
    public class FleetCombatStateDB : BaseDataBlob
    {
        /// <summary>Entity id of a REPRESENTATIVE enemy fleet (readout only). A multi-party engagement has many
        /// opponents; the resolver keeps this pointed at one live hostile fleet. -1 if none recorded yet.</summary>
        [JsonProperty] public int OpponentFleetId { get; internal set; } = -1;

        /// <summary>Accumulated, not-yet-lethal damage (joules) aimed at THIS fleet — spent on whole-ship kills by
        /// the bucketed resolve (most-hittable first). Carries between ticks so a weaker attacker still grinds
        /// kills over time. In a multi-party fight this is the combined, fire-divided damage from all hostiles.</summary>
        [JsonProperty] public double DamageTakenPool { get; internal set; }

        /// <summary>How many salvo steps this fleet has fought (readout + stalemate backstop).</summary>
        [JsonProperty] public int StepsFought { get; internal set; }

        /// <summary>Ship count when the engagement started — the denominator for the casualty-fraction retreat
        /// threshold (lose this fraction of your ships and you break off). Set at <c>StartEngagement</c>.</summary>
        [JsonProperty] public int InitialShipCount { get; internal set; }

        /// <summary>This fleet's remaining combat MANEUVER reserve (Δv, m/s) — Phase 2 (the kiting counter). Holding
        /// or dictating the range costs maneuvering, debited each step a fleet CONTROLS the gap. A fleet at 0 can no
        /// longer dictate the range — so a kiter that burns out stops being the controller and the enemy closes on it.
        /// Seeded from the fleet's Δv floor at engagement start. ONLY meaningful when <see cref="CombatEngagement.EnableClosingRange"/>
        /// is on. A separate combat-abstract reserve — it does NOT touch the ships' real fuel (v1 combat is math, not
        /// a maneuver sim); flagged for a future "real fuel burned in combat" pass.</summary>
        [JsonProperty] public double ManeuverBudget { get; internal set; }

        /// <summary>The current gap (metres) to the opposing side — the CLOSING range (Phase 1,
        /// docs/FLEET-COMBAT-CLOSING-DESIGN.md). Seeded from the real distance at first contact, then closed each step
        /// toward the controlling (faster) side's preferred range. A weapon only fires if its <see cref="WeaponProfile.Range_m"/>
        /// reaches this. ONLY meaningful when <see cref="CombatEngagement.EnableClosingRange"/> is on; 0 otherwise (which
        /// makes the range-gate a no-op, so the resolve is byte-identical to the pre-closing behaviour). v1: one shared
        /// range per engagement group (per-sub-fleet ranges are Phase 4).</summary>
        [JsonProperty] public double Separation_m { get; internal set; }

        /// <summary>The fleet's current SHIELD charge (joules) — the depleting/regenerating pool (option B,
        /// docs/WEAPON-TAXONOMY-DESIGN.md §6). Each salvo the resolver drains this BEFORE the hull's
        /// <see cref="DamageTakenPool"/>, with the weapon-NATURE matchup (Kinetic fully soaked, Energy half-bleeds,
        /// Explosive partly bypasses, Exotic anti-shield ignores it), then refills it toward the fleet's total generator
        /// capacity. <b>-1 = "not yet seeded"</b> — the resolver lazily fills it to full capacity on the first salvo the
        /// fleet takes; for an UNSHIELDED fleet (no generator → capacity 0) it seeds to 0 and the shield step is a no-op,
        /// so combat is byte-identical. v1: one aggregate pool for the whole fleet (per-ship shields are a later slice).</summary>
        [JsonProperty] public double ShieldPool_J { get; internal set; } = -1;

        /// <summary>The fleet's current AMMO charge (kg) — the depleting magazine pool (Weapons pilot W3, the ship echo
        /// of the ground <c>GroundAmmo</c>). Each salvo the fleet's AMMO-fed weapons (Kinetic/Explosive nature — railgun,
        /// flak, missiles) drain this; when it hits 0 those weapons go SILENT and the fleet fights on with its energy
        /// weapons only. <b>-1 = "not yet seeded"</b> — the resolver lazily fills it to the fleet's total magazine
        /// capacity on the first salvo; for a fleet with NO magazine (capacity 0) it stays disabled and the ammo step is
        /// a no-op, so combat is byte-identical. v1: one aggregate pool for the whole fleet (per-ship magazines are a
        /// later slice), matching the shield pool.</summary>
        [JsonProperty] public double AmmoPool_kg { get; internal set; } = -1;

        public FleetCombatStateDB() { }

        public FleetCombatStateDB(int opponentFleetId)
        {
            OpponentFleetId = opponentFleetId;
        }

        public FleetCombatStateDB(int opponentFleetId, int initialShipCount)
        {
            OpponentFleetId = opponentFleetId;
            InitialShipCount = initialShipCount;
        }

        public FleetCombatStateDB(FleetCombatStateDB db)
        {
            OpponentFleetId = db.OpponentFleetId;
            DamageTakenPool = db.DamageTakenPool;
            StepsFought = db.StepsFought;
            InitialShipCount = db.InitialShipCount;
            Separation_m = db.Separation_m;
            ManeuverBudget = db.ManeuverBudget;
            ShieldPool_J = db.ShieldPool_J;
            AmmoPool_kg = db.AmmoPool_kg;
        }

        public override object Clone()
        {
            return new FleetCombatStateDB(this);
        }
    }
}
