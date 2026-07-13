# Governance & Delegation — the Agency Valve (design)

> **Consolidated 2026-07-13 from:** `GOVERNANCE-AND-DELEGATION-DESIGN.md` (the delegation mechanism + span of control) and `AI-SELF-PLAY-DESIGN.md` (the 21-role leadership roster, the two-axis seat grid, the parity/automation audit, and the six-rung leader cradle-to-grave pipeline). This is the single home for **how agency is delegated, who the leaders are, and how one delegation layer doubles as the NPC AI.** The dated conversational framing and the AI performance-cost walkthrough from the self-play doc were compressed to their durable decisions; nothing load-bearing was dropped.

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

# The Leadership Layer — the roster, the pipeline, and AI self-play

> *(Folded 2026-07-13 from `AI-SELF-PLAY-DESIGN.md`, a 2026-07-06/10 design conversation. Everything below sits ON TOP of the delegation mechanism above: it names every leader seat, proves no seat is dead weight, audits whether the seats can do EVERYTHING a player can, and lays out the one people-pipeline that produces and loses leaders.)*

## THE KEY INSIGHT — delegation and NPC AI are the same system

This is the load-bearing idea that makes a living galaxy of AI empires affordable, and it's what ties this whole layer to the delegation mechanism above.

**A leader-seat is simultaneously the player's hand-off valve and the NPC's brain.** One system, two jobs:

- For the **player**, a leader is the *off-switch for micro*: seat an officer, set a stance, stop clicking.
- For the **NPC**, that *same* leader-seat **is the AI** — it issues the identical orders a player would.

The delegation design already locks the rule that makes this true: a delegate *"issues the same `IndustryOrder2`/tax orders"* the player uses — **there is no special AI code path.** So a Governor running a colony to a stance for a lazy *player* is the exact same machinery as "the AI" running an *NPC's* colony.

**The payoff:** we don't build a player game *plus* an AI. We build **one** delegation layer and point it at both. An NPC empire is just an empire where *every* seat is filled by a delegate; the player is a human sitting in as many or as few of those seats as they like. This is how *Distant Worlds* actually works, and it's why the scope is reachable. It kills both classic 4X failures at once: the **"it feels like a job"** failure (forced to hand-fly everything) and the **"inert AI"** failure (NPCs sit there because nobody wrote them a brain — where we are *today*).

## AI performance — thinking is nearly free; *doing* is the whole cost

The performance question that started the self-play conversation, kept here because the answer is a set of durable design decisions. **The AI *thinking* is nearly free; the AI *doing* is the whole cost.** Two different bills, must not be conflated.

- **Bill #1 — Deliberation (making decisions). Negligible.** NPC brains run in `NPCDecisionProcessor`, which fires **once a game-month** per NPC faction (`RunFrequency = 30 days`) and already staggers factions. The clock ticks in 1-hour steps (`MasterTimePulse.Ticklength = 3600 s`); a month is ~720 ticks. Even a thorough 20 ms turn across 15 empires is ~300 ms spent *once a month* — under half a millisecond per tick amortized. A rounding error next to the hourly physics. **Cadence is the friend, and it's already built** — the AI is the department head who plans at the Monday meeting, not a watchstander taking readings every hour. **Never move NPC thinking to an hourly loop.**
- **Bill #2 — Consequences (what the AI builds and animates). The real cost.** Every ship, colony, fleet, and sensor the AI creates is simulated by the same per-tick processors forever. And there's a multiplier: **today the galaxy is cheap because most of it is asleep** (`MasterTimePulse` filters `ActivityState != Stasis`; empty systems sleep; the benchmark runs 2 systems). A galaxy with 15 AI empires is a galaxy where **most systems are awake because someone lives there** — so full AI *removes the sleeping-galaxy discount*. Per-tick cost is roughly **linear** in active systems + entities (combat is proven O(ships) via weapon-class bucketing — 200 ships in <4 s, 1000 gnats in ~9 ms). The one **landmine is sensors/detection**, which can go **O(emitters × receivers)** once many factions sense at once — the "degenerate detection-quality" fix flagged elsewhere matters here.

**Constraints that bound it:** the sim is serial by default (`EnableMultiThreading = false`); the `GlobalManager` where faction brains live runs serially *after* the systems; a per-system parallel mode exists but is unproven and off by default; **there is no scaling gauge yet** (benchmark is 2 systems, 1 faction).

**The levers (path to "many AI empires, still fast"):**
- **Build the gauge FIRST** (Visibility Gate) — a scaling benchmark dialing N factions × M systems × K ships, reading the existing `PerformanceStopwatch`, plotting the curve. Turns "how much?" into data and exposes the sensor-quadratic if it's real. *You can't steer a cost you can't see.*
- **Keep deliberation slow + staggered** (already the design).
- **Level-of-Detail for distant empires — the headline lever.** An NPC the player has never met and can't see doesn't need entity-by-entity simulation. Run it in "cold layup": its economy is a handful of numbers advanced cheaply, instantiating real ships/colonies only when it interacts with the player or enters an observed system. Philosophically identical to the stasis/sleep the engine already has.
- **The survey fog is a *free* brake** — an NPC literally cannot expand into space it hasn't scouted, so a correct AI only wakes systems it has surveyed.
- **Make AI footprint a difficulty/performance dial** — consequence cost = entity count, so "how aggressively the AI expands and how many ships it fields" is simultaneously a difficulty knob and a performance knob.

## The leadership model — the shape every leader shares

Every leader is the same shape as the delegate above: **an officer seated in a post, given a stance (standing orders), who auto-runs that scope at a competence cost** — and whom the player can drop in on at any time without un-seating them.

### The three layers (the developer's own framing)

Every pillar is three layers deep:

1. **God** (player / for an NPC, the top-level AI) — sets policy, can drop into any chair.
2. **The Leader** (governor / commander / minister) — holds one scope, runs it to a stance, at a competence cost.
3. **The autonomous layer below** — executes on its own. In combat this already exists: sub-fleet components (Front Line / Flank / Rear Guard / Artillery) each carry their **own** doctrine, and there is *"no per-ship control, ever"* — the individual ship picks targets by the weapon-triangle rule with nobody micromanaging it.

### Two chains that cross (not one)

How real militaries are actually organized, and it fell out of the design naturally:

- **Administrative / territorial chain — "who you belong to."** Head of State → System Governor → Planetary Governor, with the Interior Minister and Planetary General under their Governor, and the System General under the System Governor. This chain owns **intent**: what a world protects, where a system's economic effort flows, local defense posture.
- **Operational military chain — "who gives you combat orders."** Grand Admiral → System Admiral → Fleet Commander (space, *mobile*); Grand Admiral → System General → Planetary General → Battalion Commander (ground operations).

The **ground leaders are where the two chains cross** — a Planetary General answers to the **Governor** for *what to defend* and to the **System General** for *where to move*. Distinct decisions, so it's a clean matrix, not a conflict. **Fleets stay purely on the operational chain** because they're mobile — a fleet doesn't belong to a system the way ground forces belong to a world.

## The rule — no leaders for leaders' sake

A leader who is just "+2 to a modifier" is the "pretty" disease from `docs/REALISM-VS-GAMEPLAY-AUDIT.md`: fidelity nobody acts on. Every seat must earn its place. The test:

> **A distinct leader seat is justified only if it owns a decision the player would otherwise make by hand, and that neither the leader above nor below it already owns.**

This is *why* the naval ladder got collapsed — Task Force / Task Group / Task Unit were cut because they didn't own a decision the System Admiral or Fleet Commander didn't already own. Rungs for rungs' sake. And **competence (the "+2") is demoted to its proper job**: not the reason a seat exists — it's the dial on how *well* the seat's decision gets executed. The decision is the substance; the modifier is only the texture on the outcome.

## The full roster — 21 leader role-types

Grouped by the administrative spine, with the operational military line crossing in. Each row names **the decision it owns** and its **build status** (EXISTS = real data home + wiring · STUB = hook exists, unfed · WIRE = the underlying mechanic is built, only the officer-in-the-seat is missing · NEW = net-new).

### The tree

