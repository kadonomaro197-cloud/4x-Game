# 01-IO-AURA — Projected Effects, derived — the one thing a unit does to OTHER units, and the engine pass it needs
> Source: `docs/Actual HTMLs Of designers/auraderived.html` · Component-designer door · Census (Phase 1 draft, unverified)

## What this door builds
This designer builds an **aura projector** — a component that reaches OUTWARD and changes the *other* units standing inside a radius, instead of changing the unit that carries it. Think of a rally banner, a dread presence, a command net, a jamming bubble, or a projected shield: a field that lands on someone else. That one difference — it acts on OTHERS, not on SELF — is why it is its own door and not an Enhancer.

The shape is **two forced choices plus two sliders**: choice 1 picks *what the field projects* (Rally / Dread / Command / Jamming / Ward), choice 2 picks *who it lands on* (Friends / Foes / Everyone, filtered by IFF), and the two dials set *strength* and *radius*. Everything the panel outputs is marked **ENGINE-PENDING** on purpose: the developer's own headline in the HTML says this is a **SPEC, not a live designer** — the engine has no "aura pass" (per-tick sweep of neighbours), and four of the five effects write a variable (a morale field) that does not exist yet. Only **Jamming** writes a variable that is already in the sim (detection range).

## A. INPUTS — every control the user can touch

### A.1 Forced choices (chips / radio buttons)
| Control group | Options (exact button labels) | What each option selects (JS data object key / render branch) | Default option |
|---|---|---|---|
| Effect — "what does the field project?" (`.echip`, `data-e`) | `Rally — steady friends` | `eff='rally'` → `EFFECT.rally` (writes `morale / steadiness (+)`, `mlo:0 mhi:50 munit:'%'`, `read:'no'`, opens on `tgt:'friends'`) | **Rally** (`aria-pressed="true"`) |
| Effect (same group) | `Dread — break foes` | `eff='fear'` → `EFFECT.fear` (writes `morale / to-hit (−)`, `mlo:0 mhi:50 %`, `read:'no'`, opens on `tgt:'foes'`) | — |
| Effect (same group) | `Command — quicken friends` | `eff='command'` → `EFFECT.command` (writes `switch-cooldown / to-hit (+)`, `mlo:0 mhi:60 %`, `read:'partial'`, `tgt:'friends'`) | — |
| Effect (same group) | `Jamming — blind foes` | `eff='jamming'` → `EFFECT.jamming` (writes `detection range (−)`, `mlo:0 mhi:60 %`, `read:'yes'`, `tgt:'foes'`) | — |
| Effect (same group) | `Ward — shield friends` | `eff='ward'` → `EFFECT.ward` (writes `projected shield pool (+)`, `mlo:0 mhi:800 munit:' pts'`, `read:'partial'`, `tgt:'friends'`) | — |
| Target — "who does it land on?" (`.tchip`, `data-t`) | `Friendly units` | `tgt='friends'` → `TGT.friends='friendly units'` | **Friendly units** (`aria-pressed="true"`) |
| Target (same group) | `Enemy units` | `tgt='foes'` → `TGT.foes='enemy units'` | — |
| Target (same group) | `Everyone in range` | `tgt='all'` → `TGT.all='everyone in range'` | — |

> Coupling note (render branch): clicking an **effect** chip resets the target to that effect's natural side — `qa('.echip')...c.addEventListener('click',()=>{ eff=c.dataset.e; tgt=EFFECT[eff].tgt; render(); })`. The target is then freely re-settable via the target chips (the prose calls it "yours to set, because it is a property of the emitter").

### A.2 Sliders
| Slider label | element id | Range (min–max in REAL units) | Default value | Mapping (linear / logmap / other) | Exact formula the slider feeds | The number it ultimately sets |
|---|---|---|---|---|---|---|
| "Strength — how hard it pushes" (relabelled at render to `'Strength — '+E.mlab`, e.g. "Strength — morale") | `str` | `min=0 max=100` raw; real units are per-effect: Rally/Dread 0–50 %, Command/Jamming 0–60 %, Ward 0–800 pts (from `E.mlo`/`E.mhi`) | `45` | **linear** — `lin(strV, E.mlo, E.mhi)` where `lin(d,lo,hi)=lo+(hi-lo)*d/100` | `const magnitude = lin(strV, E.mlo, E.mhi);` — and `const strengthFrac = strV/100;` feeds the mass formula | `magnitude` = the per-effect variable it would write (morale %, cooldown-cut %, detection-cut %, or shield pts) |
| "Radius — how far it reaches" (ends: `50 m — close` / `5,000 m — grand`) | `rad` | `min=0 max=100` raw; real units **50 m – 5,000 m** (`RAD_LO=50, RAD_HI=5000`) | `40` | **logmap** — `logmap(radV, RAD_LO, RAD_HI)` where `logmap(d,lo,hi)=lo*Math.pow(hi/lo,d/100)` | `const radius = logmap(radV, RAD_LO, RAD_HI);` then `const area = Math.PI*radius*radius;` | `radius` (metres), which drives `area` (π r²) → `areaKm` → mass and units-affected |

