# 01-IO-WEAPONS — Weapons, re-derived - two choices and four sliders
> Source: `docs/Actual HTMLs Of designers/weaponsderived.html` · Component-designer door · Census (Phase 1 draft, unverified)
>
> **⚠ CORRECTED against `06-OUTPUTS-BY-DOOR.md` (reader-verified 2026-08-02; folded into `02-IO-MATRIX.md` 2026-08-06).**
> This Phase-1 census was written from the door's OUTPUT end; `06`'s reader-end trace narrows four states below:
> **DamagePerShot / Rate / Penetration are LIVE (ground-only)** — a ship zeroes them (`CombatEngagement.cs:1368`);
> **runs-on is PARTIAL** (the engine keys ammo-vs-power on **Nature**, not Delivery; power/none have no reader); and
> **`HeatPerSecond` is a LIVE field with a live reader** the door just doesn't emit (not "computed, no engine var").
> PD-answerable is ship-only. See `06` §Weapons for the file:line readers.

## What this door builds
This designer builds **one weapon component** — the thing a ship or ground unit mounts and fires. It replaces the old pile of five separate weapon doors (67 → 89 → 96 templates) with a single door because the combat resolver only ever reads ten numbers off any weapon; everything else on the old templates was naming, cost and flavour.

The mechanism is **two forced choices + four sliders**. The first choice is *how the shot crosses the gap* (Contact / Beam / Projectile / Guided) — this is physics, and it forces shot speed, dodge-following, what the weapon runs on, and whether point-defence can shoot it down. The second choice is *what it is good against* (Kinetic / Energy / Explosive / Exotic) — this fixes the shield-soak fraction. The four sliders then set total damage, the shot-size↔rate-of-fire split, range, and focus (concentration). Think of it like designing a gun on a workbench: pick the mechanism and the ammo type from two switch banks, then dial four knobs for power, shot weight, reach, and spread.

## A. INPUTS — every control the user can touch

### A.1 Forced choices (chips / radio buttons)
| Control group | Options (exact button labels) | What each option selects (JS data object key / render branch) | Default option |
|---|---|---|---|
| Door 1 — how it gets there (`data-g="fam"`) | Contact | `FAM.contact` → vel `'—'`, trk `'—'`, sup `'nothing'`, pd `'no'`, rng `[0,0]` | — |
| Door 1 — how it gets there (`data-g="fam"`) | Beam | `FAM.beam` → vel `'3.0e8 m/s'`, trk `'n/a'`, sup `'power'`, pd `'no'`, rng `[1,60]` | **default** (`aria-pressed="true"`) |
| Door 1 — how it gets there (`data-g="fam"`) | Projectile | `FAM.proj` → vel `'2.0e4 m/s'`, trk `'0.05'`, sup `'ammo'`, pd `'no'`, rng `[5,100]` | — |
| Door 1 — how it gets there (`data-g="fam"`) | Guided | `FAM.guided` → vel `'1.2e3 m/s'`, trk `'0.90'`, sup `'ammo'`, pd `'YES'`, rng `[20,100]` | — |
| Door 2 — what it's good against (`data-g="nat"`) | Kinetic | `NAT.kinetic` → `soak:'1.0'` (shields soak 100%) | — |
| Door 2 — what it's good against (`data-g="nat"`) | Energy | `NAT.energy` → `soak:'0.5'` (shields soak 50%) | **default** (`aria-pressed="true"`) |
| Door 2 — what it's good against (`data-g="nat"`) | Explosive | `NAT.explos` → `soak:'0.75'` (shields soak 75%) | — |
| Door 2 — what it's good against (`data-g="nat"`) | Exotic | `NAT.exotic` → `soak:'0.0'` (shields soak 0%, ignores shields) | — |

Two forced-choice axes: **Delivery (fam) × Nature (nat)** — 4 × 4 = "sixteen real weapons before you touch a slider" (prose, §Axis 2). Selection wiring: `chip.dataset.g` picks the axis (`fam` or `nat`), `chip.dataset.v` sets the module-level `fam`/`nat` variable; the click handler flips `aria-pressed` and calls `render()`.

