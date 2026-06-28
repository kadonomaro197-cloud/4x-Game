using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar4X.Datablobs;
using Pulsar4X.Orbital;
using Pulsar4X.DataStructures;
using Pulsar4X.Extensions;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.Movement;

namespace Pulsar4X.Sensors
{

    public static class SensorTools
    {

        public static SensorReturnValues[] GetDetectedEntites(SensorReceiverAtb sensorAtb, Vector3 position, List<Entity> detectableEntities, DateTime atDate, int factionOwnerId,  bool filterSameFaction = true)
        {
            SensorReturnValues[] detectionValues = new SensorReturnValues[detectableEntities.Count];
            for (int i = 0; i < detectableEntities.Count; i++)
            {
                var detectableEntity = detectableEntities[i];
                if (filterSameFaction && detectableEntity.FactionOwnerID == factionOwnerId)
                    continue;
                else
                {
                    if(!detectableEntity.TryGetDataBlob<PositionDB>(out var detectablePosDB))
                    {
                        continue;
                    }

                    //TODO: check if the below actualy saves us anything. it might be better just to seperatly loop through each of the entites and set the reflection profiles every so often..
                    // TimeSpan timeSinceLastCalc = atDate - detectableProfile.LastDatetimeOfReflectionSet;
                    // double distanceSinceLastCalc = detectablePosDB.GetDistanceTo_m(detectableProfile.LastPositionOfReflectionSet);
                    // if (timeSinceLastCalc > TimeSpan.FromMinutes(30) || distanceSinceLastCalc > 5000) //TODO: move the time and distance numbers here to settings?
                    //     SetReflectedEMProfile.SetEntityProfile(detectableEntity, atDate);

                    var detectableProfile = detectableEntity.GetDataBlob<SensorProfileDB>();
                    var distance = detectablePosDB.GetDistanceTo_m(position);
                    var attentuatedSignal = AttenuatedForDistance(detectableProfile, distance);
                    SensorReturnValues detectionValue = DetectonQuality(sensorAtb, attentuatedSignal);
                    //if(detectionValue.SignalStrength_kW > 0)
                        detectionValues[i] = detectionValue;
                }
            }

            return detectionValues;
        }
        
