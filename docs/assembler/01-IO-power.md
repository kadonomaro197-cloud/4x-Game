# 01-IO-POWER — Power, re-derived — the trade was already in the codebase
> Source: `docs/Actual HTMLs Of designers/powerderived.html` · Component-designer door · Census (Phase 1 draft, unverified)

## What this door builds
This is the **power-plant door**: it produces every energy component a ship, station, or colony can carry — the five templates that already ship in `energy.json` (Reactor, RTG, Steam turbine, Solar array, Battery bank). In plain terms, it answers one question — *where does the energy come from, and how long do you want it to last* — and hands the sim a generator that makes kilowatts (or, for the battery, holds kilojoules).

The shape is **two forced choices, then a small set of sliders that changes with them**. First choice: the *job* (Generate by burning fuel · Collect starlight · Store it). If you chose Generate, a second choice picks *which burner* (Reactor · RTG · Steam turbine). Each generator then exposes only the dials it really has — the reactor has three live dials (mass, fuel load/lifetime, burn rate), the RTG and turbine two each, solar two, the battery one. The door's whole finding is that the "output ↔ endurance" trade it needed was already written into the RTG (`power × lifetime = const × mass`); the reactor still lacks it (slice P1), and the turbine was ruled not to need it.

## A. INPUTS — every control the user can touch

### A.1 Forced choices (chips / radio buttons)
| Control group | Options (exact button labels) | What each option selects (JS data object key / render branch) | Default option |
|---|---|---|---|
| Job — "Where does the energy come from" (`.chip`, `data-v`) | `Generate — burn something` | `job='burn'` → shows generator chips + `w2` + `sigbox`; runs the `if(job==='burn')` branch | ✅ default (`aria-pressed="true"`, `let job='burn'`) |
| Job (same group) | `Collect — starlight` | `job='collect'` → hides generator chips; shows `w3` (Bandwidth); runs `else if(job==='collect')` (solar array) | — |
| Job (same group) | `Store — hold it` | `job='store'` → hides generator chips & `w2`/`w3`; runs final `else` (battery bank) | — |
| Generator — "What kind of generator" (`.gchip`, `data-g`; only shown when `job==='burn'`) | `Reactor 🔴` | `gen='reactor'` → shows `w3` + `fixwrap`; `mass=1500+d1*235`, `out=50*mass*ove` | ✅ default (`aria-pressed="true"`, `let gen='reactor'`) |
| Generator (same group) | `RTG ✅` | `gen='rtg'` → hides `w3`/`fixwrap`; `mass=1000+d1*240`, `Power=Fuel×Eff×FuelCons` | — |
| Generator (same group) | `Steam turbine 🔴` | `gen='turbine'` → hides `w3`/`fixwrap`; `mass=2000+d1*4980`, core↔generator split | — |

Selecting a chip or gchip resets the sliders to that job/generator's **STOCK** back-solved positions (`STOCK={reactor:{d1:0,d2:50,d3:33}, rtg:{d1:0,d2:50}, turbine:{d1:0,d2:50}, collect:{d1:50,d3:0}, store:{d1:0}}`) so each job "opens on its real shipped template."

### A.2 Sliders
All three sliders share the raw HTML range `min="0" max="100"`; the *real* units come from the per-branch remap in `render()`. Which sliders are visible is set by `show()`: `w2` shown only when `job==='burn'`; `w3` shown when `job==='collect' || (job==='burn' && gen==='reactor')`.

