using System;
using Pulsar4X.Orbital;
using Pulsar4X.Interfaces;
using Pulsar4X.Engine;

namespace Pulsar4X.Energy
{
    public class EnergyGenProcessor : IInstanceProcessor
    {
        /// <summary>
        /// FUEL EXHAUSTION — the missing rung of the power cradle-to-grave chain (developer, 2026-07-30;
        /// docs/economy/DESIGNER-NORTH-STAR.md §39.7).
        ///
        /// <para>Every fuel-burning generator is charged <c>fissile-fuels</c> at build time, is given a fuel load
        /// (<c>EnergyGenerationAtb</c> → <c>LocalFuel = maxUse × Lifetime</c>), and burns that load down every tick
        /// (below). But <c>LocalFuel</c> was <b>never read as a gate anywhere</b> — five references in the whole
        /// repository and not one a condition — so it ran negative and output never stopped. <b>Every reactor in the
        /// game ran forever on nothing</b>, while the reactor's own description reads <i>"A non refuelable
        /// reactor"</i>. The counter and the drain were built; the consequence was not.</para>
        ///
        /// <para>With this on, a dry generator produces <b>nothing</b>, and every downstream consumer already handles
        /// that correctly with no change: the warp departure gate refuses (<c>WarpMoveCommand</c> checks stored energy
        /// against the bubble creation cost), the ground energy-weapon supply gate refuses
        /// (<c>WeaponSupply</c>/<c>GroundUnitAssembly</c>), and the AI stops planning attacks it cannot power
        /// (<c>MilitaryReach</c>). <b>The consequence system was already there; only the trigger was missing.</b></para>
        ///
        /// <para><b>Default OFF</b>, the same discipline as <c>CombatEngagement.RequireDetectionToEngage</c> and
        /// <c>JammerAtb.EnableJamming</c>: it changes behaviour, so the client arms it rather than CI inheriting it.
        /// Off ⇒ byte-identical (the only other change here is that <c>LocalFuel</c> now floors at 0 instead of
        /// running negative, and nothing reads it either way).</para>
        /// </summary>
        public static bool EnableFuelExhaustion = false;