        public static SensorReturnValues DetectonQuality(SensorReceiverAtb recever, Dictionary<EMWaveForm, double> signalAtPosition)
        {
            /*
             * Thoughts (spitballing):
             *
             * What we need:
             * detect enough of a signal to get a position
             * decide what "enough" is. probibly get this from the signal strength. - should the target SensorSigDB define what enough is?
             * we could require more than one detection (ie two ships in different locations) to get an acurate position, but that could get tricky to code.
             * and how would we display a non acurate position? maybe a line out to a question mark, showing the angle of detection but not range?
             *
             * detect enough of a signal to get intel if it's a ship
             * decide what "enough" for this is. maybe compare the detected waveform and the emited waveform and compare the angles to see if the triangle is simular.
             *
             * it'd be nifty if we could include background noise in there too, ie so ships close to a sun would be hidden.
             * also have resoulution be required to pick out multiple ships close together instead of just one big signal.
             *
             * With range attenuation, we'll never get the full signal uneless we're right ontop of it.
             * maybe if we get half the emited strength and its a simular triange (all same angles) we get "Full" intel?
             *
             * should we add time into the mix as well? multiple detections over a given time period to get position/velocity/orbitDB?
             *
             *
             * how are multiple components on a ship going to work? they are entitys in and of themselfs, so they could have a SensorSigDB all of thier own.
             * that could help with getting intel on individual components of a target.
             *
             * recever resolution should play into how much gets detected.
             *
             * Note that each entity will(may) have multiple waveforms.
             *
             * Data that can be glened from this detection system:
             * detectedStrength (altitide of the intersecting triangle)
             * detectedArea - the area of the detected intersection, could compare this to the target signal as well.
             * compare angles of the detected intersection and the target signal to see if the shape is simular?
             * if range is known acurately, this could affect the intel gathered.
             */

            /*
            var myPosition = recever.OwningEntity.GetDataBlob<ComponentInstanceInfoDB>().ParentEntity.GetDataBlob<PositionDB>();//recever is a componentDB. not a shipDB
            if (myPosition == null) //then it's probilby a colony
                myPosition = recever.OwningEntity.GetDataBlob<ComponentInstanceInfoDB>().ParentEntity.GetDataBlob<ColonyInfoDB>().PlanetEntity.GetDataBlob<PositionDB>();
            PositionDB targetPosition;
            if( target.OwningEntity.HasDataBlob<PositionDB>())
                targetPosition = target.OwningEntity.GetDataBlob<PositionDB>();
            else
                targetPosition = target.OwningEntity.GetDataBlob<ComponentInstanceInfoDB>().ParentEntity.GetDataBlob<PositionDB>();//target may be a componentDB. not a shipDB
            double distance = PositionDB.GetDistanceBetween(myPosition, targetPosition);

            var detectionResolution = recever.Resolution;

            var signalAtPosition = AttenuatedForDistance(target, distance);
*/
            double receverSensitivityFreqMin = recever.RecevingWaveformCapabilty.WavelengthMin_nm;
            double receverSensitivityFreqAvg = recever.RecevingWaveformCapabilty.WavelengthAverage_nm;
            double receverSensitivityFreqMax = recever.RecevingWaveformCapabilty.WavelengthMax_nm;
            double receverSensitivityBest = recever.BestSensitivity_kW;
            double receverSensitivityAltitiude = recever.BestSensitivity_kW - recever.WorstSensitivity_kW;
            PercentValue quality = new PercentValue(0.0f);
            double detectedMagnatude = 0;
            foreach (var waveSpectra in signalAtPosition)
            {
                double signalWaveSpectraFreqMin = waveSpectra.Key.WavelengthMin_nm;
                double signalWaveSpectraFreqAvg = waveSpectra.Key.WavelengthAverage_nm;
                double signalWaveSpectraFreqMax = waveSpectra.Key.WavelengthMax_nm;
                double signalWaveSpectraMagnatude_kW = waveSpectra.Value;



                if (signalWaveSpectraMagnatude_kW > recever.BestSensitivity_kW) //check if the sensitivy is enough to pick anything up at any frequency.
                {
                    if (Math.Max(receverSensitivityFreqMin, signalWaveSpectraFreqMin) < Math.Max(signalWaveSpectraFreqMin, signalWaveSpectraFreqMax))
                    {
                        //we've got something we can detect
                        double minDetectableWavelength = Math.Min(receverSensitivityFreqMin, signalWaveSpectraFreqMin);
                        double maxDetectableWavelenght = Math.Min(receverSensitivityFreqMax, signalWaveSpectraFreqMax);

                        double detectedAngleA = Math.Atan(receverSensitivityAltitiude / (receverSensitivityFreqAvg - receverSensitivityFreqMin ));
                        double receverBaseLen = maxDetectableWavelenght - minDetectableWavelength;
                        double detectedAngleB = Math.Atan(signalWaveSpectraMagnatude_kW / (signalWaveSpectraFreqAvg - signalWaveSpectraFreqMax));

                        bool doesIntersect;
                        double intersectPointX;
                        double intersectPointY;
                        double distortion;

                        if (signalWaveSpectraFreqAvg < receverSensitivityFreqAvg)  //RightsideDetection (recever's ideal wavelenght is higher than the signal wavelenght at it's loudest)
                        {
                            doesIntersect = Get_line_intersection(
                                signalWaveSpectraFreqAvg, signalWaveSpectraMagnatude_kW,
                                signalWaveSpectraFreqMin, 0,

                                receverSensitivityFreqAvg, recever.BestSensitivity_kW,
                                receverSensitivityFreqMax, recever.WorstSensitivity_kW,

                                out intersectPointX, out intersectPointY);
                            //offsetFromCenter = intersectPointX - signalWaveSpectraFreqAvg; //was going to use this for distortion but decided to simplify.
                            distortion = receverSensitivityFreqAvg - signalWaveSpectraFreqAvg;

                        }
                        else                                                        //LeftSideDetection
                        {
                            doesIntersect = Get_line_intersection(
                                signalWaveSpectraFreqAvg, signalWaveSpectraMagnatude_kW,
                                signalWaveSpectraFreqMax, 0,

                                receverSensitivityFreqAvg, recever.BestSensitivity_kW,
                                receverSensitivityFreqMin, recever.WorstSensitivity_kW,

                                out intersectPointX, out intersectPointY);
                            //offsetFromCenter = intersectPointX - signalWaveSpectraFreqAvg;
                            distortion = signalWaveSpectraFreqAvg - receverSensitivityFreqAvg;

                        }

                        if (doesIntersect) // then we're not detecting the peak of the signal
                        {
                            detectedMagnatude = intersectPointY - recever.BestSensitivity_kW;
                            distortion *= 2; //pentalty to quality of signal
                        }
                        else
                            detectedMagnatude = signalWaveSpectraMagnatude_kW - recever.BestSensitivity_kW;

                        // SignalQuality is a 0..1 fraction ("how well resolved is this contact"). PercentValue
                        // stores it as a byte (value * 255), so it MUST be handed 0..1. `distortion` is how far the
                        // signal's peak wavelength sits from the receiver's ideal band (in nm); dividing by the
                        // signal's max wavelength turns it into a dimensionless mis-tune fraction, so a well-centred
                        // signal scores ~1 and a badly-off-band one ~0. Clamp guards the case where the mis-tune
                        // exceeds the band.
                        // BUGFIX (Phase-0 prerequisite): the old line computed `100 - …` on a 0..100 scale and shoved
                        // it into PercentValue — ~100 * 255 overflowed the byte and WRAPPED, so quality came out
                        // effectively random. Because planet/star SURVEY reveal gates on this value
                        // (SystemBodyInfoDB / StarInfoDB read it against 0.20 / 0.80), survey reveal was random too.
                        // See Sensors/CLAUDE.md "Detection-quality bug".
                        quality = new PercentValue((float)Math.Clamp(1.0 - distortion / signalWaveSpectraFreqMax, 0.0, 1.0));

                    }
                }
            }



            return new SensorReturnValues()
            {
                SignalStrength_kW = detectedMagnatude,
                SignalQuality = quality
            };
        }