| Slider label | element id | Range (min–max in REAL units) | Default value | Mapping (fn) | Exact formula the slider feeds (quoted JS) | The number it ultimately sets |
|---|---|---|---|---|---|---|
| **Mass** (reactor) | `d1` | 1,500 – 25,000 kg | `value="2"` (HTML) / STOCK `d1:0` | linear | `mass  = 1500 + d1*235;` | Component `Mass` → `Power Output = 50*mass*ove` |
| **Mass** (RTG) | `d1` | 1,000 – 25,000 kg | STOCK `d1:0` | linear | `mass=1000+d1*240;` | `Mass`; `fuel=mass*0.5` |
| **Mass** (turbine) | `d1` | 2,000 – 500,000 kg | STOCK `d1:0` | linear | `mass=2000+d1*4980;` | `Mass`; `coreMass=mass*cvg*0.01` |
| **Panel area** (collect) | `d1` | 1 – 10,000 m² (shipped=100) | STOCK `d1:50` | **logmap** (`Math.pow`) | `area=100*Math.pow(100,(d1-50)/50);` | panel area → `out=area*bestEff*1.361`; `mass=area*2.0` |
| **Mass** (store/battery) | `d1` | 2,000 – 25,000 kg | STOCK `d1:0` | linear | `mass=2000+d1*230;` | `Mass`; `store=mass*500*T_BATT` |
| **Output ↔ endurance** / "Fuel load — lifetime" (reactor) | `d2` | ~876 – 87,600 h (label says 1 h – 100 yr) | `value="9"` (HTML) / STOCK `d2:50` | **logmap** (`Math.pow`) | `lifeH = 8760*Math.pow(10,(d2-50)/50);` | `Lifetime` (seconds) → `LocalFuel = maxUse × Lifetime`; fuel-load / fissile bill |
| **Output ↔ endurance** (RTG) | `d2` | 1 – 25 yr (shipped=5) | STOCK `d2:50` | **logmap** (`Math.pow`) | `const lifeY=5*Math.pow(5,(d2-50)/50);` | `Lifetime` → `fc=0.001/lifeY`, `out=fuel*eff*fc` |
| **Core ↔ generator** (turbine) | `d2` | 30 – 70 % core, step 10 | STOCK `d2:50` | linear+round | `const cvg=30+Math.round(d2*0.04)*10;` | mass split core/generator → `ResourceCost` (graphite vs tungsten) |
| **Bandwidth** (collect) | `d3` | 300 – 600 nm | (no STOCK entry; falls to HTML `33`) | linear | `const bw=T_PANEL_BW*0.5 + d3*(T_PANEL_BW*0.5/100);` | panel band width → `bestEff=T_PANEL_EFF*((T_PANEL_BW*0.5)/bw)` |
| **Output ↔ economy** (reactor; labeled "✅ NEW") | `d3` | 0.5 – 2.0 ×, step 0.1 | `value="33"` (HTML) / STOCK `d3:33` | linear+round | `const ove = Math.round((0.5+d3*0.015)*10)/10;` | burn-rate multiplier OvE → `out=50*mass*ove`, `kgPerSec=out*3.125e-11*ove` |

Notes: default `d2` HTML value is `9` and `d3` is `33`, but on any chip/gchip click the STOCK map overwrites them (reactor opens `d2:50, d3:33`; the `render()` at load also runs with STOCK-untouched HTML values until the first click).