        public static void EnergyGen(Entity entity, DateTime atDateTime)
        {
            EnergyGenAbilityDB _energyGenDB = entity.GetDataBlob<EnergyGenAbilityDB>();

            TimeSpan t = atDateTime - _energyGenDB.dateTimeLastProcess;

            // A generator that burns fuel and has none left produces nothing. Solar (maxUse == 0) is never starved —
            // it has no fuel to run out of — so a panel-only entity is unaffected in either flag state.
            bool starved = EnableFuelExhaustion
                        && _energyGenDB.TotalFuelUseAtMax.maxUse > 0
                        && _energyGenDB.LocalFuel <= 0;
            // TotalOutputMax is a computed read (installed capacity) — take a local so the INSTALLED figure the
            // colony/UI readouts show is untouched, while generation this tick is what actually stops.
            double capacity = starved ? 0 : _energyGenDB.TotalOutputMax;

            string energyType = _energyGenDB.EnergyType.UniqueID;
            // Defensive (the mining time-stall class): a SOLAR-ONLY entity has EnergyType set but NO EnergyStored/
            // EnergyStoreMax entry — only reactors/batteries seed those dicts. A hard index would throw on the sim
            // thread and freeze the clock (this runs on EVERY EnergyGenAbilityDB entity at PostNewGameInitialization).
            // Read with a 0 default; the write below uses the set-indexer (which creates the key), so a store-less
            // generator is a harmless no-op instead of a crash.
            var stored = _energyGenDB.EnergyStored.TryGetValue(energyType, out var storedVal) ? storedVal : 0;
            var storeMax = _energyGenDB.EnergyStoreMax.TryGetValue(energyType, out var maxVal) ? maxVal : 0;
            double freestore = Math.Max(0, storeMax - stored);

            double totaldemand = _energyGenDB.Demand + freestore;

            var output = capacity - _energyGenDB.Demand;

            output = GeneralMath.Clamp(output, -stored, freestore);
            _energyGenDB.EnergyStored[energyType] = stored + output;   // set-indexer: seeds the key for a store-less generator

            if (output > 0)
            {
                double timeToFill = Math.Ceiling( freestore / output);
                DateTime interuptTime = atDateTime + TimeSpan.FromSeconds(timeToFill);
                entity.Manager.ManagerSubpulses.AddEntityInterupt(interuptTime, nameof(EnergyGenProcessor), entity);
            }
            else if (output < 0)
            {
                double timeToEmpty = Math.Ceiling( Math.Abs(stored / output));
                DateTime interuptTime = atDateTime + TimeSpan.FromSeconds(timeToEmpty);
                entity.Manager.ManagerSubpulses.AddEntityInterupt(interuptTime, nameof(EnergyGenProcessor), entity);
            }


            double load = CalcLoad(_energyGenDB.Demand, capacity);
            _energyGenDB.Load = load;
            _energyGenDB.Output = output;
            double fueluse = _energyGenDB.TotalFuelUseAtMax.maxUse * load;
            // Floor at 0 rather than running negative — a tank cannot hold less than nothing, and the gate above
            // reads this value. (Flag off, nothing reads it, so this is byte-identical.)
            _energyGenDB.LocalFuel = Math.Max(0, _energyGenDB.LocalFuel - fueluse * t.TotalSeconds);

            _energyGenDB.dateTimeLastProcess = atDateTime;

            var histogram = _energyGenDB.Histogram;
            int hgFirstIdx = _energyGenDB.HistogramIndex;
            int hgLastIdx;
            if (hgFirstIdx == 0)
                hgLastIdx = histogram.Count - 1;
            else
                hgLastIdx = hgFirstIdx - 1;

            var hgFirstObj = histogram[hgFirstIdx];
            var hgLastObj = histogram[hgLastIdx];
            int optime = hgLastObj.seconds;

            int newoptime = (int)(optime + t.TotalSeconds);

            var nexval = (foo: output, demand: totaldemand, store: stored, newoptime);

            if(histogram.Count < _energyGenDB.HistogramSize)
                histogram.Add(nexval);
            else
            {
                histogram[hgFirstIdx] = nexval;
                if (hgFirstIdx == histogram.Count - 1)
                    _energyGenDB.HistogramIndex = 0;
                else
                {
                    _energyGenDB.HistogramIndex++;
                }
            }
        }


        /// <summary>
        /// Reactor load as a fraction of max output (0 = idle, 1 = maxed) — the "percent of max output" the
        /// <c>Load</c> field and the power UI mean. Clamped to [0,1]: a reactor can't be more than fully loaded,
        /// and can't burn more than max fuel — over-demand is met by the battery discharging, not by over-driving
        /// the reactor. A zero/negative capacity (no reactor) reads 0 with no divide-by-zero.
        ///
        /// Fixes a long-standing bug (2026-06-26): the old formula was <c>TotalOutputMax / spareCapacity</c>
        /// (i.e. max ÷ (max − demand)) — INVERTED and UNBOUNDED: 1.0 at idle, 2.0 at half demand, →∞ approaching
        /// full. It mislabelled the power-UI readout (shown via <c>"P1"</c> percent) AND, because reactor fuel use
        /// is <c>maxFuelUse × load</c>, made an IDLE reactor burn near-max fuel. Both consumers are corrected by
        /// this one fix; the battery/interrupt logic uses <c>output</c> (spare), not <c>load</c>, so it is unchanged.
        /// </summary>
        public static double CalcLoad(double demand, double totalOutputMax)
        {
            if (totalOutputMax <= 0)
                return 0;
            return Math.Clamp(demand / totalOutputMax, 0.0, 1.0);
        }

        internal override void ProcessEntity(Entity entity, DateTime atDateTime)
        {
            EnergyGen(entity, atDateTime);
        }
    }
}