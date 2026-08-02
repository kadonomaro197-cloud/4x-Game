# 01-IO-SENSORS — Sensors, re-derived — one equation, five jobs, five broken dials
> Source: `docs/Actual HTMLs Of designers/sensorsderived.html` · Component-designer door · Census (Phase 1 draft, unverified)

## What this door builds
This one door builds every sensor-family component in the game: the passive sensor you listen with, the geological and gravitational surveyors you resolve a known target with, the fire-control director that holds a lock, the cloak that hides you, and the barrage jammer that blinds the enemy. Under the hood they are all the **same inverse-square attenuation line** — `signal = source × 1e6 ÷ (4π r²)` and its inverse `range = √(source × 1e6 ÷ 4π·threshold)` — read from a different side.

The shape is **one forced choice + a job-dependent set of sliders**. The single forced choice is *"what does this part do with the spectrum"* — Listen · Look · Track · Hide · Blind — and that choice decides which sliders even appear (Listen has four, Look and Hide have one, Track and Blind have two). The forced choice is really two axes folded into one chip row: *what you can see* (Listen · Look · Track, which lower **your** threshold) versus *what you emit* (Hide · Blind, which change **your** source or **their** threshold).

The HTML's whole argument is that four of the dials fail the acceptance test (write a sim variable AND cost something), and one broken line of C# in the band-matching gate lets a visible-light receiver detect an infrared reactor.

## A. INPUTS — every control the user can touch

### A.1 Forced choices (chips / radio buttons)
| Control group | Options (exact button labels) | What each option selects (JS data object key / render branch) | Default option |
|---|---|---|---|
| What does this part do with the spectrum (`.chips`, `data-v`) | **Listen** (`data-v="listen"`) | `job==='listen'` branch — passive sensor; shows sliders d1–d4; `NOUN.listen`=['Search Antenna','Search Array','Deep Array']; STOCK `{d1:0,d2:68,d3:50,d4:35}` (1.25 m · 600 nm · 250 nm · 3600 s) | **Listen** (`aria-pressed="true"`) |
| " | **Look** (`data-v="look"`) | `job==='look'` branch — geological/gravitational surveyor; only d1; `NOUN.look`=['Survey Probe','Survey Suite','Survey Observatory']; STOCK `{d1:0}` (speed 1) | — |
| " | **Track** (`data-v="track"`) | `job==='track'` branch — fire director; d1 + d3; `NOUN.track`=['Ranging Sight','Fire Director','Grand Director']; STOCK `{d1:6,d3:16}` (20 km · 5000 km/s) | — |
| " | **Hide** (`data-v="hide"`) | `job==='hide'` branch — cloak device; only d1; `NOUN.hide`=['Damping Skin','Cloak Device','Cloak Mantle']; STOCK `{d1:84}` (0.2) | — |
| " | **Blind** (`data-v="blind"`) | `else` branch — barrage jammer; d1 + d3; `NOUN.blind`=['Noise Emitter','Barrage Jammer','Barrage Mast']; STOCK `{d1:20,d3:18}` (degrade 4 · reach 1 Gm) | — |

Selecting a chip sets `job=c.dataset.v`, writes `aria-pressed`, resets sliders from `STOCK[job]`, and calls `render()`. There is only **one** forced-choice axis in this HTML.

### A.2 Sliders
All four DOM sliders are `min="0" max="100"`; they are reused (relabeled) per job, and shown/hidden by `show('w2',...)`, `show('w3',...)`, `show('w4',...)`. The visibility rule: `w2`(d2) only in Listen; `w3`(d3) in Listen · Blind · Track; `w4`(d4) only in Listen; `w1`(d1) always.