### A.2 Sliders
| Slider label | element id | Range (min–max in REAL units) | Default value | Mapping (linear / logmap / other) | Exact formula the slider feeds (quoted JS) | The number it ultimately sets |
|---|---|---|---|---|---|---|
| Total damage | `scale` | 0–100 raw → dps ≈ **200 → ~745,600 J/s** (200·10^(0..3.571)) | **45** | log (inline `Math.pow(10, scale/28)`) | `const dps=Math.round(200*Math.pow(10,scale/28));` | `DamagePerSecond` (chelp: "Writes `DamagePerSecond`") |
| Shot size ↔ rate of fire | `shot` | 0–100 raw → per-shot multiplier 10^((shot−50)/26) ≈ ×0.012 … ×82 of dps | **35** | log (inline `Math.pow(10,(shot-50)/26)`) | `const perShot=dps*Math.pow(10,(shot-50)/26); const rate=dps/perShot;` | `DamagePerShot` and `Rate` (chelp: "Splits the budget into `DamagePerShot` and `Rate`") |
| Range | `rng` | 0–100 raw → metres via family band `f.rng` × 1000 (e.g. Beam 1,000–60,000 m; Proj 5,000–100,000 m; Guided 20,000–100,000 m; Contact 0) | **40** | linear (interpolates within family band) | `const range=fam==='contact'?0:Math.round((f.rng[0]+(f.rng[1]-f.rng[0])*rng/100)*1000);` | `Range_m` (chelp: "Writes `Range_m`") |
| Focus | `foc` | 0–100 raw → Penetration & VolumeOfFire (see formulas) | **50** | linear inputs into two formulas | pen: `const pen=fam==='contact'?foc*0.4:foc*(fam==='proj'?0.9:0.6);` · spread/sat: `const spread=(100-foc)/100; const sat=fam==='contact'?1:Math.max(1,rate*(0.4+3.6*spread));` | `Penetration` (armour pierced) and `VolumeOfFire` (chelp: "Writes `Penetration` (armour pierced) and `VolumeOfFire`") |

Helper maps used only for the compositional **name** (not sim values): `tier5(v)` (5 bands) and `tier3(v)` drive `nameFor(fam,nat,scale,shot,foc)` via the `NOUN` / `SCALE` / `SCALE_C` / `FOCUS` tables. These affect the displayed weapon name only.

### A.3 Toggles / checkboxes / presets / secondary choices
— none — (no checkboxes, toggles, or preset buttons; the only controls are the two chip groups and four range sliders)

