# 04 — THE ACQUISITION MAP (how you actually GET every resource)

> **What this is, in plain English.** The resource ledger (`03-RESOURCE-LEDGER.md`) said *what* every
> component is made of. This file says *how you get it* — the whole path from a rock floating in space to a
> refined material sitting in your stockpile ready to spend. It answers the practical questions: where is each
> mineral, how do you find it, how do you dig it, what makes it run out, how do you turn it into a material, and
> — if you can't mine it yourself — can you trade for it, take it, or scavenge it? Every mechanic below is
> grounded in the actual engine code (file:line), including the honest parts: several acquisition routes are
> built-but-gated-off or simply not built, and this file says so plainly.
>
> Sources: the abundance tables in `minerals.json`; a 5-agent source-verified map of the acquisition mechanics
> (survey · mine · deplete · refine · trade/capture/salvage). Companion to `03-RESOURCE-LEDGER.md`.

---

## PART 1 — THE GEOGRAPHY: where each mineral actually is

Deposits are seeded when a system is generated (abundance × body mass × a random roll), so *where* a mineral is
follows its abundance profile. This is the strategic map — it decides where you have to go.

| Where | Minerals (richest there) | What it forces |
|---|---|---|
| **Terrestrial worlds** (home + colonies) | `iron` 0.8 · `silicon` 0.9 · `regolith` 0.9 · `aluminium` 0.7 (bulk); `graphite` · `titanium` · `chromium` · `fissionables` · `rare-earths` · **`tungsten` 0.00001** (all trace) | You can bootstrap an empire on one terrestrial world — but the *trace* strategics are thin, and **tungsten is ultra-rare everywhere**, so it's the standing bottleneck |
| **Asteroid belts** | `nickel` 0.4 · `copper` 0.001 | Copper (every circuit) and nickel (superalloys) push you into **asteroid mining** — a station or automine on a rock |
| **Outer system** (gas + ice giants) | `hydrocarbons` 0.5–0.6 · `lithium` 0.001 (ice giants) | All fuels and batteries come from the **outer system** — an empire that only holds the inner worlds is chemically and electrically limited |
| **Comets & ice moons** | `water` 0.8 | Water (life support + space-crete) is a **comet/ice run** if your worlds are dry |

**The scarcity shape, in one line:** iron/aluminium/silicon/regolith are free at home; **copper and nickel
force the asteroids; fuels and lithium force the outer system; tungsten forces you to go prospecting.** That is
the pressure that makes "where do I expand" a real decision — exactly what the resources design doc wants each
mineral to do.

> **Landing note:** a mined mineral drops into the mining host's own cargo hold, sorted by cargo type —
> **metals → `general-storage`, hydrocarbons → `fuel-storage`.** So you need the right *kind* of hold on site,
> or the ore is silently skipped (see the gaps in Part 5).

---

## PART 2 — THE ONE WORKING CHAIN: survey → mine → deplete → refine → build

This is the acquisition path that actually functions end-to-end today. Five steps.

### Step 1 · SURVEY — find out what's there (knowledge, not permission)
- The body is **visible from the start**, but **how much ore it holds is hidden** (a fog-of-war "masked" value)
  until you survey it. You can see the planet; you can't see the assay.
- A **ship carrying a Geological Surveyor** (`GeoSurveyAtb`) is ordered to a body; it accumulates survey points
  per day until the body's requirement (default 1000) is met. Completing the survey **reveals the deposit's
  location and type** — but the exact tonnage stays partly masked until a **ground scout walks the hex**.
- **New systems** need a separate step: a **gravitational survey** (`GravSurveyAtb`) finds a **jump point**;
  crossing it adds the destination system to your known map. That's the prerequisite to reach deposits outside
  your home system.
- 🔑 **The load-bearing truth: survey does NOT gate mining.** The mining code reads the *true* deposit amount
  directly, so a mine physically works a deposit whether or not you surveyed it — **survey is about KNOWING what
  you're getting, not about being allowed to get it.** Settling a colony reveals the full tonnage outright; your
  home worlds start pre-surveyed.

### Step 2 · MINE — pull it out of the ground
- Two installations do it: the **Mine** (a big fixed building, on a colony or a station) and the **RoboMiner**
  (`automine` — a transportable unmanned collector). Install one and the host becomes a mining host.
