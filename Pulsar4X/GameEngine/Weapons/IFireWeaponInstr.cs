using Pulsar4X.Engine;
using System.Diagnostics.CodeAnalysis;

namespace Pulsar4X.Weapons
{
    public interface IFireWeaponInstr
    {
        public string WeaponType { get; }

        public void SetWeaponState(WeaponState state);

        public bool CanLoadOrdnance(OrdnanceDesign ordnanceDesign);
        public bool AssignOrdnance(OrdnanceDesign ordnanceDesign);

        public bool TryGetOrdnance([NotNullWhen(true)] out OrdnanceDesign? ordnanceDesign);
        public void FireWeapon(Entity launchingEntity, Entity tgtEntity, int count);

        public float ToHitChance(Entity launchingEntity, Entity tgtEntity);

        /// <summary>
        /// Returns true if the target is within this weapon's effective range.
        /// Default: always true (unlimited range, preserves legacy behaviour for weapon types
        /// that don't define a hard range limit).
        /// </summary>
        public bool IsInRange(Entity launchingEntity, Entity tgtEntity) => true;
    }
}