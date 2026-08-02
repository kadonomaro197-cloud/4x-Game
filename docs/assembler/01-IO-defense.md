# 01-IO-DEFENSE — Defense, re-derived — the four layers a shot meets, in order
> Source: `docs/Actual HTMLs Of designers/defensederived.html` · Component-designer door · Census (Phase 1 draft, unverified)

## What this door builds
This door builds a single **defensive component** — and its first act is to make you pick which of two it is: a **Shield generator** (a recharging pool of protection laid on top of hit points) or an **Armour plate** (a flat amount subtracted off every shot that lands). Those are the only two of the four defensive layers the door can actually sell. The other two layers a shot meets — **Evasion** (whether the shot is even rolled) and **Structure** (raw hit points) — are set by the Chassis and Propulsion doors, and this door can only read them back to show you the whole picture.

So the door's shape is **one forced choice — shield or plate — plus the dials that shape whichever you picked.** For a shield that's two sliders (Capacity and Regen). For armour it's one thickness slider plus four "nature resist" sliders that are zero-sum (they always renormalize to sum 4.0, so you only choose *where to be weak*, never how to be tougher everywhere). The keystone the door proves is an **asymmetry**: a shield's soak fraction is fixed by the *attacker's* weapon nature (you the defender get no say), while armour's resists are entirely the *defender's* choice — same fight, opposite ownership.

## A. INPUTS — every control the user can touch

### A.1 Forced choices (chips / radio buttons)
| Control group | Options (exact button labels) | What each option selects (JS data object key / render branch) | Default option |
|---|---|---|---|
| **The layer (forced choice)** — `.lchip`, `data-layer` | "Shield generator — a recharging pool" | `data-layer="shield"` → sets `layer='shield'` → `show('shieldsec',true)`, uses `LAYER.shield` (`a1:'Shield pool'`, `a2:'Regen fraction'`); render branch `if(layer==='shield')` | **Shield** (`aria-pressed="true"`, `let layer='shield'`) |
| **The layer (forced choice)** — `.lchip`, `data-layer` | "Armour plate — a flat per-shot bounce" | `data-layer="armour"` → sets `layer='armour'` → `show('armoursec',true)`, uses `LAYER.armour` (`a1:'Armour points (Defense)'`, `a2:'Tuned toward'`); the `else` render branch | (not default) |
| **Armour nature tuning preset** — `.chip.tn`, `data-t` (armour only) | "Plain (Composite)" | `data-t="plain"` → `PRESET.plain=[25,25,25,25]` written into sliders `vk,ve,vx,vo` | **Plain** (`aria-pressed="true"`, `let tune='plain'`) |
| **Armour nature tuning preset** — `.chip.tn`, `data-t` | "Ablative — vs Energy" | `data-t="energy"` → `PRESET.energy=[15,50,25,25]` written into `vk,ve,vx,vo` | (not default) |
| **Armour nature tuning preset** — `.chip.tn`, `data-t` | "Reactive — vs Kin/Expl" | `data-t="kinexpl"` → `PRESET.kinexpl=[40,15,50,25]` written into `vk,ve,vx,vo` | (not default) |
| **Armour nature tuning preset** — `.chip.tn`, `data-t` | "Null-ward — vs Exotic" | `data-t="exotic"` → `PRESET.exotic=[20,20,20,60]` written into `vk,ve,vx,vo` | (not default) |
| **Incoming fire nature (test rig, not a build input)** — `.nat`, `data-v` | "Kinetic" | `data-v="kinetic"` → `nat='kinetic'` → `NAT.kinetic={soak:1.00,key:'vk'}` | **Kinetic** (`aria-pressed="true"`, `let nat='kinetic'`) |
| **Incoming fire nature (test rig)** — `.nat`, `data-v` | "Energy" | `data-v="energy"` → `NAT.energy={soak:0.50,key:'ve'}` | (not default) |
| **Incoming fire nature (test rig)** — `.nat`, `data-v` | "Explosive" | `data-v="explos"` → `NAT.explos={soak:0.75,key:'vx'}` | (not default) |
| **Incoming fire nature (test rig)** — `.nat`, `data-v` | "Exotic" | `data-v="exotic"` → `NAT.exotic={soak:0.00,key:'vo'}` | (not default) |