### A.3 Toggles / checkboxes / presets / secondary choices
— none — (no checkboxes, toggles, or presets exist in this HTML; the only inputs are the two chip groups and the two range sliders.)

## B. OUTPUTS — every readout the panel produces
| Output label | Units | Formula (from the JS) | Honesty marker shown in panel | Sim variable it writes | Consumer system that reads it | Build state |
|---|---|---|---|---|---|---|
| Design name (`v_name`) | — | `auraName(radius,strengthFrac)` = `[scale,temper,E.noun]` joined; scale = Grand≥3000 / Wide≥1500 / (blank) / Close<500 m; temper = Potent≥0.66 / Faint≤0.33 | (name only) | — | (label only) | **computed** |
| Reproduction line (`v_namesub`) | — | static: `'effect ← what it projects · target ← who it lands on · strength & radius ← the two dials'` | (name only) | — | — | **computed** |
| Effect headline (`oname`) | mixed | `E.n+' — '+magnitude+munit+' over '+radius+' m to '+TGT[tgt]` | (name only) | — | — | **computed** |
| Strength (`v_a1`) | `%` or ` pts` | `magnitude = lin(strV, E.mlo, E.mhi)` | `you set it` (`mk set`) | — (would write `E.variable`; none exists) | aura pass (unbuilt) | **engine-pending** — waits on the aura pass; the variable itself waits per-effect (morale field for rally/dread) |
| Radius (`v_a2`) | `m` | `radius = logmap(radV, 50, 5000)` | `you set it` (`mk set`) | — | aura pass (unbuilt) → neighbour sweep radius | **engine-pending** — waits on the aura pass |
| Targets (`v_a3`) | — | `TGT[tgt]` (`friendly units` / `enemy units` / `everyone in range`) | `you set it` (`mk set`) | — (would drive the IFF filter) | aura pass IFF filter (unbuilt) | **engine-pending** — the IFF flag exists but no pass consumes it as a target choice |
| Area covered (`v_a4`) | `km²` or `m²` | `area = Math.PI*radius*radius`; shown as `areaKm.toFixed(2)+' km²'` if ≥1 else `fmt(area)+' m²'` | `computed` (`mk calc`) | — | comparison readout only | **computed** |
| Mass (`v_a5`) | `kg` | `mass = Math.max(20, strengthFrac*400 + areaKm*600)` (comment: "illustrative cost: strength linear, radius quadratic") | `computed` (`mk calc`) | — (illustrative; not tied to a component mass field here) | component design (illustrative) | **computed** |
| The variable it writes (`m_1`) | text | `E.variable` (e.g. `morale / steadiness (+)`, `detection range (−)`); subtext `'on each '+TGT[tgt]+' in range'`; row class `R.cls` (g/a/r) | `pending` (`mk pend`) | per-effect: Rally/Dread → morale field (**does not exist**); Command → `SwitchableAfter`; Jamming → detection range / signature; Ward → shield pool | Rally/Dread → (unbuilt morale system); Command → doctrine-switch; Jamming → sensors/fog-of-war; Ward → Defense shields | **engine-pending** — every effect marked `pending`; Jamming waits on the pass only, the other four wait on the pass and/or a new variable |
| Its effect at strength (`m_2`) | `%` or ` pts` (signed) | `signed = (E.tgt==='foes' \|\| tgt==='foes') ? '−' : '+'`; then `signed+magnitude+munit`; row forced class `mt g` | `computed` (`mk calc`) | — | comparison readout only | **computed** |
| Units affected (`m_3`) | count | `affected = Math.max(0, Math.round(area*DENSITY))`, `DENSITY=1/40000`; shown `'≈'+affected`; subtext `'EMERGENT — depends on who is in range'`; row class `mt a` | `emergent` (`mk calc` text "emergent") | — (never settable) | battlefield (who stands in radius at fire time) | **emergent** — the panel's own label; explicitly "shown but never set" |
| Can the engine read it? (`m_4`) | text | `R.m4` from `READMAP[E.read]` — `yes`→`'aura pass only'`, `partial`→`'variable exists, no pass'`, `no`→`'no — needs both'`; subtext `R.s4`; row class `R.cls` | `pending` (`mk pend`) | — | (build-state gauge) | **engine-pending** — waits on the aura pass (and, for rally/dread, the morale field) |
| Verdict line (`verdict`) | text | branch on `E.read` (`'yes'`/`'partial'`/else) — three fixed prose strings | (prose) | — | — | **computed** (narrative) |
| Forced-choice note (`enote`) | text | `'<b>Projecting: '+E.n+'.</b> '+E.desc+' … '+E.why` | (prose) | — | — | **computed** (narrative) |