        /// <summary>
        /// Rebuilds the SensorAbilityDB's InstanceAtributes/InstanceStates lists from the entity's CURRENT sensor
        /// receiver components — clearing any that are gone. Called when a sensor is installed
        /// (<see cref="SensorReceiverAtb.OnComponentInstallation"/>) AND on any ability recalc
        /// (<c>ReCalcProcessor</c> → after the damage system destroys a component). That recalc hook is the GRAVE
        /// RUNG (detection × damage): destroy a ship's sensor receivers and this rebuild leaves the lists EMPTY, so
        /// the scan loop (which iterates InstanceStates) does nothing and the ship stops detecting — you go blind.
        /// </summary>
        /// <param name="entity"></param>
        internal static void SetInstances(Entity entity)
        {
            // No components → no sensors to (re)build.
            if (!entity.TryGetDataBlob<ComponentInstancesDB>(out var components))
                return;

            // receivers is null/empty if every receiver has been removed (e.g. shot off). We must still CLEAR the
            // cache in that case — that's the grave rung — so we don't gate the rebuild on having receivers.
            components.TryGetComponentsByAttribute<SensorReceiverAtb>(out var receivers);
            bool hasReceivers = receivers != null && receivers.Count > 0;
            bool hasAbility = entity.TryGetDataBlob<SensorAbilityDB>(out var abilityDB);

            // Nothing to build and no stale cache to clear.
            if (!hasReceivers && !hasAbility)
                return;

            if (!hasAbility)
            {
                abilityDB = new SensorAbilityDB();
                entity.SetDataBlob(abilityDB);
            }

            // Rebuild from scratch so destroyed receivers drop out. With none left, the lists end up empty.
            abilityDB.InstanceAtributes = new ();
            abilityDB.InstanceStates = new ();
            if (hasReceivers)
            {
                foreach (var receiverInstance in receivers)
                {
                    //we're cloning the design to the instance here.
                    if (!receiverInstance.TryGetAbilityState<SensorReceiverAbility>(out var abilityState))
                    {
                        abilityState = new SensorReceiverAbility(receiverInstance);
                        receiverInstance.SetAbilityState<SensorReceiverAbility>(abilityState);
                    }
                    //add the state to a list in the SensorAbilityDB
                    abilityDB.InstanceStates.Add(abilityState);
                    //add the SensorReceiverAtb to a list in the SensorAbilityDB
                    var sensorAtb = receiverInstance.Design.GetAttribute<SensorReceiverAtb>();
                    abilityDB.InstanceAtributes.Add(sensorAtb);
                }
            }
        }

