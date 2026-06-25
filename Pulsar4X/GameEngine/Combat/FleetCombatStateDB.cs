using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Combat
{
    /// <summary>
    /// Marks a fleet as ENGAGED in an auto-resolved battle, and carries that battle's running state for this
    /// fleet. Present on BOTH fleets of an engagement; removed from both when the battle ends.
    ///
    /// Its presence is the "in combat" signal: while a fleet has this blob it is locked into the fight. The
    /// engagement lock (combat spine step 11) keys on it to block the fleet's regular orders — only doctrine
    /// changes apply. The battle trigger steps the fight a little each game-tick, so battles play out over
    /// time rather than instantly.
    /// </summary>
    public class FleetCombatStateDB : BaseDataBlob
    {
        /// <summary>Entity id of the fleet this fleet is fighting.</summary>
        [JsonProperty] public int OpponentFleetId { get; internal set; } = -1;

        /// <summary>Accumulated, not-yet-lethal damage (joules) aimed at THIS fleet's lead ship. Carries
        /// between ticks so a weaker attacker still grinds kills over time.</summary>
        [JsonProperty] public double DamageTakenPool { get; internal set; }

        /// <summary>How many salvo steps this fleet has fought (readout + stalemate backstop).</summary>
        [JsonProperty] public int StepsFought { get; internal set; }

        public FleetCombatStateDB() { }

        public FleetCombatStateDB(int opponentFleetId)
        {
            OpponentFleetId = opponentFleetId;
        }

        public FleetCombatStateDB(FleetCombatStateDB db)
        {
            OpponentFleetId = db.OpponentFleetId;
            DamageTakenPool = db.DamageTakenPool;
            StepsFought = db.StepsFought;
        }

        public override object Clone()
        {
            return new FleetCombatStateDB(this);
        }
    }
}
