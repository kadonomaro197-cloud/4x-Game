using System;
using System.Collections.Generic;
using Pulsar4X.Damage;
using Pulsar4X.Engine;
using Pulsar4X.Events;
using Pulsar4X.Factions;

namespace Pulsar4X.Hazards
{
    /// <summary>
    /// The survey → discover → research connection. When a faction's ship is inside a hazard, this records the
    /// hazard's damage SIGNATURES into the faction's <see cref="FactionHazardKnowledgeDB"/>, and on a NEW discovery
    /// it (a) fires an <see cref="EventType.EnvironmentalHazardDiscovered"/> notification into the faction's log and
    /// (b) UNLOCKS the counter-research for that flavour (moves its tech from locked → researchable). The unlock is
    /// a safe no-op until that tech exists in data, so the wire is in place ahead of the JSON tech tree.
    /// </summary>
    public static class HazardDiscovery
    {
        // Discovery is rare (mostly a no-op after first contact) and is reached from per-system processors that may
        // run in parallel, so serialise the whole record-and-announce on one lock — negligible cost, and it removes
        // every race on the faction's knowledge set and on the get-or-create of the knowledge blob.
        private static readonly object _lock = new object();

        /// <summary>Record (and on first contact, announce + unlock research for) every damage flavour of the hazard
        /// the given ship is currently inside.</summary>
        public static void RecordAndAnnounce(Entity ship, SpaceHazardDB hazDb, DateTime now)
        {
            if (ship == null || hazDb?.Effects == null)
                return;
            int factionId = ship.FactionOwnerID;
            if (factionId == Game.NeutralFactionId)
                return;
            var game = ship.Manager?.Game;
            if (game == null || !game.Factions.TryGetValue(factionId, out var faction))
                return;

            List<DamageSignature> newlyDiscovered = null;
            lock (_lock)
            {
                if (!faction.TryGetDataBlob<FactionHazardKnowledgeDB>(out var knowledge))
                {
                    knowledge = new FactionHazardKnowledgeDB();
                    faction.SetDataBlob(knowledge);
                }

                foreach (var e in hazDb.Effects)
                {
                    if (e.Signature is DamageSignature sig && knowledge.Discover(sig))
                    {
                        (newlyDiscovered ??= new List<DamageSignature>()).Add(sig);

                        // Unlock the counter-research for this flavour. Safe no-op until the tech exists in data
                        // (FactionDataStore.Unlock does nothing for an id it doesn't hold locked).
                        var techId = CounterTechFor(sig);
                        if (techId != null && faction.TryGetDataBlob<FactionInfoDB>(out var fInfo))
                            fInfo.Data?.Unlock(techId);
                    }
                }
            }

            if (newlyDiscovered == null)
                return;
            foreach (var sig in newlyDiscovered)
            {
                EventManager.Instance.Publish(Event.Create(
                    EventType.EnvironmentalHazardDiscovered,
                    now,
                    $"Environmental hazard discovered: {Describe(sig)}. Counter-research is now available.",
                    factionId));
            }
        }

        /// <summary>The research-tech id that counters a given hazard signature (defined in the Stellar Science tech
        /// tree). Returns null for a signature with no counter-tech.</summary>
        public static string CounterTechFor(DamageSignature sig) => sig switch
        {
            DamageSignature.Thermal => "tech-thermal-shielding",
            DamageSignature.HardRadiation => "tech-radiation-shielding",
            DamageSignature.Kinetic => "tech-ablative-plating",
            DamageSignature.EMStorm => "tech-em-hardening",
            DamageSignature.Gravimetric => "tech-structural-reinforcement",
            DamageSignature.Corrosive => "tech-corrosion-plating",
            _ => null,
        };

        /// <summary>Plain-English name of a damage flavour, for the discovery notification.</summary>
        public static string Describe(DamageSignature sig) => sig switch
        {
            DamageSignature.Thermal => "thermal (extreme heat)",
            DamageSignature.HardRadiation => "hard radiation",
            DamageSignature.Kinetic => "kinetic debris",
            DamageSignature.EMStorm => "EM storm",
            DamageSignature.Gravimetric => "gravimetric stress",
            DamageSignature.Corrosive => "corrosive medium",
            _ => sig.ToString(),
        };
    }
}