        /// <summary>
        /// Gets the line intersection.
        /// </summary>
        /// <returns><c>true</c>, if lines intesect, <c>false</c> otherwise.</returns>
        /// <param name="p0_x">P0 x.</param>
        /// <param name="p0_y">P0 y.</param>
        /// <param name="p1_x">P1 x.</param>
        /// <param name="p1_y">P1 y.</param>
        /// <param name="p2_x">P2 x.</param>
        /// <param name="p2_y">P2 y.</param>
        /// <param name="p3_x">P3 x.</param>
        /// <param name="p3_y">P3 y.</param>
        /// <param name="i_x">the x position of the intersection</param>
        /// <param name="i_y">the y position of the intersection</param>
        internal static bool Get_line_intersection(double p0_x, double p0_y, double p1_x, double p1_y,
            double p2_x, double p2_y, double p3_x, double p3_y, out double i_x, out double i_y)
        {
            double s1_x, s1_y, s2_x, s2_y;
            s1_x = p1_x - p0_x; s1_y = p1_y - p0_y;
            s2_x = p3_x - p2_x; s2_y = p3_y - p2_y;

            double s, t;
            s = (-s1_y * (p0_x - p2_x) + s1_x * (p0_y - p2_y)) / (-s2_x * s1_y + s1_x * s2_y);
            t = (s2_x * (p0_y - p2_y) - s2_y * (p0_x - p2_x)) / (-s2_x * s1_y + s1_x * s2_y);

            i_x = 0;
            i_y = 0;

            if (s >= 0 && s <= 1 && t >= 0 && t <= 1)
            {
                // Collision detected

                    i_x = p0_x + (t * s1_x);

                    i_y = p0_y + (t * s1_y);

                return true;
            }

            return false; // No collision
        }

        /// <summary>
        /// returns a dictionary of all emmisions including reflected emmisions.
        /// </summary>
        /// <returns>The for distance.</returns>
        /// <param name="emissionProfile">Emission.</param>
        /// <param name="distance">Distance.</param>
        public static Dictionary<EMWaveForm, double> AttenuatedForDistance(SensorProfileDB emissionProfile, double distance)
        {
            var dict = new Dictionary<EMWaveForm, double>();
            void Add(EMWaveForm wf, double v)
            {
                if (!dict.ContainsKey(wf)) dict.Add(wf, v);
                else dict[wf] += v;
            }
            // EMITTED signature scales with the entity's current activity / EMCON posture (run hot = loud, dark = quiet).
            double emit = emissionProfile.ActivityMultiplier;
            foreach (var emdat in emissionProfile.EmittedEMSpectra)
                Add(emdat.WaveForm, AttenuationCalc(emdat.Magnitude * emit, distance));
            // REFLECTED (radar return) is NOT scaled by EMCON — going quiet doesn't shrink your cross-section.
            foreach (var emdat in emissionProfile.ReflectedEMSpectra)
                Add(emdat.WaveForm, AttenuationCalc(emdat.Magnitude, distance));
            return dict;
        }
        
