# Missing design 3 — THE EMPLOYMENT MODEL

> **What it is, in one breath:** the game has a colony-morale term for employment — "are your people working?"
> — and it reads a "how many jobs exist here" number that **nothing ever fills.** The obvious fix (give
> buildings a jobs number) *backfires*: the game measures employment as `jobs ÷ workforce`, and the workforce
> is half the population — ~4 billion on Earth. A normal building's worth of jobs (a few thousand) reads as
> **near-total unemployment** and tanks the homeworld's morale on turn one. So this isn't a data add — the
> **model** has to be made coherent first. It answers crack **C1** and its denominator trap.
>
> **Honest scoping:** there's no multi-option *door* here — employment is a single measured ratio. The design
> work is deriving *what that ratio should be* so it's meaningful and neutral-safe, and *what shape the Jobs
> dial must take* so a building can fill it without lying.

---

## 1. The input surface (verified in source)

The whole loop already exists and is wired end-to-end — only the producer is missing, and the denominator is
a trap:

| Piece | Evidence |
|-------|----------|
| Jobs summer | `ComponentInstancesDBExtensions.GetTotalJobs` (:19-28) — sums `EmploymentAtbDB.Jobs × HealthPercent` over installed components |
| The ratio + sentinel | `PopulationProcessor.cs:74-96` — `employmentRatio = (jobs>0 && workforce>0) ? jobs/workforce : -1.0`; the **−1 sentinel** = "no job data → neutral" |
| The morale term | `ColonyMoraleDB.cs:132-142` — `MaxEmploymentBonus 15` / `MaxUnemploymentPenalty 25`, contributes 0 only while `ratio < 0` |
| The denominator | `ColonyManpowerDB.cs:25,45` — `Workforce = population × WorkforceFraction (0.5)` → **~4.1 billion on Earth** |
| The producer | **MISSING** — no template declares `EmploymentAtbDB` (grep of GameData is empty) |

**The trap, precisely:** it's a **step, not a ramp.** With 0 jobs the sentinel keeps the term neutral. The
instant *any* building declares `Jobs > 0`, the sentinel vanishes and `jobs / 4.1e9` is read literally — a few
thousand jobs ÷ 4 billion ≈ 0 → read as ~100% unemployment → **−25 morale**, flipping homeworld morale 50 →
~25 on the first tick and reddening `MoraleTests.StartingColony_HasMorale_NeutralOnHomeworld`. A units
mismatch: jobs conceived as *installation slots*, divided by a *billions-scale workforce*.

---

## 2. How the SIBLING terms avoid this (the pattern to copy)

The other colony-morale terms are neutral-by-default *and* scale sanely — worth seeing why, because the fix
mirrors them:
- **Comfort** (`HousingAtbDB`): an **additive, capped** bonus (`min(+20, Σ comfort)`). Adding a comfort
  building can only *help*; absence is 0. No ratio, no cliff.
- **Crowding** (`PopulationSupportAtbDB`): a penalty that only fires when `worstColonyCost > 0` (a
  support-capped hostile world) — conditionally gated, so a healthy homeworld reads 0.
- **Food** (`SustenanceProcessor`): `PerCapitaFoodDemand` defaults 0 → shortage 0 → neutral until calibrated.

**The lesson:** every sane morale term is either *additive-capped* or *conditionally-gated* or
*zero-demand-by-default*. Employment is the odd one out — a raw **ratio** with a **1.0 pivot** and a hard −25
floor. That structure is what makes it dangerous.

---

## 3. The design — two coherent models (developer picks one)