## B. OUTPUTS — every readout the panel produces
| Output label | Units | Formula (from JS) | Honesty marker shown | Sim variable it writes | Consumer system | Build state |
|---|---|---|---|---|---|---|
| Damage per second (`v_dps`) | J/s | `dps=Math.round(200*Math.pow(10,scale/28))` | teal / `.num set` ("you set it") | `DamagePerSecond` | resolver damage budget | **live** |
| Damage per shot (`v_shot`) | J | `perShot=dps*Math.pow(10,(shot-50)/26)` | teal / `.num set` | `DamagePerShot` | resolver per-shot armour math | **live** |
| Rate of fire (`v_rate`) | /s | `rate=dps/perShot` | teal / `.num set` | `Rate` | resolver (dps = shot × rate coupling) | **live** |
| Volume of fire (`v_sat`) | count | `sat=fam==='contact'?1:Math.max(1,rate*(0.4+3.6*spread))`, `spread=(100-foc)/100` | teal / `.num set` | `VolumeOfFire` | resolver dodge-floor / saturation | **live** |
| Reach (`v_rng`) | m | `range=fam==='contact'?0:Math.round((f.rng[0]+(f.rng[1]-f.rng[0])*rng/100)*1000)` | teal / `.num set` | `Range_m` | resolver battle-size / engagement range | **live** |
| Shot speed (`v_vel`) | m/s | `f.vel` (Contact `—`, Beam `3.0e8`, Proj `2.0e4`, Guided `1.2e3`) | grey / `.num fixed` ("door 1 forced") | shot speed (file names no engine var; forced by delivery) | resolver dodge check | **live** (door-1 forced) |
| Follows a dodge (`v_trk`) | ratio | `f.trk` (Contact `—`, Beam `n/a`, Proj `0.05`, Guided `0.90`) | grey / `.num fixed` | dodge-following (file names no engine var; forced by delivery) | resolver dodge check | **live** (door-1 forced) |
| Armour pierced (`v_pen`) | points | `pen=fam==='contact'?foc*0.4:foc*(fam==='proj'?0.9:0.6)` | teal / `.num set` | `Penetration` | resolver flat-per-shot armour subtraction | **live** |
| Heat build-up (`v_heat`) | kJ/s | `heat=fam==='beam'?dps*0.004*(1+(100-shot)/70):dps*0.0008` | teal / `.num set` (but NOT in the okbox "ten") | — (no engine var cited; not among the canonical ten) | — (unclaimed — see Notes) | **computed** |
| Shields stop (`v_shield`) | % of it | `Math.round(nt.soak*100)+'% of it'`; class `.sh g/a/r` by `nt.soak` | grey / `.num fixed sh` ("door 2 forced") | shield soak fraction (`NAT[*].soak`; file names no engine var) | resolver shield drain (attacker-fixed) | **live** (door-2 forced) |
| Shot down in flight (`v_pd`) | yes/no | `f.pd==='YES'?'yes — PD can':'no'` | grey / `.num fixed` | point-defence-answerable flag (forced by delivery) | point-defence resolver | **live** (door-1 forced) |
| Runs on (`v_sup`) | label | `f.sup` (Contact `nothing`, Beam `power`, Proj/Guided `ammo`) | grey / `.num fixed` | runs-on / supply type (forced by delivery) | logistics / power / ammo | **live** (door-1 forced) |
| Lands: Unprotected (`m_none`) | % | static `100%` (nothing in the way) | tile (comparison readout) | — | comparison-only display | **computed** |
| Lands: Shielded (`m_sh`) | % | `fSh=1-nt.soak` | tile, class `.g/.a/.r` by fraction | — | comparison-only display | **computed** |
| Lands: Armoured (`m_ar`) | % | `fAr=thru(perShot)` where `thru(ps)=Math.max(ps-eff*PERPT*NF, ps*MINPASS)/ps`, `eff=Math.max(0,ARM-pen)`, `ARM=400,PERPT=1.5,MINPASS=0.1,NF=1.0` | tile | — (reference armour 400, defender-owned) | comparison-only display | **computed** (vs reference; true value **emergent** on defender's plate) |
| Lands: Shield + armour (`m_bo`) | % | `fBo=fSh<=0?0:fSh*thru(perShot*fSh)` | tile | — | comparison-only display | **computed** (true value **emergent**) |

Notes on markers: the panel's own legend (lines 217–221) defines **teal = "a value you chose with a slider — it reaches the simulation directly"** and **grey = "a value door 1 forced… you don't get a say."** The four "how much lands" tiles are explicitly comparison ("the same flat-per-shot maths the resolver runs" against "Reference armour 400") — real in-combat landing is emergent on the actual defender.

## C. CROSS-DOOR READS — values the panel shows but does NOT own
| Value | Owned by (which door) | Why this door displays it | Marker used |
|---|---|---|---|
| Reference armour = 400 (`ARM=400`, `PERPT=1.5`) | Defender's armour / plating door (§Axis 2: "whatever their plate is tuned to… the defender chose it") | To show what fraction of each shot pierces plate — the "Armoured" and "Shield + armour" landing tiles | reference constant in a comparison tile; caption "pierces N of 400" |
| Shield presence / capacity (implied by `fSh=1-nt.soak` applied to a shielded target) | Defender's shield door | To show the "Shielded" and "Shield + armour" landing fractions (attacker sets the *soak fraction*, defender supplies the *shield*) | comparison tile |

Note: the shield **soak fraction** itself (`nt.soak`) IS owned by this weapon door (attacker-fixed, "the same on every target") — only the *defender's shield and armour* are cross-door. §Axis 2 states the asymmetry explicitly: shield numbers are attacker-fixed; armour numbers are defender-chosen; hull is "nobody decides."

## D. NOTES / DISCREPANCIES
- **The whole door is a PROPOSAL, not shipped.** Footer Status (lines 281–282): "This is a proposal that reproduces every existing gun, not yet the shipped designer." It is *derived* — the okbox (line 275) asserts every dial writes a variable the resolver already reads, so no individual output is engine-pending; the door as a whole awaits being built as the actual designer.
- **Heat build-up is a marker discrepancy.** `v_heat` carries the teal `.num set` class (legend: "reaches the simulation directly"), but the okbox's canonical ten sim-variable list (line 275: `DamagePerSecond, DamagePerShot, Rate, VolumeOfFire, Range_m, Penetration`, shield soak, shot speed, dodge-following, runs-on) does **not** include heat, and no slider chelp says heat writes anything. Treated here as a **computed** readout with no cited engine variable — flag for whoever wires the door (candidate target would be a waste-heat / thermal-signature field, but this HTML does not name one).
- **Two different "tens" appear in the file.** The §"ten, as five questions" grouping counts: Damage per second · shot speed · follows-a-dodge · volume of fire · good-against(shield soak) · armour-pierced · damage-per-shot · range · runs-on · shot-down = 10 (excludes Rate as a standalone). The okbox's ten *includes* Rate but *excludes* shot-down. The panel renders 12 `.num` cells + 4 landing tiles = 16 outputs total; 11 carry a live/forced build state, heat is computed, and the 4 tiles are computed/emergent comparisons.
- **DPS / DamagePerShot / Rate are coupled, not independent** (legend line 222): "Set two and the third is decided for you." The `shot` slider trades per-shot ↔ rate while dps holds.
- **Contact family zeroes several outputs**: range = `contact` (0), sup = `nothing`, pd = `no`, vel/trk = `—`; `sat` forced to 1 and `pen` uses the `foc*0.4` branch. The `SCALE_C` name table exists solely because contact "reads oddly with naval/spinal."
- **Nature door names no engine variable directly.** Door 2 sets `nt.soak` (the shield soak fraction); the file describes it as "the shield soak fraction" rather than an engine field name. If the assembler needs a `Nature` enum, that mapping is implied by the four `NAT` keys (kinetic/energy/explosive/exotic) but not spelled as a sim variable here.
- **§"What this kills" collapses** confirm scope: Pulse-vs-Continuous beam is "one weapon at two shot-size positions"; the old Exotic *door* becomes the Nature *setting*; and "Bolt/Slug/Cloud" collapse to one row (all answer "no" to point-defence). Non-damage effects (mind control, jump inhibition) "write none of the ten" and are explicitly excluded from this weapon door.

## VERIFIED (Phase 2 — personal pass, 2026-08-02)
Checked the census against `weaponsderived.html` source (`<script>` + slider inputs), line by line.
- **Slider defaults confirmed** (lines 177–183): `scale` def **45**, `shot` def **35**, `rng` def **40**, `foc` def **50**. All match.
- **Formulas confirmed:** `dps=Math.round(200*Math.pow(10,scale/28))` (`:372`); `perShot=dps*Math.pow(10,(shot-50)/26)` (`:373`); `pen=fam==='contact'?foc*0.4:foc*(fam==='proj'?0.9:0.6)` (`:377`); `range=fam==='contact'?0:Math.round((f.rng[0]+(f.rng[1]-f.rng[0])*rng/100)*1000)` (`:379`); `ARM=400,PERPT=1.5,MINPASS=0.1,NF=1.0` (`:396`). All exact.
- **NAT soak confirmed** (`:297-300`): Kinetic 1.0 / Energy 0.5 / Explosive 0.75 / Exotic 0.0.
- **FAM axis + defaults confirmed**: Beam is `aria-pressed="true"` default; Energy is the nat default.
- **No corrections.** The two flagged discrepancies stand and are correct: (1) `v_heat` is teal-marked but writes no engine variable in the canonical ten — a real marker discrepancy for whoever wires the door; (2) the four "how much lands" tiles compute against reference armour 400, whose true value is emergent on the defender's plate.
