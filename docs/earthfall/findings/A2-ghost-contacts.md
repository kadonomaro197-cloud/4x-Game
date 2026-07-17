# A2 — GHOST CONTACTS (agent dossier, condensed)

## VERDICT
The live-glued-blip bug is ALREADY FIXED (commit 165d2c9 — blips are scan snapshots that age out). Log: 271 LAGGED / 0 FROZEN / 0 LIVE / 0 aged-out. The symptom persists because the fleet is GENUINELY continuously detected by the Earth homeworld colony's deliberate **200 Gm sensor horizon** (electronics.json:175, the 7th SensorReceiverAtb ctor arg = MaxDetectionRange_m, set on purpose 2026-07-14 as system-wide early warning) + DetectionSensitivityScale 1e6 (SensorTools.cs:391). UEF has no ships in-system (0 [EMCON]/[REACH] lines) → colony is the only detector. sig=0kW is a ROUNDED tiny-positive (prints "0.##"; contact in LAGGED and inside the signal>0 branch proves >0).

## Mechanism
1. Earth colony sensor: 200 Gm horizon; MFS fleet transits 129-147 Gm from star = within 200 Gm of Earth, above threshold by a hair.
2. Every hourly scan the refresh branch (SensorScan.cs:140-155) re-stamps LastDetection (:145) + SnapshotContactPosition (:154 → SensorPositionDB.SetSnapshot :47-52, honest snapshot of live position at scan time). Never reaches lost-track else-if (:176-198: Sensors→Memory flip :188, removal after ContactStaleSeconds=4h :48,:190-196).
3. Client steps 3600s/tick == ScanTime 3600s → one snapshot per frame → blip glides smoothly. fogLag = one scan of motion (0-34km).

## Who writes the log lines
Client SessionLog.cs → DetectionSnapshot (:325, ~3s heartbeat). [ENGINE] :334 (SensorScan.ScanCount). [DETECT] :354 — held = GetAllContacts().Count (:341); "detects D" = SensorContactExists per ship (:352) = **contacts-HELD, not seen-this-scan**. [DETECT-CONTACT] :382-385 (max 6): blipDistFromStar :375 (blip pos), fogLag :379 (blip vs real ship km), sig :385 (SignalStrength_kW "0.##").
SensorContact.cs: PositionSourceLabel :46-49 (Parent=LIVE / Sensors=LAGGED / Memory=FROZEN); SignalStrength_kW :54 ← SensorInfo.LatestDetectionQuality. DetonQuality = signal ABOVE sensitivity threshold (SensorTools.cs:193/197).

## Render path
With RequireDetectionToEngage on, SystemMapRendering.AddIconable (:321, guard :328-338) suppresses foreign mobile units' real icons; they draw ONLY as SensorContactIcon blips (_contactIcons :50, UpdateContactBlips :653) at SensorContact.Position. Render gate is correct.
**The misleading readout:** colony's green detection ring at SystemMapRendering.cs:874-878 is sized by DetectionRangeAgainst(colony, refTarget) against ONE reference ship — under-draws vs a louder/different target → genuinely-detected fleet renders OUTSIDE the drawn ring → "saw them before sensor range."

## Hostile gate shares the held-vs-seen conflation
EntityManager.GetFilteredEntities admits Hostile via EvaluateSensorContact → SensorContactExists (EntityManager.cs:763,782-785) — a FROZEN contact still counts "detected."

## Design intent (docs/combat/DETECTION-DESIGN.md)
Track table + last-known-lag is a KEEP feature (:26-30,:56); age-out shipped post-doc (:197 residual note). Current code implements the design; it simply never triggers here because detection is continuous.

## Ranked fixes
1. DESIGN CALL (developer): gate/curve the 200 Gm homeworld horizon (or signature/EMCON-gate it) so a cruising fleet isn't seen system-wide. One literal: electronics.json:175. Tension: set deliberately 2026-07-14.
2. CHEAP READOUT FIX: make the colony detection ring honest — size vs the actual contact (SystemMapRendering.cs:874) so a detected ship is never outside the ring. Likely resolves the perception.
3. Track-quality floor on blip refresh (SensorScan.cs:132 signal>0 → raise bar) / decouple blip cadence.
4. Separate held vs detected-this-scan in [DETECT] readout + hostile gate (test LastDetection recency; SessionLog.cs:352, EntityManager.cs:784).
5. Render gate + expiry: already implemented; expiry only matters after #1.

## Tests
SensorDetectionTests: point-blank contact :57; grave-rung blind :88; DetectedContact_BlipIsAScanSnapshot_NotLive :126 (LAGGED never LIVE — regression guard for 165d2c9). RangeReadoutTests: ring math.
GAPS: no test for (a) continuous colony-horizon long-range detection, (b) age-out firing, (c) Sensors→Memory flip.