### Model A — Population-scaled Jobs dial (smallest blast; the trace's recommendation)
Keep the ratio, but make `Jobs` a **population-scaled** quantity, not a flat integer. A building's Jobs
formula scales with its `Support Colonists` capacity (the population it supports), so **total jobs across a
colony's buildings ≈ the population they support ≈ the workforce**, giving `ratio ≈ 1.0` (full employment,
+15). Author it as an NCalc `PropertyFormula` exactly like `HousingAtbDB`, on the `infrastructure` template
every colony carries.
- *Intrinsic test:* PASS — a building's job capacity is settable knowing only the building (its size/support),
  like its housing capacity. It's a legitimate **dial**, just expressed as a capacity, not a constant.
- *Why it works:* jobs and workforce are now the **same order of magnitude** (both population-scaled), so the
  ratio is meaningful and the homeworld stays near full employment by default.

### Model B — Re-scope the denominator (deeper, more honest)
Change what "unemployment" *means*: measure jobs against the workforce **segment that must be employed** — the
industrial/service workforce an economy actually needs to staff — not the whole 4.1e9. Then a shortfall reads
as "your industry is under-staffed / your people lack productive work," which is the real Aurora/BP flavour,
and the number is naturally in the millions, not billions.
- *Intrinsic test:* the denominator is **emergent** (a fraction of population), the numerator (jobs) is the
  dial. Same as A, but the *ratio's meaning* is sharper.

**Recommendation:** ship **A** first (one template, one colony, watch the tripwire, then widen — it's the
least-blast form and reuses the housing wiring), and treat **B** as the model to migrate to once the term is
proven alive, because B is the one that makes employment a genuine *economic* pressure rather than a morale
knob.

---

## 4. The coherence catch — jobs and crew draw the SAME workforce

A subtlety the design must not ignore: **`ManpowerTools` already draws crew for ship/component builds from the
same `Workforce` pool** (design B's denominator). So the workforce has *two* demands on it:
- **Jobs** — the labor installations *employ* (this design's numerator).
- **Crew** — the labor a *build* consumes (`ManpowerTools.ResolveBuild`, the existing gate).

If both are modelled as draws on one workforce, they must not double-count. The coherent framing:
**workforce is the labor SUPPLY; jobs are the standing DEMAND (are people employed?); crew is a transient
DEMAND (can I staff this build?).** Employment morale reads `jobs / workforce`; the manpower gate reads
`(committed crew) vs (workforce − employed)`. A full design should unify them as *one labor ledger* — total
demand vs supply — so the same pool isn't measured two incompatible ways. **This is the connection to flag:**
the employment fix and the existing crew gate share state, and the denominator ruling (Model A vs B) decides
whether they stay separate or merge.

---

## 5. Cradle to grave

**people** (grown at a colony) → become **workforce** (pop × 0.5, emergent) → **employed** by installations
that declare job capacity (the dial, authored at population scale) → the **decision** (build enough
workplaces to keep your people employed, or eat the unemployment morale hit — and balance that against the
crew those same people owe your shipyards) → an installation **destroyed** removes its jobs → employment falls
→ morale drops: the loss rung, shared with the whole colony-infrastructure stack. Every rung exists; only the
"employed by an installation" middle is missing, and this design supplies it *at the right scale*.

---

## 6. Gauge & blast radius

- **Gauge:** the existing `MoraleTests.StartingColony_HasMorale_NeutralOnHomeworld` is the tripwire (reds if
  jobs are seeded too low). Add a sensor: `GetTotalJobs() / Workforce ≈ 1.0` on a healthy colony (full
  employment, +15, not a penalty). And a `BaseModIntegrityTests`-style check that any starting colony with a
  job-bearing building keeps morale ≥ neutral.
- **Blast radius:** high if done naively (the whole morale → migration → legitimacy → rebellion chain, and the
  `ComputeCurrentMorale` mirror that `LegitimacyProcessor` reads). Contained by Model A's approach: add the
  scaled Jobs dial to **one** template + **one** colony, confirm the tripwire stays green, then widen. Settle
  the **denominator ruling first** (§3) — until then, do not declare `EmploymentAtbDB` on any template.

---

*Design 3 of 5. Next: heat & signature — what makes a unit loud.*