        /// <summary>
        /// returns a dictionary of all emmisions including reflected emmisions.
        /// </summary>
        /// <returns>The for distance.</returns>
        /// <param name="emissionProfile">Emission.</param>
        /// <param name="distance">Distance.</param>
        public static List<EMData> AttenuatedForDistanceList(SensorProfileDB emissionProfile, double distance, double cullBelow = 0.01)
        {
            var list = new List<EMData>();
            var factor = AttenuationFactor(distance);
            double emit = emissionProfile.ActivityMultiplier; // EMITTED scales with EMCON activity; REFLECTED does not.
            void AddScaled(EMData emdat, double mult)
            {
                var value = emdat.Magnitude * mult * factor;
                if (value >= cullBelow)
                {
                    EMData newdata = new EMData();
                    newdata.WaveForm = emdat.WaveForm;
                    newdata.Magnitude = value;
                    list.Add(newdata);
                }
            }
            foreach (var emdat in emissionProfile.EmittedEMSpectra) AddScaled(emdat, emit);
            foreach (var emdat in emissionProfile.ReflectedEMSpectra) AddScaled(emdat, 1.0);
            return list;
        }

        /// <summary>
        /// Power per unit of area.
        /// note that this is *not* a decebel mesurment, decebels are mesured logrithmicaly.
        /// </summary>
        /// <returns>souce / (4 pi r^2)</returns>
        /// <param name="sourceValue">Source value.</param>
        /// <param name="distance">Distance. must be > 1 or it'll just return the source value</param>
        // Detection-sensitivity rebalance (2026-06-27). The raw inverse-square law (source / 4π r²) is so harsh at
        // realistic scales that a ship detected another ship at only ~292 km (measured by DetectionTuningTests:
        // ~0.0003 Gm). With fog of war on that meant a fleet could sit AT a body and never see
        // the hostiles parked there, AND combat (which requires mutual detection) could never trigger. This was the
        // exact "sat at Luna, saw nothing, no battle" play-test report, and it's the long-standing in-code TODO
        // ("the default sensor on Earth doesn't even detect Uranus"). DetectionSensitivityScale multiplies the
        // received signal uniformly, so detection RANGE scales by its square root: 1e6 → ~1000× range → a ship
        // detects a ship at ~0.29 Gm. That comfortably covers same-body combat and gives modest approach warning,
        // while staying far below the ~60 Gm inner-system scale (so fog of war is preserved — you do NOT see the
        // whole system). It's UNIFORM (multiplies every signal equally), so every RELATIVE detection result —
        // loud-seen-farther-than-quiet, shrinks-on-Silent, the EMCON ladder — is unchanged; only the absolute reach
        // moves. Applied in AttenuationCalc + AttenuationFactor (the live scan) AND RangeForSignal (the readout), so
        // the scan and the "how far can I see" number stay in exact agreement. Tunable like Combat's SalvoDamageScale.
        public const double DetectionSensitivityScale = 1e6;

        public static double AttenuationCalc(double sourceValue, double distance)
        {
            // source * scale / (4 pi r^2)  — see DetectionSensitivityScale above
            if (distance < 1) //if distance is too small, 4 pi r^2 ends up being < 1
                distance = 1;

            return sourceValue * DetectionSensitivityScale / (4 * Math.PI * distance * distance);
        }

        /// <summary>
        /// Multiply sourceValue by this to get attenuation value.
        /// usefull for instances where you have multiple sources for the same distance.
        /// </summary>
        /// <param name="distance">in meters</param>
        /// <returns></returns>
        public static double AttenuationFactor(double distance)
        {
            if(distance < 1)
                distance = 1;
            return DetectionSensitivityScale / (4 * Math.PI * distance * distance);
        }

