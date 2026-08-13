# Three aircraft, translated — Apache · F-22 · LAAT

**As of 2026-08-08.** The first three units built against the **locked atmospheric layer**
(`docs/ground/ATMOSPHERIC-LAYER-DESIGN.md`). Each is deep-researched from real/canon specs, translated through the
component designer + Entity Assembler, and min-maxed to preserve its signature. All three are **clickable presets** in the
Entity Assembler (`⬢ AH-64 Apache`, `⬢ F-22 Raptor`, `⬢ LAAT Gunship`).

> **The one caveat up front — the whole air layer is DESIGN-ONLY.** Per the locked design, an aircraft is a **ground unit
> at an altitude band + a Flight drive**, and none of that runs in the engine yet. So **flight itself, the altitude band,
> `EngageBands` (air-vs-surface targeting), stealth-as-undetectability, fire-and-forget-from-defilade, over-horizon
> targeting, the troop-drop, and sortie endurance are all flagged PENDING** — exactly the way the Sazabi's flight, funnels,
> and psycho-frame are. On the ground today these read as ground units at the **0.42 evasion ceiling** (their true agility
> — the F-22's ~0.85 — is the ground-host cost, flagged). What IS live: firepower, toughness, detection, crew, the power
> gate, and the full build + run cost.

---

## The three side by side (the assembler's own `compute()`)

| Unit | Band | **Firepower** | **Toughness** | Evasion | **Detect** | Crew | Mass / 6000 | March | The identity it's tuned to |
|---|:--:|:--:|:--:|:--:|:--:|:--:|:--:|:--:|---|
| **AH-64 Apache** | Low (hover) | 2.12 | **97** | 0.42 | 300 | 2 | 1,770 (30%) | 30 | Hover-loiter **anti-armour** — toughest of the three (hard to one-shot) |
| **F-22 Raptor** | High | 1.56 | 62 | 0.42 | **400** | 1 | 1,945 (32%) | 105 | **See-first / shoot-first** — highest detection, fastest, light |
| **LAAT Gunship** | Low | **2.32** | 79 | 0.42 | 0 | 4 | 2,614 (44%) | 66 | **Over-gunned** dropship — most firepower, glass cannon, carries troops |

The read is honest to each aircraft: the Apache is the **tank-killer that survives** (top toughness, Hellfire punch), the
F-22 **sees furthest and moves fastest** (the air-dominance sensor platform), the LAAT **out-guns both** but is a **glass
cannon** with no radar (it wins by volume of fire once engaged, not by seeing first).

---

## 1 · AH-64 Apache Longbow — the hunt-from-hiding tank-killer

**Role:** a two-crew, all-weather attack helicopter that **loiters over the battlefield for hours** and destroys armour
from standoff with fire-and-forget missiles cued by a **hide-behind-terrain mast radar.** Defining capabilities (any
translation must keep): **(1)** hunt-from-hiding — peek the mast radar over a ridge, classify 128 targets, volley 16
Hellfires, re-hide; **(2)** hover-and-loiter anti-armour CAS (persistence + precision, not speed); **(3)** own-the-night
all-weather sensor dominance.

*Canon:* ~15 m, 10.4 t max, 2× T700 (~3,960 shp, **293 km/h — slow**, 2.5–3 hr loiter, true VTOL). M230 30 mm chain gun
(1,200 rds) · up to 16 AGM-114 Hellfire (tandem-HEAT tank-killer) · Hydra 70 rockets · APG-78 Longbow **mast** radar ·
M-TADS/PNVS · armoured cockpit + crashworthy airframe.

**The build** (a realistic *mixed* pylon loadout, not all maxes at once):

| Capability | Component | Count | Door | State |
|---|---|:--:|---|:--:|
| Armoured two-crew rotor airframe | **Rotorcraft Gunship Airframe** `af_rotor` | 1 | Chassis | ✅ ground · 🟠 flight/hover/band PENDING |
| Turboshaft + rotor (VTOL, hover) | **Turboshaft + Rotor** `ap_rotor` | 1 | Propulsion | ✅ speed · 🟠 flight/hover PENDING |
| 30 mm chain gun (danger-close CAS) | **M230 Chain Gun** `aw_chaingun` | 1 | Weapons | ✅ firepower/pen · 🟠 EngageBands PENDING |
| Tank-killer missiles | **AGM-114 Hellfire** `aw_hellfire` | 8 | Weapons | ✅ firepower + **high pen 20** · 🟠 fire-and-forget-from-defilade PENDING |
| Area suppression | **Hydra 70 Rocket Pod** `aw_rockets` | 2 | Weapons | ✅ area firepower |
| Mast fire-control radar | **Longbow Mast FCR** `as_longbow` | 1 | Sensors | ✅ detect 300 · 🟠 hunt-from-hiding PENDING |
| Thermal targeting / own-the-night | **Sensor Fusion / TADS** `ae_fusion` | 1 | Enhancers | ✅ caliber ×1.2 · 🟠 first-look/MUM-T PENDING |
| Cockpit armour + crashworthy | **Composite Plating** `ga_plating` | 2 | Defense | ✅ toughness |

**Computed:** firepower **2.12** · toughness **97** (the highest — its armour is the point) · detect 300 · crew 2 · march
30 km/h (slowest) · **build 5,790 pts · 140k credits · 1,852 t · RP 44.** Power: no draw (kinetic/explosive weapons).

**Min-max — persistence + standoff punch, built to survive.** Light on its host (30%) so it stays cheap and mobile;
**Hellfire-dense** (8 rails → firepower dominated by high-penetration anti-armour, ×1.2 from TADS); and unlike the F-22
and LAAT it is **deliberately armoured** (2 plates → toughness 97, "hard to one-shot," graceful degradation). It is the
one of the three you *keep on station*, not the one you throw.

**🟠 PENDING (the Apache-ness the engine can't read yet):** hover/loiter, flight, the **hunt-from-hiding** peek-and-volley
from defilade, MUM-T drone teaming, and the 2.5–3 hr loiter (which is **emergent** from fuel ÷ burn once the sortie model
is built).

---

## 2 · F-22 Raptor — see-first, shoot-first air dominance

**Role:** the premier air-dominance fighter — a stealthy, supercruising sensor platform built to detect the enemy first,
shoot first, and kill first beyond visual range, before it is ever seen. Defining capabilities: **(1)** see-first/shoot-
first (stealth + LPI AESA + sensor fusion → very low detectability, very long detection, first-strike); **(2)** supercruise
(a *standing* speed/energy advantage, not a boost); **(3)** BVR missile kill power (6× fire-and-forget AMRAAM) + extreme
thrust-vectoring agility.

*Canon:* 18.9 m, ~38 t max, 2× F119 (~70,000 lbf AB, **Mach 2.25**, supercruise ~Mach 1.8 without afterburner,
thrust-vectoring, T/W >1.25). Internal (stealth) carriage: 6× AIM-120 AMRAAM · 2× AIM-9 Sidewinder · M61A2 20 mm Vulcan
(480 rds). APG-77 AESA (LPI) + sensor fusion. RCS ≈ a marble. Single seat.

**The build:**

| Capability | Component | Count | Door | State |
|---|---|:--:|---|:--:|
| Single-seat stealth airframe | **Stealth Fighter Airframe** `af_jet` | 1 | Chassis | ✅ ground · 🟠 flight/band/**stealth** PENDING |
| Afterburning turbofan + thrust-vectoring | **Turbofan + Thrust-Vectoring** `ap_jet` | 1 | Propulsion | ✅ speed (fastest) · 🟠 flight/supercruise PENDING |
| BVR air-to-air kill weapon | **AIM-120 AMRAAM** `aw_amraam` | 6 | Weapons | ✅ firepower · 🟠 **EngageBands: air** + BVR-first-shot PENDING |
| Short-range dogfight missile | **AIM-9 Sidewinder** `aw_sidewinder` | 2 | Weapons | ✅ firepower · 🟠 air PENDING |
| Gun (wall of rounds) | **M61 Vulcan** `aw_vulcan` | 1 | Weapons | ✅ firepower (very high saturation) |
| AESA radar (see-first) | **AESA Radar (APG-77)** `as_aesa` | 1 | Sensors | ✅ detect **400** (highest) · 🟠 LPI/fusion PENDING |
| Sensor fusion (first-shot) | **Sensor Fusion** `ae_fusion` | 1 | Enhancers | ✅ caliber ×1.2 · 🟠 first-look initiative PENDING |
| Light airframe skin | **Composite Plating** `ga_plating` | 1 | Defense | ✅ toughness (kept low on purpose) |

**Computed:** firepower **1.56** (AMRAAM-dominated) · toughness **62** (light — survives by not being hit) · detect **400**
(the highest of the three) · crew 1 · march 105 km/h (fastest) · **build 6,325 pts · 186k credits · 1,935 t · RP 52.**

**Min-max — the sensor + the missile, everything else minimal.** The identity is *initiative*: it gets the **highest
detection** (AESA 400 — it finds the fight first) and a **deep AMRAAM magazine** (6 → BVR kill power), on the **fastest,
lightest** frame with only one plate. It is expensive (186k, most of the three) and elite — a scarce high-value asset, not
a mass unit, which reads right.

**🟠 PENDING (and here it hurts most, because it IS the plane):** **stealth / low-observability** — being *unseen* — has
no mock reader, so the F-22's whole "shoot from where they can't see you" advantage is invisible and its true ~0.85 agility
reads as the 0.42 ground ceiling; plus flight, supercruise-as-standing-energy, and the BVR first-shot. The engine's
**detection/EMCON layer** is what would finally make this unit what it is.

---

## 3 · LAAT Gunship — the over-gunned glass-cannon dropship

**Role:** an armoured repulsorlift dropship that flies **30 clone troopers into a hot LZ under its own covering fire** —
troop insertion and suppression are the *same action.* Defining capabilities: **(1)** insert troops into a hot LZ under
covering fire; **(2)** a **flying gun-platform with many gunner turrets** (over-gunned for its size); **(3)** CAS + troop
delivery fused in one airframe. Corollary to preserve — **no combat shields, thin armour = fragile glass cannon.**

*Canon:* ~17.4 m, 620 km/h, low-altitude, 4 crew, 30 troops. **4 composite-beam laser turrets** (2 manned ball + 2 auto) ·
**3 anti-personnel laser turrets** · **2 dorsal mass-driver missile launchers** (over-horizon anti-armour) · **8 ventral
air-to-air rockets.** No deflector shields; limited superdispersive armour.

**The build** (the whole over-gunned fit — that density IS the point):

| Capability | Component | Count | Door | State |
|---|---|:--:|---|:--:|
| Four-crew repulsorlift gunship hull | **Assault Gunship Airframe** `af_gunship` | 1 | Chassis | ✅ ground · 🟠 flight/band/troop-drop PENDING |
| Repulsorlift + turbine (hover, 620 km/h) | **Repulsorlift + Turbine** `ap_repulsor` | 1 | Propulsion | ✅ speed · 🟠 flight/hover PENDING |
| Power for the lasers | **Gunship Power Generator** `af_generator` | 1 | Power | ✅ supply 12 |
| Main gun turrets (air + ground) | **Composite-Beam Laser Turret** `aw_beamturret` | 4 | Weapons | ✅ firepower · 🟠 EngageBands PENDING |
| LZ suppression | **Anti-Personnel Laser Turret** `aw_aplaser` | 3 | Weapons | ✅ firepower |
| Anti-armour missiles | **Mass-Driver Launcher** `aw_massdriver` | 2 | Weapons | ✅ firepower + **high pen 18** · 🟠 over-horizon PENDING |
| Anti-air rockets | **Light Air-to-Air Rocket** `aw_aarocket` | 8 | Weapons | ✅ firepower · 🟠 air PENDING |
| 30-trooper assault cabin | **Assault Troop Bay** `ap_troopbay` | 1 | Logistical | 🟠 transport PENDING (no surface troop-carry reader) |
| Light armour (deliberately thin) | **Composite Plating** `ga_plating` | 1 | Defense | ✅ toughness (kept low — glass cannon) |

**Computed:** firepower **2.32** (the highest — 17 weapon mounts) · toughness **79** (moderate — but *low relative to its
firepower*, the glass-cannon signature) · **detect 0** (no search radar — gunner-directed) · crew 4 · power OK (draw 1.12 ≤
supply 12) · march 66 km/h · **build 7,642 pts · 139k credits · 2,611 t · RP 42.**

**Min-max — max guns, min protection, carry the troops.** Everything about it says *over-gunned*: it has the **most
firepower** of the three from sheer mount count, powers the lasers with a generator, and hauls a troop bay — but is
**deliberately under-armoured** (one plate; no shields, per canon) so its toughness is low *for its firepower.* Note the
**detect 0**: unlike the radar-carrying Apache and F-22, the LAAT is a gun platform, not a sensor platform — it doesn't win
the see-first race, it wins by walking in and unloading. That's faithful.

**🟠 PENDING:** flight/hover, the **hot-LZ troop-drop** (the deployable-squadron model — same flag as the AT-TE's bay),
over-horizon targeting, and the no-shield fragility as a *deliberate* balance trait rather than a bug.

---

## The 21 new atmospheric parts

Added to the assembler catalog (an `ATMOSPHERIC AIRCRAFT PARTS` block), all `hosts:['ground']`, all air-layer behaviour
flagged PENDING in each part's honesty text:

- **Chassis (3):** `af_rotor` (rotorcraft) · `af_jet` (stealth fighter) · `af_gunship` (assault gunship).
- **Propulsion (3):** `ap_rotor` (turboshaft+rotor) · `ap_jet` (turbofan+thrust-vectoring) · `ap_repulsor` (repulsorlift+turbine).
- **Power (1):** `af_generator` (feeds the LAAT's lasers).
- **Weapons (10):** `aw_chaingun` · `aw_hellfire` · `aw_rockets` · `aw_amraam` · `aw_sidewinder` · `aw_vulcan` ·
  `aw_beamturret` · `aw_aplaser` · `aw_massdriver` · `aw_aarocket`.
- **Sensors (2):** `as_aesa` (F-22) · `as_longbow` (Apache mast radar).
- **Enhancers (1):** `ae_fusion` (sensor fusion / TADS — caliber ×1.2, the one LIVE "first-shot" slice).
- **Logistical (1):** `ap_troopbay` (LAAT 30-trooper bay, transport PENDING).

**Verified:** the assembler script parses and renders clean (no throw), all three presets compute with no errors, no bad
component ids, within budget/volume, power-balanced; the existing ground presets (Sazabi/Clone/AT-TE/Carnifex) still
compute unchanged (regression clean).

---

## Sources

- **AH-64 Apache** — GlobalSecurity/Army-Technology/FAS specs · GE T700 datasheet · M230 chain gun · AGM-114 Hellfire
  (Air&Space Forces, CSIS) · Hydra 70 · AN/APG-78 Longbow FCR (Northrop Grumman) · M-TADS/PNVS "Arrowhead."
- **F-22 Raptor** — GlobalSecurity F-22 specs/weapons · P&W F119 · AN/APG-77 AESA (Northrop Grumman) · AIM-120/AIM-9 ·
  M61 Vulcan · RCS ("bumblebee/marble") references.
- **LAAT** — Wookieepedia (LAAT/i Canon + Legends, LAAT/c) · FFG / Saga Edition / SWRPGGM / Worldofjaymz RPG stat wikis.

*Design/reference only — no franchise assets; the aircraft are translation test cases built from the generic atmospheric
parts added to the assembler catalog. Air-layer behaviour is design-only per `docs/ground/ATMOSPHERIC-LAYER-DESIGN.md`.*
