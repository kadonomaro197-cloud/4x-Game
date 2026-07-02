# Governance & Delegation — the Agency Valve (design)

**What it does, in one line:** lets the player **choose their own altitude** — fly every fleet and run every colony by hand if they love it, or seat an officer in each post and play empire-CEO who only sets policy — so the game is never "a job" you're forced to micro, and never a spreadsheet you can't get your hands into. **Delegation is the default; intervention is opt-in.**

**Why it matters (the developer's north star for this layer):** *"Governors should control/maintain worlds so 'too much agency' doesn't make the game feel like a job — like combat auto-resolve."* Most 4X games make micro **mandatory**: you personally click every colony's build queue and every fleet's move order, and the game collapses under its own busywork by the midgame. The fix is to make **every pillar delegate-able to a capable officer**, exactly as space combat is already auto-resolvable. This is the system half of the *Every-Layer-a-Complete-Game* principle locked in `docs/GOVERNMENT-AND-POLITICS-DESIGN.md` — each pillar deep enough to MAIN, and hand-off-able enough to IGNORE.

> **Navy analogy (the whole design in one picture).** A CO doesn't stand every watch. He sets the standing orders and the night orders, then his department heads and watch-standers run their spaces to those orders — and he intervenes only when something's off or when he chooses to. **Span of control** limits how much one command structure can hold; a good officer runs his space well, a green one needs watching. This game is that, at empire scale: you write the standing orders (a delegate's *stance*), seat qualified officers in the posts your **command structure** (the chain of command) can support, and drop down to take the conn yourself wherever you want to.

---

## Survey — what already EXISTS (this is mostly CONNECT, not build)

The Prime-Directive pass found the delegation skeleton already in the code, half-built and currently used for one narrow case (research labs). The design below **generalizes what's there** rather than inventing a parallel system (`CONVENTIONS.md` §6).

| Piece | File | What it is today | What it becomes |
|---|---|---|---|
| **Span-of-control / chain-of-command** | `People/AdminSpaceAtb.cs` + `AdminSpaceDB.cs` + `AdminSpaceProcessor.cs` | A **component** (`AdminSpaceAtb`) that provides **command "seats"** at an `AdminLevel` (`Ship · TaskUnit · TaskGroup · TaskForce · Fleet · Colony · Planet · SOI · System · Sector · Empire`). `AdminSpaceProcessor.CalcEntityAdminSpace` sums installed seats into `AdminSpaceDB.CommanderSeats`; each seat (`AdminSpaceAbilityState`) can hold a `CommanderID`. | The **command-capacity limiter**: how many posts you can delegate, and at what level, is set by the command infrastructure you've built (a flag bridge, an HQ, a sector capital). Seating an officer in a seat = delegating that scope. |
| **The generic delegate (an officer in a post)** | `People/AdministratorDB.cs` | An administrator assigned to a job: `AdministratorId` (the person), `LocationId` (the post), `FundingLevel` (0–5, scales output AND cost), `BonusCategories` (category→% competence bonus), `CostPerDay`. **Research-flavored, but structurally generic.** | The **universal DELEGATE record** — the same shape works for a governor, an admiral, a minister. The funding dial + competence bonuses + per-day cost are already the right knobs. |
| **The people** | `People/CommanderDB.cs` + `CommanderFactory.cs` | Officers with `Rank`, `Type` (`CommanderTypes`), `Experience`, `AssignedTo`. **No skill-bonus fields yet** (the known gap). | The delegates themselves — drawn from the M3 people pool, talent-gated. Needs skill fields (mirror `Scientist`/`BonusesDB`). |
| **Competence/bonuses** | `People/BonusesDB.cs` | Generic `Bonus` list (name/value/type/category) — scientists already use it to multiply research. | The mechanism for "a good officer outperforms a green one" across every pillar. |
| **Officer supply** | `People/NavalAcademyProcessor.cs` | Academies graduate officers on a schedule (`ClassSize`/`ClassLength`). | The cradle: where delegates come from. Ground/ministerial academies follow the same pattern. |

**The one load-bearing finding:** the seat/AdminLevel system and `AdministratorDB` already encode *span of control* and *an officer in a post with a competence dial*. We are **finishing and generalizing** that, not starting over.

---

## The unifying model — one DELEGATE shape for every pillar

A **delegate** is an officer (M3 person) **seated** in a **post**, given a **stance** (standing orders), who **auto-runs that scope at a competence cost**. Every pillar uses the identical shape — only the post and the stance differ:

| Pillar | Post (the delegate) | The stance the player sets (standing orders) | Auto-resolve already exists? |
|---|---|---|---|
| **Combat** | Admiral / fleet commander | doctrine (Front-line/Flank/Artillery), EMCON, ROE | ✅ auto-resolve + doctrine built |
| **A colony/world** | **Governor** | build priorities, tax, what to stockpile, growth-vs-military | ⚠️ economy built; the governor auto-runner is the new piece |
| **Research** | Science lead | category funding (the `AdministratorDB` funding dial — already there) | ✅ `AdministratorDB` + `ResearchProcessor` |
| **Internal politics** | **Interior Minister** | favour stability / military / low taxes / balance the blocs | (designed — `GOVERNMENT-AND-POLITICS-DESIGN.md`) |
| **External politics** | **Foreign Minister** | seek allies / isolationist / expand by tribute / keep the peace | (designed — `DIPLOMACY-DESIGN.md`) |
| **Espionage** | **Spymaster** | counter-intel focus / steal tech / sow unrest | (designed — diplomacy frontier) |
| **A whole region** | **Sector Governor** | the `AdminLevel.Sector` seat — delegates a *cluster* of systems under one officer | the top of the span-of-control tree |

**The contract every delegate honors (so it's one system, not seven):**
1. **A stance** = a small set of standing orders the player picks (presets, like the government dials — no fiddly sliders).
2. **Competence** = the officer's skill bonuses decide how *well* the auto path runs. A master governor keeps morale up and the queue full; a poor one lets the colony drift. Same officer-quality lever as a master admiral winning a battle.
3. **A funding/attention level** (the `AdministratorDB.FundingLevel` 0–5 dial) = how much money/priority the post gets.
4. **The player can always drop in** and take the conn for one decision, one cycle, or for good — without un-seating the delegate. Taking manual control of one colony's build queue doesn't fire the governor; it just means you're standing that watch this turn.

---

## Span of control — why you can't just "set and forget" everything

This is the limiter that makes delegation a **decision** instead of a free "delegate the entire empire on turn 1" button — and it falls straight out of the existing `AdminSpace` seat system:

- **Delegation needs command infrastructure.** A seat at a given `AdminLevel` is provided by a **component you build** (`AdminSpaceAtb` — a flag bridge on a command ship, a planetary HQ, a sector capital). No seat → no delegate for that scope. So *the ability to delegate is itself a cradle-to-grave thing you research, build, and can lose.*
- **Good officers are scarce** (M3 talent pool). You can't seat a master in every post; you triage your best people to the posts that matter most — exactly like choosing which front to reinforce.
- **A delegate costs money/attention** (`FundingLevel` × `CostPerDay`). Running a deep delegated bureaucracy has an overhead.
- **Higher seats nest lower ones.** A Sector Governor (`AdminLevel.Sector`) holds several System/Colony posts under him — delegating the cluster in one move, at the cost of finer control and a cut of competence (orders pass through another layer).

So the anti-"feels like a job" valve cuts **both ways**: micro isn't mandatory (delegate it), but total hands-off isn't free either (you must build the command structure, spend the officers, and pay the overhead). That tension is the gameplay.

---

## The agency spectrum — the same game at three altitudes

The point is that **one player can sit anywhere on this line, and move along it freely**:

- **Hands-on commander** — flies individual fleets, sets each colony's build queue, handles every treaty. Delegates nothing. The classic Aurora micromanager. Fully supported.
- **Theater commander** — seats governors and a science lead so the economy runs itself, but personally commands the war and the diplomacy that matters this decade. The expected default.
- **Empire CEO** — seats a delegate in every post, sets six stances, and plays the game as *policy + exception handling*: the ministers run the empire and surface only the decisions that need the boss. The "I want to think big-picture, not click 200 colonies" player.

No mode switch, no difficulty setting — it's the **same systems**, and the player chooses how much to hold personally by how many seats they fill. Dropping in to take one decision never costs you the delegate; you're just standing that watch.

---

## Cradle to grave (delegation)

> a **command component** (`AdminSpaceAtb`: flag bridge / HQ / sector capital) is **researched → built → installed**, opening a **seat** at some `AdminLevel` → an **officer** (academy-trained, M3 talent) is **seated** in it as a delegate → the player sets the post's **stance + funding** → the delegate **auto-runs that scope at a competence cost** (good officer = good outcomes) → the player **drops in** wherever they choose → the officer gains **experience** (gets better) or is **lost** — killed (the command ship destroyed), defected (turned by a rival spymaster — the diplomacy grave rung), or the **HQ destroyed** removes the seat and the delegation collapses (a decapitation strike on your command structure is a real attack vector).

Every rung is a real player touch-point, and the **grave rung is sharp**: blow up the enemy's flag bridge / sector capital and their delegated command of that scope *falls apart* — they revert to hands-on or go rudderless. That makes command infrastructure a target, and makes "who's holding this together" a question with teeth.

---

## Connections (Prime Directive)

- **People / Commanders** (built, with the no-bonus-fields gap) — delegates ARE commanders; needs the skill-bonus fields the `Scientist`/`BonusesDB` path already proves out. The academy supplies them.
- **Command components** (`AdminSpaceAtb`/`AdminSpaceDB`, built) — the seat/span-of-control substrate; the one real limiter. Generalize from research to all posts.
- **Colonies / economy** (built) — the Governor auto-runner reads the same build/tax/stockpile levers the player uses (no special AI path — it issues the same `IndustryOrder2`/tax orders).
- **Combat / fleets** (built) — the Admiral is the already-existing doctrine/auto-resolve delegate; this layer just names it as one post among many.
- **Internal & external politics** (designed) — the Interior and Foreign Ministers and the Spymaster are delegates of this exact shape; their stances are written in those docs. **This layer is the chassis those ministers bolt onto.**
- **Research** (built) — `AdministratorDB` + funding dial is the template the whole model generalizes from.
- **The autonomous-loop trap** — like the politics engines, a delegate that acts on a schedule needs faction/colony processors to actually fire; the **GlobalManager-not-iterated keystone** (`#34`) is the shared prerequisite for empire-level delegates (ministers). Colony/fleet delegates ride processors that already fire.

---

## Locked vs. open

**Proposed for lock (developer to confirm):**
- **Delegation is the DEFAULT, intervention is OPT-IN** — the anti-"feels like a job" rule. The player chooses their altitude by how many seats they fill; dropping in for one decision never un-seats the delegate.
- **One DELEGATE shape for every pillar** — generalize `AdministratorDB` (officer + post + stance + funding + competence) into the universal record; do NOT build a parallel system per pillar (`CONVENTIONS.md` §6).
- **Span of control is the limiter** — delegation requires a built command component (`AdminSpaceAtb` seat), scarce good officers, and per-post overhead. Total hands-off is earned, not free.
- **Competence matters** — officer skill bonuses decide how well the auto path runs (mirror the `Scientist` bonus mechanism); finish the `CommanderDB` skill-field gap.
- **The command structure is a target** — destroying an HQ/flag bridge collapses the delegation it held (the grave rung).

**Open (decide when we build):**
- The exact **stance presets** per pillar (how many, what they're called) — and whether stances re-skin by government type like the politics layer does.
- How **competence** maps to outcomes per pillar (a curve, or notches) — calibration/feel.
- Whether a **Sector Governor** (multi-system delegate) ships in v1 or waits — it's the top of the tree and the most complex.
- How a delegate **surfaces exceptions** to the player ("Governor of X needs a decision") without becoming notification spam — the UI half (Visibility Gate).
- Officer **experience growth** while seated (today experience is stored but unused — wire it so delegates improve with tenure).

**Build order (after the keystone, alongside the ministers):**
generalize `AdministratorDB` → the universal delegate record → finish `CommanderDB` skill fields (competence) → the **Governor** auto-runner (the highest-value, most-asked: worlds that maintain themselves) → wire the politics ministers onto this chassis → span-of-control UI (seat/un-seat, set stance, the "drop in" path) → Sector Governor (later) → experience growth.