```
HEAD OF STATE   (the empire's DESTINATION/objective + regime + budget + HR/character-assignment)
│
│   ── THE TWO-AXIS RULE (2026-07-10) ──────────────────────────────────────────────
│   Every seat has exactly TWO lines:
│     • an ADMIN line — VERTICAL, to your Governor/HoS ("who you belong to / where you live")
│     • a DOMAIN-DIRECTION line — ⟵ HORIZONTAL, from your empire domain-head ("what your specialty does")
│   The civil/economic chain (Chancellor→Governor→Governor) IS the admin spine, so its seats' two
│   lines converge; military & science seats live on the spine but take direction from their minister.
│   ─────────────────────────────────────────────────────────────────────────────────
│
├─ Grand Admiral ....... SPACE-military effort: which systems get the war   ─direction→ System Admiral
├─ Field Marshal ....... GROUND-military effort: which worlds get the push  ─direction→ System / Planetary General
├─ Chief Scientist ..... research direction + design goals ("reach for the Defiant") ─direction→ System / Planetary Scientist
├─ Foreign Minister .... overall external posture
│     └─ Per-faction Foreign Minister → Ambassador · Envoy · Agent  (⟵ Spymaster for tradecraft)
├─ Spymaster ........... espionage doctrine + counter-intel (home of Agents)
│
└─ CHANCELLOR .......... the empire's CIVIL/ECONOMIC head — which systems get the development effort
      │                  (the "empire Governor": top of the admin spine; civil twin of Grand Admiral/Field Marshal)
      ├─ Trade Minister .... commerce specialist: routes / tariffs / import-export
      └─ System Governor ... the SYSTEM'S head — ALL system-level leaders report here
            ├─ System Admiral ...... in-system fleets + composition            ⟵ direction: Grand Admiral
            │     └─ Fleet Commander ... one fleet's doctrine  →  [autonomous ships]
            ├─ System Scientist .... system science loop (discover→build→staff→upgrade)  ⟵ direction: Chief Scientist
            │     └─ Lab Scientist ..... does the research at a station
            ├─ System General ...... which worlds to reinforce / hold / invade  ⟵ direction: Field Marshal
            └─ Planetary Governor .. one world's development (build / tax / stockpile)
                  ├─ Interior Minister .... that world's politics / stability / blocs
                  ├─ Planetary Scientist .. the world's science loop            ⟵ direction: Chief Scientist
                  └─ Planetary General .... the surface campaign                ⟵ direction: System General ↑ Field Marshal
                        └─ Battalion Commander ... one battalion  →  [autonomous units]

── THE SYMMETRY: three empire EFFORT-ALLOCATORS, each with a system EXECUTOR ──
   Grand Admiral (space war)  → System Admiral   ·   Field Marshal (ground war) → System General
   Chancellor  (economy/dev)  → System Governor
   (plus Chief Scientist→Scientists · Foreign Minister/Spymaster = external · HoS = destination+regime+budget+HR)
   Roster is now 21 (added Field Marshal #20 + Chancellor #21).
```

### The roster table

