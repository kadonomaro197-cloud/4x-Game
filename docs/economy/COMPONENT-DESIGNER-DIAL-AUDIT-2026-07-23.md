# Component-Designer DIAL AUDIT — every dial of every door (2026-07-23)

**What this is.** The developer's call: the Component Designer + Entity Assembler is ONE universal tool
(the same one a human faction and a Zerg-like faction use — "the only specificity lives in the DIALS"),
so **every dial of every door** must be audited to that standard. This is that sweep. It grades each
player-settable dial on **five tests**:

1. **Wired** — does the dial actually drive a live mechanic (trace Property → AtbConstrArgs → `*Atb` →
   the consuming processor/resolver), or is it a dead knob nobody reads?
2. **Universal** — works for any faction/franchise (human, Zerg/organic, drone/AI, hive), or does it bake
   in a human-specific assumption that breaks "the same designer serves a Zerg faction"?
3. **Redundant** — duplicates another dial / shows the same quantity twice (the worked example: the hex
   `Range` dial vs `Range_m`, now hidden).
4. **Costed** — dialing the benefit UP costs something real (mass → materials/credits/crew/research/time)?
   A free-win dial violates `CONVENTIONS.md` §16 ("benefits AND costs must be apparent").
5. **Cradle-to-grave + DevTest** — the capability is researchable/buildable/losable (a component, not a
   bare flag) and reachable in the DevTest sandbox.

**Coverage.** 27 of 34 doors audited (below). **7 doors pending** — cut off by a session usage cap:
`Chassis ▸ Personnel / Vehicle / Hull / Structure / Prebuilt Units`, `Command ▸ Command`,
`Civic ▸ Development`. These re-run after the cap resets; **Chassis is the highest-value gap** (the
frames are where crew-source + human-vs-Zerg universality land hardest — `human-frame` vs `swarm-frame`).
The per-door adversarial VERIFY pass also mostly got cut off, so the flagged items below are the raw
audit reads — treat them as *strong leads to confirm against source before the fix*, exactly the way the
ground-Attack "free dial" turned out fixable byte-identically once the Mass formula was actually read.

**Headline.** 96 dials clean, **123 flagged** across 27 doors — but they collapse into **7 recurring
patterns**. The single most common is the **free-win dial** (a benefit that costs nothing), which is the
*same* pattern already closed for ship weapons (S9–S15) and the ground Attack dial. The single most
important is the **crew-source universality gap**, which shows up in ~12 doors and is answered by ONE
fix (the universal-crew design, filed separately).

---

## The 7 recurring patterns (what's actually wrong, in plain terms)

### 1. FREE-WIN DIALS — a benefit you crank for zero cost (the dominant pattern, ~60+ dials)
A benefit slider that never appears in the Mass formula, so you dial it to the ceiling for no weight,
materials, crew, or research. Two flavors:
- **(a) Benefit not in the Mass formula** → add an *anchored* Mass term (byte-identical if anchored at
  the current default — the exact S9–S15 / ground-Attack move). Examples: every augment bonus
  (`power-armor`/`reflex-booster`/`ward-projector`/`shield-generator` StrengthBonus/EvasionBonus/
  ToughnessBonus/Shield), `unit-caliber` Firepower/Toughness Caliber, `ground-rifle`/`autocannon`/`cannon`
  Range_m, `claw` Range_m, `energy-weapon` Range_m, `pulse-laser` Range, railgun/siege/flak **Tracking**,
  `plasma-repeater` Tracking, `passive-sensor` Scan Time, `troop-bay` Capacity, `missile-srb` Exhaust
  Velocity, `inertialess-drive` Evasion Override, `alcubierre` Efficiency-vs-Power, `missile-launcher`
  Max Calibre/Max Length (launch force).
- **(b) Hardcoded Mass constant** → the template's Mass is a fixed number that *ignores every dial*, so
  ALL its dials are free. `infrastructure`, `space-habitat`, `bunker`, `ward-projector`, `logistics-office`.
  Fixing means making Mass a formula of the dials — that *changes the cost curve*, so it's a rebalance
  decision, not byte-identical.
- **Downside dials dialed to zero for free** (same disease, inverted): `pulse-laser` Combat Heat,
  `siege-railgun` Recoil, `jammer` Self-Signature-Boost — the intended *weakness* costs nothing to erase.

### 2. INERT / DEAD DIALS — a gauge wired to nothing (~30 dials)
Visible sliders that author a number no code reads. The worst offenders:
- **`missile-payload` warhead block** (Explosive Mass, Frag Mass/Count/Cone, Liner Radius/Height/Thickness,
  Submunition Type/Count) — all mass-only; `MissileImpactProcessor` delivers **kinetic only**, so every
  warhead dial is a fidelity showcase with no blast.
- **`missile-electronics-suite`** — the whole seeker block is dead because **Guidance Type is a dead switch**
  (Dumbfire/Passive/Active picks nothing; the missile flies the same).
