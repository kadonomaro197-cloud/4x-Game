using System;
using System.Collections.Generic;
using Pulsar4X.Colonies;
using Pulsar4X.Components;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.Movement;
using Pulsar4X.Orbits;
using Pulsar4X.Ships;

namespace Pulsar4X.Damage
{
    public struct DamageResult
    {
        public int Damage;
        public bool Destroyed;
    }

    internal static class DamageProcessor
    {
        public static void Initialize()
        {
        }

        static public void Process(Game game, StarSystem starSystem)
        {

        }

        /// <summary>
        /// This will work for missiles, ships, asteroids, and populations at some point.
        /// Damage type may eventually be required.
        /// </summary>
        /// <param name="damageableEntity"></param>
        /// <param name="damageAmount"></param>
        public static DamageResult OnTakingDamage(Entity damageableEntity, DamageFragment damageFragment)
        {

            if(!damageableEntity.TryGetDataBlob<EntityDamageProfileDB>(out var entityDamageProfileDB))
            {
                //I think currently most damageable entites should already have this,
                //need to consider whether an undamaged entity needs this or if we should create it if and when it gets damaged.

                if(damageableEntity.TryGetDataBlob<ShipInfoDB>(out var shipInfoDB))
                {
                    entityDamageProfileDB = new EntityDamageProfileDB(shipInfoDB.Design);
                    damageableEntity.SetDataBlob(entityDamageProfileDB);
                }
                else if (damageableEntity.HasDataBlob<ColonyInfoDB>())
                {
                    return OnColonyDamage(damageableEntity, damageFragment);
                }
                //return;
            }

            if(entityDamageProfileDB == null) return new DamageResult { Damage = 0, Destroyed = false };

            // Non-wavelength flavours (EMStorm / Gravimetric / Corrosive) aren't photons — they can't run through the
            // per-pixel wavelength sim, so they're applied flat to the hull here, reduced by the ship's armour-material
            // resistance to that flavour. Default-identical for every existing caller: a beam/missile/kinetic-hazard
            // fragment carries a wavelength-path signature (Kinetic is the struct default), so this never fires for them.
            if (!DamageSignatures.UsesWavelengthArmorPath(damageFragment.Signature))
                return ApplyNonWavelengthDamage(entityDamageProfileDB, damageFragment);

            var damages = DamageTools.DealDamageEnergyBeamSim(entityDamageProfileDB, damageFragment);

            // G-channel in the damage bitmap is 1-indexed (ComponentPlacement.CreateShipBmp starts
            // componentInstance at 0 and increments before painting the first component).
            // ComponentLookupTable is 0-indexed, so subtract 1 when using id as a list index.
            foreach (var damage in damages.damageToComponents)
            {
                int componentIdx = damage.id - 1;
                if (componentIdx >= 0 && componentIdx < entityDamageProfileDB.ComponentLookupTable.Count)
                    entityDamageProfileDB.ComponentLookupTable[componentIdx].HealthPercent -= damage.damageAmount * 0.001f;
            }

            if(damageableEntity.TryGetDataBlob<ComponentInstancesDB>(out var damagedComponentInstancesDB))
            {
                int totalDamage = 0;
                foreach (var damage in damages.damageToComponents)
                {
                    totalDamage += damage.damageAmount;
                    int componentIdx = damage.id - 1;
                    if (componentIdx >= 0 && componentIdx < entityDamageProfileDB.ComponentLookupTable.Count)
                    {
                        var profileInstance = entityDamageProfileDB.ComponentLookupTable[componentIdx];
                        if (profileInstance.HealthPercent <= 0 && profileInstance.Design != null)
                        {
                            var matchList = damagedComponentInstancesDB.GetComponentsBySpecificDesign(profileInstance.Design.UniqueID);
                            if (matchList.Count > 0)
                                damagedComponentInstancesDB.RemoveComponentInstance(matchList[0]);
                        }
                    }
                }

                if (damagedComponentInstancesDB.AllComponents.Count <= 0)
                {
                    if (damageableEntity.HasDataBlob<ShipInfoDB>())
                        ShipFactory.DestroyShip(damageableEntity);
                    else
                        damageableEntity.Destroy();
                    return new DamageResult { Damage = totalDamage, Destroyed = true };
                }
                return new DamageResult { Damage = totalDamage, Destroyed = false };
            }

            /*
            if (damageableEntity.HasDataBlob<AsteroidDamageDB>())
            {
                AsteroidDamageDB AstDmgDB = damageableEntity.GetDataBlob<AsteroidDamageDB>();
                AstDmgDB.Health = AstDmgDB.Health - damageAmount;

                if (AstDmgDB.Health <= 0)
                    SpawnSubAsteroids(damageableEntity, atDateTime);
            }
            else if (damageableEntity.HasDataBlob<ShipInfoDB>())
            {
                //do shield damage
                //do armor damage
                //for components:
                Game game = damageableEntity.Manager.Game;
                PositionDB ShipPosition = damageableEntity.GetDataBlob<PositionDB>();

                StarSystem mySystem;
                if (!game.Systems.TryGetValue(ShipPosition.SystemGuid, out mySystem))
                    throw new GuidNotFoundException(ShipPosition.SystemGuid);

                ComponentInstancesDB instancesDB = damageableEntity.GetDataBlob<ComponentInstancesDB>(); //These are ship components in this context

                int damageAttempt = 0;
                while (damageAmount > 0)
                {

                    int randValue = mySystem.RNGNext((int)(damageableEntity.GetDataBlob<MassVolumeDB>().Volume_m3)); //volume in m^3


                    if (damageAttempt == 20) // need to copy this to fully break out of the loop;
                        break;
                }

                if (damageAttempt == 20) // the ship is destroyed. how to mark it as such?
                {
                    SpawnWreck(damageableEntity);
                }
                else
                {
                    ReCalcProcessor.ReCalcAbilities(damageableEntity);
                }
            }
            else if (damageableEntity.HasDataBlob<ColonyInfoDB>())
            {
                //Think about how to unify this one and shipInfoDB if possible.
                //do Terraforming/Infra/Pop damage
                Game game = damageableEntity.Manager.Game;

                ColonyInfoDB ColIDB = damageableEntity.GetDataBlob<ColonyInfoDB>();
                SystemBodyInfoDB SysInfoDB = ColIDB.PlanetEntity.GetDataBlob<SystemBodyInfoDB>();

                PositionDB ColonyPosition = ColIDB.PlanetEntity.GetDataBlob<PositionDB>();

                StarSystem mySystem; //I need all of this to get to the rng.
                if (!game.Systems.TryGetValue(ColonyPosition.SystemGuid, out mySystem))
                    throw new GuidNotFoundException(ColonyPosition.SystemGuid);

                //How should damage work here?
                //quarter million dead per strength of nuclear attack? 1 radiation/1 dust per strength?
                //Same chance to destroy components as ship destruction?

                //I need damage type for these. Missiles/bombs(missile damage but no engine basically) will be the only thing that causes this damage.
                //ColIDB.Population
                //SysInfoDB.AtmosphericDust
                //SysInfoDB.RadiationLevel


                //Installation Damage section:
                ComponentInstancesDB ColInst = damageableEntity.GetDataBlob<ComponentInstancesDB>(); //These are installations in this context
                int damageAttempt = 0;
                while (damageAmount > 0)
                {
                    int randValue = mySystem.RNGNext((int)damageableEntity.GetDataBlob<MassVolumeDB>().Volume_km3);

                    foreach (KeyValuePair<Entity, double> pair in ColInst.ComponentDictionary)
                    {
                        if (pair.Value > randValue) //This installation was targeted
                        {

                            //check if this Installation is destroyed
                            //if it isn't get density
                            MassVolumeDB mvDB = pair.Key.GetDataBlob<MassVolumeDB>();

                            double DensityThreshold = 1.0; //what should this be?
                            double dmgPercent = DensityThreshold * mvDB.Density;

                            int dmgDone = (int)(damageAmount * dmgPercent);

                            ComponentInfoDB ciDB = pair.Key.GetDataBlob<ComponentInfoDB>();
                            ComponentInstanceData cii = pair.Key.GetDataBlob<ComponentInstanceData>();

                            if (cii.HTKRemaining > 0) //Installation is not destroyed yet
                            {
                                if (dmgDone >= cii.HTKRemaining) //Installation is definitely wrecked
                                {
                                    damageAmount = damageAmount - cii.HTKRemaining;
                                    cii.HTKRemaining = 0;
                                }
                                else
                                {
                                    cii.HTKRemaining = cii.HTKRemaining - damageAmount;
                                    damageAmount = 0;

                                }
                            }
                            else
                            {
                                damageAttempt++;
                                if (damageAttempt == 20) // The planet won't blow up because of this, but no more attempts to damage installations should be made here.
                                    break;

                                continue;

                            }
                        }
                    }
                    if (damageAttempt == 20) // need to copy this to fully break out of the loop;
                        break;
                }

                //This will need to be updated to deal with colonies.
                ReCalcProcessor.ReCalcAbilities(damageableEntity);
            }
            */

            return new DamageResult { Damage = 0, Destroyed = false };
        }