        // ─── Detection RANGE (the reverse of attenuation) ──────────────────────────────────────────────────────
        //
        // The scan loop only ever asks "at the target's CURRENT distance, is its faded signal above my threshold?"
        // (a yes/no, computed fresh every scan). It never produces a "how far can I see?" number — so the UI has
        // nothing to draw as a sensor ring. These helpers run that same physics BACKWARDS: given a loud source and
        // a sensitivity threshold, solve AttenuationCalc(source, d) = threshold for d.
        //
        //     source / (4π d²) = threshold   ⇒   d = √( source / (4π · threshold) )
        //
        // This inverts only the FIRST gate of DetectonQuality (line ~119: a band is detectable when its attenuated
        // magnitude exceeds BestSensitivity_kW). It deliberately ignores the waveform-overlap quality refinement —
        // that shapes how WELL a contact is resolved, not whether it's seen at all. The gameplay truth survives the
        // simplification: a louder target (or one running hot, higher ActivityMultiplier) is seen farther; a more
        // sensitive sensor (lower BestSensitivity_kW) sees farther.

        /// <summary>
        /// The distance at which a single band of <paramref name="source_kW"/> fades to <paramref name="threshold_kW"/>.
        /// Exact inverse of <see cref="AttenuationCalc"/>. Returns 0 for a non-positive source or threshold (nothing
        /// to detect / a perfect-sensor degenerate case the caller should treat as "unbounded, don't draw").
        /// </summary>
        public static double RangeForSignal(double source_kW, double threshold_kW)
        {
            if (source_kW <= 0 || threshold_kW <= 0)
                return 0;
            // Includes DetectionSensitivityScale so this stays the EXACT inverse of AttenuationCalc (the readout
            // range matches the distance at which the live scan actually breaks threshold).
            return Math.Sqrt(source_kW * DetectionSensitivityScale / (4 * Math.PI * threshold_kW));
        }

        /// <summary>
        /// How far the given receiver would first pick up the given target: the largest single-band detection range
        /// across the target's emitted spectra (scaled by its current EMCON/activity, exactly as the scan does) and
        /// its reflected spectra (NOT scaled — going dark doesn't shrink your radar cross-section). The loudest band
        /// wins because that's the band that breaks threshold first as you close. Returns 0 if there's nothing to
        /// detect or the receiver's threshold is non-positive.
        /// </summary>
        public static double DetectionRange_m(SensorReceiverAtb receiver, SensorProfileDB target, double activityOverride = -1)
        {
            if (receiver == null || target == null)
                return 0;
            double threshold = receiver.BestSensitivity_kW;
            if (threshold <= 0)
                return 0;

            double bestRange = 0;
            // EMITTED scales with run-hot/dark; REFLECTED does not. activityOverride >= 0 PINS the emitted scaling
            // (e.g. 1.0 = "as if at full activity"), so "how far I SEE" can be measured against a reference target
            // without MY own EMCON shrinking it — only the TARGET's loudness should move that number.
            double activity = activityOverride >= 0 ? activityOverride : target.ActivityMultiplier;
            foreach (var emdat in target.EmittedEMSpectra)
            {
                double r = RangeForSignal(emdat.Magnitude * activity, threshold);
                if (r > bestRange) bestRange = r;
            }
            foreach (var emdat in target.ReflectedEMSpectra)
            {
                double r = RangeForSignal(emdat.Magnitude, threshold);
                if (r > bestRange) bestRange = r;
            }
            return bestRange;
        }

        /// <summary>
        /// Picks the ship's most capable sensor: the receiver with the lowest (best) BestSensitivity_kW among its
        /// installed receivers, skipping solar arrays (IsEnergyGen — they share the sensor code but aren't sensors).
        /// Reads the same InstanceAtributes cache the scan loop uses, so if the ship's receivers have been shot off
        /// (the grave rung empties that cache) this returns false and the ship has no reach to draw.
        /// </summary>
        public static bool TryGetBestReceiver(Entity entity, out SensorReceiverAtb best)
        {
            best = null;
            if (!entity.TryGetDataBlob<SensorAbilityDB>(out var ability))
                return false;
            double bestThreshold = double.PositiveInfinity;
            foreach (var atb in ability.InstanceAtributes)
            {
                if (atb == null || atb.IsEnergyGen)
                    continue;
                if (atb.BestSensitivity_kW > 0 && atb.BestSensitivity_kW < bestThreshold)
                {
                    bestThreshold = atb.BestSensitivity_kW;
                    best = atb;
                }
            }
            return best != null;
        }