> Note: the "Incoming fire nature" chips and the "Incoming shot size" slider (§A.2) are a **test harness** — they set nothing on the built component. They only drive the "What it is actually worth" comparison panel (§B, `m_1`–`m_4`) so the user can watch the asymmetry move. They are NOT inputs to the component's saved stats.

### A.2 Sliders
| Slider label | element id | Range (min–max in REAL units) | Default value | Mapping (linear / logmap / other) | Exact formula the slider feeds (quote the JS line) | The number it ultimately sets |
|---|---|---|---|---|---|---|
| "Shield capacity — how big the pool is" | `cap` | raw 0–100 → **0–1,500 pts** (`CAP_MAX=1500`) | 10 (raw) → 150 pts | linear (`lin`) | `const pool = lin(capV,0,CAP_MAX);` where `lin(d,lo,hi){return lo+(hi-lo)*d/100;}` | Shield pool size (pts) — the depleting buffer capacity |
| "Shield regen — how fast it refills" | `regen` | raw 0–100 → **0–5.0 /s** (`REGEN_MAX=5`) | 0 (raw) → 0.00 /s | linear (`lin`) | `const regenFrac = lin(regenV,0,REGEN_MAX);` then `const regenPts = regenFrac*300;` (fraction→points/s, illustrative) | Shield regen fraction (/s); `regenPts` = points/s |
| "Plate thickness — flat bounce per shot" | `arm` | raw 0–100 → **Defense 0–50** (`DEF_MAX=50`) | 10 (raw) → Defense 5 | linear (`lin`) | `const Defense = lin(armV,0,DEF_MAX);` | Armour points (Defense value) |
| "vs Kinetic" (armour resist) | `vk` | raw 1–100 | 25 | renormalized (`resists`) | `raw.vk/s*4` where `s=raw.vk+raw.ve+raw.vx+raw.vo` — see `resists()` | Kinetic nature-resist (× multiplier, zero-sum to 4.0) |
| "vs Energy" (armour resist) | `ve` | raw 1–100 | 25 | renormalized (`resists`) | `raw.ve/s*4` | Energy nature-resist (× multiplier, zero-sum to 4.0) |
| "vs Explosive" (armour resist) | `vx` | raw 1–100 | 25 | renormalized (`resists`) | `raw.vx/s*4` | Explosive nature-resist (× multiplier, zero-sum to 4.0) |
| "vs Exotic" (armour resist) | `vo` | raw 1–100 | 25 | renormalized (`resists`) | `raw.vo/s*4` | Exotic nature-resist (× multiplier, zero-sum to 4.0) |
| "Incoming shot size — same damage, packaged differently" (TEST RIG) | `shot` | raw 0–100 (many small ↔ one huge) | 35 | logmap (base-10 exponential) | `const perShot=DPS*Math.pow(10,(shot-50)/26), rate=DPS/perShot;` | Nothing on the component — drives the worth/TTK comparison only |

> `resists()` verbatim: `const raw={vk:+$('vk').value, ve:+$('ve').value, vx:+$('vx').value, vo:+$('vo').value}; const s=raw.vk+raw.ve+raw.vx+raw.vo||1; return {vk:raw.vk/s*4, ve:raw.ve/s*4, vx:raw.vx/s*4, vo:raw.vo/s*4};` — the four raw slider values are divided by their sum and multiplied by 4, so the four resists always total 4.0.

### A.3 Toggles / checkboxes / presets / secondary choices
| Control | id | Effect when active |
|---|---|---|
| Nature-tuning presets (Plain / Ablative / Reactive / Null-ward) | `.chip.tn` (`data-t`) | Writes a fixed 4-value array (`PRESET[tune]`) into the four resist sliders `vk,ve,vx,vo`, then re-renders. Presets are a convenience shortcut over the four sliders; the sliders remain individually adjustable afterward. |
| Section visibility (shield vs armour dials) | `shieldsec` / `armoursec` | Not a user control — driven by the forced-layer choice via `show('shieldsec', layer==='shield')` / `show('armoursec', layer==='armour')`. `armoursec` starts `style="display:none"`. |
| — none (no checkboxes) — | | This door has no independent toggles/checkboxes. |