        /// <summary>
        /// The flat damage SITE for the non-wavelength flavours (EMStorm / Gravimetric / Corrosive). They aren't
        /// photons, so they can't drive the per-pixel wavelength armour sim; the hit is instead applied FLAT and
        /// spread evenly across the ship's components (a field effect, not a beam puncture), REDUCED by the ship's
        /// armour-material resistance to that flavour — the same <c>SignatureResistance</c> the wavelength path uses,
        /// so "the armour material IS the counter" holds for all six flavours. v1: even spread + health attrition.
        /// Per-flavour targeting (EM → electronics, gravimetric → structure scaled by hull size, corrosive → surface
        /// over time) and full destruction bookkeeping are flagged refinements, not built. See Hazards/CLAUDE.md.
        /// </summary>
        private static DamageResult ApplyNonWavelengthDamage(EntityDamageProfileDB profile, DamageFragment frag)
        {
            var components = profile?.ComponentLookupTable;
            if (components == null || components.Count == 0)
                return new DamageResult { Damage = 0, Destroyed = false };

            // Armour material's resistance to this flavour (0 = none … 1 = immune) — the researched-armour payoff.
            float resist = 0f;
            string armorId = profile.Armor.armorType?.ResourceID;
            if (!string.IsNullOrEmpty(armorId))
            {
                byte id = DamageTools.IDCodeForMaterial(armorId);
                if (DamageTools.DamageResistsLookupTable.TryGetValue(id, out var mat))
                    resist = DamageTools.GetSignatureResistance(mat, frag.Signature);
            }

            // Energy → damage points on the SAME scale as the beam sim (1 pt / 100 J), reduced by resistance.
            int damageAmount = Math.Max(1, (int)(frag.Energy * 0.01 * (1f - resist)));

            // Spread evenly across components. HealthPercent is the same field the wavelength path reduces
            // (1000 pts = 100% health), so the scale matches.
            float perComponentHealth = (damageAmount * 0.001f) / components.Count;
            foreach (var c in components)
                c.HealthPercent -= perComponentHealth;

            return new DamageResult { Damage = damageAmount, Destroyed = false };
        }