- **Rate** = (the mine's base rate × its condition) × colony mining bonus × **deposit accessibility** ×
  **infrastructure efficiency**, capped by what's actually left in the ground. A default Mine pulls **~10
  units/mineral/day** (its rate = area × 0.00001); a RoboMiner scales with its size. Runs once a day.
- **Infrastructure efficiency** is the shared throttle: if a colony's buildings out-demand its infrastructure
  grid, mining (and refining, and construction) all scale down together.
- Mining is **automatic and faction-blind** — any colony or station with a mine installed mines every day, no
  order needed, for player and AI alike.

### Step 3 · DEPLETE — why it runs out
- Every day, what you mine is subtracted from the deposit. As a deposit is drawn down, its **accessibility
  decays** — and the decay is **cubic**: above the halfway mark it barely moves, but once a deposit drops below
  half, accessibility collapses and keeps compounding, so the last third comes out painfully slowly.
- It's a **one-way ratchet** (accessibility never recovers) with a floor of 10% (a nearly-dead deposit still
  trickles). A deposit is truly finished only when it hits zero.
- 🔴 **Deposits never refill.** Nothing regenerates them. Once a body is mined out, **the only way to get more of
  that mineral is to mine a *different* body** (every body has its own deposits) **or haul it in.** This is what
  turns "where's my next tungsten" into a permanent expansion driver.

### Step 4 · REFINE — turn minerals into materials
- A **Refinery** installation adds a "refining" job line to a colony. The **same daily processor** runs all
  three industry stages — refining, component-building, ship-assembly — on the same colony, under the same
  infrastructure throttle.
- A material's recipe is fixed data (e.g. `stainless-steel` = 88 iron + 11 chromium + 1 hydrocarbon → 100
  units). The refinery **pulls the input minerals out of the colony's hold, spends work-points, and drops the
  refined material back into the hold.**
- If the input minerals aren't in cargo, the job **just waits** (it doesn't crash) — so refining is naturally
  gated by your mineral supply, not only by tech.

### Step 5 · BUILD — spend materials on a component
- A Factory or Shipyard build consumes **minerals *and* refined materials through the identical path** — the
  component's own recipe (e.g. a shipyard costs iron + aluminium + copper + plastic + stainless-steel +
  electronics). This is the point where the whole chain meets the twelve doors: **the component you designed in
  a door is built here, out of the materials you refined, out of the minerals you mined.**
- **Research is the unlock gate:** a material or component is buildable only once it's on the faction's
  buildable list (unlocked by tech). In the base mod most core materials start unlocked, so the tech gate is
  *lightly used* today — the real constraint is usually mineral supply, not research.