        /// <summary>
        /// "A ship like me, running as I am now, I'd first detect at THIS range." Uses the ship's own signature
        /// (its <see cref="SensorProfileDB"/>) as the reference target against its own best receiver — a self-contained,
        /// magic-constant-free reach the UI can draw as a sensor ring without inventing a reference enemy. Because it
        /// reads the ship's live ActivityMultiplier, the ring SHRINKS when the ship goes Silent and GROWS when it
        /// lights its drive — making the dark-vs-loud EMCON lever visible. Returns 0 if the ship can't sense or has
        /// no signature.
        /// </summary>
        public static double SelfDetectionRange_m(Entity entity)
        {
            if (!TryGetBestReceiver(entity, out var receiver))
                return 0;
            if (!entity.TryGetDataBlob<SensorProfileDB>(out var profile))
                return 0;
            return DetectionRange_m(receiver, profile);
        }

        /// <summary>
        /// HOW FAR THIS SHIP CAN SEE ("sensor reach"). Its best receiver against a reference target as loud as
        /// itself at FULL activity (EMCON pinned to 1.0), so the number does NOT shrink when YOU go Silent — going
        /// dark hides you, it doesn't blind you. This is the honest reach the map ring should draw (distinct from
        /// detectability below). Returns 0 if the ship can't sense or has no signature reference.
        /// </summary>
        public static double SensorReachRange_m(Entity entity)
        {
            if (!TryGetBestReceiver(entity, out var receiver))
                return 0;
            if (!entity.TryGetDataBlob<SensorProfileDB>(out var profile))
                return 0;
            return DetectionRange_m(receiver, profile, activityOverride: 1.0);
        }

        /// <summary>
        /// HOW FAR THIS SHIP CAN BE DETECTED ("detectability"). Its LIVE emitted signature — scaled by what it's
        /// doing right now (EMCON posture × thrust × weapons heat, via <see cref="SensorProfileDB.ActivityMultiplier"/>)
        /// — against a receiver as good as its own. SHRINKS on Silent, GROWS running hot: the "how loud am I, and
        /// how does that change with what I'm doing" number. (Same value as <see cref="SelfDetectionRange_m"/>,
        /// named for the detectability reading so callers don't conflate it with sensor REACH.)
        /// </summary>
        public static double DetectabilityRange_m(Entity entity) => SelfDetectionRange_m(entity);

        /// <summary>
        /// The ship's current emitted-signature activity scale (EMCON posture × heat): 1.0 = as-designed; &lt;1 =
        /// quieter (Silent / coasting); &gt;1 = running hot (thrusting / firing). The driver behind
        /// <see cref="DetectabilityRange_m"/> — the UI shows it so the player sees WHY their detectability moved.
        /// Returns 1.0 if the ship has no signature profile.
        /// </summary>
        public static double CurrentActivityMultiplier(Entity entity)
            => entity.TryGetDataBlob<SensorProfileDB>(out var p) ? p.ActivityMultiplier : 1.0;

        /// <summary>
        /// "How far can THIS ship pick up THAT specific target?" — the honest version of the self-ring, against a
        /// real enemy instead of a ship-like-me reference. Uses the detector's best receiver and the target's actual
        /// signature, so a LOUD target (running hot) yields a bigger range than the same target gone Silent. Drawn
        /// as a "detectability bubble" around the target: if the detector is inside it, it sees the target. Returns
        /// 0 if the detector can't sense (no/destroyed receivers) or the target has no signature.
        /// </summary>
        public static double DetectionRangeAgainst(Entity detector, Entity target)
        {
            if (detector == null || target == null)
                return 0;
            if (!TryGetBestReceiver(detector, out var receiver))
                return 0;
            if (!target.TryGetDataBlob<SensorProfileDB>(out var profile))
                return 0;
            return DetectionRange_m(receiver, profile);
        }