        /// <summary>
        /// Applies orbital bombardment damage to a colony: population casualties,
        /// atmospheric contamination, and randomized installation destruction.
        /// Called when a beam weapon or missile strikes a colony entity directly.
        /// </summary>
        private static DamageResult OnColonyDamage(Entity colonyEntity, DamageFragment damageFragment)
        {
            var colonyInfoDB = colonyEntity.GetDataBlob<ColonyInfoDB>();
            var starSystem = colonyEntity.Manager as StarSystem;
            if (starSystem == null)
                return new DamageResult { Damage = 0, Destroyed = false };

            // Scale energy to damage-strength units. 100 MJ per unit so a typical
            // missile warhead (1–100 TJ) yields 10,000–1,000,000 units.
            // Tuning: adjust the divisor when warhead energy values are finalized.
            int damageStrength = Math.Max(1, (int)(damageFragment.Energy / 1e8));

            // Population casualties: quarter million dead per damage unit.
            if (colonyInfoDB.Population.Count > 0)
            {
                long totalPop = 0;
                foreach (var pop in colonyInfoDB.Population.Values)
                    totalPop += pop;

                if (totalPop > 0)
                {
                    long casualties = Math.Min(damageStrength * 250_000L, totalPop);
                    var speciesIds = new List<int>(colonyInfoDB.Population.Keys);
                    foreach (int speciesId in speciesIds)
                    {
                        long share = (long)((double)colonyInfoDB.Population[speciesId] / totalPop * casualties);
                        colonyInfoDB.Population[speciesId] = Math.Max(0L, colonyInfoDB.Population[speciesId] - share);
                    }
                }
            }

            // Atmospheric contamination from explosions: raise dust and radiation.
            if (colonyInfoDB.PlanetEntity.TryGetDataBlob<SystemBodyInfoDB>(out var sysBodyInfo))
            {
                float contamination = damageStrength * 0.001f;
                sysBodyInfo.AtmosphericDust  = Math.Min(1.0f,  sysBodyInfo.AtmosphericDust  + contamination);
                sysBodyInfo.RadiationLevel   = Math.Min(10.0f, sysBodyInfo.RadiationLevel   + contamination);
            }

            // Installation damage: randomly pick and damage installations until
            // the damage budget is spent or all installations are destroyed.
            if (colonyEntity.TryGetDataBlob<ComponentInstancesDB>(out var installsDB))
            {
                var targets = new List<ComponentInstance>(installsDB.AllComponents.Values);
                int budget = damageStrength;
                int misses = 0;

                while (budget > 0 && targets.Count > 0 && misses < 20)
                {
                    int idx = starSystem.RNGNext(targets.Count);
                    var target = targets[idx];

                    if (target.HealthPercent > 0)
                    {
                        float drain = Math.Min(budget * 0.001f, target.HealthPercent);
                        target.HealthPercent -= drain;
                        budget -= Math.Max(1, (int)(drain * 1000));

                        if (target.HealthPercent <= 0)
                        {
                            installsDB.RemoveComponentInstance(target);
                            targets.RemoveAt(idx);
                        }
                    }
                    else
                    {
                        misses++;
                    }
                }
            }

            ReCalcProcessor.ReCalcAbilities(colonyEntity);
            return new DamageResult { Damage = damageStrength, Destroyed = false };
        }