### A.3 Toggles / checkboxes / presets / secondary choices
| Control | id | Effect when active |
|---|---|---|
| "Apply the RTG's law to this generator **(still proposed — slice P1)**" | `fix` (checkbox) | Only shown when `job==='burn' && gen==='reactor'` (`show('fixwrap',...)`). When checked: `out = 50*mass*ove*(8760/lifeH)` (anchors output to the RTG's `output×lifetime=const` law, byte-identical at the shipped 8760 h), and relabels a3 to `Output × lifetime — FIXED`. Unchecked: output stays linear in mass and a3 reads `Output × lifetime … ▲ climbs` (red). This is a **PROPOSED / unbuilt** dial. |

## B. OUTPUTS — every readout the panel produces
The six `.num` boxes (a1–a4, sig, mass) plus the four `.mus` consumer boxes (leave/warp/laser/seen). Labels change per branch; all listed.

| Output label | Units | Formula (quoted/derived JS) | Honesty marker in panel | Sim variable it writes | Consumer system that reads it | Build state |
|---|---|---|---|---|---|---|
| Sustained output (burn) | kW | `out=50*mass*ove` (reactor) · `out=fuel*eff*fc` (RTG) · `out=coreOut*0.8` (turbine) | "Sustained output — **emergent**" | `PowerOutputMax` / `EnergyGenAbilityDB.TotalOutputMax` | warp, ground lasers, colony `SustenanceProcessor` | **live** |
| Output at 1 AU (collect) | kW | `out=area*bestEff*1.361` (1.361 = "solar constant kW/m², illustrative") | "Output at 1 AU — **emergent**" | `EnergySolarGenerationAtb` output → `TotalOutputMax`, attenuated `AttenuatedForDistanceList(...)` | same power consumers | **live** |
| Energy storage (store) | kJ | `store=mass*500*T_BATT` (`T_BATT=1`; 500 kJ/kg) | "Energy storage — **emergent**" | `EnergyStoreMax` (via `EnergyStoreAtb`) | warp departure buffer | **live** |
| Endurance (burn) | time (`hrs()`) | `lifeH` per branch | "Endurance — **set**" | `Lifetime` → `LocalFuel = maxUse × Lifetime` (`EnergyGenerationAtb.cs:60`) | `EnergyGenProcessor` burn + `EnableFuelExhaustion` gate | **live** (fuel gate BUILT; flag default off, client arms it) |
| Best efficiency (collect) | % | `bestEff=T_PANEL_EFF*((T_PANEL_BW*0.5)/bw)` | "Best efficiency — **emergent**" | efficiency term inside `EnergySolarGenerationAtb` | solar output | **computed** (feeds live output) |
| Per kg (store) | kJ | fixed `500 kJ` | "Per kg — **fixed**" | `EnergyStoreAtb` per-kg constant | storage total | **computed** |
| a3 — Output × lifetime (reactor) | kW·h | `fmt(out*lifeH)` (with `▲ climbs`, or `— FIXED` if `fix`) | "**climbs**" (red) / "**FIXED**" if box ticked | none — comparison of `out`×`Lifetime` | — (design-time readout) | **computed** (the `fix` branch is **engine-pending** — waits on slice P1, the RTG-law rewrite of reactor output) |
| a3 — Output × lifetime (RTG) | (÷ mass shown) | `fmt(out*lifeY)+' (÷ mass: '+(out*lifeY/mass).toFixed(3)+')'` | "**INVARIANT**" | none — demonstrates `power×lifetime=const×mass` invariant | — | **computed** |
| a3 — Fuel duration (turbine) | time | `hrs(lifeH)` where `lifeH=4e8/3600` (constant) | "Fuel duration — **FIXED BY DESIGN**  ◄ at every setting" | `FuelDuration = FuelMass×40e9/CoreOutput = 4e8 s` | endurance | **computed** (constant 12.7 yr, ruled as-is) |
| a3 — Coverage × efficiency (collect) | — | `(bw*bestEff).toFixed(1)` | "Coverage × efficiency — **fixed**" | none — the band zero-sum readout | — | **computed** |
| a3 — Warp departures (store) | count | `(store/WARP_CREATE).toFixed(2)` | "Warp departures it holds" | `EnergyStoreMax / WARP_CREATE` | warp departure | **computed** |
| a4 — Fissile fuel to build (reactor) | kg | `fuelKg=kgPerSec*lifeH*3600` where `kgPerSec=out*3.125e-11*ove` | value colored g/a | `ResourceCost` → `fissile-fuels = Fuel Consumption × 3600 × Lifetime` | industry / build cost | **live** |
| a4 — Crew it needs (RTG) | count | fixed `0` (was authored `1`) | "0 ✅ nobody" | `CrewReq` | ship crew requirement / colony infrastructure demand | **live** (fixed to 0) |
| a4 — Tungsten ↔ graphite (turbine) | kg | `graphite=coreMass*0.15`, `tungsten=genny*0.05` | value g | `ResourceCost` (tungsten, graphite, nickel, etc.) | industry bill of materials | **live** |
| a4 — Can it go on a colony? (collect) | text | `'🔴 NO — MountType 1'` (note: HTML prose elsewhere says now FIXED to name `PlanetInstallation`) | 🔴 red | `MountType` / `ComponentMountType` (`PlanetInstallation`, `Station`) | colony/station install eligibility | **live** (prose: FIXED; the live JS readout still prints the old "NO") |
| a4 — A reactor already gives you (store) | text | `'its kW, as kJ'` | amber | `EnergyStoreMax += PowerOutputMax` (`EnergyGenerationAtb.cs:65,70`) | storage total (the kW→kJ accident) | **live** (flagged defect) |
| Signature (burn) | signature magnitude | `sig=out*0.1*mass` (turbine uses `sig=coreOut`) | "Signature — **1700 K**" | `SensorSignatureAtb` (1700 K) | detection / who-shoots-first | **live** |
| Mass | kg | `fmt(mass)` per branch | (plain fixed box) | `Mass` | Chassis door gate, ship design | **live** |
| Can it leave? (`m_leave`) | yes/no | `storedKJ>=WARP_CREATE` (`storedKJ=out` for burn; `store` for battery; `WARP_CREATE=3342250 kJ`) | `<s>stored ≥ bubble creation</s>` | `WarpMoveCommand:258` departure gate | warp drive | **live** |
| Warp endurance (`m_warp`) | text | `out>WARP_SUSTAIN` (`WARP_SUSTAIN=3179.31 kW`) | `<s>sustain charged per second</s>` | warp sustain draw | warp drive | **live** |
| Ground lasers powered (`m_laser`) | count | `Math.floor(out*1000/GROUND_LASER_W)` (`GROUND_LASER_W=250000`) | `<s>the supply gate</s>` | `WeaponSupply` ground energy-weapon gate | ground combat lasers | **live** |
| Seen from (`m_seen`) | × vs stock | `Math.sqrt(sig/BASE_SIG)` (`BASE_SIG=1.125e7`) | `<s>vs a stock reactor</s>` | derived from `SensorSignatureAtb` vs observer | detection (opposing sensors) | **emergent** (depends on the observer/battlefield) |

## C. CROSS-DOOR READS — values the panel shows but does NOT own
| Value | Owned by (which door) | Why this door displays it | Marker used |
|---|---|---|---|
| Warp bubble creation cost `WARP_CREATE=3342250 kJ` | Propulsion / Warp door ("the shipped 2 t Alcubierre drive") | To answer "Can it leave?" — whether stored energy ≥ bubble creation | `<s>stored ≥ bubble creation</s>` |
| Warp sustain draw `WARP_SUSTAIN=3179.31 kW` | Propulsion / Warp door | To answer "Warp endurance" — can output hold a bubble | `<s>sustain charged per second</s>` |
| Ground laser draw `GROUND_LASER_W=250000 W` | Weapons / Ground-combat door ("illustrative") | To show how many ground lasers this plant powers | `<s>the supply gate</s>` |
| Stock-reactor signature `BASE_SIG=1.125e7` | This door's own reference constant, but "Seen from" resolves against opposing **Sensors** | Detection decides who shoots first — shown to make the stealth trade legible | `<s>vs a stock reactor</s>` / "0.00× — silent" |
| Solar `bestEff` band math (`T_PANEL_EFF`, `T_PANEL_BW`) | **Sensors** door — `EnergySolarGenerationAtb` shares the `SensorReceiverAtb` waveform + `IsEnergyGen` overload | Solar absorption is band-matched the same way detection is; the Sensors band ruling changes solar output | prose: "runs on the sensor receiver code"; warnbox flags Sensors↔Power coupling |
| Colony power demand (`SustenanceProcessor:51` reads `egen.TotalOutputMax`) | Colonies / population-morale door | Shows the consumer for `TotalOutputMax` exists and is waiting | prose warnbox ("The consumer is built. The producer is unbuildable.") |
| Tech levels `T_COND=1, T_BATT=1, T_PANEL_EFF=0.2, T_PANEL_BW=600` | Research/Tech door (level-0 tech) | Feed the efficiency/capacity formulas | inline comment "// level-0 tech" |

## D. NOTES / DISCREPANCIES

- **PROPOSED / engine-pending:** The `fix` checkbox ("Apply the RTG's law … still proposed — slice P1") is the only unbuilt dial. It waits on the reactor's output being rewritten to `output×lifetime=const×mass` (currently `Power=50×Mass`, "strictly linear in mass"). Until then it is a preview only.
- **Live JS vs. prose disagree on the solar mount fix.** The prose bands say the solar array's mount is **FIXED** — now names `ShipComponent, ShipCargo, PlanetInstallation, Station` so "a colony can build a power plant." But the live `collect` render branch still hard-prints `set('v_a4','🔴 NO — MountType 1')` in red. The interactive readout was not updated to match the claimed fix; treat the prose (energy.json fixed) as the current state, the JS readout as stale display text.
- **The `d2` reactor lifetime range is inconsistent between formula and labels.** `lifeH = 8760*Math.pow(10,(d2-50)/50)` spans ~876 h (d2=0) to ~87,600 h = 10 yr (d2=100), but the end-labels read "1 h" and "100 yr". Quote the formula, not the labels; the authored intent (per prose) is a 1 h … 100 yr / 876,000 h dial.
- **`Lifetime` costs fuel, not mass (flagged, not fixed).** The kill-card corrects an earlier claim: `Lifetime` is priced via `ResourceCost` (`fissile-fuels = Fuel Consumption × 3600 × Lifetime`) but adds **no mass** — "fuel with no weight" — and mass is the Chassis door's gate. The RTG avoids this by construction (`Fuel = Mass × 0.5`); reactor and turbine do not. Still open.
- **Reactor is silently a battery, units mismatched (open defect).** `EnergyGenerationAtb.cs:65,70` does `EnergyStoreMax[type] += PowerOutputMax` — adding **kW into a kJ store**, i.e. exactly one second of buffer by dimensional accident. Makes the battery bank partly redundant and any future storage balance pass fights a hidden term.
- **Reactor output linear in mass (open, slice P1).** `Power = 50 × Mass × OvE` — a bigger reactor is strictly better per kilogram; the "output↔endurance" trade the RTG carries is the missing law.
- **RTG power-density gap flagged for a calibration ruling**, not silently tuned — a developer decision, not a bug fix.
- **Sensors↔Power↔FTL-band coupling:** because `EnergySolarGenerationAtb` and `SensorReceiverAtb` share band-overlap code, one Sensors ruling (band-overlap test, an infrared receiver) also changes solar output — "one decision now touches Sensors, Power, and the deferred FTL band."
- **91 distinct part names reachable** (`partName()` composes scale × temper × noun with a de-dup so no two settings collide); footer/build-section prose cites "91 distinct part names."

## VERIFIED (Phase 2 — personal pass, 2026-08-02)
Checked the census against `powerderived.html` source.
- **Confirmed:** init `let job='burn', gen='reactor'` (`:344`); constants `WARP_CREATE=3342250, WARP_SUSTAIN=3179.31` (`:341`), `GROUND_LASER_W=250000` (`:342`), `BASE_SIG=1.125e7` (`:343`); `STOCK={reactor:{d1:0,d2:50,d3:33}, rtg:{d1:0,d2:50}, turbine:{d1:0,d2:50}, …}` (`:378`); formulas `mass=1500+d1*235` (`:394`), `lifeH=8760*Math.pow(10,(d2-50)/50)` (`:395`), `ove=Math.round((0.5+d3*0.015)*10)/10` (`:369,396`). Sliders d1 def **2**, d2 def **9**, d3 def **33** (`:242-244`); `fix` unchecked (`:245`).
- **No corrections.** The two internal HTML discrepancies the census flags are real and correctly recorded (they are HTML-internal, not census errors): (1) the solar `collect` branch still prints `🔴 NO — MountType 1` while the prose claims the mount was fixed; (2) the reactor `d2` lifetime formula spans ~876 h…10 yr while the end-labels read "1 h"/"100 yr".
- **Net for the Assembler:** Power's `TotalOutputMax` is the **supply** side of the ship's power gate — the number the Assembler's power-supply-vs-draw budget (rule #5) checks weapon/warp draw against. Its `SensorSignatureAtb` (1700 K) feeds the detection footprint (an emergent readout). Live throughout; the only engine-pending dial is the reactor RTG-law `fix` (slice P1).