## B. OUTPUTS — every readout the panel produces
| Output label | Units | Formula (quote or derive from JS) | Honesty marker shown in panel | Sim variable it writes (EXACT engine name, or "—") | Consumer system that reads it | Build state |
|---|---|---|---|---|---|---|
| Component name (`v_name`) | text | `shieldName(pool,regenFrac)` or `armourName(Defense,r)` (compositional name fns) | — (no marker) | — | UI label only | **computed** |
| Name-sub / provenance line (`v_namesub`) | text | shield: `'capacity ← pool size · regen ← refill rate · soak ← the ATTACKER (fixed)'`; armour: `'thickness ← total protection (mass) · tuning ← zero-sum, sums to 4.0 (free)'` | — | — | UI label only | **computed** |
| Reproduction line (`odesc`) | text | `reproShield(pool,regen)` / `reproArmour(r)` → maps current dials back to a shipped template name | — | — | UI label only | **computed** |
| Primary dial value #1 (`v_a1`, label `l_a1`) | pts | shield: `a1v=fmt(pool)+' pts'` (label "Shield pool"); armour: `a1v=Defense.toFixed(0)+' pts'` (label "Armour points (Defense)") | **you set it** (`mk set`) | shield: shield pool capacity (pts); armour: `Defense` (armour points) — see D on names | Combat kernel (`Combat/CombatKernel.cs`) | **live** |
| Primary dial value #2 (`v_a2`, label `l_a2`) | shield: /s · armour: ×tuned-toward | shield: `a2v=regenFrac.toFixed(2)+' /s'` (label "Regen fraction"); armour: `a2v = {vk:'Kinetic',...}[mxk]+' '+r[mxk].toFixed(2)+'×'` (label "Tuned toward") | **you set it** (`mk set`) | shield: ShieldRegenFraction (/s); armour: the four nature resists (`vk/ve/vx/vo`) | Combat kernel | **live** |
| Evasion mult (`v_ev`) | × multiplier | `EVA_READ.toFixed(1)+'× (max '+EVA_MAX+')'` — `EVA_READ=3.2`, `EVA_MAX=20` (constants, "illustrative baseline") | **read** (`mk read`) | — (owned by Chassis + Propulsion) | Combat kernel; displayed here for context | **read** |
| Structure (`v_st`) | HP | `fmt(HP_READ)+' HP'` — `HP_READ=4000` (constant) | **read** (`mk read`) | — (owned by Chassis) | Combat kernel; displayed here for context | **read** |
| Mass (`v_mass`) | kg | shield: `mass = Math.max(1, pool/9 + regenFrac*40)`; armour: `mass = Math.max(1, Defense*30)` (both "CarryMass-scale, illustrative") | **computed** (`mk calc`) | CarryMass (illustrative — installations.json CarryMass 1..2000) | Mass/economy budget; combat mass term | **computed** |
| Shield vs \<nature\> (`m_1`, label `l_m1`) | % soak | `Math.round(soakFrac*100)+'% soak'` where `soakFrac=nt.soak` (Kinetic 1.00 / Energy 0.50 / Explosive 0.75 / Exotic 0.00) | **reaches the sim** (`mk sim`) | shield-soak-by-nature table (from `Combat/CombatKernel.cs`: Kinetic 1.0 / Energy 0.5 / Explosive 0.75 / Exotic 0.0) — set by attacker, not this component | Combat kernel | **live** (attacker-owned constant, read by kernel) |
| Armour lets past (`m_2`, label `l_m2`) | % | `Math.round(fracPast*100)+'%'` where `fracPast = Defense<=0 ? 1 : Math.max(perShot - Defense*ARMSOAK*resistNat, perShot*MINPASS)/perShot` (`ARMSOAK=1.5`, `MINPASS=0.1`) | **reaches the sim** (`mk sim`) | armour subtraction: `ArmourSoakPerPoint 1.5`, `ArmourMinPassFraction 0.1`, plate `Defense` × its nature resist | Combat kernel | **live** |
| Effective health (`m_3`, label `l_m3`) | HP (or ∞) | `effEHP = shieldEHP===Infinity? Infinity : baseEHP+shieldEHP` where `baseEHP=HP_READ*EVA_READ*armourMult`, `armourMult=1/Math.max(fracPast,1e-4)` | **computed** (`mk calc`) | — (derived comparison; combines read Structure/Evasion with this door's armour/shield) | Comparison panel only | **computed** |
| Time to kill (`m_4`, label `l_m4`) | s (or "never" / ">1 h") | `ttk = effEHP===Infinity? Infinity : effEHP/DPS` (`DPS=8000`) | **computed** (`mk calc`) | — (derived, uses test-rig incoming fire) | Comparison panel only | **computed** |
| Tuning grid (`tun`) | × per nature | `r[k].toFixed(2)` for `vk,ve,vx,vo` (the renormalized resists), colored up/dn/eq vs 1.0 | — (grid readback of the four resist sliders) | the four nature resists | Combat kernel | **live** (echo of `v_a2` resists) |
| Verdict line (`verdict`) | text | Branch on `soakFrac`/`shieldHolds`/`fracPast` — prose explaining the asymmetry for current settings | — | — | UI prose only | **computed** |
| Slider readouts (`s_cap`, `s_regen`, `s_arm`, `s_shot`, `s_vk`/`s_ve`/`s_vx`/`s_vo`) | pts, /s, Defense, /100, × | echoes of the mapped slider values (e.g. `set('s_cap', fmt(pool)+' pts')`) | — (inline slider labels) | mirror of the input dials | — | **computed** |

## C. CROSS-DOOR READS — values the panel shows but does NOT own
| Value | Owned by (which door) | Why this door displays it | Marker used |
|---|---|---|---|
| **Evasion mult** (`v_ev`, `EVA_READ=3.2`, max `EVA_MAX=20`) | Chassis (hull volume) + Propulsion (thrust-to-mass) | Evasion is the FIRST of the four layers a shot meets (whether the shot is even rolled) and the strongest term (×20 vs armour's ×10); the door shows it to display the full effective-health picture, but "cannot sell you the layer that stops damage from ever being rolled." | **read** (`mk read`) |
| **Structure** (`v_st`, `HP_READ=4000`) | Chassis (frame size) | Structure is the FOURTH/innermost layer (raw hit points); it's the base term in `effEHP` (`baseEHP=HP_READ*EVA_READ*armourMult`), so the door reads it to compute effective health and TTK. | **read** (`mk read`) |
| **Shield soak fraction by nature** (`m_1`, `NAT[nat].soak` = 1.00/0.50/0.75/0.00) | The ATTACKER (the incoming weapon's Nature — a Weapons-door / kernel-owned constant) | The load-bearing asymmetry: a shield's soak against a hit is fixed by the attacker's weapon nature, "identical on every target in the galaxy," and the defender gets no say. Shown so the player sees their pool's real worth swing with incoming nature. | **reaches the sim** (`mk sim`) — but attacker-set, not this door's dial |
| **Incoming fire nature + shot size** (test rig: `nat`, `shot`) | The attacker's weapons (not a build value at all) | A test harness to demonstrate the asymmetry and shot-size sensitivity; drives `m_1`–`m_4` but writes nothing on the component. | — (no marker; test controls) |

## D. NOTES / DISCREPANCIES
- **Constants are explicitly "illustrative baseline," not sim-authoritative reads.** The JS comment on line 271 says `EVA_READ=3.2, EVA_MAX=20, HP_READ=4000; // read from chassis + propulsion (illustrative baseline)`. So Evasion and Structure are shown as placeholder numbers standing in for whatever Chassis/Propulsion actually produce — the door demonstrates the *wiring* (that it reads them), not live cross-door values.
- **Verbatim kernel constants the door claims to reproduce** (JS comment, lines 259–263): shield soak by attacker nature Kinetic 1.0 / Energy 0.5 / Explosive 0.75 / Exotic 0.0; `ArmourSoakPerPoint 1.5`; `ArmourMinPassFraction 0.1`. From `installations.json`: shield-generator Shield 0..1500 (150), CarryMass 1..2000 (20); ward-projector Shield 0..600 (60) + ShieldRegenFraction 0..5 (1.0); composite plating HP 1..1500 (150), Defense 0..50 (5), resists all 1.0; ablative Vs 0.6/2.0/1.0/1.0; reactive Vs 1.6/0.6/2.0/1.0.
- **Zero-sum ruling is imposed by the door, and shipped values violate it.** The `warnbox` (line 240) admits the shipped ablative (0.6/2.0/1.0/1.0 = **4.6**) and reactive (1.6/0.6/2.0/1.0 = **5.2**) do NOT sum to 4.0 — they are "flagged balance values in the JSON, authored before the 🔒 2026-07-29 zero-sum ruling." The door renormalizes to 4.0, so it reproduces their *intent* (energy-strong / kin-expl-strong) but not the exact shipped numbers, "awaiting the same balance pass their own descriptions already flag."
- **A published floor/ceiling on resists is still open (balance, not design).** Footer (line 254): "Still flagged (balance, not design): a floor and ceiling so no design sits at 0.00 against a nature — suggested band 0.25–2.50, unset until playtest." The door currently lets a nature resist approach ~0 (e.g. Null-ward preset `[20,20,20,60]` pushes non-exotic resists low).
- **`regenPts = regenFrac*300` and mass formulas are marked "illustrative."** The fraction→points/s conversion (line 349) and both mass formulas (`pool/9 + regenFrac*40`; `Defense*30`, lines 389/397) are labeled "illustrative" / "CarryMass-scale, illustrative" — so Mass is a computed placeholder, not a verified engine value.
- **Three sibling capabilities were explicitly moved OUT of this door** ("What the four authored doors got wrong," lines 245–247): **Hardening** writes `EnvironmentalResistance` read only in the environmental-attrition step of `GroundForcesProcessor.cs` (now Defense ▸ Hardening 5.3 → ground attrition, i.e. survival/environment, not combat); **Fortification** is a building that divides incoming (capped at halving), ground-only — infrastructure, not kit; **Evasion** is Chassis+Propulsion, not sold here. A naive reading that Defense owns hardening/fortification/evasion is wrong per this file.
- **The incoming-fire chips + shot-size slider are a test rig, not build inputs** — worth restating: they change no saved component stat; they only animate `m_1`–`m_4`. Treat them as "does not write to the component."
- **`a2v` for armour reports the max-resist nature** (`mxk = reduce(...r[b]>r[a])`), i.e. the "Tuned toward" readout names whichever of the four resists is highest and its multiplier — a summary of the four dials, not a fifth dial.

## VERIFIED (Phase 2 — personal pass, 2026-08-02)
Checked the census against `defensederived.html` source (`<script>` constants + slider inputs), line by line.
- **Constants confirmed** (`:270-272`, `:284`): `ARMSOAK=1.5, MINPASS=0.1, DPS=8000`; `EVA_READ=3.2, EVA_MAX=20, HP_READ=4000` (commented "illustrative baseline"); `CAP_MAX=1500, REGEN_MAX=5, DEF_MAX=50`; `PRESET={plain:[25,25,25,25], energy:[15,50,25,25], kinexpl:[40,15,50,25], exotic:[20,20,20,60]}`. All exact.
- **Slider defaults confirmed** (`:149-179`): `cap`=10, `regen`=0, `arm`=10, `vk/ve/vx/vo`=25 (min **1** max 100), `shot`=35 (test rig). All match.
- **Layer default confirmed**: shield (`let layer='shield'`, `.lchip[shield]` aria-pressed true); armoursec starts `display:none`.
- **No corrections.** The census correctly separates the two buildable layers (shield, armour) from the two read layers (evasion←chassis+propulsion, structure←chassis) and flags the test-rig chips/slider as writing nothing to the component. The zero-sum-4.0 ruling and the illustrative mass/regen formulas are recorded accurately. **This is a load-bearing input for the Assembler's `Toughness`/`ShieldCapacity`/`Evasion` totals — the read markers here are exactly the cross-door wires the Assembler must carry.**