## C. CROSS-DOOR READS — values the panel shows but does NOT own
| Value | Owned by (which door) | Why this door displays it | Marker used |
|---|---|---|---|
| `SwitchableAfter` — the doctrine/stance switch cooldown | Enhancers door (the "Interface" enhancer already cuts it for a SELF host) | The **Command** effect names this as the variable it would write, by area, onto neighbours | `partial` → row class `a` (amber); "variable exists, no pass" |
| Projected **shield pool** (the `ShieldCapacity`-style pool) | Defense door (builds exactly this shield pool, applied to SELF) | The **Ward** effect names this as the variable it would project outward onto friends | `partial` → row class `a` (amber); "variable is real but applied to SELF, not by area" |
| **Detection range / signature** (the sensor/fog-of-war system) | Sensors / Detection door (the "detection / signature system is real and rigorous") | The **Jamming** effect writes a detection-range penalty onto enemies — the one target variable that already exists | `yes` → row class `g` (green); "the target variable EXISTS (detection range)" |
| **IFF** (identify-friend-or-foe flag) | Targeting / every unit already carries it | The target choice (friends/foes/all) filters the aura by this existing flag | prose only (Section A note: "Filtered by IFF … every unit already carries for targeting") |

## D. NOTES / DISCREPANCIES
- **The whole door is PROPOSED / a SPEC.** The top band and the `<script>` comment both state it plainly: "this door is a SPEC, not a live designer." Nothing here reaches the running game. The single missing engine primitive is the **aura pass**: once per tick, for each aura-projector-mounting unit, walk nearby units, keep those matching the IFF target choice inside the radius, and add/subtract the effect from a named variable. The HTML likens it to "the same shape as a weapon's target-acquisition sweep the engine already runs, except it writes a buff instead of a hit."
- **Morale field does not exist.** Rally and Dread (`read:'no'`) write a morale/steadiness variable that has no home — "a Pulsar unit is whole-or-dead and never routs," the same wall the **Steadiness / fury enhancer** hit. Build order in the HTML: ① aura pass + Jamming (variable exists), ② Command + Ward (variables exist, need projecting by area), ③ Rally + Dread (wait on the morale field).
- **Two guard-rails stated as non-negotiable** (design constraints on the future pass, not controls in this HTML): (1) **take-the-best, not sum** — two rally beacons over one squad do not stack (mirrors the Defense door's shields-take-the-best rule); (2) **a destroyed projector drops its field that tick** (the grave rung — the aura is a component that can be shot off).
- **Two effects named but deliberately absent from the five buttons:** a **healing / repair field** is ⚠ HELD BACK (blocked on the whole-or-dead ruling, like field repair), and a **permanent "presence" that never moves or dies** is 🚫 REFUSED (fails cradle-to-grave — un-researchable/un-buildable/un-losable). Neither is a control; both are in the science-fiction cross-check table as stated exclusions.
- **Emergent vs dial boundary is explicit:** strength, radius, and target pass the intrinsic test (settable knowing only the projector) so they are legitimate dials; **units affected** depends on who is standing in the radius, so it is an emergent readout (`DENSITY=1/40000` is labelled "illustrative … EMERGENT, not a dial").
- **Mass** here is labelled "illustrative cost" (`strengthFrac*400 + areaKm*600`, floor 20 kg). It is a computed comparison figure in this panel, not asserted as writing a real component-mass sim field — treat any downstream use as unverified.