**That's cradle-to-grave for acquisition:** survey the body → mine the mineral → (it depletes) → refine it into a
material → build the component → (it's designed in a door, assembled into an entity, and eventually lost).

---

## PART 3 — THE OTHER ROUTES: settle, trade, capture, salvage (and why only one works)

If you can't mine a resource at home, there are four ways to get it. **Only the first works fully today.**

| Route | What it does | State |
|---|---|---|
| **Settle / expand** | Put a colony or mining station on a body that has the deposit; a colony grants full access to the tonnage immediately. This is the real answer to scarcity. | ✅ **WORKS** — the main acquisition strategy |
| **Trade / buy** | Freight ships haul cargo between bases on a profit-ranked market. | ⚠️ **HALF-BUILT** — cross-faction hauling needs a `LogisticsAccess` treaty (off by default), and **even then no money changes hands** — you can move goods with an ally but **you cannot *buy* a resource** from anyone (the trade-income payout is gated off; a "buy/gift/supply" catalog is designed but has no executor) |
| **Capture** | Take an enemy colony by ground invasion. | ⚠️ **BARE FLIP** — capture is a v1 ownership change and nothing else; the code itself says *"deeper transfer later."* Stockpiles and buildings come along only because they sit on the same colony that changed hands, and **mineral deposits live on the planet body, not the colony — so you get them only by owning the colony that works them.** *What capture actually transfers is an OPEN developer ruling* (the same one flagged in the Assembler audit) |
| **Salvage** | Recover materials from destroyed ships / battlefield wreckage. | 🔴 **DOES NOT EXIST** — the "spawn a wreck" step is a stub that just deletes the ship; the wreck/salvage events are dead placeholders. No battlefield scavenging yields any resource anywhere |

**The one-line consequence:** today, **you acquire resources by going and mining them.** Expansion is the whole
game of supply. Trade can shuttle goods between your own colonies (and an ally's, with a treaty) but can't buy
scarcity away; conquest gives you the enemy's *colony* (and thus their mines) but isn't modeled as seizing a
stockpile; and you can't scavenge a battlefield at all. That makes **the geography in Part 1 the binding
constraint** — which is good design pressure, but it also means the "buy it / take it / scavenge it" pressure
valves are still on the workbench.

---

## PART 4 — ACQUIRING THE NEW RESOURCES (from `03-RESOURCE-LEDGER.md`)

Each new material from the ledger, placed on the acquisition map — where its inputs come from, and how you'd get
it:

| New resource | Mine its inputs from… | Then |
|---|---|---|
| **explosive-compound** (NEW-1) | `hydrocarbons` (outer system) + `fissionables` (terrestrial trace) + `copper` (asteroids) | refine at a colony refinery |
| **ammunition** (NEW-1) | `tungsten` (prospect for it — ultra-rare) + explosive-compound + `stainless-steel` | build at a factory; **fixes the broken `gallicite` reference** |
| **biomass** (NEW-2) | **NOT mined — GROWN.** `water` + `hydrocarbons` + light, at a Hydroponics/Farm building | the one resource with a non-mining acquisition path — a farm, not a mine; refine into `food` |
| **reactive-plating** (NEW-3) | `stainless-steel` + explosive-compound | build at a factory |
| **null-ward-plating** (NEW-3) | `ree-magnetics` (rare-earths, terrestrial trace) + `tungsten-plating` | build; also fixes an unconsumed material |
| **myomer** (NEW-4) | `plastic` (hydrocarbons) + `electronics` (copper/silicon) + `aluminium` | build; all inputs already mineable |
| **nanite-stock** (NEW-4) | `ree-magnetics` + `electronics` + `rare-earths` | ENGINE-PENDING (waits on the self-repair model) |

**The key acquisition insight for the new resources:** every one except **biomass** rides the *existing* mining
chain — its inputs are among the 15 minerals, so no new *acquisition* mechanic is needed, only the refine recipe
from `03`. **Biomass is the exception:** it is **grown, not mined**, which means it needs the "grown" industry
type (a farm that consumes water + carbon + light) — a genuinely new acquisition path, and the reason it waits on
that industry type being built.

---

## PART 5 — THE ACQUISITION GAPS (honest ledger, each with the file it's in)

What's missing, stubbed, or gated-off in the acquisition chain — so nobody plans on a route that isn't wired:

| Gap | Where | Effect |
|---|---|---|
| **Survey doesn't gate mining** | `MineResourcesProcessor` reads the true amount | Survey is fog-of-war only; you can mine what you never scanned. "Prospect before you can extract" is not enforced |
| **Geo-survey ignores ship position** | `GeoSurveyProcessor` TODO | A survey order accumulates points without checking the fleet is actually at the body (the jump-survey does check distance) |
| **Mining loss is invisible** | `MineResourcesProcessor` (events commented out) | If the hold is full or lacks the cargo type, ore is silently skipped — no log, no gauge. You can't see you're under-mining |
| **Deposits never refill** | no refill code in Industry/Galaxy | Minerals are strictly finite; long games exhaust every deposit with no in-model recovery. Expansion is the only answer |
| **Per-hex located mining not wired** | `HexMinerals` is a view only | "Build the mine *on* the deposit and that hex depletes" is designed but v1 mines the whole body's pool regardless of where the mine sits |
| **You can't BUY a resource** | `TradeIncomeProcessor.EnablePayout=false`; `ExchangeCatalog` has no executor | Cross-faction trade moves goods (with a treaty) but no money; the buy/gift/supply catalog is inert data |
| **Capture is a bare ownership flip** | `GroundForcesProcessor` ("v1: ownership flip; deeper transfer later") | Taking a colony doesn't explicitly seize stockpiles/deposits; deposits live on the body. **What capture transfers is an OPEN developer ruling** — the same one the Assembler audit flagged as the MVP finish line |
| **Salvage doesn't exist** | `DamageProcessor.SpawnWreck` is a stub | Destroyed ships create no wreck; no battlefield scavenging yields any resource |
| **`gallicite` faults the build** | `ordnance.json:311` → `IndustryTools` | A build of that ordnance design would fault ("Cant build from non ICargoable Items") because gallicite is defined nowhere — the ledger's NEW-1 fix retires this |

---

## The one-screen summary
- **Where:** iron/aluminium/silicon/regolith at home; **copper + nickel in the asteroids; fuels + lithium in the
  outer system; tungsten by prospecting.** Geography is the binding constraint.
- **How (the working chain):** survey a body (to *know* the tonnage) → mine it (automatic, throttled by
  infrastructure + accessibility) → it depletes cubically and never refills → refine minerals into materials →
  build the component. All five steps work end-to-end.
- **The other routes are the workbench:** settle = the real strategy (works); **trade can't buy** (no money
  moves); **capture is a bare flip** (open ruling on what it seizes); **salvage doesn't exist.**
- **The new resources** all ride the existing mining chain except **biomass**, which is grown, not mined.
- **Biggest acquisition-side open ruling:** what a captured colony transfers — deposits sit on the body, not the
  colony, so today you inherit them only by holding the colony. That's the MVP finish-line decision.

*Acquisition map complete. Companion to `03-RESOURCE-LEDGER.md`: the ledger says what a component is made of;
this says how you get it, cradle (a rock in space) to grave (the material spent on the build).*