        /// <summary>
        /// I want to delete the existing ship, and replace it with a wreck here that can be salvaged for materials and parts.
        /// </summary>
        /// <param name="DestroyedShip"></param>
        internal static void SpawnWreck(Entity DestroyedShip)
        {
            //create the wreck here



            //Destroy the ship.

            DestroyedShip.Manager.TagEntityForRemoval(DestroyedShip);

        }

        /// <summary>
        /// This asteroid was destroyed, see if it is big enough for child asteroids to spawn, and if so spawn them.
        /// </summary>
        /// <param name="Asteroid"></param>
        internal static void SpawnSubAsteroids(Entity Asteroid, DateTime atDateTime)
        {
            Game game = Asteroid.Manager.Game;
            MassVolumeDB ADB = Asteroid.GetDataBlob<MassVolumeDB>();

            //const double massDefault = 1.5e+12; //150 B tonnes?
            const double massThreshold = 1.5e+9; //150 M tonnes?
            if (ADB.MassDry > massThreshold)
            {
                //spawn new asteroids. call the asteroid factory?

                double newMass = ADB.MassDry * 0.4; //add a random factor into this? do we care? will mass be printed to the player?

                OrbitDB origOrbit = Asteroid.GetDataBlob<OrbitDB>();
                PositionDB pDB = Asteroid.GetDataBlob<PositionDB>();

                EntityManager mySystem = Asteroid.Manager;


                var origVel = origOrbit.AbsoluteOrbitalVector_m(atDateTime);

                //public static Entity CreateAsteroid(StarSystem starSys, Entity target, DateTime collisionDate, double asteroidMass = -1.0)
                //I need the target entity, the collisionDate, and the starSystem. I may have starsystem from guid.
                //Ok so this should create the asteroid without having to add the new asteroids to a list. as that is done in the factory.
                Entity newAsteroid1 = AsteroidFactory.CreateAsteroid4(pDB.AbsolutePosition, origOrbit, atDateTime,mySystem.RNG, newMass);
                //var newOrbit = OrbitDB.FromVector(origOrbit.Parent, )
                Entity newAsteroid2 = AsteroidFactory.CreateAsteroid4(pDB.AbsolutePosition, origOrbit, atDateTime,mySystem.RNG, newMass);

                mySystem.TagEntityForRemoval(Asteroid);

                //Randomize the number of created asteroids?
            }
            else
            {
                //delete the existing asteroid.
                PositionDB pDB = Asteroid.GetDataBlob<PositionDB>();

                Asteroid.Manager.TagEntityForRemoval(Asteroid);
            }
        }
    }
}