| # | Role | Scope | The decision it owns | Status |
|---|------|-------|----------------------|--------|
| 1 | Grand Admiral | Empire (space) | Which systems get the war effort | STUB (commander entity exists, unwired; v1 flat modifier) |
| 2 | Foreign Minister | Empire + per-faction | Overall external posture; policy toward each rival | NEW |
| 3 | Interior Minister | Planet (under Governor) | How to answer a world's bloc demands — stability vs tax vs military | NEW (`GovernmentDB` substrate partial) |
| 4 | Spymaster | Empire | Which covert ops, against which rivals; counter-intel | NEW |
| 5 | Chief Scientist | Empire | Research direction — which categories get effort **+ the empire's design-goal targets** (the research-targeting that reaches for a known-better authored design) | EXISTS (research funding-dial delegate is the one built example) |
| 6 | Trade Minister | Empire | Routes, tariffs, import/export | NEW (+ the trade-money wire must come first — see commerce note) |
| 7 | System Governor | System | Where the system's economic effort flows across its worlds | NEW (seat scope `AdminLevel.System` exists) |
| 8 | Planetary Governor | Planet | Build priorities, tax, stockpile, growth-vs-military for one world | STUB (`LegitimacyDB.GovernorCompetence` hook exists, never fed) |
| 9 | System Admiral | System (space) | Where fleets move & what they engage in-system | NEW |
| 10 | Fleet Commander | Fleet | One fleet's combat doctrine/posture | STUB (`FleetDB.FlagShipID` is a ship, no commander link) |
| 11 | System General | System (ground) | Which worlds to reinforce / hold / invade | NEW |
| 12 | Planetary General | Planet (ground) | The surface campaign — where battalions push or hold | NEW (over the built formation layer) |
| 13 | Battalion Commander | Formation | One battalion's doctrine/posture | WIRE (`GroundFormation` + `GroundFormationDoctrine` built; person-commander net-new) |
| 14 | Ambassador | Per-faction (field) | *Where to post a scarce specialist* (allocation) | NEW |
| 15 | Envoy | Per-faction (field) | Terms of one negotiation | NEW |
| 16 | Agent / Operative | Per-faction (field) | Which op, which target (allocation) | NEW |
| 17 | Lab Scientist | Institution (field) | (runs research to the Chief Scientist's priorities) | EXISTS (fully wired) |
| 18 | System Scientist | System (field) | The system's whole science loop: **discover → build a research station → staff it → upgrade it** (not just "survey first") | WIRE (survey + station build/deploy + scientist-assign + upgrade orders all built; the run-the-loop delegate is new) |
| 19 | Planetary Scientist | Planet (field) | The world's science loop: survey deposits/anomalies → build + staff + upgrade local research infrastructure | WIRE (as above) |
| 20 | **Field Marshal** | Empire (ground) | **Which worlds/theatres get the empire's GROUND effort** (split out from the Grand Admiral 2026-07-10; the empire-ground head) | NEW |
| 21 | **Chancellor** | Empire (civil/economic) | **Which systems get the empire's ECONOMIC/development effort** — the "empire Governor", top of the admin spine, civil twin of Grand Admiral/Field Marshal | NEW |

### Symmetry check — now clean at EVERY scope (2026-07-10)

| Scope | Space military | Ground military | Civil / economic governance |
|-------|----------------|-----------------|---------------------|
| **Empire** | Grand Admiral | **Field Marshal** | **Chancellor** |
| **System** | System Admiral | System General | System Governor |
| **Planet** | — (ships don't hold worlds) | Planetary General | Planetary Governor |

*(The Head of State sits ABOVE all three empire heads — it owns the destination, the regime, the budget, and HR, not a single domain.)* The old empire-scope asymmetries are **resolved**: the ground ceiling is now the **Field Marshal** (split from the Grand Admiral), and the civil ceiling is the **Chancellor** (the empire-level System-Governor-equivalent). Each empire effort-allocator has a matching system executor.

### The SEAT GRID — every seat owns a unique (scope × domain) cell (the no-dead-weight proof + checklist)

The two-axis rule makes distinctness *structural*: every seat sits at a unique **(scope × domain)** intersection, so no two own the same decision. Lay all 21 + the HoS on the grid — nothing shares a cell, and the empty cells are intentional:

| Scope | Space-mil | Ground-mil | Civil / econ | Science | Diplomacy | Espionage | Commerce | Politics |
|---|---|---|---|---|---|---|---|---|
| **Empire** | Grand Admiral | Field Marshal | Chancellor | Chief Scientist | Foreign Minister | Spymaster | Trade Minister | *(HoS — regime)* |
| **System** | System Admiral | System General | System Governor | System Scientist | — | — | — | — |
| **Planet** | — | Planetary General | Planetary Governor | Planetary Scientist | — | — | — | Interior Minister |
| **Field / one-of** | Fleet Cmdr | Battalion Cmdr | — | Lab Scientist | Ambassador · Envoy | Agent | — | — |

**Intentional blanks:** Planet×Space-mil (ships don't hold worlds); Empire×Politics is the **HoS** directly (holds the regime, no minister); the external domains (Diplomacy/Espionage/Commerce) don't nest into the territorial spine below empire because they're empire-outward.

**The four TIGHT-but-distinct pairs** (each still owns a decision the other doesn't, but at *small* scale one officer holds both — you seat the split only when the empire is big enough to need it; the three-mode dial keeps the unsplit one on auto):
- **Fleet Commander vs. System Admiral** — how one fleet *fights* vs. where the system's fleets *go*.
- **Ambassador vs. Envoy** — a standing court presence vs. a one-off negotiation.
- **Interior Minister vs. Planetary Governor** — a world's *politics* vs. its *economy*.
- **Trade Minister vs. Chancellor** — commerce *flow* vs. production *allocation*.

**Coverage vs. built — the honest line.** Every *aspect of the game* maps to a cell/owner (space & ground war, economy, commerce, research, ship design, science/exploration, diplomacy, espionage, internal politics, colonization, tax, budget, retrofit, mining, terraforming, migration, rebellion, character-HR; reactive events ride the spine; the crisis is deliberately ownerless = emergent). **So the leadership covers everything.** BUT *owned ≠ built* — many cells own a mechanic still on the punch-list (espionage engine, terraforming, migration, rebellion-response levers, retrofit, bloc-demands, trade-money). **The roster is complete; the engine behind it is the backlog.**

## Parity audit + the automation model (the Distant Worlds test, 2026-07-10)

The acceptance question for the whole roster: **can 21 seated leaders, each with a functioning AI, do EVERYTHING a player can?** — the Distant Worlds promise (any function delegable, the AI plays the same game, take any piece back anytime). A four-agent audit (a code-side player-action sweep + a DW1/DW2 deep-dive) answered it.

### Verdict: NOT YET — five holes, ranked by how badly each breaks "watch the AI play the whole game"

The movement / combat / research / industry / survey / diplomacy / ground **spine is genuinely delegatable** — every one of those actions has an order or direct-call API that maps to a seat, so a brain could play them. But five gaps remain, three with **no owning seat** and one with **no order at all**:

| # | Gap | Why it breaks parity | The fix (owner + what to build) |
|---|-----|----------------------|----------------------------------|
| 1 | **DESIGN — ship / component / ordnance / ground-unit** | *In an Aurora-lineage 4X, design IS the game.* No seat owns "compose a hull from unlocked parts," AND the mechanic is a **client-only UI mutation that never goes through `Game.OrderHandler`** (`ShipDesignWindow.cs:279`, `ComponentDesignDisplay.cs:162`, `OrdnanceDesignWindow.cs:293`) — so there's no order for a brain to issue even if you wrote one. An AI that can't design can only ever build the templates the mod shipped. | See the RESOLVED reframe below — the AI never solves generative design; it research-targets an authored ladder. |
| 2 | **Fleet formation & composition** | Nobody owns "take these built hulls and make a task force of this mix." Ships would pool at a colony, unorganized. | Order EXISTS (`FleetOrder.Create/AssignShip/SetFlagShip`) — just assign the decision to the **System Admiral #9** (or Grand Admiral #1 for force structure). Cheap, no new mechanic. |
| 3 | **Colonization TARGET selection** | System/Planetary Governors run *existing* worlds; nobody converts "expand" into "found a colony at *that* body." The eXpand pillar has no decision-owner. | Order EXISTS (`CreateColonyOrder`) — give the **System Governor #7** "found at body X in my system," Head-of-State Expansion dial as modulator; "which system next" to Head of State or an eXpand seat. |
| 4 | **Empire BUDGET allocation** | `AI-COMMAND…DESIGN §1` puts "the budget split across departments" at Tier 1, but **there is no budget system** — `FundingLevel` is a per-post 0–5 dial, no empire pool. The Head of State owns a decision it cannot express. | Build a `BudgetDB` empire pool; per-post `FundingLevel` draws from it; the split becomes a real issuable allocation. New substrate. |
| 5 | **Tax / stockpile** | The Planetary Governor's headline levers aren't wired — `ColonyEconomyDB.TaxRate` is **read-only in the client, no tax order** (`EntityDisplay.cs:230` displays it, nothing sets it); no stockpile-target order. | Add a `SetColonyPolicyOrder` (tax / stockpile reserve / growth-vs-military) through `OrderHandler`, owned by Planetary Governor #8. Small — the data field already exists. |

Plus the already-flagged **dark pillars** (owner in the roster, engine system unbuilt): Spymaster/Agent (no espionage engine), Interior Minister (no bloc-demand order), Trade Minister (trade earns no money). These are known-blocked, ranked lower only because the design already names them.

**So:** seating all 21 today produces an empire that can fight, research, survey, and expand its *existing templates* — but **cannot design new ships, organize hulls into fleets, choose where to plant colonies, allocate a budget, or set taxes.** Design (#1) is the category-defining hole: it needs both a new order-type and a new seat, and until it's closed the "no special AI code path" promise is violated for the single most important 4X activity.

### RESOLVED — the DESIGN hole: authored faction ladders + AI research-targeting (developer's call, 2026-07-10)

The developer's reframe dissolves Gap #1 so the AI **never solves generative ship design at all:**

- **Factions are authored top-to-bottom.** A faction ships as a fully dialed-in artifact — e.g. the post-Dominion-War Federation with its complete design ladder (Miranda → … → Defiant) and full tech tree. This is *content the developer/modder wants to make*, not an AI job.
- **The AI starts with what it ALREADY has, not the whole tree.** A scenario Federation might know only the Miranda; it does NOT get the Defiant or transwarp on day one. It builds and fights with its current, already-designed ships.
- **The AI's "design" decision becomes RESEARCH-TARGETING, driven by observed threat.** The loop: spy Faction A's frigate (or lose a fight to it) → "my Miranda can't match that, but I *know of* the Defiant" → set research objectives to unlock the Defiant's prerequisites, across whatever pillars they span (weapons + power + hull). *"I need a Defiant; I only know how to build a Miranda; let me research everything in the Defiant's direction so I can keep up."*

**Why it's the right solution:**
- **Sidesteps the single hardest AI problem** (inventing a good hull from parts) — the good ships already exist as authored designs; the AI only has to *reach* for one.
- **The AI issues only orders that already exist** (research queue + build) — no "special AI path," no generative-design AI, no new design *order* needed for the AI (it unlocks a pre-authored design via research; it never creates geometry). The UI-only design path stays for the *player's* generative freedom.
- **Fuses the pillars into ONE stacking loop** (the north-star made concrete): espionage → threat assessment → design goal → multi-pillar research → industry → combat → now-I-match-them. A faction visibly *reaches* for the Defiant — its tech priorities swing when it sees a threat.

**Owner: the science chain — NOT a Chief Engineer.** Because "design" is now a research-direction decision, it folds into science leadership. **This RETIRES the earlier proposed seat "Chief Engineer / Naval Architect"** — no generative-designer seat is needed. The science chain, clarified (developer's call, 2026-07-10):
- **Chief Scientist #5 = empire-wide DIRECTION** — the tech priorities *and* the design-goal targeting ("we're reaching for the Defiant"). Empire-level "what are we researching toward." (The design-targeting from Gap #1 lives HERE, not in a per-system rung.)
- **System / Planetary Scientist #18/#19 = the DOMAIN SCIENCE LOOP, cradle to grave** — not "which body to survey," but *run the whole local R&D pipeline*: **discover → build → staff → upgrade.** Find a black hole in the system → issue the build order for a **research station** on it → get a **great scientist stationed** there → **keep upgrading** the station to push its science output higher. It turns map discoveries into standing research infrastructure with no player input. This is the delegate that *runs* the exploration-content field-site loop (`EXPLORATION-CONTENT-DESIGN.md`: survey → assign-scientist → yield → deplete/persist).
  - **Why it's mostly wire-existing-levers:** survey (built), research-station build (`IndustryOrder2` + `DeployStationOrder`, built), station science (`ResearchPointsAtbDB.bonusCategory`, exists), scientist staffing (`AssignScientistOrder`, built), upgrade (component-install orders, built). **FEASIBILITY TO VERIFY:** a research *station* is a buildable that boosts science when a scientist is stationed + upgraded.
  - **It demonstrates the mandate/report protocol on a real example:** the System Scientist reports UP "I need a top scientist for this new station"; the Head-of-State HR numbers-game (character-assignment) fulfills DOWN by matching the best available scientist to that post. Two-channel communication, working end-to-end.
  - **It also gives a natural owner to two DW-audit orphans:** exploration-*dispatch* (who sends survey ships + prioritizes) and science-infrastructure *retrofit/upgrade* both live here.

**What it needs (build/verify):**
1. **Authored design ladders** — pre-author a faction's full design tree, each ship tech/component-gated so researching the prereqs unlocks *buildability* of an already-designed ship. **FEASIBILITY TO VERIFY in code:** how the engine represents a design the faction "knows of" but can't yet build (designs live in `ShipDesigns`; templates gate on `StartingItems` + tech — confirm a full *locked* ladder can be authored and progressively unlocked).
2. **Threat→design-goal map** — rank the faction's own authored designs by capability; see an enemy's capability; pick the target design that closes the gap.
3. **Research back-solve** — given a target design, set research objectives for its missing prerequisites (the design→component→tech chain already exists).

**Dependency + v1 fallback:** the *proactive* spy-driven version leans on espionage (unbuilt, a dark pillar). **The v1 fallback needs no espionage: the AI reacts to combat OUTCOMES** — gets mauled → researches up to its next authored design. Full spy-driven targeting lands when espionage does.

### Other resolutions (developer's calls, 2026-07-10)
- **Gap #3 Colonization target → System Governor #7.** Locked (Head-of-State Expansion dial as modulator).
- **Character-assignment ("HR") → a Head-of-State numbers game.** No dedicated seat: the HoS AI runs a best-fit match (each officer's competence + traits scored against each open seat → assign the best pairing). Locked.
- **Gap #2 Fleet composition → System Admiral #9.** Locked. (The mechanic is already built — `FleetOrder.Create/AssignShip/SetFlagShip` — so a player forms fleets today; this just assigns the *AI decision-owner* for when it's delegated.)
- **Gap #5 Tax / stockpile → Planetary Governor #8, via a new `SetColonyPolicyOrder`** (tax rate + stockpile reserve + growth-vs-military). Locked — small build; the `ColonyEconomyDB.TaxRate` field already exists, it just has no order/lever yet.
- **Gap #4 Empire budget → BUILD IT, but only when it earns its weight.** Deferred to when the economy is deep enough that funding is genuinely *scarce* and ministries *compete* for it (that's when the split is a real stacking decision). Until then per-post `FundingLevel` dials stand alone; when built, they draw from the pool. "Build it when it bites," not v1.
- **Civilian economy → GRABBABLE-WITH-A-POLICY-ONLY-FALLBACK** (developer's call, 2026-07-10). Two layers: (1) a **policy-only fallback** — the economy can run itself 100% on policy alone (tax/subsidy/priority/port-placement), so the player is *never forced* to touch a freighter (DW's scalability trick, made a *guarantee* that full delegation is always complete); (2) **grabbable** — unlike DW's hard wall, the player *can* take direct control of a specific hauler/convoy/route and hand-fly it while the rest stays on auto (the three-mode dial applied to the economy; Delegate is a *complete* mode). **Cheap for us, expensive for DW:** Pulsar already models the economy as real ships on real logistics routes (`SetLogisticsOrder` + the built automated freight market), so individual haulers are already concrete controllable entities — we get DW's scalability *and* the extra freedom without DW's flow-abstraction cost. The only real work is guaranteeing the policy-only layer is complete enough to never *require* a grab. **CORRECTION (completeness sweep, 2026-07-10): the policy-only layer is NOT yet achievable with what's built** — the central stockpile-reserve knob (`LogiBaseDB.DesiredLevels`, the "keep N of mineral X, auto-import the shortfall" lever that drives the entire freight-advertise loop) **has no setter order**; `SetLogisticsOrder` only sets what a base *exports*, never what it *wants imported*. So a brain can hand-fly freighters but can't steer the auto-economy by policy alone. **Fix: the `SetColonyPolicyOrder` (below) must carry stockpile-reserve (`DesiredLevels`)** or the "never forced to touch a freighter" guarantee doesn't hold.

**→ ROSTER LOCKED (2026-07-10):** all parity gaps now have an owner or a deliberate deferral. Design→science-chain research-targeting; fleet-composition→System Admiral; colonization→System Governor; tax/stockpile→Planetary Governor (`SetColonyPolicyOrder`); character-assignment→HoS numbers game; budget→build-when-it-bites; civilian economy→grabbable+policy-only-fallback; dark pillars (espionage/trade-money/bloc-demands) wire with their systems.

### Completeness sweep — EVERY facet, not just the order catalog (four-agent audit, 2026-07-10)

The parity audit above swept player *actions* (orders). This sweep walked every *subsystem + reactive loop* to answer the developer's harder question: **can every facet be run by AI leaders enacting HoS direction?** Verdict: **more areas need shoring up — but they cluster into four piles, and the highest-impact one (reactivity) is a WIRING job on already-built substrate, not a rebuild.** The recurring pattern is *"the vocabulary was authored, the mechanic never built"* — e.g. ~250 `EventType` values and dozens of lifecycle enums (`ShipRefit*`, `ShipScrapping*`, `WreckSalvaged`) that exist with **zero producers/consumers**.

**PILE 1 — STRUCTURAL PREREQUISITES (build FIRST; each unblocks a whole category):**
1. **The reactive spine** — *the single highest-leverage fix; unblocks every reactive facet at once.* Today the only brain is `NPCDecisionProcessor`, a **monthly** poll with an empty `// TODO` body — so a leader-AI *plans* but cannot *react* (a battle is over in seconds). **The good news: the reactive substrate is already built and proven** — `EventManager`/`EventType` is a working pub/sub bus, every faction already subscribes to the full firehose (`FactionEventLog`), and `ResearchProcessor` *already reacts to events* (rebuilds on `TechDiscovered` rather than polling). So the fix is WIRING, not building: (a) add the **goal slot** `StrategicObjectiveDB` on `FactionInfoDB` + the per-seat mandate/report pair (pure data — currently 100% net-new but tiny); (b) subscribe the leader brain to interrupt events (`ShipDestroyed`, `NewHostileContact`, `PopulationBombarded`, `UnrestIncreased`, `TreatyAgreed`, hazard) exactly as `ResearchProcessor` does, so it reacts *and* keeps the monthly plan (the cadence pyramid = "monthly plan + event react"); (c) **publish a `CombatStarted` event** from `CombatEngagement.EnsureInCombat` (today combat is a 5 s poll nobody subscribes to — the most important interrupt isn't even on the bus).
2. **Seat durability + a `LeaderLost` handler** — `AdminSpaceProcessor` rebuilds the seat list from scratch each pass (code says *"need to check if we want that"*) and `DestroyCommander` fires `CrewLosses` that **nothing consumes**. So seating isn't durable and a killed officer leaks refs. This sits UNDER the whole delegation layer — if a seat can't hold its officer through a combat tick, no mandate-flow survives. Prerequisite, not feature (already build-order step 2).
3. **Wrap the key ACTS as `EntityCommand`s** — diplomacy (`DeclareWar`/`MakePeace`/`Propose`) and the reactive/lifecycle acts are static utility methods that **bypass `OrderHandler`** (same shape as the ship-design gap). No order record = nothing for Advise-mode to propose/notify/replay. Wrapping them is the prerequisite for the three-mode dial on those domains.
4. **A `ThreatCondition : ICondition`** — the standing-order machinery (`FleetDB.StandingOrders` + `FleetOrderProcessor`) is **built and actually executed on idle fleets**, but the only conditions are `Comparison` and `Fuel`. There's no "hostile detected / under attack / took damage," so you can't even author "if an enemy enters my system, fall back." One new condition type lights up reactive standing orders.

**PILE 2 — NEW ORDERS NEEDED (mechanism/field exists, no order to drive it):**
- **`SetColonyPolicyOrder` → Planetary Governor #8** — bundle **tax + stockpile-reserve (`DesiredLevels`) + specialization stance** in one order. Closes three gaps at once (tax has a field/no order; the freight policy-only fallback is unreachable without the reserve setter; colony specialization has no substrate). *The cheapest high-value build in the economy domain.*
- **`RefitOrder` → Grand Admiral #1 (ships) / Scientist #18-19 (infrastructure)** — diff a hull's installed components vs a target `ShipDesign`, enqueue install/uninstall + cost, fire the already-defined `ShipRefit*` events. **This reconnects the "reach for the Defiant" loop** (you can research + *build new* better ships today, but can't *modernize existing* hulls). Component-swap primitives already exist.
- **Implement the empty `RefuelAction`/`ResupplyAction.Execute()` bodies** — the orders exist and silently no-op. Two method bodies.
- **Scrap/mothball order → Planetary Governor** (reclaim materials / cut upkeep / lay up in reserve). Orphan enums only today.
- **`SetGovernmentOrder`** (regime change — dials exist, no change-order) → Head of State.
- **Troop load→move→land invasion handoff** (no "load troops"/"land troops" action) → Grand Admiral × System General × Planetary General.

**PILE 3 — UNBUILT MECHANICS (whole systems missing; owner assigned, wire with the system):**
- **Known dark pillars:** espionage engine (zero code — Spymaster/Agent), bloc/demand engine (zero code — Interior Minister), trade-money (Trade Minister).
- **Newly surfaced:** terraforming (processor stub), migration/evacuation control (auto-only), mining priority (mines all deposits flat), rebellion *response* levers (state exists, no act), salvage/wreck (`SpawnWreck` empty stub), repair/damage-control (grep "repair"→nothing; *mitigated* while the auto-resolver is whole-ship), piracy (doesn't exist), the **HR/competence pipeline** (recruit/train/promote + the competence generator — and this is the marquee report-up→fulfill-down demo, unbuilt end-to-end), empire budget pool (deferred — build-when-it-bites), empire-scale EMCON/sensor-picket doctrine, colony specialization substrate.

**PILE 4 — BUILT + HEADLESS, just ASSIGN THE OWNER (no code, roster bookkeeping):** installation siting (`PlaceInstallationIn{Region,Hex}Order`)→Planetary Governor; station siting (`DeployStationOrder`)→System Governor/Scientist; colony build queue (`IndustryOrder2`)→Planetary Governor; per-fleet EMCON (`FleetEmcon.SetPosture`)→System Admiral; freight haul-side (`SetLogisticsOrder`)→Trade Minister.

**Design-completeness (the reframe holds):** the "known vs buildable" locked design split is REAL (`FactionInfoDB.ComponentDesigns` = all incl. unresearched; `IndustryDesigns` = buildable-now; gated by `FactionDataStore.Unlock()`), so authored tech-gated ladders are feasible — the developer's research-targeting resolution is structurally supported. Ship + component + **ground-unit** (which already rides the industry rails end-to-end) all extend cleanly; **ordnance and station-class just need their authored ladders written** (no new mechanism). The only residual: a player-side headless `*DesignOrder` is still unbuilt, but the AI never needs it (it unlocks pre-authored designs via research).

**BOTTOM LINE:** the net-new *engine* work concentrates in a short list — the mandate/report slots + event subscription (Pile 1.1), seat-durability (1.2), the `SetColonyPolicyOrder` + `RefitOrder` (Pile 2), and the HR/competence pipeline (Pile 3) — **with seat-durability the load-bearing prerequisite under all of it.** Everything else is either "assign an owner" (Pile 4, free) or a known dark pillar that waits for its system. Reactivity — the scariest-sounding gap — is the cheapest, because the event bus is already built and one processor already reacts through it.

### The automation model — three modes, not two (the DW lesson to lock)

Distant Worlds' identity is "play as much or as little as you want," and it's delivered by **three control modes per seat, not two** — this is the piece to adopt wholesale, and it's already latent in our autonomy dial ("execute freely / hold-and-report / ask-first"):

- **Delegate** — the officer acts silently (DW "Automated"). = "execute freely."
- **Advise** — the officer computes the action it *would* take and posts it as a **one-click proposal** (Accept / Dismiss); the player does zero analysis but keeps veto. (DW "Suggest.") = "ask first." **This is the bridge between "watch it play" and "hand-fly one thing," and the mode is only as good as its notification plumbing — the proposal must reach the player as a one-click decision *at the moment it matters* (Visibility-Gate applied to delegation), not buried in a log.**
- **Hand-fly** — the player does it (DW "Manual"). = sit in the seat.

Plus DW's two granularity levers, both worth stealing: an **empire-wide default per function** + a **per-object override** (automate every fleet, then grab *one* and hand-fly it), with **instant, reversible take-over** at any time. Mechanically this is a **third stance value on the delegate record** (Delegate/Advise/Hand-fly) — our officer+post+stance chassis already almost supports it. DW also confirms our core bet: **a skilled character improves the AUTO-run function** (a good admiral makes an auto-fought fleet win) — competence is the quality dial on delegated work, exactly officer+post+competence.

**The three-mode dial storage (the code detail):** the seat record (`AdminSpaceAbilityState`) holds only who-sits-where — **no mode field, no stance, no funding.** The mode is a new field on the (finally-instantiated) delegate record; "Advise" additionally needs a **`ProposedOrder` staging state** (an `EntityCommand` computed but not yet handed to `OrderHandler`, surfaced as one-click Accept/Dismiss). Correctly a later player-side slice (scoped out of the NPC v1), but 100% net-new.

### One structural decision DW raises (RESOLVED — see civilian-economy resolution above)

DW runs its **entire civilian economy as an autonomous "private sector" the player can NEVER directly control at any automation level** — you set policy (taxes, subsidies, port placement), private companies decide what to haul. It's a big reason DW scales to hundreds of systems without micro drowning. Our fork was: **is our economy "delegable-but-grabbable" or DW-style "structurally policy-only"?** **Resolved (2026-07-10): grabbable, with a guaranteed policy-only fallback** — keep override, but guarantee the economy never *requires* hands (see the civilian-economy resolution in "Other resolutions" above). That's what keeps a big galaxy playable *and* preserves our extra freedom over DW.

---

## The leader cradle-to-grave pipeline (born → grave)

> This is the **people** half of cradle-to-grave — the delegation section above covered the *command component* (research → build → install a flag bridge → the seat opens → the delegation collapses when the HQ is destroyed). This section covers the *officer* who fills that seat, born to grave.

A leader isn't mined, so it doesn't ride the mineral→component chain — it rides the **people chain**, and it's the **same six rungs for all 21 roles.** The whole point: don't build 21 leaders — build **one pipeline**, prove it on one role, then every other role is configuration (a `BonusCategory` + a `Refresh` method), not a rebuild.

Navy picture: how you get an officer from nothing to running a department — recruited → rated at a school → assigned to a billet → stands the watch (his skill runs the plant) → advances with quals → transferred/lost, billet gapped.

| Rung | What it means | State in the code today (file:line) |
|------|---------------|-------------------------------------|
| **1. Born** | a leader is generated *with a competence value* | ⚠️ **Weakest.** `NavalAcademyProcessor` graduates Navy officers with a bell-curve `Experience` int (`NavalAcademyProcessor.cs:31-46`) but **no competence bonuses** — the graduate's `BonusesDB` is left **empty** (`CommanderFactory.cs:24`). The one "skilled" example hardcodes `0.1` in `NewGameMenu.cs:629`. **No competence generator exists.** |
| **2. Skilled** | competence stored in a reusable container | ✅ **Built & reusable.** `BonusesDB.Bonuses` — `Bonus(Value, Type, FilterId)`; `FilterId` routes a bonus to a scope (`BonusesDB.cs:20-47`). |
| **3. Seated** | the leader put in a post with command scope | ✅ **Strongest.** `AdminSpace` seat ladder (11 levels Ship→Empire), `AssignAdministratorOrder` links both ways, `FundingLevel 0–5`. The billet exists only because you **built the command component** that opens it (`AdminSpaceAtb`) — so leaders connect back to the mineral→material→component chain. |
| **4. Acts** | competence multiplies a real game number | 🔸 **Works in exactly one place, as a copyable pattern.** Only research: target holds a `ModifiableValue`, `RefreshPointModifiers` folds the officer's `BonusesDB` in, `GetValue()` reads it (`ResearchProcessor.cs:87,292-333`). Dead for the other 20 roles. |
| **5. Improves** | leader gets better with tenure/success | ❌ **Absent** as an auto-loop — `Experience` is stored and never read into any computation. **Replaced by the retraining loop below** (deliberate re-enrollment, not passive XP). |
| **6. Lost** | killed/captured/turned/died → seat empties, delegation collapses | 🔸 **Mostly absent + a bug.** Only a ship's captain dies today (`ShipFactory.cs:247`); it leaks dangling seat refs, and `AdminSpaceProcessor` resets seats each pass. Fix = one `LeaderLost` handler (the `CrewLosses` event is already published, unconsumed). See Rung 6 in depth — the rung that makes the player *care*. |

**The essence:** rungs 2 and 3 are built and reusable; rung 4 exists once as a pattern to copy; rungs 1, 5, 6 are the real work — and even the proven example lacks a competence generator (1) and growth (5).

### Rung 1 in depth — Leader Academies (the installation)

Leaders are **produced by installations** — schools / colleges / universities — that take a fraction of a colony's citizens and turn them into placeable leaders. This is the bottom rung: *it takes from the colony and gives the player/NPC leaders to use.* It is mostly **connect-and-generalize**, because the pieces are already half-built:

| Design element | Today | Move |
|----------------|-------|------|
| Installation = a designed component | `NavalAcademyAtb : IComponentDesignAttribute` with design-tunable `ClassSize`, `TrainingPeriodInMonths` (`NavalAcademyAtb.cs:9-12`) | **Connect** — generalize off "Navy only" |
| Design knob: quality via training investment | `TrainingPeriodInMonths` already shifts graduate quality via a bell-curve mean (`NavalAcademyProcessor.cs:31`) | **Exists** |
| Design knob: which type/tier it produces | academy hardcodes `CommanderTypes.Navy` | **Small build** — make type/tier a design field |
| "More valuable resources → better output" | component is built from materials; `CostPerDay` pattern on `ResearcherDB` | **Connect** — add a per-cycle material draw |
| Modifier: colony populousness | `ColonyInfoDB.Population.Values.Sum()` | **Exists** — plugs in |
| Modifier: colony development level | ❌ no colony-wide stat (only per-hex `HexTile.InfrastructureLevel:47`) | **Build one accessor** (see below) |
| Modifier: a leader assigned to *teach* | the scientist-seated-at-a-lab pattern (`AssignScientistOrder` + `ResearcherDB.ScientistId`) | **Connect** — copy assignment + the `RefreshModifiers` competence read |
| "Takes a fraction of citizens" | `ColonyManpowerDB.TalentPool` (pop × 0.005) + `AvailableTalent`/`CommitTalent` — **purpose-built for "officers, scientists, governors," zero consumers today** (`ColonyManpowerDB.cs:15,27,61`) | **Connect** — the academy is the first thing to draw talent |
| Output: leaders with *inherent value* (competence) | ❌ graduates get only an `Experience` int; `BonusesDB` is **empty** | **THE core build** — the competence generator |

**Two purpose-built sockets nobody has plugged into:** the **talent pool** (a scarce population slice literally documented for officers/scientists/governors, never drawn) and the **empty `BonusesDB`** on every officer (the competence slot rung 4 already reads). Your academy is the first consumer of both.

**The one core new mechanic — the competence generator:** at graduation, **roll the leader's `BonusesDB` values**, scaled by `(design tier + material investment) × populousness × development level × teacher competence`. That single roll IS "the leaders produced and their inherent value." It emits into the shape (`BonusesDB`) the rest of the pipeline already consumes.

**The roll's math (reuses existing machinery).** Each graduate rolls a competence `score` (0–100) on a bell curve — reusing `NextBellCurve(RNG, floor, ceiling, mean, stddev)`, already used for `ExperienceCap` (`NavalAcademyProcessor.cs:31`). The four inputs **shift the mean** (and tier sets the ceiling); they do **not** flat-add to the result:

```
mean =  BaseMean(designTier)          // school ~30 · college ~50 · university ~70   [DESIGNABLE, dominant]
      + MaterialInvestment(materials) //                                            [DESIGNABLE]
      + Populousness(pop)             // log-scaled, diminishing                     [modifier]
      + Development(colonyDevelopment) // the ColonyDevelopment accessor             [modifier]
      + Teacher(teacherCompetence)     // capped/diminishing (runaway control)       [modifier]
score = NextBellCurve(RNG, floor, ceiling = TierCap(designTier), mean, stddev)
```

- **Mean-shift, not flat-add**, so variance survives: the odd prodigy from a poor school, the odd dud from a university. Loaded dice, not rigged — keeps "good officers are scarce" real.
- `score` drives two outputs: the **`BonusesDB` value** (inherent competence, rung 4 reads it) and the **eligibility ceiling** (which rung they qualify for, hard-capped by tier).
- **Quantity vs quality is a design choice:** `ClassSize` sets *how many*; the mean sets *how good* — a mass academy (big class, low mean) vs an elite one (small class, high mean).
- **Diminishing returns on the three soft modifiers** so none dominates and the teacher loop can't runaway (the contract also locks the teacher in place).
- **v1:** one primary competence bonus (`FilterId` = the pillar); secondary aptitudes / traits parked. The roll is *starting* competence; retraining (rung 5) adds more over a career.

**Tier = ceiling, competence = value.** The installation *tier* (school → college → university) sets the **highest rung** a graduate is eligible for; their rolled competence + record decides how good they are and which seats they qualify for. (The `COLONY-PROGRESSION` tier ladder, when built, is the natural backing for a higher academy tier on a more-developed world.)

**Colony development level — one accessor, swappable backing.** No colony-wide development number exists (only per-hex `InfrastructureLevel`). Build a single `ColonyDevelopment` accessor that **aggregates the hex infra levels now**, and is forward-compatible with the `COLONY-PROGRESSION-DESIGN.md` tier ladder later (the tier becomes a factor inside the same accessor). One figure, never two competing ones (`CONVENTIONS.md` §6). Its grave rung matches the progression doc's open question — a bombarded world both *demotes* and *starves its leader pipeline*.

**The installation's own cradle-to-grave (vertical):** designed → built from minerals/materials → installed → draws talent → produces leaders → **destroyed** (bombardment / captured when the planet falls) → **the leader pipeline goes dark.** That grave rung is strategic — hitting an enemy's universities starves their *future* leadership, wiring this system straight into ground combat and orbital bombardment.

### Rung 3 in depth — "Seated" (command capacity you build up)

Delegation is never free — you **earn** command capacity by building it, which is the span-of-control limiter that stops "delegate the whole empire on turn 1." (This restates the Span-of-control section above from the people side.)

- **Seats come from components built onto installations** (the command components — HQ, flag bridge, sector capital). Generalizes the existing `AdminSpaceAtb` (a component that grants seats at an `AdminLevel`).
- **You start from nothing. Your first HQ grants 1 seat.** Every position must be **filled as you progress** — more command infrastructure → more seats → more of the empire you can delegate. Total hands-off is a thing you *build toward*, not a default.
- **`FundingLevel` (0–5) = a spend dial** — crank it up and the post runs harder at higher daily cost. Same knob for every pillar (labs → points, governors → budget, spymasters → ops, admirals → readiness).
- **Grave-rung tie-in:** because a seat is a built component, destroying it (decapitation, rung 6) collapses the seat and its delegation. Command infrastructure you build is command infrastructure the enemy can blow up.

### Rung 4 in depth — "Acts" (the competence → outcome pattern, generalized)

The scientist→research chain is the one place this works end-to-end; every leader reuses its four parts:

1. **The target is a `ModifiableValue<T>`** — the number the leader moves (research: `ResearcherDB.PointsPerDay`).
2. **A `Refresh…Modifiers()` method** folds modifiers in priority order: `base → funding(×) → local bonus → the seated officer's BonusesDB` (matched by `FilterId`) — `ResearchProcessor.cs:292-333`.
3. **Consume reads `GetValue()`** at use-time (`:87`).
4. **Rebuild is event-driven** (assign / unassign / funding / stance change), **not per tick** — cheap, which matters for the AI-cost bill.

**Two flavors of "Acts" — keep them distinct:**
- **(a) Passive competence bonus** — the officer makes a standing number *better*, continuously (Governor → legitimacy). Pure `ModifiableValue` + `Refresh`. No orders.
- **(b) Active orders** — the officer *issues orders* per their stance (Admiral moves fleets, Spymaster runs ops, Governor queues builds). This is what fills the dead `NPCDecisionProcessor` stub.

Competence modulates *how well* both go; the **stance** decides *what* the active ones do.

**The two-layer modifier model (already half-built):** *local post competence* (the seated officer's `BonusesDB` folded into the target) vs the *empire-wide modulator* applied after `GetValue()` (research already does this: `× GovernmentTools…ResearchMultiplier()`, `:91`). That maps exactly onto our hierarchy — the **leader's competence** is the modifier; the **Head of State's regime** is the modulator on top.

**Per-pillar targets** (name the `ModifiableValue`, copy `RefreshPointModifiers`):

| Leader | Target the competence moves | Passive / Active |
|--------|-----------------------------|------------------|
| Chief Scientist / Lab | research points | passive (built) |
| Governor | colony legitimacy (`GovernorCompetence` slot) + build orders | both |
| Fleet Commander | fleet firepower/toughness/speed (`FleetDoctrineDB`) + move/engage orders | both |
| System Admiral | which fleets move where | active |
| Spymaster | op success / counter-intel + op orders | both |
| Foreign Minister | deal quality / relation drift + treaty proposals | both |
| Trade Minister | trade income & route efficiency + route orders | both |
| Academy teacher | the graduate competence roll | passive (a rung-4 act pointed back at rung 1) |

The same code path runs whether the actor is player, delegate, or NPC — a seated delegate's `Refresh` runs automatically; the player dropping in sets the value directly. One implementation, not two.

### Stances — the decision surface

A **stance is a small, named bundle of standing orders** the player or NPC picks — *presets, not sliders*. The stance **is the decision the leader owns**; competence is only how well it's executed.

**Templates already shipped:**
- **`FleetDoctrineDB`** (built) — families Offensive / Defensive / Utilitarian + ROE (WeaponsFree / Hold / ReturnFire) + a **switch cooldown**.
- **`GroundFormationDoctrine`** (built, **data-driven**) — stances defined in `groundStances.json` as `GroundStanceBlueprint`s. **This moddable-JSON-catalog pattern is the one to reuse for every pillar** — a stance = coefficient tweaks + a behavior rule, defined in data.
- **`DoctrineVector`** on `FactionInfoDB` (four floats: Economic / Military / Tech / Expansion) is the NPC "personality" that **biases which stance an NPC picks**. Filling the `NPCDecisionProcessor` stub = *DoctrineVector biases stance choice → the seated delegate executes it → emits orders through rung 4.*

**Example preset sets (3–4 per pillar, from the design docs):**

| Leader | Stances |
|--------|---------|
| Governor | Growth · Industry · Fortress · Balanced |
| System Governor | Breadbasket · Mining Hub · Fortress |
| Fleet Commander *(built)* | Offensive · Defensive · Utilitarian + ROE |
| Planetary General | Hold Ground · Counter-attack · Fighting Withdrawal |
| Foreign Minister | Seek Allies · Isolationist · Expand by Tribute · Keep the Peace |
| Interior Minister | Favor Stability · Favor Military · Low Taxes · Balance the Blocs |
| Spymaster | Steal Tech · Counter-intel · Destabilize [rival] · Keep Us Informed |
| Trade Minister | Maximize Income · Stockpile Strategic · Free Trade · Autarky |

**Three points:** (1) stance = the stacking *decision*, competence = *texture* — keeps every seat honest against the realism firewall; (2) the existing switch **cooldown composes with contracts** — a leader on contract runs their stance for the term, no per-tick flip-flopping (good for feel and the AI-cost bill); (3) whether stances **re-skin by government type** (a dictatorship's "Favor Stability" reaching for harsher tools) is the one open call — the `GovernmentDB` dial hook to do it already exists.

### Two empire-level modulators — race and government

Beyond the leader's own competence, two empire-wide factors shape the whole pipeline. Both are the **modulator layer** (applied on top, like the regime multiplier on research) — not per-leader stats:

- **Race (species setup)** — sets leader **lifespan** (rung 6 mortality) and biases **doctrine tendencies** (which stances an NPC of that race leans toward, rung 4). Different species live and lead differently. Wires to the species system (`ColonyInfoDB.Population` is already per-species; verify/define the trait hooks — lifespan, doctrine lean — when we build).
- **Government (regime)** — **renames and re-tools stances**: a communist state's stances are named and behave differently from a republic's (rung 4). Uses the `GovernmentDB` dial hook.

This is why stance content and mortality aren't fixed constants — they're modulated by *who you are*.

### The retraining loop (rung 1 + rung 5, merged)

Leaders don't improve on their own (passive `Experience` is dead). Instead, **a leader can be sent back to school to gain more modifiers** — re-enrolling in an academy as a student. This is a deliberate, costly decision: it takes a school slot, costs time, and pulls the leader **out of service** while they train. It reuses the academy mechanic wholesale (the leader is a student again), gives academies permanent relevance beyond first spawn, and makes competence growth something you *invest in and plan around* — never automatic. This is how "Improves" is delivered.

### Contracts — the universal commitment term (and the runaway fix)

The teacher-feedback loop (a great leader teaching → better graduates) would spiral if you could cycle your best officer through the academy every year. The fix generalizes into the connective tissue for rungs 3→6:

**Every assignment is a fixed-term contract.** When a leader is seated — as a teacher, a governor, an admiral — they're committed for a term and **locked in place** for it.

- Caps the teacher loop (a star teacher can't be cycled).
- Makes "which leader, which post" a **durable strategic choice**, not per-turn optimization — parking your one genius admiral at the war college for a decade means he's *not* on the front for a decade. Real opportunity cost.
- **Net-new but small** — today's assignment orders are instant with no duration; add a term-end date + an early-break cost (payout / morale hit / reputation ding). **Default term: 5 years** (calibrate later).
- Preserves the governance rule "dropping in for one decision never un-seats the delegate" (you take the conn without breaking the contract).
- Quietly helps the performance bill — leaders don't get reassigned every tick.

Navy framing: a commission/enlistment term. You don't PCS a department head every week; cutting a tour short is a real event with real cost. "Back to school" is that officer going to the War College mid-career — off the watch bill, back sharper.

### Rung 6 in depth — "Lost" (the rung that makes the player care)

**This is the load-bearing rung for the whole design's *feel*.** A leader you can't lose is just a permanent modifier — and modifiers don't get names, don't get protected, don't get mourned. A leader you *can* lose — killed, captured, turned, retired — is someone the player invests in, escorts, and grieves, and someone whose enemies they hunt. **Loss with real cost is what converts a stat into a character the player cares about.** Everything below serves that.

**The reframe — one event, one handler.** Every way a leader dies is just a *producer* of a single **`LeaderLost` event**; **one handler** consumes it — vacate the seat, drop the competence, collapse the delegation. The event effectively **already exists and is unconsumed**: `DestroyCommander` publishes `EventType.CrewLosses` (`CommanderFactory.cs:115`) and *nothing listens.* So the spine is cheap: add the one subscriber.

**Prerequisite (and it fixes a real bug).** Today `DestroyCommander` clears **none** of the back-references — not `CommanderDB.AssignedTo`, not the seat's `CommanderID`/`Commander`, not `ShipInfoDB.CommanderID` — and `AdminSpaceProcessor.CalcEntityAdminSpace` **rebuilds the seat list from scratch each pass with `CommanderID = -1`** (the code flags it: *"need to check if we want that"*). So seating isn't durable and death leaks dangling references. **Fix first:** make seat occupancy durable (reconcile, don't reset) and route every removal through the one vacate handler. This is a prerequisite for rung 3 being durable and the Governor slice too — it pays for itself as a bug fix before it's a feature.

**Current state of the four causes:**

| Cause | Today |
|-------|-------|
| Killed (ship) | 🔸 Partial — captain killed via `DestroyShip`→`DestroyCommander` (`ShipFactory.cs:247-251`); only the captain; leaves dangling refs |
| Killed (colony bombardment) | ❌ absent for leaders |
| Captured (planet falls) | ❌ absent — capture only flips `FactionOwnerID` (`GroundForcesProcessor.cs:528`, "deeper transfer later"); seated officers orphaned |
| Turned / defected | ❌ no code |
| Died / retired (age) | ❌ commanders are immortal — no aging/lifespan/mortality anywhere |
| Decapitation (seat-host destroyed) | ❌ seat silently stops regenerating |

**The four causes, each a producer of `LeaderLost`:**
- **Killed** — *ship* exists (extend past the captain, route through the handler); *colony bombardment* hooks a casualty roll into `DamageProcessor.OnColonyDamage` (already exists). → **combat + damage.**
- **Captured** — the "deeper transfer later" hook. A captured leader becomes a **prisoner**: intel windfall + bargaining chip (ransom / exchange). v1 = removed + intel chance; full = a diplomacy sub-game. → **ground combat + diplomacy.**
- **Turned / defected** — the espionage grave rung (already named in `ESPIONAGE-AND-INTELLIGENCE-DESIGN.md`). Clean defection (they leave) or a **mole** (stays seated, leaks/sabotages until counter-intel catches them). → **espionage + the Information Ledger.**
- **Died / retired** — give commanders a lifespan and a mortality/retirement processor. Ties to **contracts** (a term can end in retirement) and keeps the **academy permanently relevant** — rungs 1 and 6 are the loop: leaders are born and lost, forever. Without it you accumulate immortal god-officers.

**What "collapse" means (the stakes the handler enforces):** the scope loses the competence bonus (rung-4 `Refresh` re-runs, the `ModifiableValue` drops); a *delegate* seat's delegation **collapses** — reverts to hands-on for the player, or **rudderless for an NPC** (stops executing its stance, loses coordination); the seat empties → back to rung 1 to appoint/train a replacement. A master admiral killed = years to replace. It is *felt*.

**Decapitation — the vertical grave rung.** Seats come from a **built command component** (`AdminSpaceAtb` — flag bridge / HQ / sector capital). Destroying it should publish `LeaderLost` for every commander under it → same handler → the delegation it held falls apart. Kill the enemy flagship or bomb their sector capital and their fleets/colonies go rudderless until re-seated. Makes command infrastructure a **target**, gives "who's holding this together" teeth, and closes the `AdminSpaceAtb` component's own cradle-to-grave.

**The gameplay it unlocks (earns-weight check):** *protect yours* (don't post your best governor on a frontier world; escort the flagship; garrison the capital), *hunt theirs* (assassination, decapitation strikes, turn their best officer into your mole), *the churn* (death/retirement keeps every good officer genuinely scarce and the academy always in play).

**Resolved (2026-07-06):**
- **Turned leader → both outcomes, decided by `RNG × counter-intel × leader stats`.** A defection attempt can land the leader as a **mole** (stays seated, feeds the enemy), a **clean defection** (they leave), or a caught/failed attempt — the roll and your counter-intel decide which. Not a fixed v1 pick; it's a probabilistic outcome.
- **Captured leader → both, same model.** Killed / prisoner (intel + ransom) / escape is decided the same way (`RNG × counter-intel × leader stats`).
- **Ground-formation leader loss → leave the no-penalty reassign, but notify the player** when it happens (visibility, no mechanical change).
- **Mortality → race-dependent lifespan** (see the race modulator above); no single global number.

### First vertical slice — the Governor (proves the whole pipeline)

Cheapest end-to-end proof, because the grave-end target already exists and is already dead-wired for it: `LegitimacyDB.GovernorCompetence` is a `0..1` slot feeding a `×15` legitimacy bonus that **nothing has ever written to** (`LegitimacyDB.cs:88-92,128,138`), and `AdministratorDB` is already a near-identical copy of the working `ResearcherDB`. The slice: **generate an officer with rolled competence (build rung 1) → seat via the existing `AdminSpace` order (rung 3, done) → copy `RefreshPointModifiers` to write competence into `GovernorCompetence` (rung 4) → watch legitimacy move.** It forces the reusable pieces into existence; after that, the System Admiral, Spymaster, and Trade Minister are the same wiring pointed at a different `ModifiableValue`.

---

## Connections (Prime Directive)

- **People / Commanders** (built, with the no-bonus-fields gap) — delegates ARE commanders; needs the skill-bonus fields the `Scientist`/`BonusesDB` path already proves out. The academy supplies them.
- **Command components** (`AdminSpaceAtb`/`AdminSpaceDB`, built) — the seat/span-of-control substrate; the one real limiter. Generalize from research to all posts.
- **Colonies / economy** (built) — the Governor auto-runner reads the same build/tax/stockpile levers the player uses (no special AI path — it issues the same `IndustryOrder2`/tax orders).
- **Combat / fleets** (built) — the Admiral is the already-existing doctrine/auto-resolve delegate; this layer just names it as one post among many.
- **Internal & external politics** (designed) — the Interior and Foreign Ministers and the Spymaster are delegates of this exact shape; their stances are written in those docs. **This layer is the chassis those ministers bolt onto.**
- **Research** (built) — `AdministratorDB` + funding dial is the template the whole model generalizes from.
- **The autonomous-loop trap** — like the politics engines, a delegate that acts on a schedule needs faction/colony processors to actually fire; the **GlobalManager-not-iterated keystone** (`#34`) is the shared prerequisite for empire-level delegates (ministers). Colony/fleet delegates ride processors that already fire.

### Additional connections from the leadership/self-play layer

- **`NPCDecisionProcessor` / the GlobalManager keystone** — the keystone is DONE (`MasterTimePulse` iterates the GlobalManager so the processor genuinely fires monthly per NPC), **but its decision body is an empty `TODO` stub** — NPCs today only drift diplomatic-relationship numbers, take no actions. Filling that stub with "DoctrineVector biases stance → seated delegate executes → emits orders through rung 4" is the AI-self-play payoff.
- **Leader academies (rung 1)** — generalize `NavalAcademyAtb` off Navy-only; first consumer of `ColonyManpowerDB.TalentPool` (the unused talent draw); emits competence into the empty `BonusesDB`. Feeds every seat above.
- **Colony development** — a new single `ColonyDevelopment` accessor aggregating `HexTile.InfrastructureLevel`, forward-compatible with `docs/COLONY-PROGRESSION-DESIGN.md`'s tier ladder. Read by the academy competence roll; a bombarded world demotes it AND starves the pipeline (shared grave rung).
- **Legitimacy / colony happiness** (`Colonies/LegitimacyDB.cs`, `ColonyMoraleDB.cs`) — the Governor's `Acts` target: the dead `GovernorCompetence` slot (`×15` bonus) is the first payoff to wire. Legitimacy → `RebellionDB` is the downstream stake.
- **Contracts** — a net-new fixed-term on assignment (extends `AssignAdministratorOrder`); the commitment layer for rungs 3→6; caps the teacher loop and throttles AI reassignment churn.
- **Loss (rung 6)** — one `LeaderLost` handler (consume the already-published, unconsumed `EventType.CrewLosses`) vacates the seat + collapses delegation; producers wire in from **damage** (`DamageProcessor.OnColonyDamage` casualty roll), **ground capture** (`GroundForcesProcessor.TryCapturePlanet`), **espionage** (turn/defect), and a new **mortality/retirement** processor. Fixes the dangling-reference bug in `DestroyCommander` + the seat-reset in `AdminSpaceProcessor`.
- **Ground combat** (`docs/GROUND-COMBAT-MAP-DESIGN.md`, `docs/HEX-GROUND-AND-ORDERS-DESIGN.md`) — the General chain sits over the built formation/doctrine layer; the person-commander is the net-new wrapper.
- **Diplomacy / espionage / internal politics** (`docs/DIPLOMACY-DESIGN.md`, `docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md`, `docs/GOVERNMENT-AND-POLITICS-DESIGN.md`) — Foreign Minister, Spymaster, Interior Minister are delegates of this exact shape.
- **Survey / detection** (`docs/DETECTION-DESIGN.md`) — the survey fog is the free LOD brake; the sensor O(n²) risk is the performance landmine to watch.
- **Commerce** (`docs/RESOURCES-AND-MATERIALS-DESIGN.md`, `docs/SPACE-STATIONS-DESIGN.md`) — the Trade Minister is blocked on the trade-money wire (`Ledger` has no `Trade` category; `ExchangeCatalog`/`TradeAgreement` are inert data).
- **Performance** (`docs/SYSTEMS-STATUS-AND-TEST-PLAN.md`, `Benchmarks/`, `PerformanceReadoutSmokeTests`) — the scaling gauge is the first build.
- **Survey (the eXplore arm) is fully built** — geological + jump-point/gravitational survey each have a component, order, processor, completion events, and fog-of-war reveal; minerals hidden by default (`Masked<T>`), jump points hidden until surveyed, discovering one reveals the far-side system + adds to `KnownSystems`. **One content gap: no ruins/artifacts** beyond jump points — net-new, becomes its own system (deferred).

---

## Locked vs. open

### Locked (delegation mechanism)

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

### Locked (the leadership roster & pipeline — 2026-07-06 / 2026-07-10)

- **Delegation = NPC AI** — one system runs the player's hand-off and the NPC's brain; no separate AI path.
- **The 21-role roster** (was 19; Field Marshal #20 + Chancellor #21 added 2026-07-10) and its two-chain (administrative + operational) structure.
- **Head of State holds the regime directly** (government type + empire legitimacy) — no empire Interior Minister.
- **The TWO-AXIS RULE (2026-07-10)** governs the whole chart: every seat has an **admin line** (vertical — who you belong to) and a **domain-direction line** (horizontal — from your empire domain-head). Concretely: **Interior Minister & Planetary General report under the Planetary Governor; ALL system-level leaders (System Admiral, System Scientist, System General) report to the System Governor; the System Governor reports to the CHANCELLOR; the Chancellor reports to the HoS** — that vertical is the civil/admin spine. The specialist domains **cross in** horizontally: **Grand Admiral** → the Admiral's war-effort orders, **Field Marshal** → the General's ground orders, **Chief Scientist** → the Scientists' research direction. (Superseded the earlier "the mobile System Admiral only coordinates with the Governor.") The System Governor is the unambiguous **head of the system** — why objective-decomposition starts there (subsidiarity) — and the **Chancellor is the head of the civil empire**, the System-Governor-equivalent one scope up.
- **No leaders for leaders' sake** — every seat must own a distinct decision; competence is texture, not the justification.
- **Ship Captain cut** (a lone ship is a fleet of one); **company/unit commander deferred to v2** (keeps the AI-seat count honest).
- **One six-rung people pipeline** (born → skilled → seated → acts → improves → lost) for all 21 roles — build once, prove on the Governor, reuse.
- **Leaders are produced by academy installations** (rung 1) — generalize `NavalAcademyAtb`; draw the (unused) talent pool; the **competence generator** rolls `BonusesDB` from `(design tier + materials) × populousness × development × teacher`. Tier = eligibility ceiling; competence = value.
- **One `ColonyDevelopment` accessor** — aggregates hex infra now, tier-ladder-ready later (never two competing development numbers).
- **Retraining loop replaces passive XP** (rung 5) — leaders improve only by deliberate, costly re-enrollment.
- **Contracts = the universal commitment term** — every assignment is fixed-term (default 5 years); caps the teacher loop, makes seating a durable strategic choice.
- **Rung 4 "Acts" pattern** — `ModifiableValue` + `Refresh…Modifiers` reading `BonusesDB` (copy the research chain); the two-layer split (leader competence = modifier, regime = post-`GetValue` modulator); passive-bonus vs active-orders as the two flavors.
- **Stances = data-driven presets** (reuse the `GroundStanceBlueprint`/JSON-catalog pattern), biased for NPCs by `DoctrineVector`; stance = decision, competence = texture.
- **Competence roll = mean-shifted bell curve with a tier-gated ceiling** (`NextBellCurve`); inputs shift the mean, not the result.
- **Rung 6 = one `LeaderLost` event + one vacate/collapse handler** (leaders aren't modifiers — the player must be able to *lose* them). Four causes (killed / captured / turned / died); seat-durability fix is the prerequisite; decapitation collapses a whole scope. **Rung 6 outcomes are probabilistic, not fixed picks** — a turned leader lands as mole / clean defection / caught, and a captured leader as killed / prisoner / escape, both decided by `RNG × counter-intel × leader stats`. Ground-formation leader loss keeps the no-penalty reassign but **notifies the player**.
- **Rung 3 seating = built command capacity** — seats come from components built onto installations; you start from nothing, the first HQ grants 1 seat, and every position is filled as you progress. `FundingLevel` = a 0–5 spend dial (output↑, cost↑) on any post.
- **Two empire modulators — race and government.** Race sets leader **lifespan** and biases **doctrine tendencies**; government **renames and re-tools stances**. Both are the modulator layer, not per-leader stats.
- **The three-mode automation dial (Delegate / Advise / Hand-fly)** — the DW lesson, adopted wholesale; a third stance value on the delegate record + a `ProposedOrder` staging state for Advise. Empire-wide default per function + per-object override, instant reversible take-over.
- **The DESIGN parity hole is dissolved by authored ladders + research-targeting** — the AI never solves generative ship design; it unlocks a pre-authored, tech-gated design ladder via research (owner = the science chain; the proposed Chief Engineer / Naval Architect seat is RETIRED). v1 fallback reacts to combat outcomes (no espionage needed); full spy-driven targeting lands with espionage.
- **Parity resolutions (2026-07-10):** fleet-composition → System Admiral #9; colonization target → System Governor #7 (HoS Expansion dial modulates); tax/stockpile → Planetary Governor #8 via a new `SetColonyPolicyOrder` (must carry `DesiredLevels`); character-assignment ("HR") → HoS best-fit numbers game (no seat); empire budget → build-when-it-bites (deferred); civilian economy → grabbable-with-a-guaranteed-policy-only-fallback.

### Open (the leadership roster & pipeline)

- **The actual stance names** per pillar per government type (the fact that they re-skin by government is locked; the specific names come with the government content).
- **How race biases doctrine/stance selection** — the trait→lean mapping (locked that it's race-driven; the mapping itself is content).
- **The competence-roll numbers** — base means per tier, stddev, weights/caps on the three soft modifiers.
- **Contract early-break penalty** — the number/shape (term = 5y locked).
- **Race lifespan values** — per-species numbers.
- ~~The empire-scope ground ceiling~~ **RESOLVED 2026-07-10: Field Marshal** (ground) + **Chancellor** (civil); both report to the HoS (no joint Supreme Commander rung — the HoS is commander-in-chief; joint space→ground ops are handled by the transition engine's phase-gates, not a new seat).

**Sequencing notes (decided, deferred — do NOT build yet):**
- **Commerce / Trade Minister** — build the trade-money wire (and the Trade Minister) **after** the leadership pipeline is established.
- **Ruins / anomalies** — **completely in**, but becomes **its own system** to develop later (not part of the leader pipeline build).

**Parked design threads (explored the roster, not yet the depth):**
- **Foreign Minister pillar** — worked out 2026-07-07 (in `docs/DIPLOMACY-DESIGN.md`). The overt twin of the Spymaster: empire FM → per-faction FM → Envoy/Ambassador hands, on the leader pipeline. **Key finding: the three "keystone" prerequisites are cleared/dissolved** (GlobalManager fixed, `SignalQuality` cut → gradient in the Ledger, hostility-from-diplomacy done) and the substrate is built — so the FM is the *decider* that drives built-but-DARK machinery (reactive overture engine, treaty proposal, exchange-catalog executor, casus-belli gate). FM MVP = per-faction FM + the "Are we good?" loop + the NPC counterparty + an observation feeder.
- **Race / species trait system** — became load-bearing (sets leader **lifespan**, biases **doctrine lean**, plausibly **espionage aptitude**). Needs its own design pass; wires to the species system (`ColonyInfoDB.Population` is per-species).
- **Spymaster / espionage pillar** — worked out 2026-07-07 (`docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md`): agents are full leader-entities; org = empire **Director of Intelligence** → per-faction **Spymaster** → agents; **counter-intel = same agent, target = self**; `SignalQuality` **CUT**; Spymaster MVP = Ledger + passive + per-faction seat + `gather` + the NPC counterparty, as the **second** leader vertical slice after the Governor. **Agent delivery = PHYSICAL** (troop-transport reuse, EMCON=stealth, "attach as infiltrator" the one new mechanic).

### Build order (the leadership pipeline — after the shared prerequisites)

1. **Build the scaling gauge** (Visibility Gate) — the faction/entity performance benchmark, before any AI logic.
2. **Finish the delegate substrate**: generalize `AdministratorDB` → the universal delegate record → close the `CommanderDB` skill-field gap → **make seating durable** (fix the `AdminSpaceProcessor` seat-reset + the `DestroyCommander` dangling-reference leak; add the one `LeaderLost` vacate handler). Shared prerequisite for rungs 3 and 6.
3. **Rung 1 — leader academies**: generalize the academy, wire the talent draw, build the competence generator + the `ColonyDevelopment` accessor. The cradle that gives every later rung something to seat.
4. **The Governor vertical slice** (rungs 1→4 end-to-end): rolled officer → `AdminSpace` seat → competence into the dead `GovernorCompetence` slot → legitimacy moves. Proves the whole pipeline.
5. **Contracts, retraining, and loss** (rungs 5–6) once one role is seated and acting: wire the `LeaderLost` producers (bombardment casualty roll, colony-capture, mortality/retirement, then espionage turning) so a leader can actually be *lost*.
6. **Fill the `NPCDecisionProcessor` stub** — translate the doctrine vector into real orders through the seated delegates.
7. **Level-of-Detail for distant empires** — the affordability lever, once there's a gauge to prove it works.
