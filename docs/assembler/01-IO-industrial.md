# 01-IO-INDUSTRIAL — Industrial, re-derived - the healthiest door, and its three exceptions
> Source: `docs/Actual HTMLs Of designers/industrialderived.html` · Component-designer door · Census (Phase 1 draft, unverified)
>
> **⚠ CORRECTED against `06-OUTPUTS-BY-DOOR.md` (reader-verified 2026-08-02).** The lab **`Cost Per Day` is LIVE, not
> dead** — it is read and paid at `ResearchProcessor.cs:107-118` (write `ResearchPointsAtbDB.cs:71`). Only **Fighter
> Construction Points** is the genuine dead dial (no `fighter-construction` industry type exists). Everything else in
> this "healthiest door" is correct. See `06` §Industrial + correction #13.

## What this door builds
This is the **industry / installation** door. It stamps out the buildings and plant that mine, refine, manufacture, assemble ships, research, construct, and fortify — mines, automines, refineries, factories, unit-assembly yards, research labs, and bunkers (the interactive builder ships seven of the ten templates; `infrastructure` and the two extra construction templates are discussed only in prose).

Its shape is the plainest in the whole designer: **one forced choice — what kind of plant — and one size slider.** Output is proportional to size, and size (through `Mass`) is what you pay for. There is no rate-vs-efficiency trade inside a factory; industry is a **pure scale dial** — how much plant did you build. Some templates add a second slider: a manned plant gets an **Automation** dial (spend mass to shed crew), the yard gets an **Assembly rate** (throughput, paid in crew) plus a **domain** sub-choice that names the facility, and the lab gets **Field focus** (concentrate research into one field).

The door's whole point is a **mechanically checkable rule**: a dial is free exactly when the template's `Mass` formula does not read it. Seven templates price honestly (mass tracks the dial); three (`research-lab`, `bunker`, `infrastructure`) have a **hard-coded constant `Mass`**, which makes every dial on them free at once. The builder's checkbox ("Price the constant-mass templates…") demonstrates the proposed fix.

## A. INPUTS — every control the user can touch

### A.1 Forced choices (chips / radio buttons)
| Control group | Options (exact button labels) | What each option selects (JS data object key / render branch) | Default option |
|---|---|---|---|
| **The door — what kind of industry** (`.chip[data-v]`) | `Mine` | `IND.mine` — Area mine, `out=Area*0.00001`, `mass=Area*0.05*(1+3*b)`, priced | **Mine** (`aria-pressed="true"`) |
| | `Automine` | `IND.automine` — `out=Size*0.005`, `mass=Size*2000`, crew 0, ShipCargo mount, priced | |
| | `Refinery` | `IND.refinery` — `out=Size*0.1`, `mass=Size*(1+3*b)`, priced | |
| | `Factory` | `IND.factory` — `out=Size*0.1` ×3 point types, `mass=Size*1000*(1+3*b)`, priced | |
| | `Unit Assembly ` | `IND.yard` — `isYard:true`; opens the domain sub-row; `out=b` (assembly rate), `mass=Berth*8`, priced | |
| | `Research lab ` | `IND.lab` — `mass:()=>100000` CONSTANT, `priced:false` | |
| | `Bunker ` | `IND.bunker` — `mass:()=>50000` CONSTANT, `priced:false`, `linear:true` | |
| **Yard domain** (`.chip.ydom[data-dom]`, `#domrow`, shown only when `isYard`) | `Space` | `YARD_DOM.space` — Orbital Drydock; d1 `Berth tonnage`, builds "spacecraft and orbital stations" | **Space** (`aria-pressed="true"`) |
| | `Naval` | `YARD_DOM.naval` — Naval Yard; d1 `Slip tonnage`, builds "warships and submarines" | |
| | `Air` | `YARD_DOM.air` — Airfield; d1 `Airframe mass`, builds "aircraft and aerospace" | |
| | `Vehicle` | `YARD_DOM.vehicle` — Vehicle Works; d1 `Chassis mass`, builds "tanks, artillery and walkers" | |
| | `Infantry` | `YARD_DOM.infantry` — Training Depot; d1 `Billet capacity`, d2 `Training rate`, unit `recruits/cycle`, "TRAINED not built" | |

