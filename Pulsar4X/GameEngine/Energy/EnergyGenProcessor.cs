using System;
using Pulsar4X.Orbital;
using Pulsar4X.Interfaces;
using Pulsar4X.Engine;

namespace Pulsar4X.Energy
{
    public class EnergyGenProcessor : IInstanceProcessor
    {

        public static void EnergyGen(Entity entity, DateTime atDateTime)
        {
            EnergyGenAbilityDB _energyGenDB = entity.GetDataBlob<EnergyGenAbilityDB>();

            TimeSpan t = atDateTime - _energyGenDB.dateTimeLastProcess;

            string energyType = _energyGenDB.EnergyType.UniqueID;
            var stored = _energyGenDB.EnergyStored[energyType];
            var storeMax = _energyGenDB.EnergyStoreMax[energyType];
            double freestore = Math.Max(0, storeMax - stored);

            double totaldemand = _energyGenDB.Demand + freestore;

            var output = _energyGenDB.TotalOutputMax - _energyGenDB.Demand;

            output = GeneralMath.Clamp(output, -stored, freestore);
            _energyGenDB.EnergyStored[energyType] += output;

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


            double load = CalcLoad(_energyGenDB.Demand, _energyGenDB.TotalOutputMax);
            _energyGenDB.Load = load;
            _energyGenDB.Output = output;
            double fueluse = _energyGenDB.TotalFuelUseAtMax.maxUse * load;
            _energyGenDB.LocalFuel -= fueluse * t.TotalSeconds;

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