| Slider label | element id | Range (min–max in REAL units) | Default value | Mapping (name the JS fn) | Exact formula the slider feeds (quoted JS) | The number it ultimately sets |
|---|---|---|---|---|---|---|
| **Antenna size** (Listen) | `d1` | 1.25 m — 30 m | value=0 → STOCK.listen d1=0 (1.25 m) | linear | `const size=1.25+d1*0.2875;` (comment: `1.25 (the SHIPPED sensor) .. 30 — the useful part of a 1..2500 dial`) | Effective antenna size → `effSize=size*T_EFF` → sensitivity/threshold; also mass |
| **Band centre** (Listen) | `d2` | ~3 nm — ~2500 nm | value=68 → STOCK.listen d2=68 (600 nm) | logmap (`Math.pow(10,…)`) | `const peak=Math.pow(10, 0.5+d2*0.0335);` (comment: `~3 nm .. ~2500 nm, log — the real dial spans 0.01..1e12`) | Receiver band centre `peak` → `rec={min:peak-bw/2,avg:peak,max:peak+bw/2}` (SensorReceiverAtb) |
| **Bandwidth** (Listen) | `d3` | 1 nm — 500 nm | value=50 → STOCK.listen d3=50 (250 nm) | linear | `const bw=1+d3*4.99;` (comment: `1 .. 500 (the tech ceiling)`) | Window width AND `efficiency=1/(bw/T_BW)` inside sensitivity — the one genuine zero-sum |
| **Scan interval** (Listen) | `d4` | 1 s — 86400 s (24 h) | value=35 → STOCK.listen d4=35 (3600 s) | cubic (`Math.pow(d4/100,3)`) | `const scanS=Math.round(1+Math.pow(d4/100,3)*86399);` | Reschedule interval (real — but costs no mass/power for a sensor) |
| **Survey speed** (Look) | `d1` | 1 — 10 | STOCK.look d1=0 (speed 1) | linear+round | `const speed=1+Math.round(d1*0.09);` (comment: `1..10, the template's real range`) | Survey speed → `mass=(10*speed)^2`, `passes=ceil(200/speed)` |
| **Range** (Track) | `d1` | 10 km — 175 km | STOCK.track d1=6 (20 km) | linear | `const rangeK=10+d1*1.65;` (comment: `10..175, the real dial`) | Fire-control range (km) → `honest=rangeK+track/100` (mass) |
| **Tracking speed** (Track) | `d3` | 1250 km/s — 25000 km/s | STOCK.track d3=16 (5000 km/s) | linear | `const track=1250+d3*237.5;` (comment: `1250..25000`) | Fire-control tracking speed → `honest` mass |
| **Concealment** (Hide) | `d1` | 1.00 — 0.05 (signature multiplier) | STOCK.hide d1=84 (0.2) | linear (descending) | `const mult=1.0-d1*0.0095;` (comment: `1.0 .. 0.05, the real dial`) | Signature shown (multiplier on source) → `mass=200+400*(1-mult)`, `rangeMult=√mult` |
| **Barrage strength** (Blind) | `d1` | 1 — 16 (÷ their signal) | STOCK.blind d1=20 (degrade 4) | linear | `const degrade=1+d1*0.15;` (comment: `1..16, the real dial`) | Enemy threshold multiplier → their range `×1/√degrade`; `mass=200+100*degrade+50*reachGm` |
| **Reach** (Blind) | `d3` | 0.1 Gm — 5 Gm | STOCK.blind d3=18 (1 Gm) | linear | `const reachGm=0.1+d3*0.049;` (comment: `0.1..5 Gm`) | Jammer reach (Gm) → mass |

### A.3 Toggles / checkboxes / presets / secondary choices
| Control | id | Effect when active |
|---|---|---|
| STOCK reset (implicit preset, fires on every chip click) | — | `for(const [k,v] of Object.entries(STOCK[job]||{})) $(k).value=v;` — each job **opens on its real shipped component**, so the first number shown is a number the game actually ships (slider positions back-solved from base-mod templates). Not a user-facing widget; a behavior baked into the chip handler. |

No checkboxes, no radio groups beyond the chip row, no theme toggle in markup (theme is CSS-only via `prefers-color-scheme` / `data-theme`).

## B. OUTPUTS — every readout the panel produces
Honesty markers come from the panel's own label suffixes (`— set` = teal "you set it" / `.num set`; `— fixed`/`— emergent`/`Mass as designed` = grey `.num fixed`; `(proposed)` = engine-pending). Color classes `g`/`a`/`r` = good/amber/red flag.