- **`shipyard` Slip Size** — the yard's *defining* spec (how big a ship it can build) does nothing; a
  1,000-ton slip happily assembles a 100,000-ton battleship (no tonnage gate).
- **`pd-director` Tracking Speed / Range**, **`beam-fire-control` Range / Size-vs-Range / Size-vs-Tracking**
  — cost mass, change nothing (the PD/fire-control math ignores them).
- **`passive-sensor` Resolution** (MegaPixel gauge nobody reads), **laser** Lens Diameter/Emissivity/
  ReloadRate/Radiator Volume (dead debug chains), **`factory` Fighter Construction Points**,
  **`missile-launcher` Auto Reloader Mass** (both advertised effects fake), **`reactor` Lifetime**
  (a cost with no payoff — fuel depletion isn't built), **`ground-locomotion` Amphibious** (cost, no benefit).

### 3. REDUNDANT DIALS / TWINS — the range-hide pattern (~12 dials)
Same quantity twice, or one component wearing several names:
- **`sensor-hardening-module` / `warp-stabilizer` / `drive-reinforcement`** are ONE component (same
  `HazardResistanceAtb`) wearing three names; the "Resisted Effect" selector *is* the template identity —
  exposing it lets a "Warp Stabilizer" resist sensor-jam (nonsense). Hide it (preset) — byte-identical.
- **`laser` ReloadRate** duplicates Charge Period; **`beam-fire-control` Size-vs-Range/Tracking** duplicate
  the direct Range/Tracking dials; **`missile-launcher` Max Calibre** duplicates Max Length; **`fuel-cargo-
  hold` Tank Radius** duplicates `stainless-steel-fuel-tank`.

### 4. UNIVERSALITY GAP — crew is population-only (the ruling-#1 theme, ~12 doors)
Nearly every door flags the same thing: `CrewReq` is authored as a population-drawn number with **no
self-crewing dial**, so a Zerg/drone/hive faction can't man its machines any other way. This is **one
cross-cutting fix**, already designed: crew *count* stays universal, crew *source* becomes a mounted
"manning organ" dial (human Automation Suite vs Zerg Self-Crew Organ), routed through one shared helper,
with the tank-vs-beast payoff on the ground supply-gate. (Full plan in the crew design.) Other human-bias
flags: `shipyard` Crew Size, `crew-automation` Crew Reduction, `rtg` (a tech hard-index).

### 5. LANDMINES — things that can CRASH a player (fix first)
- **`spaceport` UniqueID COLLISION** — two base-mod templates both claim `spaceport` (`storage.json`
  "Space Port" cargo hold vs `installations.json` "Planetary Spaceport Complex" facility). Load-order
  dependent dead-code-that-looks-live + a latent crash. Delete the shadowed `installations.json` payload.
- **`ordnance-cargo-hold` is BROKEN** — its dials bind a **non-existent class** (`StorageTransferRateAtbDB`,
  a stale rename), so instantiating the template **throws**. A corpse; fix-or-cut. (Check whether any
  starting colony lists it → New-Game crash risk.)
- **`TechData` hard-indexes unlocked techs** (`ChainedExpression.cs:487` — `Techs[techID]` with no guard)
  → blocks `rtg` and any tech-referencing formula; guard with `ContainsKey`.
- **`EnergyStoreAtb.OnComponentUninstallation` is an empty stub** — destroying a battery bank doesn't
  subtract its capacity (energy-family grave-rung gap).

### 6. FALSE TRADEOFFS / MISLEADING LABELS (rename-clarify)
- **Survey Speed unit bug** — `geo-surveyor`'s dial reads "per hour" but `GeoSurveyOrder` clocks points
  once per **day**; the two surveyor labels are swapped and both ~**24× wrong**. (Survey door headline.)
- **`steam-turbine` Output-vs-Efficiency** — a false tradeoff; efficiency is a hardcoded constant, so max
  core-% is strictly optimal.
- **`energy-weapon` Mode** — a real damage-nature knob presented as a cryptic int slider
  ("0=Melee 1=Ballistic 2=Energy 3=Artillery"); needs a labeled picker.
- **`scntr-engine` Fuel Type** — a degenerate one-member dropdown (no real choice).

### 7. MISSING DIALS / RUNGS (add — mostly larger builds)
Active-sensor/illuminator component; a single legible detection-range readout; missile seeker-range;
**research gates** on the many templates with `ResearchCost=0`; fuel-type pickers; a `mine` automation
dial (the exact pattern `automine` already has); cargo-TYPE and energy-TYPE pickers for franchise
universality; per-design range on railgun/siege/flak (currently a hardcoded class constant).

---

## Prioritized slice backlog (byte-identical quick wins first, crashes even sooner)

### Tier 0 — LANDMINES (do first; player-facing crash/data risk)
| Slice | Scope | Byte-identical? | Gauge |
|---|---|---|---|
| L0a | `ordnance-cargo-hold` binds a non-existent class → throws. Fix the class ref or cut the template. Check `StartingItems`. | no (bug fix) | `BaseModIntegrityTests` (base mod loads, zero skipped) |
| L0b | Delete the shadowed `installations.json` "Planetary Spaceport Complex" (UniqueID collision on `spaceport`). | yes (removes dead) | `BaseModIntegrityTests` |
| L0c | Guard `TechData` (`ChainedExpression.cs:487`) with `ContainsKey`. | no (bug fix) | a unit test on an unlocked-tech formula |
| L0d | Wire `EnergyStoreAtb.OnComponentUninstallation` to subtract capacity (grave rung). | no (behavior) | an energy uninstall test |

### Tier 1 — BYTE-IDENTICAL QUICK WINS (the range-hide + Attack-cost move; no behavior change)
| Slice | Scope | Gauge |
|---|---|---|
| T1a — HIDE dead/redundant dials (`GuiHint: None`) | Hardening Resisted-Effect ×3 (preset identity); `laser` ReloadRate/Lens Diameter/Emissivity; `beam-fire-control` Size-vs-Range/Tracking; `factory` Fighter Construction Points | `BaseModIntegrityTests` + designs still valid |
| T1b — COST the free benefit dials (anchored Mass term, anchored at current default) | augment bonuses (`power-armor`/`reflex-booster` StrengthBonus/Evasion/Toughness/Shield ×8), `unit-caliber` Firepower/Toughness Caliber, `ground-rifle`/`autocannon`/`cannon` Range_m, `energy-weapon` Range_m, `pulse-laser` Range, `passive-sensor` Scan Time, `troop-bay` Capacity | a per-dial "stock design unchanged, heavier design costs more" test (the S9–S15 shape) |

### Tier 2 — COSTED FIXES NEEDING A DECISION (they change the cost curve = a deliberate rebalance)
| Slice | Scope |
|---|---|
| T2a — hardcoded-Mass templates | Make Mass a formula of the dials on `infrastructure`, `space-habitat`, `bunker`, `ward-projector`, `logistics-office` (every dial is currently free) |
| T2b — armor nature budget | `ablative`/`reactive` VsKinetic/Energy/Explosive/Exotic are free AND unenforced (no zero-sum tradeoff); cost HP/Defense + add a nature budget |
| T2c — the rebalance free-dials | railgun/siege/flak **Tracking**, `siege` Recoil, `missile-launcher` launch-force, `alcubierre` EvP, `shield-generator` bonuses, `crew-automation` Crew Reduction |
| T2d — false tradeoffs / labels | Survey Speed 24× unit bug; `steam-turbine` Output-vs-Efficiency; `energy-weapon` Mode labeled picker; `scntr` Fuel Type |

### Tier 3 — UNIVERSALITY: the crew source (ruling #1 — answers ~12 doors at once)
The universal-crew design (separate file): `CrewTools.ExternalCrewDraw` shared helper + `CrewSource.Classify()`
readout + a mounted manning-organ dial (human Automation Suite / Zerg Self-Crew Organ) + open the ground
crew supply-gate. 5 slices; slice 1 is a byte-identical refactor.

### Tier 4 — WIRE-OR-CUT the inert mechanics (larger builds — the mechanic behind the dial doesn't exist)
Missile warhead damage model (Explosive/Frag/Liner/Submunition); missile guidance/seeker; `shipyard` tonnage
gate; PD-director range/tracking effect; fire-control range (flag default-off); `passive-sensor` Resolution;
reactor fuel-depletion; the "add missing dials" list.

---

## Decisions that are the developer's to make
1. **Cost curves (Tier 2):** the hardcoded-Mass templates (infrastructure/habitat/bunker/etc.) need a real
   cost formula — that re-prices them. Bless the curve, or keep them cheap on purpose?
2. **Armor nature dials:** zero-sum budget (tough vs one nature = weak vs another) or a per-point mass cost?
3. **Inert warhead/guidance/PD (Tier 4):** WIRE the mechanic (a real missile warhead + seeker model, PD
   range) or HIDE the dials until built? (Wiring is a real build; hiding is honest in the meantime.)
4. **Crew (Tier 3):** the 6 open crew questions in the crew design (drone vs beast label, block-vs-degraded,
   biomass pool shape, zero-vs-trickle crew, DevTest demonstrator, grave-rung severity).
5. **`ordnance-cargo-hold` / `spaceport` collision:** fix or delete the broken/duplicate templates.

---

## Pending (finish after the cap resets ~13:10 UTC)
- **Audit the 7 remaining doors:** `Chassis ▸ Personnel / Vehicle / Hull / Structure / Prebuilt Units`,
  `Command ▸ Command`, `Civic ▸ Development`. Chassis first (frames = the human-vs-Zerg crux).
- **Run the adversarial VERIFY pass** on the flagged items (confirm each "free"/"inert" claim against
  source before the fix — a "free" claim must survive reading the Mass formula, as the ground-Attack one did).
- Fold both into this ledger.

*Raw per-door findings (all 27 doors, full rationales) preserved in the session workflow journal; this doc
is the actionable synthesis.*
