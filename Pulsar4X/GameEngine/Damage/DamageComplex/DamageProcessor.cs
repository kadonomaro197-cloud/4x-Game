using System;
using System.Collections.Generic;
using Pulsar4X.Colonies;
using Pulsar4X.Components;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.GroundCombat;
using Pulsar4X.Movement;
using Pulsar4X.Orbits;
using Pulsar4X.Ships;
using Pulsar4X.Stations;

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
                else if (damageableEntity.HasDataBlob<StationInfoDB>())
                {
                    // A station is the "cheap to kill" host — it routes to its OWN bombardment path (population +
                    // module damage like a colony, PLUS a structural-integrity kill trigger a planet doesn't have).
                    return OnStationDamage(damageableEntity, damageFragment);
                }
                //return;
            }

            if(entityDamageProfileDB == null) return new DamageResult { Damage = 0, Destroyed = false };

            // Non-wavelength flavours (EMStorm / Gravimetric / Corrosive) aren't photons — they can't run through the
            // per-pixel wavelength sim, so they're applied flat to the hull here, reduced by the ship's armour-material
            // resistance to that flavour. Default-identical for every existing caller: a beam/missile/kinetic-hazard
            // fragment carries a wavelength-path signature (Kinetic is the struct default), so this never fires for them.
            if (!DamageSignatures.UsesWavelengthArmorPath(damageFragment.Signature))
                return ApplyNonWavelengthDamage(damageableEntity, entityDamageProfileDB, damageFragment);

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
        private static DamageResult ApplyNonWavelengthDamage(Entity ship, EntityDamageProfileDB profile, DamageFragment frag)
        {
            // Armour material's resistance to this flavour (0 = none … 1 = immune) — the researched-armour payoff.
            // Read off the damage profile's armour (the same place the wavelength sim reads the material).
            float resist = 0f;
            string armorId = profile?.Armor.armorType?.ResourceID;
            if (!string.IsNullOrEmpty(armorId))
            {
                byte id = DamageTools.IDCodeForMaterial(armorId);
                if (DamageTools.DamageResistsLookupTable.TryGetValue(id, out var mat))
                    resist = DamageTools.GetSignatureResistance(mat, frag.Signature);
            }

            // Energy → damage points on the SAME scale as the beam sim (1 pt / 100 J), reduced by resistance.
            // Computed independently of any component list so the hit always registers (the bitmap lookup table
            // can be empty on a lazily-built profile — the real components live in ComponentInstancesDB).
            int damageAmount = Math.Max(1, (int)(frag.Energy * 0.01 * (1f - resist)));

            // Spread evenly across the ship's REAL components (the always-populated store the colony path uses).
            // HealthPercent is the same field the wavelength path reduces (1000 pts = 100% health).
            if (ship != null && ship.TryGetDataBlob<ComponentInstancesDB>(out var instances) && instances.AllComponents.Count > 0)
            {
                float perComponentHealth = (damageAmount * 0.001f) / instances.AllComponents.Count;
                foreach (var c in instances.AllComponents.Values)
                    c.HealthPercent -= perComponentHealth;
            }

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

            // Population casualties: quarter million dead per damage unit (shared with the station bombardment path).
            ApplyPopulationCasualties(colonyInfoDB.Population, damageStrength);

            // Atmospheric contamination from explosions: raise dust and radiation. (Planet-only — a station has no
            // atmosphere to poison, which is why OnStationDamage skips this step.)
            if (colonyInfoDB.PlanetEntity.TryGetDataBlob<SystemBodyInfoDB>(out var sysBodyInfo))
            {
                float contamination = damageStrength * 0.001f;
                sysBodyInfo.AtmosphericDust  = Math.Min(1.0f,  sysBodyInfo.AtmosphericDust  + contamination);
                sysBodyInfo.RadiationLevel   = Math.Min(10.0f, sysBodyInfo.RadiationLevel   + contamination);
            }

            // Installation damage (shared with the station bombardment path).
            ApplyInstallationDamage(colonyEntity, starSystem, damageStrength);

            // Orbital bombardment ALSO softens the planet's DEFENDING GROUND GARRISON — the "soften them from orbit
            // before you land" step the MVP's "you can take a planet" needs (docs/MVP.md). Area effect on the colony
            // owner's units; defensive + never throws (no garrison → no-op).
            ApplyGroundBombardment(colonyInfoDB.PlanetEntity, colonyEntity.FactionOwnerID, damageStrength);

            ReCalcProcessor.ReCalcAbilities(colonyEntity);
            return new DamageResult { Damage = damageStrength, Destroyed = false };
        }

        /// <summary>Ground-unit health drained PER defending unit PER unit of colony damage-strength by orbital
        /// bombardment — the "soften the garrison before you land" conversion. Area effect: every defending unit takes
        /// this × damageStrength. FLAGGED PLACEHOLDER, tied to the same unfinalized warhead-energy calibration as the
        /// colony energy divisor (Damage/CLAUDE.md gotcha #2): on the 100 MJ/strength scale a ship's beam (strength ~1)
        /// barely scratches a garrison, a missile/heavy strike genuinely softens or devastates it. Tune when warhead
        /// energies + ground-unit HP lock.</summary>
        public const double GroundBombardmentDamagePerStrength = 0.01;

        /// <summary>
        /// Apply orbital bombardment to the planet's DEFENDING ground garrison (the colony owner's units) — the space
        /// combat → ground combat connection the "take a planet" milestone needs. Area effect: each defending unit on
        /// the body takes <see cref="GroundBombardmentDamagePerStrength"/> × <paramref name="damageStrength"/> raw
        /// health, REDUCED by the unit's own defences through the same <see cref="GroundCombat.GroundDamageMatrix"/> the
        /// ground resolver uses — an orbital strike is an undodgeable AREA attack (dodge doesn't help), but a SHIELD
        /// soaks a fraction and FLAT ARMOUR bounces a little off the top. So a shielded/armoured garrison genuinely
        /// resists softening — "build to survive the bombardment" is a real decision. Units driven to 0 are removed (a
        /// stale-leader formation is fixed by the next GroundForcesProcessor tick, like any combat casualty). Defensive:
        /// no body / no garrison / no defenders → a clean no-op, so a colony with no ground forces (every current
        /// colony-damage test) is byte-identical. Never throws.
        ///
        /// v1 scope (flagged): whole-surface (not region-targeted); softens only the DEFENDER (the colony owner) —
        /// friendly-fire on a landed invader's own troops is a v2 targeting nuance; bombardment is treated as an
        /// <c>Artillery</c>-class (area) attack for the matchup.
        /// </summary>
        private static void ApplyGroundBombardment(Entity planetBody, int defenderFactionId, int damageStrength)
        {
            if (planetBody == null || !planetBody.TryGetDataBlob<GroundForcesDB>(out var forces)) return;
            if (forces.Units.Count == 0) return;

            double raw = damageStrength * GroundBombardmentDamagePerStrength;
            if (raw <= 0) return;

            List<GroundUnit> dead = null;
            foreach (var u in forces.Units)
            {
                if (u.FactionOwnerID != defenderFactionId) continue;   // soften the DEFENDER, not a landed invader
                // Orbital strike = an undodgeable AREA attack: the matchup zeroes dodge (area) and lets a shield soak a
                // fraction; flat armour then bounces a little off the (single, big) source. Mirrors ResolveRegionCombat.
                double landed = GroundCombat.GroundDamageMatrix.ArmourSoak(
                    u.Defense, raw * GroundCombat.GroundDamageMatrix.Matchup(GroundCombat.GroundWeaponMode.Artillery, u));
                u.Health -= landed;
                if (u.Health <= 0)
                    (dead ??= new List<GroundUnit>()).Add(u);
            }
            if (dead != null)
                foreach (var d in dead) forces.Units.Remove(d);
        }

        /// <summary>
        /// Orbital bombardment of a STATION — the parallel to <see cref="OnColonyDamage"/>. Reached when a weapon
        /// strikes a station entity (<see cref="Pulsar4X.Stations.StationInfoDB"/>) directly. It shares the colony's
        /// population-casualty and module-damage passes, but differs in the two ways that make a station the cheap,
        /// fragile alternative to a planet (docs/SPACE-STATIONS-DESIGN.md):
        ///  • NO atmospheric contamination (a sealed habitat has no atmosphere to poison), and
        ///  • a STRUCTURAL-INTEGRITY kill trigger — unlike a colony (which this path never destroys), a station is
        ///    DESTROYED once its <see cref="StationInfoDB.StructuralIntegrity"/> pool is exhausted. That finite pool
        ///    vs. the planet's effectively-infinite one IS the durability asymmetry ("a fraction of the effort to
        ///    destroy that a planet does"). The pool is a PLACEHOLDER ratio (Slice B) — tune when the numbers lock.
        /// </summary>
        private static DamageResult OnStationDamage(Entity stationEntity, DamageFragment damageFragment)
        {
            var stationInfo = stationEntity.GetDataBlob<StationInfoDB>();
            var starSystem = stationEntity.Manager as StarSystem;
            if (starSystem == null)
                return new DamageResult { Damage = 0, Destroyed = false };

            // Same energy → damage-strength scale the colony bombardment path uses (100 MJ per unit).
            int damageStrength = Math.Max(1, (int)(damageFragment.Energy / 1e8));

            ApplyPopulationCasualties(stationInfo.Population, damageStrength);
            ApplyInstallationDamage(stationEntity, starSystem, damageStrength);

            // The "cheap to kill" structural pool: a station dies when its integrity is spent (a planet has no such
            // trigger). This is what makes destroying a station an attacker's quick choice rather than a campaign.
            stationInfo.StructuralIntegrity -= damageStrength;
            if (stationInfo.StructuralIntegrity <= 0)
            {
                StationFactory.DestroyStation(stationEntity);
                return new DamageResult { Damage = damageStrength, Destroyed = true };
            }

            ReCalcProcessor.ReCalcAbilities(stationEntity);
            return new DamageResult { Damage = damageStrength, Destroyed = false };
        }

        /// <summary>
        /// Population casualties from a bombardment hit — a quarter million dead per damage unit, capped at the total
        /// population and spread proportionally across the species present. Shared by the colony and station paths so
        /// the two hosts never drift apart in how a strike kills people.
        /// </summary>
        private static void ApplyPopulationCasualties(Dictionary<int, long> population, int damageStrength)
        {
            if (population.Count == 0)
                return;

            long totalPop = 0;
            foreach (var pop in population.Values)
                totalPop += pop;

            if (totalPop <= 0)
                return;

            long casualties = Math.Min(damageStrength * 250_000L, totalPop);
            var speciesIds = new List<int>(population.Keys);
            foreach (int speciesId in speciesIds)
            {
                long share = (long)((double)population[speciesId] / totalPop * casualties);
                population[speciesId] = Math.Max(0L, population[speciesId] - share);
            }
        }

        /// <summary>
        /// Installation / module damage from a bombardment hit — randomly pick and drain installed components until
        /// the damage budget is spent or all are destroyed (20 consecutive misses breaks out). Destroyed components
        /// are removed. Shared by the colony and station paths.
        /// </summary>
        private static void ApplyInstallationDamage(Entity entity, StarSystem starSystem, int damageStrength)
        {
            if (!entity.TryGetDataBlob<ComponentInstancesDB>(out var installsDB))
                return;

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
