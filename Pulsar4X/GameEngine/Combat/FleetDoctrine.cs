using System;
using Pulsar4X.Blueprints;
using Pulsar4X.Engine;

namespace Pulsar4X.Combat
{
    /// <summary>
    /// Helpers to read and set a fleet's active combat doctrine (<see cref="FleetDoctrineDB"/>). The catalog of
    /// selectable postures is <c>ModDataStore.CombatDoctrines</c> (loaded from combatDoctrines.json). The
    /// auto-resolver (<see cref="CombatEngagement"/>) reads <see cref="FirepowerMult"/>/<see cref="ToughnessMult"/>
    /// to scale a fleet's effective strength.
    /// </summary>
    public static class FleetDoctrine
    {
        /// <summary>This fleet's firepower multiplier from its active doctrine (1.0 if it has none).</summary>
        public static double FirepowerMult(Entity fleet)
            => fleet != null && fleet.TryGetDataBlob<FleetDoctrineDB>(out var d) ? d.FirepowerMult : 1.0;

        /// <summary>This fleet's toughness multiplier from its active doctrine (1.0 if it has none).</summary>
        public static double ToughnessMult(Entity fleet)
            => fleet != null && fleet.TryGetDataBlob<FleetDoctrineDB>(out var d) ? d.ToughnessMult : 1.0;

        /// <summary>True if this fleet's active doctrine is a withdraw/disengage posture.</summary>
        public static bool IsRetreat(Entity fleet)
            => fleet != null && fleet.TryGetDataBlob<FleetDoctrineDB>(out var d) && d.IsRetreat;

        /// <summary>This fleet's weapons-release posture (Phase 3). WeaponsFree if it has no doctrine — fight on contact.</summary>
        public static EngagementPosture PostureOf(Entity fleet)
            => fleet != null && fleet.TryGetDataBlob<FleetDoctrineDB>(out var d) ? d.Posture : EngagementPosture.WeaponsFree;

        /// <summary>Set a fleet's weapons-release posture — a DIRECT call (like the doctrine + EMCON setters), so it
        /// works mid-battle. Mutates the existing doctrine blob in place (preserving its multipliers) or creates a
        /// neutral one carrying just the posture.</summary>
        public static void SetEngagementPosture(Entity fleet, EngagementPosture posture)
        {
            if (fleet == null) return;
            if (fleet.TryGetDataBlob<FleetDoctrineDB>(out var d))
                d.Posture = posture;
            else
                fleet.SetDataBlob(new FleetDoctrineDB { Posture = posture });
        }

        /// <summary>
        /// Set a fleet's posture from a catalog blueprint, honouring the switch cooldown. Returns false (no
        /// change) if the fleet is still within its cooldown window. <paramref name="now"/> is the current game time.
        /// </summary>
        public static bool TrySetDoctrine(Entity fleet, CombatDoctrineBlueprint doctrine, DateTime now)
        {
            if (fleet == null || doctrine == null) return false;
            bool hadDoctrine = fleet.TryGetDataBlob<FleetDoctrineDB>(out var existing);
            if (hadDoctrine && now < existing.SwitchableAfter)
                return false; // still on cooldown

            var db = new FleetDoctrineDB
            {
                DoctrineId = doctrine.UniqueID,
                Family = doctrine.Family,
                FirepowerMult = doctrine.FirepowerMult,
                ToughnessMult = doctrine.ToughnessMult,
                SpeedMult = doctrine.SpeedMult,
                IsRetreat = doctrine.IsRetreat,
                // Preserve the weapons-release posture across a doctrine switch (they're separate ROE knobs; Phase 5
                // unifies them). Without this, changing doctrine would silently reset a fleet to WeaponsFree.
                Posture = hadDoctrine ? existing.Posture : EngagementPosture.WeaponsFree,
                SwitchableAfter = now + TimeSpan.FromSeconds(doctrine.CooldownSeconds),
            };
            fleet.SetDataBlob(db);
            return true;
        }
    }
}