| Output label | Units | Formula (quoted / derived from JS) | Honesty marker shown | Sim variable it writes | Consumer system | Build state |
|---|---|---|---|---|---|---|
| **Part name** (`v_name`) | — | `partName({d1,d3})` — scale←size bucket, temper←window/2nd dial, noun←job | name label | component name (assembly-level) | designer/UI | **computed** |
| **Detection range** (`oname`, Listen) | Gm/Mm/km | `'Detection to '+gm(rangeFor(EM[0].mag,thr))+' against a reactor'` | oname | — (derived from threshold vs a stock emitter) | player readout | **computed** (vs stock reactor magnitude) |
| **Window — set** (`v_a1`, Listen) | nm | `Math.round(rec.min)+'–'+Math.round(rec.max)+' nm'`, `rec=peak±bw/2` | `.num set` "set" | `SensorReceiverAtb` (band min/max = peak ± bandwidth/2) | detection band-match gate (`SensorTools.cs`) | **live** |
| **Sensitivity — emergent** (`v_a2`, Listen) | kW | `thr=sens*0.001`, `sens=T_SENS/(effSize²·efficiency)` | `.num set` "emergent" | sensor threshold_kW (feeds `RangeForSignal`) | `SensorTools.RangeForSignal` / detection | **live** |
| **Coverage × range² — fixed** (`v_a3`, Listen) | ratio | `(bw*Math.pow(rangeFor(EM[0].mag,thr)/1e9,2)).toFixed(2)` — constant ≈ 39.34 | `.num fixed` "fixed" | — (invariant proof) | player readout | **computed** |
| **Emitters your band admits** (`v_a4`, Listen) | count | `seenNow+' of 3  ▸ '+seenFixed+' if fixed'`; `seenNow=EM.filter(gateAsWritten)`, `seenFixed=…gateCorrect` | `.num g/a/r` flag | — (compares broken vs correct gate) | player readout / bug flag | **computed** (illustrates the `SensorTools.cs` gate bug) |
| **Mass** (`v_mass`, Listen) | kg | `mass=90+0.01*size*size` | `.num fixed` | component mass | build/chassis budget | **live** |
| **Reactor first-detect** (`m_rx`) | Gm/Mm/km | `rangeFor(EM.rx.mag,thr)` gated by `gateAsWritten`; mag `50*1500*0.1*1500` (11.25M) | `.mt` "1705 nm · loudest" | — (readout vs stock template) | player readout | **emergent** (depends on emitter templates) |
| **Warp bubble first-detect** (`m_wp`) | Gm/Mm/km | `rangeFor(EM.wp.mag,thr)`; mag `3179.31*1000` (3.179M), 828 nm | `.mt` "828 nm · FTL" | — | player readout | **emergent** |
| **Thruster plume first-detect** (`m_th`) | Gm/Mm/km | `rangeFor(EM.th.mag,thr)`; mag `109103` (=Thrust), 828 nm | `.mt` "828 nm · accel" | — | player readout | **emergent** |
| **Cloaked thruster first-detect** (`m_ck`) | Gm/Mm/km | `rangeFor(EM.ck.mag,thr)`; mag `109103*0.2`, 828 nm | `.mt` "×0.2 concealment" | — | player readout | **emergent** |
| **Verdict line** (`verdict`, Listen) | text | branches on `seenNow>seenFixed` / `seenFixed===0` | `.sub` warn/ok | — | player readout | **computed** |
| **A probe / survey ship / flotilla** (`oname`, Look) | text | `speed<3?'probe':speed<7?'survey ship':'survey flotilla'` | oname | — | player readout | **computed** |
| **Survey speed — set** (`v_a1`, Look) | ×speed | `'×'+speed` | `.num set` "set" | survey speed attribute | survey/detection processor | **live** |
| **Passes for a typical point** (`v_a2`, Look) | count | `passes=Math.ceil(200/speed)` | `.num fixed` | — (derived) | player readout | **computed** |
| **Cost law — fixed** (`v_a3`, Look) | formula | `'(10 × speed)²'`; `mass=Math.pow(10*speed,2)` | `.num fixed` "fixed" | component mass | build budget | **live** (mass) / **computed** (label) |
| **Points per pass** (`v_a4`, Look) | count | `speed` | `.num fixed` | — | player readout | **computed** |
| **Mass** (`v_mass`, Look) | kg | `fmt((10*speed)²)` | `.num fixed` | component mass | build budget | **live** |
| **A director good to N km** (`oname`, Track) | text | `'A director good to '+rangeK+' km'` | oname | — | player readout | **computed** |
| **Range — set** (`v_a1`, Track) | km | `rangeK.toFixed(0)+' km'` | `.num set` "set" | fire-control range | fire-control / weapon-lock | **live** |
| **Tracking speed — set** (`v_a2`, Track) | km/s | `Math.round(track)+' km/s'` | `.num set` "set" | fire-control tracking speed | fire-control angular-rate gate | **live** |
| **Mass as designed** (`v_a3`, Track) | kg | `honest=rangeK+track/100` | `.num fixed` | component mass | build budget | **live** |
| **Mass if you exploit it** (`v_a4`, Track) | kg | `(honest/16)+' kg  ▸ ÷16'` (both `Size vs Range` + `Size vs TrackingSpeed` at 0.25) | `.num r` red flag | — (exploit demo) | player readout / bug flag | **computed** (shows the 16× mass exploit) |
| **Mass** (`v_mass`, Track) | kg | `honest.toFixed(0)+' kg'` | `.num fixed` | component mass | build budget | **live** |
| **A ghost / quiet hull / barely damped** (`oname`, Hide) | text | `mult<0.15?'A ghost':mult<0.5?'A quiet hull':'Barely damped'` | oname | — | player readout | **computed** |
| **Signature shown — set** (`v_a1`, Hide) | % | `(mult*100).toFixed(0)+'%'` | `.num set` "set" | cloak signature multiplier (source-reducer, cf. `SensorSignatureAtb`) | detection (their `RangeForSignal` vs you) | **live** |
| **Detected at — emergent** (`v_a2`, Hide) | ×range | `rangeMult=Math.sqrt(mult)` → `'×'+rangeMult+' the range'` | `.num set` "emergent" | — (derived √signal) | player readout | **computed** |
| **Against a stock sensor** (`v_a3`, Hide) | Gm | `gm(rangeFor(EM[2].mag*mult, 5.689e-6))` (5.689e-6 = stock-sensor threshold) | `.num fixed` | — (readout vs the Listen door's stock sensor) | player readout | **computed** (cross-door: reads stock Listen sensor) |
| **Lost with the component** (`v_a4`, Hide) | text | `'health-scaled'` | `.num fixed` | health-scaled cloak effect (grave rung) | damage system | **live** |
| **Mass** (`v_mass`, Hide) | kg | `mass=200+400*(1-mult)` | `.num fixed` | component mass | build budget | **live** |
| **Blinds to ×N out to R Gm** (`oname`, Blind) | text | `'Blinds to ×'+(1/√degrade)+' out to '+reachGm+' Gm'` | oname | — | player readout | **computed** |
| **Their range becomes** (`v_a1`, Blind) | ×range | `'×'+(1/Math.sqrt(degrade))` | `.num set` (label not "set"-suffixed) | jammer barrage strength → raises enemy threshold | combat resolver (blind-fleet wipe) | **live** |
| **Reach — set** (`v_a2`, Blind) | Gm | `reachGm.toFixed(2)+' Gm'` | `.num set` "set" | jammer reach | detection/combat area | **live** |
| **Your own noise — COUPLED** (`v_a3`, Blind) | ×mult | `coupled=Math.sqrt(degrade)` → `'×'+coupled+'  (proposed)'` | "(proposed)" | — (proposed self-noise = √strength) | detection (self-signature) | **engine-pending** (waits on coupling self-noise to strength; currently a free dial) |
| **Your own noise — as shipped** (`v_a4`, Blind) | text | `'a free dial, 1–10'` | `.num r` red flag | Self Signature Boost dial (1–10) — writes self-signature but NOT mass | detection (their sensor vs you) | **live** (but exploitable — costs no mass) |
| **Mass** (`v_mass`, Blind) | kg | `mass=200+100*degrade+50*reachGm` | `.num fixed` | component mass | build budget | **live** |
| **Band bar** (`bar`, Listen only) | nm scale | `drawBand(rec)` — draws EM emitter bands + receiver window across 0–2600 nm | visual | — (visualizes SensorReceiverAtb vs emitters) | player readout | **computed** |

## C. CROSS-DOOR READS — values the panel shows but does NOT own
| Value | Owned by (which door) | Why this door displays it | Marker used |
|---|---|---|---|
| Gravitational Surveyor (Look) | **Propulsion** (jump points / FTL) | Prose §proof: *"Gravitational Surveyor … honest — arrives from Propulsion this pass"*; jobnote: *"the gravitational one only arrived at this door because Propulsion handed it over."* | text note ("arrives from Propulsion") |
| Intelligence Directorate — op capacity · counter-intel (`IntelDirectorateAtb`) | **Command** (officer + post + capacity door) | Listed as the sixth "job" but explicitly *"not a sensor at all … a seat and a staff rating"* → *"moves to Command."* | ➡ "moves to Command" |
| Stock sensor threshold `5.689e-6` kW (Hide `v_a3`, "Against a stock sensor") | **Listen** (this same door's shipped passive sensor) | Hide computes how far the shipped Listen sensor would see the cloaked hull; it reads the Listen door's stock threshold, not its own | `.num fixed` "Against a stock sensor" |
| Emitter magnitudes (Reactor / Warp / Thruster) — `EM[]` | **Reactor** (power×0.1×mass), **Propulsion** (warp sustain kW, thrust) | The `.mus` first-detect tiles and band bar read emitter signatures owned by other components to show detection ranges | `.muN` "Real magnitudes from the shipped templates" |

## D. NOTES / DISCREPANCIES
- **Blind self-noise is PROPOSED / engine-pending.** `v_a3` "Your own noise — COUPLED ×√degrade (proposed)" is the *fix*; the shipped `v_a4` self-signature is *"a free dial, 1–10"* not in the mass formula. Waits on: coupling self-signature to barrage strength (one number, not two dials). The panel flags this red (`.num r`).
- **The band-matching gate bug is the door's headline.** `gateAsWritten(rec,sig)` = `Math.max(rec.min,sig.min) < Math.max(sig.min,sig.max)` — the receiver's **upper edge (`rec.max`) is never consulted** (RHS reduces to `sigMax`). `gateCorrect` = `Math.max(rec.min,sig.min) < Math.min(rec.max,sig.max)`. Consequence quoted: fix alone drops reactor detection from 0.40 Gm → 0.04 Gm and a parked ship becomes undetectable — *"the game DEPENDS on that bug."* Ruling awaited: correct the overlap test AND add an infrared receiver to the base mod in one change (same ruling the FTL-band question waits on).
- **Four dials fail the acceptance test (write a sim variable AND cost something):**
  1. **Resolution** — *"dead AND free"* (`SensorTools.cs:125` reads it into a local and never uses it; not in the mass formula). `SignalQuality` already exists; the door says wire it, don't cut it. **Note: resolution is NOT one of this HTML's four Listen sliders** — it is described in prose (`+ resolution` struck through in the proof table) as the fifth attribute of the shipped passive sensor.
  2. **Size vs Range** and **Size vs TrackingSpeed** (Track) — appear *only* in the mass formula, never passed to the attribute; set both to 0.25 → fire-control mass ÷16 free. **Also NOT sliders in this HTML** — the Track branch only exposes Range (d1) + Tracking speed (d3); the two phantom dials are shown via the `v_a4` "Mass if you exploit it ▸ ÷16" readout, not as controls.
  3. **Jammer self-noise** (Blind) — a free dial 1–10, not in mass → downside is opt-out.
  4. **Scan interval** (Listen d4) — writes a real reschedule interval but costs no mass/power for a sensor → shorter is strictly better; the door wants a power draw added.
- **Antenna size is a fifth free ladder** (self-corrected in the HTML): `mass=90+0.01*size²`; the quadratic term doesn't overtake the 90 kg constant until size ≈ 95, while the shipped sensor sits at 1.25 — so across the usable range mass is effectively flat (*"size 10 buys 8× the detection range for +1 kg"*).
- **The one honest trade** is Bandwidth (Listen d3): it writes both window width and the efficiency term, so `coverage × range²` is invariant (≈39.34, shown in `v_a3`). But the gate bug cancels it — a 1 nm window still admits everything, so the trade is only real once the overlap test is fixed.
- **Ground Radar** is flagged in the proof table as *"prices reach LINEARLY where space is quadratic"* — an open cost-law inconsistency (Yours-to-call ②), but it is not a control in this builder.
- **Boundary note vs a naive reading:** the sixth "job," Intelligence Directorate (`IntelDirectorateAtb`), is explicitly disowned by this door and reassigned to the Command door — anyone treating "sensors" as owning all detection/intel attributes would wrongly claim it here.