Note: the domain is a genuine forced choice — "you cannot build a submarine in a vacuum drydock." The numeric ranges are identical across domains; the domain only changes the facility name, dial labels, unit string, and noun ladder. Each domain builds only units whose chassis-environment it matches (owned by the Chassis door).

### A.2 Sliders
Both sliders are raw `0..100` HTML ranges; each template maps them to REAL units via `logmap` (default) or `lin` (when `linear:true`). Values shown below are the per-template real-unit ranges from the `IND` data object.

| Slider label | element id | Range (min–max in REAL units) | Default value | Mapping (JS fn) | Exact formula the slider feeds (quoted JS) | The number it ultimately sets |
|---|---|---|---|---|---|---|
| **Primary dial** (label per template — Area / Size / Berth tonnage / Research Points / LocalFortify) | `d1` (raw `min=0 max=100 value=43`) | mine `Area 10000..1e8` (def 1e6); automine `Size 5..5000` (def 5); refinery `Size 1000..10000` (def 5000); factory `Size 1000..25000` (def 5000); yard `Berth tonnage 100..200000` (def 10000); lab `Research Points 1..100` (def 10); bunker `LocalFortify 0..1` (def 0.25) | shipped `def` (STOCK snaps `d1` back to it on chip switch) | `logmap(d,lo,hi)` = `lo*Math.pow(hi/lo,d/100)`, except bunker `lin(d,lo,hi)` (`linear:true`) | `const v = I.linear? lin(d1,I.lo,I.hi): (d1===STOCK[job].d1? I.def: logmap(d1,I.lo,I.hi));` then `out = I.two? I.out(v,v2): I.out(v);` | The template's Output (industry rate / research points / fortification) and, through `mass`, the cost block |
| **Second dial** (Automation / Assembly rate / Field focus / Adjacent projection; hidden when `!I.two`) | `d2` (raw `min=0 max=100 value=50`) | mine/refinery/factory `Automation 0..0.9` (def 0); yard `Assembly rate 10..5000` (def 200); lab `Field focus 0..100` (def 0); bunker `Adjacent projection 0..1` (def 0.12) | shipped `def2` | `lin(d,l2,h2)` when `lin2:true`; `logmap` for yard (no `lin2`) | `const v2 = I.two? (d2===STOCK[job].d2? I.def2: (I.lin2? lin(d2,I.l2,I.h2): logmap(d2,I.l2,I.h2))): 0;` | Automation: `mass*(1+3*b)` up, `crew*(1-b)` down. Yard: `out=b`, `crew=b*0.2`. Lab/bunker: the second capability (bonusCategory / AdjacentProjection) |

### A.3 Toggles / checkboxes / presets / secondary choices
| Control | id | Effect when active |
|---|---|---|
| "Price the constant-mass templates the way the other seven already are **(proposed - slice I1)**" | `fix` (checkbox, `#fixwrap`) | On lab/bunker (templates with `fixMass`), swaps `mass` from the constant to `I.fixMass(v)` — lab `Mass=Research Points*10000`, bunker `Mass=50000*(0.2+a*3.2)`. Re-labels the dial "now priced", flips markers from CONSTANT/dead to computed/honest. Purely a demonstration of the proposed fix; no effect on already-priced templates. |

