using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Engine;
using Pulsar4X.Components;
using Pulsar4X.Datablobs;


namespace Pulsar4X.Sensors
{
    public class SensorReceiverAtb : IComponentDesignAttribute
    {
        [JsonProperty]
        public EMWaveForm RecevingWaveformCapabilty { get; internal set; }

        /// <summary>
        /// Sensitivity at the ideal wavelength, lower is better, 0 is (imposible) best. should not be negitive.
        /// </summary>
        [JsonProperty]
        public double BestSensitivity_kW { get; internal set; }//sensitivity at ideal wavelength

        /// <summary>
        /// The sensitivity at worst detectable wavelengths, lower is better, should be higher than BestSensitivity_kW
        /// </summary>
        [JsonProperty]
        public double WorstSensitivity_kW { get; internal set; } // sensitivity at worst detectable wavelengths
        /// <summary>
        /// In MegaPixels
        /// </summary>
        [JsonProperty]
        public float Resolution { get; internal set; } //will give more details on the target. low res will detect *something* but not *what*
        /// <summary>
        /// In Seconds
        /// </summary>
        [JsonProperty]
        public int ScanTime { get; internal set; } //the time it takes to complete a full 360 degree sweep.
        //internal int Size; //basicly increases sensitivity at the cost of mass

        /// <summary>
        /// Solar arrays use the same code as sensor detection.
        /// </summary>
        [JsonProperty] public bool IsEnergyGen { get; internal set; } = false;

        /// <summary>
        /// A HARD detection horizon in metres — this receiver detects NOTHING beyond it, regardless of how loud the
        /// target is. 0 = unlimited (the default; every existing sensor). This is the signature-INDEPENDENT reach cap
        /// the docs call for (`Sensors/CLAUDE.md`): antenna/signal decide detection WITHIN the horizon, but a very
        /// loud target can't be seen past it. The colony megasensor sets one so the homeworld can't see the belt/Ceres;
        /// a ship sensor leaves it 0 (its real reach is ~0.3 Gm, far inside any horizon we'd set → no-op / byte-identical).
        /// </summary>
        [JsonProperty] public double MaxDetectionRange_m { get; internal set; } = 0;


        [JsonConstructor]
        public SensorReceiverAtb() { }

        /// <summary>Horizon-capped overload: the 6-arg receiver PLUS a hard <see cref="MaxDetectionRange_m"/>. A separate
        /// arity so existing 6-arg templates are byte-identical (the exact-arity binder rule, Weapons gotcha #0); only a
        /// template that passes the 7th arg (the colony passive-sensor) binds here and gets a horizon.</summary>
        public SensorReceiverAtb(double peakWaveLength, double bandwidth, double bestSensitivity, double worstSensitivity, double resolution, double scanTime, double maxDetectionRange)
            : this(peakWaveLength, bandwidth, bestSensitivity, worstSensitivity, resolution, scanTime)
        {
            MaxDetectionRange_m = maxDetectionRange < 0 ? 0 : maxDetectionRange;
        }

        //ParserConstrutor
        /// <summary>
        ///
        /// </summary>
        /// <param name="peakWaveLength">nm</param>
        /// <param name="bandwidth">nm</param>
        /// <param name="bestSensitivity">watts</param>
        /// <param name="worstSensitivity">watts</param>
        /// <param name="resolution">mp</param>
        /// <param name="scanTime">sec</param>
        public SensorReceiverAtb(double peakWaveLength, double bandwidth, double bestSensitivity, double worstSensitivity, double resolution, double scanTime)
        {
            //TODO:  should make this component invalid.
            if (bestSensitivity < 0)
            {
                // var ev = new Event("Sensitivity is" + bestSensitivity + " *Must* be a positiveNumber Sensitivity is the kilowatt threshhold");
                // StaticRefLib.EventLog.AddEvent(ev);
                bestSensitivity = 0;

            }
            if (bestSensitivity > worstSensitivity)
            {
                // var ev = new Event("bestSensitivity " + bestSensitivity + " *Must* be < than worstSensitivity" + worstSensitivity +
                //                    "(lower is better) Sensitivity is the kilowatt threshhold");
                // StaticRefLib.EventLog.AddEvent(ev);
                worstSensitivity = bestSensitivity;
            }
            RecevingWaveformCapabilty = new EMWaveForm(peakWaveLength - bandwidth * 0.5,peakWaveLength, peakWaveLength + bandwidth * 0.5);
            BestSensitivity_kW = bestSensitivity * 0.001;
            WorstSensitivity_kW = worstSensitivity * 0.001;
            Resolution = (float)resolution;
            ScanTime = (int)scanTime;
        }
        
