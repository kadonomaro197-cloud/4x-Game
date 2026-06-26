using System;
using System.Collections.Generic;
using Pulsar4X.Components;
using Pulsar4X.Orbital;
using Pulsar4X.Datablobs;
using Pulsar4X.DataStructures;
using Pulsar4X.Engine;
using Pulsar4X.Extensions;
using Pulsar4X.Galaxy;

namespace Pulsar4X.Sensors
{
    public class SensorProfileDB : BaseDataBlob
    {
        //Currently Unused.
        /*
        internal double GravSig
        {
            get
            {
                if (OwningEntity.HasDataBlob<MassVolumeDB>())
                    return OwningEntity.GetDataBlob<MassVolumeDB>().Mass;
                else
                    return 0;
            }
        }*/

        private double? _targetCrossSection;
        /// <summary>
        /// Cross section in meters squared.
        /// </summary>
        /// <exception cref="Exception"></exception>
        public double TargetCrossSection_msq
        {
            get
            {
                if (_targetCrossSection != null)
                    return (double)_targetCrossSection;
                else if (this.OwningEntity.HasDataBlob<MassVolumeDB>())
                {
                    _targetCrossSection = Math.PI * Math.Pow(this.OwningEntity.GetDataBlob<MassVolumeDB>().RadiusInM, 2);
                    return (double)_targetCrossSection;
                }
                else return 0;
                    //throw new Exception("Parent Entity does not have an MassVolumeDB");
            }
        }

        //TODO make this a bit more complex so it reflects different amounts at different wavelengths
        //this will define how effective active sensors are, and will increase a ships detection when its closer to a star.
        //key is frequency, value is 0.0-1.0 for that freqency. for most entites this will create a wave type spectrum.
        //internal Dictionary<double, float> Reflectivity { get; private set; } = new Dictionary<double, float>();
        internal double Reflectivity = 0.9;
        
        /// <summary>
        /// reflection coefficent. 
        /// </summary>
        internal double ReflectionCoefficent {get {return Reflectivity * TargetCrossSection_msq;}}

        /// <summary>
        /// This dictionary gets replaced frequently by SetReflectedEMSig()
        /// </summary>
        /// <value>The reflected EMS pectra.</value>
        //public Dictionary<EMWaveForm, double> ReflectedEMSpectra { get; } = new Dictionary<EMWaveForm, double>();
        public List<EMData> ReflectedEMSpectra { get; } = new();
        internal DateTime LastDatetimeOfReflectionSet = new DateTime();
        internal Vector3 LastPositionOfReflectionSet = new Vector3();


        /// <summary>
        /// Multiple Emissions make up the signature of the entity.
        /// the volume of each emission can increase and decrease, ie by running engines for movement, or active sensors.
        /// </summary>
        /// <key>defines the average and dropout wavelengths in nanometers</key>
        /// <value>the volume or magnatude of the spectra</value>
        //public Dictionary<EMWaveForm, double> EmittedEMSpectra { get; } = new Dictionary<EMWaveForm, double>();

        public List<EMData> EmittedEMSpectra = new();

        /// <summary>
        /// EMCON activity dial (default 1.0): the FINAL runtime multiplier on this entity's EMITTED signature —
        /// "how loud am I right now." Applied to the EMITTED spectra in
        /// <see cref="Pulsar4X.Sensors.SensorTools.AttenuatedForDistance"/>; the REFLECTED (radar return) is NOT
        /// scaled — going dark doesn't shrink your hull. Default 1.0 so detection is unchanged until the EMCON
        /// stack drives it. This is the OUTPUT field the detection math reads; it is computed as
        /// <see cref="SignatureBaseMultiplier"/> × an activity heat factor by
        /// <see cref="Pulsar4X.Sensors.EmconActivityProcessor"/> (and set directly by the posture lever for
        /// instant feedback). Finishes the dynamic-signature the original author scaffolded (see the EMITTED
        /// comment above + the inert <see cref="EMData.StateLoad"/>).
        /// </summary>
        public double ActivityMultiplier = 1.0;

        /// <summary>
        /// The posture BASE (default 1.0): the signature scale the fleet's chosen EMCON posture
        /// (Full 1.0 / Cruise 0.5 / Silent 0.15) implies, BEFORE runtime activity. Pushed here by
        /// <see cref="FleetEmcon.SetPosture"/> (the player's lever). The <see cref="EmconActivityProcessor"/> reads
        /// it each tick and multiplies by a heat factor (running hot / thrusting / firing makes you louder) to
        /// produce the final <see cref="ActivityMultiplier"/>. So: posture sets your baseline loudness; activity
        /// modulates around it — and hard activity (a lit drive plume) can betray you even on a Silent posture
        /// (you can't burn quietly). Default 1.0 = Full, so detection is unchanged until a posture is set.
        /// </summary>
        public double SignatureBaseMultiplier = 1.0;

        public SensorProfileDB() { }

        public SensorProfileDB(SensorProfileDB db)
        {
            //EmittedEMSpectra = new Dictionary<EMWaveForm, double>(db.EmittedEMSpectra);
            //ReflectedEMSpectra = new Dictionary<EMWaveForm, double>(db.ReflectedEMSpectra);
            EmittedEMSpectra = new List<EMData>( db.EmittedEMSpectra);
            ReflectedEMSpectra = new List<EMData>( db.ReflectedEMSpectra);
            _targetCrossSection = db._targetCrossSection;
            ActivityMultiplier = db.ActivityMultiplier;
            SignatureBaseMultiplier = db.SignatureBaseMultiplier;
        }

        public override object Clone()
        {
            return new SensorProfileDB(this);
        }
    }

    public struct EMData
    {
        internal ComponentInstance Instance;
        internal Entity SourceEntity;
        public EMWaveForm WaveForm;
        public double Magnitude;
        public float StateLoad
        {
            get
            {
                if (Instance != null)
                    return Instance.ComponentLoadPercent;
                else return 1;
            }
        }

        public string GetName
        {
            get
            {
                if(Instance != null)
                    return Instance.Name;
                else if (SourceEntity != null)
                    return SourceEntity.GetOwnersName();
                else
                    return "Unknown EM Signature";
            }
        }

    }
}
