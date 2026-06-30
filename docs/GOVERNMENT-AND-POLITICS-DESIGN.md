# Government Types & Politics — Design (capture, 2026-06-29)

**Status: design capture, recorded 2026-06-29 at the developer's request.** The empire-wide **regime** layer that modulates how every other system plays, plus the future **popular-demands** (Stellaris-parties) layer. Companion to `docs/MORALE-AND-POPULATION-DESIGN.md` (the levers a government modulates) and the governance/delegation layer (task #23). Build AFTER the core levers exist (M3–M5) — but those levers are built *government-ready* so this slots in without rework.

> **Naming — two distinct systems, do not conflate:**
> - **Government type** (this doc) — the empire-wide regime (democracy/dictatorship/…) that sets the *rules of the game*.
> - **Governance / delegation** (task #23) — per-colony, how hands-on the player is (governors). A governor *operates a colony within* the rules the government sets.

---

## ★ THE GOVERNING PRINCIPLE — "Every Layer a Complete Game" (project-wide law, 2026-06-29)

**Each pillar — combat, economy, politics, ground war, exploration — must be deep enough to be a player's MAIN game, and delegate-able enough to ignore.** A combat-lover auto-resolves politics; a schemer auto-resolves combat; both are playing a complete game. It cuts every direction. This is the bar the political layer (and every layer) must clear — and it's *why* politics gets built to real depth instead of the usual three relation sliders (the genre's chronic gap).

**It generalises delegation.** Delegation isn't just colony governors — **every pillar gets a DELEGATE you hand it to, at a talent cost**: an admiral (combat doctrine), a governor (colony), a **foreign minister** (diplomacy), a **spymaster** (espionage), an **interior minister** (internal politics). Depth is opt-in *per pillar, per game, even per relationship/world*. The delegate is a commander (gated by the M3 talent pool); competence sets how well the auto path performs.

**The relationship to the audit:** this is the player-experience form of `docs/REALISM-VS-GAMEPLAY-AUDIT.md`'s "earn weight," but stronger — not just "a decision that stacks," but "a decision-space rich enough to *live in*," with an auto-resolve escape hatch so it's never *mandatory* depth.

---

## The framework: a government = coefficients + rule-overrides

A `GovernmentDB` (on the faction) carries two kinds of effect, and the engine is already built for both:

1. **Coefficient modulation** — scalar dials on existing levers. Every morale weight (and the M3–M5 levers) is a **named constant**, so the government just supplies overrides. Processors read `government?.Coeff(x) ?? default`.
2. **Rule overrides** — swap a behavior *branch*, not just a number. Processors read `government?.Rule(y) ?? default`.

**Why this works:** the levers are coefficients and the rules are identifiable branch points, so a regime re-skins the game without touching the engine. This is the "government-ready" discipline the morale/people slices have been banking (named coefficients everywhere).

**The load-bearing example (the developer's):** a dictatorship building a ship it lacks crew for. Consent governments **Block** the build (can't conscript); a dictatorship **Builds-understaffed-with-a-debuff** (conscription) until the crew fills in. That is a *rule override*, not a coefficient — which is why M3-2's crew shortage must be built as a **swappable policy** (`CrewShortagePolicy = Block | BuildUnderstaffed`, default `Block`), so the regime swaps it later with zero rework.

Likewise **discontent response** is a rule override: consent governments → **emigration** (people leave); command governments (closed borders) → people *can't* leave, so it converts to **unrest → revolt**.

---

## Starter matrix — government types × gameplay levers

A few named types for v1, each a **preset bundle** of coefficients + rules over the underlying table (so composable axes/civics can be added later). The Hive/Machine column is the deliberate edge that proves the framework's range — and it is also the **droid path** parked in task #20 (a machine empire has no morale and "builds" its populace).

| Lever | Democracy | Republic | Dictatorship | Theocracy | Hive / Machine |
|---|---|---|---|---|---|
| **Morale → output** | very high (both ways) | high | low (suppressed) | high if faith held | none / replaced stat |
| **Discontent response** | emigration | emigration | unrest → revolt | schism / unrest | none |
| **Crew shortage (M3)** | **Block** | Block | **BuildUnderstaffed (debuff)** | Block | built, not crewed |
| **Tax ceiling (M4)** | low, morale-sensitive | medium | high, extract anyway | tithe (faith-gated) | n/a |
| **Military build speed/cost** | normal, needs consent | normal | **fast / cheap** | normal | fast |
| **Research** | high (free inquiry) | high | skewed military | dogma-limited | very high |
| **Popular demands** | strong, refusal costly | medium (representatives) | weak, refuse cheap → unrest | religious demands | none |
| **Legitimacy from** | approval / elections | law / representation | stability / force | faith | n/a |

*(Numbers are illustrative directions, not final — calibration is a local-build/feel job, like the rest of the morale system.)*

---

## INTERNAL politics — legitimacy + emergent demands (the "people speak" layer) — LOCKED 2026-06-29

The internal half of "politics with teeth," designed to the *Every-Layer-a-Complete-Game* bar. Two theaters exist (INTERNAL = hold your own house; EXTERNAL = deal with rivals); **internal is designed first** because it rides directly on the morale/government engine already built. The core loop the developer named: **the people speak → you enact (buff) or refuse (debuff) → consequences stack → mishandle it and you lose your regime.**

### Legitimacy — the regime's health bar (the grave rung for politics)
An empire-wide meter (0–100), **DERIVED each cycle** (not a parallel system) from: weighted **colony morale** (the people's contentment — already computed) + the recent **demand track-record** (enact/refuse outcomes) + **war outcomes** (wins ↑, war-weariness ↓) + economic state. **Government type modulates the weights** (democracy: morale + demands dominate; dictatorship: stability/force + military dominate, morale matters less but unrest accrues). **Low legitimacy → the regime-change grave rung** (the phased coup/revolution/secession already locked above). High legitimacy → room to act *against* the popular will. This is politics' cradle-to-grave loss: mismanage it and you lose your government, or the empire fractures — then you re-earn it.

### Blocs — the sources of demands (Stellaris-parties)
A **small FIXED set of interest blocs**, each with **EMERGENT support** that shifts with conditions and your responses (presets-over-a-substrate, like the dials — civics can extend it later):
- **Labor** — jobs, low unemployment, housing.
- **Merchants / Industry** — low tax, trade, growth.
- **Militarists** — military spending, war, confront rivals.
- **Liberty / Reformers** — openness, civil liberty, less authority.
- **Order / Traditionalists** — stability, authority, tradition.

Each bloc's **support** (share of population backing it) rises/falls from the sim (war boosts Militarists; recession boosts Labor; etc.) and from your demand responses (meeting Labor's demands grows Labor support + loyalty). **The government dials bias which blocs are loud** (Militarism dial → Militarists; Openness → Liberty; Authority → Order).

### Demands — EMERGENT from the simulation (locked: emergent, not scripted)
Each cycle the demand engine reads the **same state the morale system already computes** (the morale *factors*) + government lean + external situation, and surfaces the strongest pressures as **demands voiced by the relevant bloc**. A demand = {source bloc, the ask, a satisfaction condition}. The cheapness: a demand is essentially "a morale factor bad enough that a bloc organises around it." Emergent triggers → demand:
- tax factor very negative → Merchants/Labor: **"Lower Taxes."**
- unemployment factor negative → Labor: **"Create Jobs."**
- war ongoing + pacifist/low-militarism → public: **"End the War."**
- rival detected + high militarism → Militarists: **"Confront [Rival]"** *(bridges to EXTERNAL casus belli).*
- crowding/poor conditions → **"Expand"** / **"Improve Living."**
- high Authority + Liberty strong → **"Reform"** (loosen a dial).

Flavor text dresses the emergent trigger; the *substance* is generated, so it's endlessly replayable and reuses everything.

### The decision — enact / refuse (the teeth)
- **Enact** (do the ask): the demanding bloc's support + loyalty rise; legitimacy ↑ — BUT it **costs** (money, a policy change, a war) and usually **angers an OPPOSED bloc** (cutting taxes pleases Merchants, angers Labor who wanted the jobs that tax funded). **Demands CONFLICT — you can't please everyone.** That tension is the depth.
- **Refuse** (ignore/suppress): the bloc's loyalty ↓, unrest ↑, legitimacy ↓. **Government type sets the refusal cost** (the rule-override): consent regimes → emigration / lost elections; command regimes → unrest stacking toward **coup/revolt**.
- **Binary for v1** (clear teeth); partial/negotiated responses later.

### The delegate — the Interior Minister (the auto-resolve path; the principle made concrete)
A commander (talent-gated, M3) seated as **Interior Minister**. The player sets a **stance/policy** ("favour stability," "favour the military," "keep taxes low," "balance the blocs"), and the minister **auto-responds to demands per that stance, at a competence cost** (a good minister optimises; a poor one mishandles → legitimacy leaks). So a player who doesn't want to micro politics sets a stance + a capable minister and forgets it; a player who *loves* it handles every demand personally. **That symmetry is the bar from the governing principle.**

### Cradle to grave (politics)
> a simulation **PRESSURE** (a real condition) → a **BLOC** voices a **DEMAND** (organised interest) → the player **ENACTS or REFUSES** (the lever) → **buff/debuff** to bloc support + legitimacy + morale (the effect) → sustained mishandling collapses **LEGITIMACY** → **regime change / revolt / secession** (the grave rung — you can lose your government or fracture your empire) → re-earn legitimacy to recover.

### Connections (Prime Directive)
- **Morale/population** (built) — demands are generated from its factors; unrest is the per-colony manifestation; war-weariness is a morale input.
- **Government** (substrate built) — the modulator: which blocs are loud, how loud demands are, the refusal cost (emigration vs revolt), and legitimacy's weights; legitimacy-collapse triggers the locked regime-change.
- **Economy** (built) — most demands are economic (taxes/jobs); enacting spends money/resources.
- **Military** (built) — war demands; war outcomes feed legitimacy; "Confront [Rival]" bridges to EXTERNAL casus belli.
- **People** (M3 talent) — the Interior Minister is a delegate commander.
- **EXTERNAL politics** (next) — rival-facing demands hand off to diplomacy/casus belli.

### Build order (design now; build when scheduled)
legitimacy meter (derived) → blocs + emergent support → the emergent demand engine (reads the morale factors) → enact/refuse + buff/debuff + bloc-conflict → the Interior-Minister delegate → wire legitimacy-collapse to the locked regime-change. *(EXTERNAL diplomacy — relations/treaties/casus belli/espionage/first-contact — is the larger, later half; see `docs/DIPLOMACY-DESIGN.md`.)*

### Open (decide when we build)
- Legitimacy's exact weight formula + the collapse threshold (calibration — feel).
- Bloc **support** dynamics (how fast support shifts; loyalty vs support split).
- Whether a 6th bloc (Faithful) appears only when the theocracy/tradition lean is high.
- Demand cadence (how often the people speak) + how many can be "open" at once.

---

## Locked vs. open

**Locked (developer, 2026-06-29):**
- Government is a **modulator** = coefficients **+ rule overrides** (not just numbers). The dictatorship build-understaffed example is the canonical rule override.
- Built as **named presets over a general coefficient/rule table** (axes/civics can come later).
- M3-2's crew shortage is built as a **swappable `CrewShortagePolicy`** (Block default) so the regime can flip it.
- A future **popular-demands** layer (enact→buff / refuse→debuff), weighted by government type.

**Locked (developer, 2026-06-29) cont.:**
- **Regime CAN change mid-game — phased.** Tier 1 *the switch itself* is nearly free (government is a modulator; swapping its values re-skins the rules next tick). Tier 2 *player-chosen reform* (cost + cooldown + a temporary instability dip) is cheap and lands with the government substrate. Tier 3 *forced change* (revolution/coup, driven by unrest) waits on the unrest system; the *upheaval drama* (civil war / secession / coup-installs-rival) is a bigger, optional, later layer riding on unrest + demands.

**Locked (developer, 2026-06-29) — Fork 1 = BOTH (dials under a menu).** Build the underlying dials; ship the named types as saved dial-settings. Player sees the simple menu day one; dials exposed later for custom + moddable governments with zero rework (presets-over-a-substrate, same as the ship/component designer).

**Locked (developer, 2026-06-29) — sub-decisions:**
- **THREE NOTCHES per dial (Low / Mid / High)**, not free sliders. Each notch = one fixed coefficient/rule set (no interpolation). The classifier reads the notch combo directly (the combo *is* the key). 4 dials × 3 notches = 81 combos; hand-name the ~12 iconic ones, auto-describe the rest.
- **FOURTH DIAL = MILITARISM** (Pacifist ⟷ Militarist). Chosen over Tradition/Religion because it plugs into systems already being built (combat, armies, M3 manpower) and sharpens the war-weariness case. (Religion/Tradition remains a candidate 5th civic later. Developer may swap.)

---

## The control panel — the dials + the live classifier (design detail, 2026-06-29)

A government is a **panel of four dials, each with three notches (Low / Mid / High)**. Each notch just *sets the coefficient + rule-flag values the modulator already exposes* (`government.Coeff(x)` / `government.Rule(y)`) — so this is wiring values, not new engine plumbing. Each dial is a trade, no "correct" setting. Mid is the balanced middle of each.

**Dial 1 — AUTHORITY (The People ⟷ One Ruler):** toward People → morale & popular-demand weight HIGH, govern by consent (`CrewShortagePolicy = Block`), tax ceiling LOW, discontent = emigration/elections. Toward One Ruler → opinion weight LOW but **unrest** accrues, conscription (`CrewShortagePolicy = BuildUnderstaffed`), tax ceiling HIGH, discontent = **unrest → revolt**.

**Dial 2 — ECONOMY (Free Market ⟷ State Command):** Free → trade wealth/money, builds cost cash & follow demand. Command → military/infrastructure builds **fast & cheap (materials not money)**, innovation/wealth penalty, state can force builds.

**Dial 3 — OPENNESS (Closed ⟷ Open):** Open → research bonus, immigration appeal, discontent = emigration, demands loud, espionage-vulnerable. Closed → research penalty, **closed borders** (discontent = unrest), demands muffled, espionage-resistant.

**Dial 4 — MILITARISM (Pacifist ⟷ Militarist):** Pacifist → war is a **morale penalty** (war-weariness, amplified by Authority-toward-People), military upkeep resented, costly casus belli, **recruitment harder** (M3 draw), but civilian trade/research/diplomacy bonuses. Militarist → war is tolerated or a **morale bonus** (martial pride), cheap/accepted upkeep, easy casus belli, **recruitment easier** (M3 draw), but civilian economy/research/diplomacy penalties. Modulates the "war" morale input's SIGN.

**The live classifier (what the player sees while twiddling):** each dial has **three notches**, so the notch combo *is* the lookup key (no bucketing). Iconic combos get iconic names (table below); any un-named combo gets an **auto-assembled description** ("a closed, command-run, militarist autocracy") so every setting always shows something sensible. The named-combo table is a **JSON file** (moddable). The panel also shows **live consequences** ("+25% research · −15% build cost · tax cap 30% · conscription OFF · war: morale −X · citizens may emigrate") so the player sees the trade as they make it.

| Authority | Economy | Openness | Militarism | Names itself |
|---|---|---|---|---|
| People | Free Market | Open | Pacifist | Liberal Democracy |
| People | Free Market | Open | Militarist | Martial Republic |
| People | Command | Open | Pacifist | Democratic Socialist Union |
| One Ruler | Command | Closed | Pacifist | Totalitarian State |
| One Ruler | Command | Closed | Militarist | Totalitarian War-State |
| One Ruler | Free Market | Open | Pacifist | Corporate Plutocracy |
| One Ruler | Command | Mid | Militarist | Military Junta |
| Mid | Mid | Mid | Mid | Federal Republic |

*(Names/values illustrative; calibration is a local-build/feel job. Implementation: `GovernmentDB` holds the 4 notch values; `Coeff()`/`Rule()` derive from them via a per-notch table; a `GovernmentClassifier` (JSON-backed) maps the notch combo → name + description + the live modifier summary.)*
- Exact coefficient/rule values per type (calibration — local-build feel).
- What replaces "morale" for a Hive/Machine empire (a unity/processing stat?), and how the droid/people rules differ.
- Where `GovernmentDB` lives (faction entity) and how per-colony processors read it given the GlobalManager-not-iterated trap (pass it down, or cache on the colony).

---

## Build order

Capture now; **build the GovernmentDB substrate after the core levers exist (M3-2 / M4 / M5)** so there's something real to modulate. Each of those slices is built government-ready (named coefficients + policy-flag indirection). The popular-demands layer comes after the regime substrate. Governance/delegation (task #23) is independent and can land earlier.

*This is a capture, not a build ticket. Next action when scheduled: lock the two open forks, then define the coefficient/rule table + a first two types (democracy vs dictatorship) as the minimal contrast.*
