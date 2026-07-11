using System.Collections.Generic;
using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// The 12-trait personality model (docs/AI-COMMAND-AND-COMMUNICATION-DESIGN.md §3a). Each trait is 0..1 with 0.5
    /// neutral; a faction's traits filter every scored decision its brain makes (retreat nerve, treaty tolerance,
    /// first-contact stance, expansion drive, willingness to bombard civilians, and so on).
    /// </summary>
    public enum PersonalityTrait
    {
        /// <summary>Individual↔collective: low flees to save the unit, high fights to the last for the whole.</summary>
        Collectivism,
        /// <summary>Ideological fervour: high won't retreat, refuses deals with the impure, keeps taboos.</summary>
        Zealotry,
        /// <summary>Keeps faith: high honours treaties even when betrayal would pay; low reneges freely.</summary>
        Honor,
        /// <summary>Distrust of outsiders: high opens hostile on first contact and demands more to sign.</summary>
        Xenophobia,
        /// <summary>Drive to grow: high expands aggressively and reaches for its grand aim.</summary>
        Ambition,
        /// <summary>Willingness to do harm: high bombards civilians, purges, uses terror.</summary>
        Ruthlessness,
        /// <summary>Rule by force vs consent: high taxes hard and suppresses; low appeases.</summary>
        Authoritarianism,
        /// <summary>Bias toward attack: high runs Weapons-Free and picks fights.</summary>
        Aggression,
        /// <summary>Risk appetite: high engages at parity; low demands overwhelming odds.</summary>
        Risk,
        /// <summary>Preference for the covert: high spies, feints, and fights dark.</summary>
        Guile,
        /// <summary>Generosity: high gives aid and honours the spirit of a deal.</summary>
        Altruism,
        /// <summary>Drive to explore and learn: high surveys, investigates anomalies, researches.</summary>
        Curiosity,
    }

    /// <summary>
    /// M2-0a (docs/AI-BRAIN-BUILD-TRACKER.md, Movement II — the Organism brain): the faction PERSONALITY blob — the
    /// authored identity (the 12 <see cref="PersonalityTrait"/>s) that every scored decision reads. This slice is the
    /// DATA MODEL + reader only: a new blob NOT yet attached to factions or read by anything → byte-identical. Each
    /// trait wire (M2-1a onward) reads a trait through a formula that is a NO-OP at the 0.5 <see cref="Neutral"/>, so
    /// a fresh (all-neutral) faction behaves exactly as today; authored NPCs (and the player's own faction) set trait
    /// values and diverge.
    /// </summary>
    public class PersonalityDB : BaseDataBlob
    {
        /// <summary>The middle of every trait's 0..1 range — the value that leaves behaviour unchanged (byte-identical).</summary>
        public const double Neutral = 0.5;

        /// <summary>trait → value (0..1). Absent = <see cref="Neutral"/>.</summary>
        [JsonProperty]
        public Dictionary<PersonalityTrait, double> Traits { get; internal set; } = new();

        public PersonalityDB() { }

        public PersonalityDB(PersonalityDB other)
        {
            Traits = new Dictionary<PersonalityTrait, double>(other.Traits);
        }

        public override object Clone() => new PersonalityDB(this);

        /// <summary>This faction's value for a trait (0..1), or <see cref="Neutral"/> if unset.</summary>
        public double TraitOf(PersonalityTrait trait) => Traits.TryGetValue(trait, out var v) ? Clamp01(v) : Neutral;

        /// <summary>Set a trait, clamped to 0..1.</summary>
        public void SetTrait(PersonalityTrait trait, double value) => Traits[trait] = Clamp01(value);

        private static double Clamp01(double v) => v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);
    }
}