## B. OUTPUTS — every readout the panel produces
| Output label | Units | Formula (from JS) | Honesty marker shown | Sim variable it writes | Consumer system that reads it | Build state |
|---|---|---|---|---|---|---|
| **Output** (`v_a1`) | per template `unit` (min/tick · refine pts · pts×3 types · build pts/day · RP/tick · fortify) | `I.out(v[,v2])` — e.g. mine `Area*0.00001`, factory `Size*0.1`, yard `=b`, lab `=RP`, bunker `=LocalFortify` | `reaches the sim` (`mk sim`) | Industry rate → `IndustryAtb`/`DataDict` (keys `refining`, `component-construction`, `installation-construction`, `ordnance-construction`, `ship-assembly`); lab → `ResearchPointsAtbDB`; mine/automine → mineral output rate | Industry processor / research processor / mining processor | **live** |
| **Mass** (`v_a2`) | kg / t / kt (`kg()`) | `(fixed && I.fixMass)? I.fixMass(v): I.mass(v,v2)` — mine `Area*0.05*(1+3*b)`, factory `Size*1000*(1+3*b)`, yard `Berth*8`, lab const `100000`, bunker const `50000` | `computed` when priced (`mk calc`) / `a CONSTANT` when unpriced (`mk dead`) | `Mass` (the component's mass attribute; the single point of control feeding all seven cost channels) | Component/construction cost system; every other cost formula reads `[Mass]` | **live** (priced templates) / **live-but-flat** (constant on lab/bunker — the failure the door exists to name) |
| **Output per tonne / crew — ladder** (`v_a3`) | ratio | `ladder = I.isYard? perCrew: perT`; `perT=out/(mass/1000)`, `perCrew=out/crew` | `design check` → `flat - honest` (`mk sim`) or `unbounded` (`mk dead`) | — (comparison metric only) | This panel's own honesty verdict | **computed** |
| **Mass** (`v_a4`) | kg / t / kt | `kg(mass)` (same `mass` as `v_a2`) | plain (no marker) | `Mass` | cost system | **live** |
| **Crew** (`v_a5`) | count | `I.crew(v[,v2])` — mine `Area*0.005*(1-b)`, automine `0`, refinery `Size*0.1*(1-b)`, factory `Size*5*(1-b)`, yard `b*0.2`, lab `20`, bunker `50` | plain (`0 - unmanned` when <1) | `CrewReq` (draws against the `ManpowerTools` pool) | Colony manpower / crewing system | **live** |
| **Build cost** (`v_cost`) | credits · BP · RP | `credits=I.cr(mass)` (120, bunker 200); `bp=I.bpFromDial? I.bp(v): I.bp(mass)` (factory `Size*20`, lab `mass/2`, else `=mass`); `research=I.rc(mass)` (lab 10, else 0) | plain | `CreditCost`, `BuildPointCost`, `ResearchCost` | Construction / production queue; research unlock | **live** |
| **Output** (ladder, `m_1`) | template `unit` | `fmt(out)` (same as `v_a1`) | — | industry rate (as above) | industry/research/mining processors | **live** |
| **Output per tonne** (`m_2`) | ratio | `I.isYard? perCrew: perT` | `flat - no dominant setting` / `rises for free` | — | ladder/balance readout | **computed** |
| **Output per crew** (`m_3`) | ratio | `I.isYard? perT: perCrew` (swapped for yard) | `people are scarce` / `nobody aboard` / `mass tracks the berth instead` | — | ladder/balance readout | **computed** |
| **Ladder test** (`m_4`) | pass/fail | `flat? ' passes': ' fails'` where `flat = I.priced || fixed` | `benefit / cost is constant` / `benefit rises, cost stands still` | — | this panel's verdict | **computed** |

Note on markers: the panel's `reaches the sim` / `computed` / `a CONSTANT` / `design check` markers describe **whether the dial tracks a cost**, not solely whether the value reaches the engine. `Mass`, `Crew`, and the cost channels are all real component attributes that reach the sim (hence "live" above) — the `computed`/`CONSTANT` marker is the door's honesty flag about whether they move with the dial.

## C. CROSS-DOOR READS — values the panel shows but does NOT own
| Value | Owned by (which door) | Why this door displays it | Marker used |
|---|---|---|---|
| Housing Comfort, Support Colonists, gravity/pressure tolerance ranges (on the `infrastructure` template) | **Civic door** | The prose "What this kills" band lists them among `infrastructure`'s six free dials, but explicitly hands them to Civic — "Most of those dials belong to the Civic door … and are covered there." Only `Support Capacity` (`InfrastructureCapacityAtb`) is Industrial's own. | prose only (not in interactive builder) |
| Chassis-environment match per yard domain (which units a domain can build) | **Chassis door** | Each yard domain "builds only the units whose chassis-environment it matches (the Chassis door)." The domain forced choice here consumes the Chassis door's environment tagging. | prose (`verdict` for `yard`) |
| Fortification-is-infrastructure ruling | **Defense door** | Bunker `LocalFortify`/`AdjacentProjection` "was cut from Defense because it is infrastructure, not a combat defence … and it arrived here unpriced." Displayed to explain why fortification lives on this door now. | prose (bunker `verdict`) |

## D. NOTES / DISCREPANCIES
- **`fix` checkbox is PROPOSED (slice I1), engine-pending.** It demonstrates `Mass = Research Points × 10,000` (lab) and `Mass = 50000*(0.2+a*3.2)` (bunker). Neither `fixMass` exists in the shipped code — the shipped templates have constant `Mass`. This is a design proposal, not current behaviour.
- **Two truly dead dials called out but NOT present in this builder.** The factory's **`Fighter Construction Points`** (0→1,000) writes `0`, is absent from the `DataDict`/`IndustryAtb` rate table, and there is **no `fighter-construction` industry type** (the five are `refining`, `component-construction`, `installation-construction`, `ordnance-construction`, `ship-assembly`) — engine-pending on a decision to add or delete. The lab's **`Cost Per Day`** is stored on `ResearchPointsAtbDB._costPerDay` with a getter and **read by nothing** (grep of `.CostPerDay` hits only `ResearcherDB`/`AdministratorDB`, different classes). In this interactive builder the factory's second dial is Automation and the lab's is Field focus, so neither dead dial is exposed here — they are prose findings only.
- **Three constant-`Mass` templates = the door's failure mode.** `research-lab` (`Mass=100000`), `bunker` (`Mass=50000`), `infrastructure` (`Mass=1000` AND `BuildPointCost=100`, `CreditCost=0`). Constant mass makes every dial on them free simultaneously. `infrastructure` is discussed but has no chip in the builder.
- **Boundary dispute — `Support Capacity`.** The `InfrastructureCapacityAtb` (the colony's chassis-budget every other installation draws against) is explicitly claimed as **Industrial's**, while the other infrastructure dials go to Civic. An assembler must not double-count it under Civic.
- **The proposed CI gauge.** The door's real deliverable: for every `GuiSelection*` property in every template, assert its name appears in at least one of the seven cost formulas (Mass/Volume/CrewReq/ResearchCost/CreditCost/BuildPointCost/ResourceCost) **or** in an `AtbConstrArgs` argument list — flag it if neither. Data-only, no engine/save risk. Catches shape A (constant Mass), shape B (Mass = a different dial), and shape C (dial writes nothing).
- **`TileFootprint` (bunker, 1→40)** is a cost-with-no-benefit dial (same shape as the building foundation's footprint) — minimum strictly dominates; the fix proposes it should BUY footprint. Not exposed in this builder.

## VERIFIED (Phase 2 — personal pass, 2026-08-02) — one engine cross-reference added
Checked the census against `industrialderived.html` source.
- **Confirmed:** data objects `IND` (`:370`), `YARD_DOM` (`:400`), `STOCK` (`:446`); init `let job='mine', dom='space'` (`:407`); sliders `d1` def **43** (`:268`), `d2` def **50** (`:270`), `fix` unchecked (`:272`). Constant-mass templates exact: lab `mass:()=>100000` + `fixMass:a=>a*10000` (`:387-389`), bunker `mass:()=>50000` + `fixMass:a=>50000*(0.2+a*3.2)` (`:391-393`). Yard `isYard:true` (`:385`). 7 interactive templates (mine/automine/refinery/factory/yard/lab/bunker); infrastructure + two construction templates are prose-only. The industry rate-table keys (`refining`, `component-construction`, `installation-construction`, `ordnance-construction`, `ship-assembly`) and the two dead dials (Fighter Construction Points, Cost Per Day) are recorded accurately as prose findings.
- **🔎 Engine cross-reference (not a census error — a HTML-vs-engine discrepancy to carry into the matrix):** the census §D repeats the door's claim that the lab's **`Cost Per Day` is "read by nothing."** The prior designer-audit Phase 2 (`docs/archive/designers-audit/02-OUTPUT-READABILITY-AUDIT.md`, crack C4) **found the opposite**: `ResearchPointsAtbDB.cs:71` copies `_costPerDay` into the live `ResearcherDB.CostPerDay`, so the value **does** reach the sim. The census correctly recorded what the HTML says (per the mission's "HTML wins" rule), but for the master matrix the build-state of `Cost Per Day` should be **live**, not dead. Fighter Construction Points (no matching `fighter-construction` industry type) remains genuinely dead/engine-pending.
- **No structural corrections.** For the Assembler, Industrial is the **build+tech backbone (C)**: it is the plant that physically builds every component the other 11 doors design, and its research-lab points gate what each door may design. Its outputs don't become a combatant's totals — they become the *construction and research rates* the Assembler's cost surface (rule #7) draws on.