        public SensorReceiverAtb(double peakWaveLength, double bandwidth, double bestSensitivity, double worstSensitivity, double efficency)
        {
            //TODO:  should make this component invalid.
            if (bestSensitivity < 0)
            {
                // var ev = new Event("Sensitivity is" + bestSensitivity + " *Must* be a positiveNumber Sensitivity is the kilowatt threshhold");
                // StaticRefLib.EventLog.AddEvent(ev);
                bestSensitivity = 0;

            }
            if (bestSensitivity > worstSensitivity)
            {
                // var ev = new Event("bestSensitivity " + bestSensitivity + " *Must* be < than worstSensitivity" + worstSensitivity +
                //                    "(lower is better) Sensitivity is the kilowatt threshhold");
                // StaticRefLib.EventLog.AddEvent(ev);
                worstSensitivity = bestSensitivity;
            }
            RecevingWaveformCapabilty = new EMWaveForm(peakWaveLength - bandwidth * 0.5,peakWaveLength, peakWaveLength + bandwidth * 0.5);
            BestSensitivity_kW = bestSensitivity * 0.001;
            WorstSensitivity_kW = worstSensitivity * 0.001;
            Resolution = (float)efficency;
            ScanTime = (int)3600;
            IsEnergyGen = true;
        }

        public SensorReceiverAtb(SensorReceiverAtb db)
        {
            RecevingWaveformCapabilty = db.RecevingWaveformCapabilty;
            BestSensitivity_kW = db.BestSensitivity_kW;
            WorstSensitivity_kW = db.WorstSensitivity_kW;
            Resolution = db.Resolution;
            ScanTime = db.ScanTime;
            IsEnergyGen = db.IsEnergyGen;                    // was missing — carry the solar-array flag on clone
            MaxDetectionRange_m = db.MaxDetectionRange_m;    // carry the detection horizon on clone
        }
        

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance)
        {
            //add the instance atb to the ship SensorAbilityDB.
            SensorTools.SetInstances(parentEntity);

            // Kick off the FIRST scan. SetInstances only rebuilds the receiver cache — it does NOT schedule a scan.
            // At New Game, Game.PostNewGameInitialization fires the first scan on every sensor-bearing entity to
            // bootstrap SensorScan's self-rescheduling loop. But a sensor installed MID-GAME — a freshly built
            // listening-outpost station, a DevTools-spawned ship — never passes through that path, so without this
            // it would sit deaf forever (SensorScan reschedules itself, but only once it has fired at least once).
            // Schedule the first scan one ScanTime out; SensorScan.ProcessEntity self-reschedules from there.
            // Guarded and future-dated off StarSysDateTime so it can never throw the "interrupt in the past" during
            // construction. Harmless if PostNewGameInitialization also fires it (idempotent scan; TimeQueue tolerates
            // a duplicate time).
            var manager = parentEntity?.Manager;
            if (manager != null)
            {
                int scanTime = ScanTime > 0 ? ScanTime : 3600;
                manager.ManagerSubpulses.AddEntityInterupt(
                    manager.StarSysDateTime + System.TimeSpan.FromSeconds(scanTime),
                    nameof(SensorScan),
                    parentEntity);
            }
        }

        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance)
        {

        }

        public string AtbName()
        {
            return "Sensor Recever";
        }

        public string AtbDescription()
        {

            return " ";
        }
    }
}