        /// <summary>
        /// Probibly only needs to be done at star creation, unless we do funky stuff like change a stars temprature and stuff.
        /// </summary>
        /// <returns>The star emmision sig.</returns>
        /// <param name="starInfoDB">Star info db.</param>
        /// <param name="starMassVolumeDB">Star mass volume db.</param>
        internal static SensorProfileDB SetStarEmmisionSig(StarInfoDB starInfoDB, MassVolumeDB starMassVolumeDB)
        {

            var tempDegreesC = starInfoDB.Temperature;
            var kelvin = tempDegreesC + 273.15;
            double b = 2898000; //Wien's displacement constant for nanometers.
            var wavelength = b / kelvin; //Wien's displacement law https://en.wikipedia.org/wiki/Wien%27s_displacement_law
            var magnitudeInKW = starInfoDB.Luminosity * 3.827e23; //tempDegreesC / starMassVolumeDB.Volume_km3; //maybe this should be lum / volume?

            //-300, + 600, semi arbitrary number pulled outa my ass from 10min of internet research.
            EMWaveForm waveform = new EMWaveForm(wavelength - 300, wavelength, wavelength + 600);


            var emisionSignature = new SensorProfileDB() {

            };
            EMData emdata = new EMData()
            {
                WaveForm = waveform,
                Magnitude = magnitudeInKW,
            };
                
            emisionSignature.EmittedEMSpectra.Add(emdata);

            return emisionSignature;
        }

        /// <summary>
        /// probibly only needs to be done at entity creation, once the bodies mass is set.
        /// some of this should be taken out and done with reflective.
        /// </summary>
        /// <returns>The emmision sig.</returns>
        /// <param name="sysBodyInfoDB">Sys body info db.</param>
        /// <param name="massVolDB">Mass vol db.</param>
        internal static void PlanetEmmisionSig(SensorProfileDB profile, SystemBodyInfoDB sysBodyInfoDB, MassVolumeDB massVolDB)
        {
            var tempDegreesC = sysBodyInfoDB.BaseTemperature;
            var kelvin = tempDegreesC + 273.15;
            double b = 2898000; //Wien's displacement constant for nanometers.
            var wavelength = b / kelvin; //Wien's displacement law https://en.wikipedia.org/wiki/Wien%27s_displacement_law


            var cop = 5.670373E-8;
            var emisivity = 1 - sysBodyInfoDB.Albedo;
            var j = emisivity * cop * Math.Pow(kelvin, 4);
            var surfaceArea = 4 * Math.PI * massVolDB.RadiusInM * massVolDB.RadiusInM;
            var magnitude = j * surfaceArea * 0.001;

            //-400 & +600, semi arbitrary number pulled outa my ass from 0min of internet research.
            EMWaveForm waveform = new EMWaveForm(wavelength - 400, wavelength, wavelength + 600);

            EMData emdata = new EMData()
            {
                WaveForm = waveform,
                Magnitude = magnitude,
            };
            profile.EmittedEMSpectra.Add(emdata);//TODO this may need adjusting to make good balanced detections.
            profile.Reflectivity = sysBodyInfoDB.Albedo;
        }


        /// <summary>
        /// TODO: Refactor: each entity (or parent) should have thier own Random based off a seed.
        /// all random should be psudo random and threadsafe. or at least, we need to be aware of higher level randoms which can be called by any thread. ie avoid this.
        /// some random should be able to be figured out by remote clients, and some not.
        /// </summary>
        /// <returns>The sigmoid.</returns>
        /// <param name="acurateNumber">Acurate number.</param>
        /// <param name="acuracy">Acuracy.</param>
        public static double RndSigmoid(double acurateNumber, PercentValue acuracy, Random rng)
        {
            double sigmoid = Math.Tanh(acuracy * 10);
            double maxRand = Rnd(1, sigmoid, rng);
            double result = Rnd(acurateNumber + (acurateNumber * maxRand), acurateNumber - (acurateNumber * maxRand), rng);
            return result;
        }

        public static double Rnd(double max, double min, Random rng)
        {
            return rng.NextDouble() * (max - min) + max;
        }
    }
}
