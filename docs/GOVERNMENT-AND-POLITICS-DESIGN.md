# Government Types & Politics — Design (capture, 2026-06-29)

**Status: design capture, recorded 2026-06-29 at the developer's request.** The empire-wide **regime** layer that modulates how every other system plays, plus the future **popular-demands** (Stellaris-parties) layer. Companion to `docs/MORALE-AND-POPULATION-DESIGN.md` (the levers a government modulates) and the governance/delegation layer (task #23). Build AFTER the core levers exist (M3–M5) — but those levers are built *government-ready* so this slots in without rework.

> **Naming — two distinct systems, do not conflate:**
> - **Government type** (this doc) — the empire-wide regime (democracy/dictatorship/…) that sets the *rules of the game*.
> - **Governance / delegation** (task #23) — per-colony, how hands-on the player is (governors). A governor *operates a colony within* the rules the government sets.

---

## ★ THE GOVERNING PRINCIPLE — "Every Layer a Complete Game" (project-wide law, 2026-06-29)

**Each pillar — combat, economy, politics, ground war, exploration — must be deep enough to be a player's MAIN game, and delegate-able enough to ignore.** A combat-lover auto-resolves politics; a schemer auto-resolves combat; both are playing a complete game. It cuts every direction. This is the bar the political layer (and every layer) must clear — and it's *why* politics gets built to real depth instead of the usual three relation sliders (the genre's chronic gap).

**It generalises delegation.** Delegation isn't just colony governors — **every pillar gets a DELEGATE you hand it to, at a talent cost**: an admiral (combat doctrine), a governor (colony), a **foreign minister** (diplomacy), a **spymaster** (espionage), an **interior minister** (internal politics). Depth is opt-in *per pillar, per game, even per relationship/world*. The delegate is a commander (gated by the M3 talent pool); competence sets how well the auto path performs. **The full system — the one DELEGATE shape, the span-of-control limiter, and the "play at your own altitude" agency valve — is designed in `docs/GOVERNANCE-AND-DELEGATION-DESIGN.md`; this politics layer's Ministers are posts that bolt onto that chassis.**

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

### Legitimacy — the regime's health bar, **LOCAL not empire-wide** (the grave rung for politics)
**Legitimacy is tracked PER SYSTEM (the province), not as one empire-wide number** — so the whole empire can never rebel at once; you lose *provinces*, regionally, one at a time, and can fight to hold or retake them. A **station** (especially a lone or major one) can hold its **own** legitimacy and break away independently — fitting the station as the fragile, frontier, easily-lost node.

Each system's legitimacy (0–100) is **DERIVED each cycle** (not a parallel system) from: its **hosts' morale** (the local people's contentment — already computed per colony/station) + the local **demand track-record** + **war outcomes** affecting it + **governor competence** (the delegation layer holds a restless province) + **distance/connectivity to the capital** (a far, poorly-connected system is harder to hold — ties to logistics and the Stargate gate-network pillar: a gated province is easier to keep). **Government type modulates the weights** (democracy: morale + demands dominate; dictatorship: garrison/force + control dominate, morale matters less but unrest accrues).

**Two tiers of collapse:**
- **Local (common, manageable):** a single system's legitimacy collapses → **that system rebels** — secedes to independence, installs a rival local government, or defects to an enemy. Regional, not fatal. A restless frontier while the core stays loyal is the normal texture.
- **Central (rare, catastrophic):** if the **capital's** legitimacy collapses, or **enough** provinces rebel at once, the central **REGIME** falls — the empire-wide coup/revolution/regime-change locked above. This is the extreme, reached *through* widespread local failure, not a single empire bar ticking to zero.

This is politics' cradle-to-grave loss: mismanage a province and you lose *it*; mismanage the whole and you lose your government. Re-earn legitimacy to recover a wavering system before it goes.

### What "the system rebels" ACTUALLY does — the rebellion mechanics (LOCKED 2026-06-30: a structured combo of all three paths)

When a system's legitimacy hits collapse, it does NOT just flip ownership. It runs a **process with a reaction window**, and the *outcome* depends on what's driving it. The locked design is a combo, not a single path — **rebellion-as-process → one of three resolutions:**

1. **Rebellion is a PROCESS you can fight (the reaction window).** Collapse first puts the system into a **REBELLION state** — hostile-but-not-yet-gone, with a timer/struggle — instead of an instant loss. You get a window to **respond**: pour in legitimacy (enact the local demands, ship aid, replace the governor), OR **militarily suppress it**. *This is the load-bearing wire: suppressing/retaking a rebelling colony IS the ground-combat MVP ("you can take a planet"), pointed inward — internal politics becomes a generator of ground-combat scenarios.* Fail to act and the rebellion resolves into one of:
2. **Secession → a NEW independent faction is BORN (B1).** The system breaks away as its own faction, inheriting its colonies (and some local forces), carrying a grudge against you. **Your mismanagement literally spawns a rival** — the galaxy gets more crowded organically, and you can try to re-absorb it later by diplomacy or conquest. *(Engine note: needs runtime faction-spawn + asset inheritance — the biggest new piece.)*
3. **Defection → flips to an EXISTING rival (B2)** — especially the one whose **espionage sowed the unrest** (the ESPIONAGE `sow-unrest` action, `ESPIONAGE-AND-INTELLIGENCE-DESIGN.md` cat E3). This is the cheaper path (transfer ownership, no new faction) and the concrete **payoff for the spy game**: their agents flip your frontier worlds; yours flip theirs. Borders become dynamic.

**Why the combo:** this single mechanic is the **hub that wires three pillars together** — it is *driven by* espionage (defection), it *generates* ground combat (the suppress/retake window), and it *creates* new diplomatic actors (secession). Build order: the **rebellion-state + suppress-window** first (rides ground combat), then **secession-to-new-faction**, then **espionage-driven defection** (lands with the espionage `sow-unrest` slice). Calibration lock from the espionage side: a spy can *nudge* a wavering province toward rebellion but not single-handedly topple a healthy one — legitimacy still has to be low.

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

### Government RE-SKINS the names AND the process (the modulator extends to presentation + flow)
**The same machinery underneath; the government type changes what it's CALLED and HOW it plays out.** Government-as-modulator isn't only coefficients and rule-overrides on the numbers — it also overrides the **vocabulary** and the **process flow** of this whole internal layer, so a democracy and a hive don't look like the same game with different multipliers. The substrate (legitimacy meter, blocs, emergent demands, enact/refuse, collapse) is identical; the **skin and the steps** are the government's to set. Same as the dial classifier names the regime, an extra column of that JSON names the *politics*:

| Concept (engine) | Democracy / Republic | Dictatorship / Autocracy | Theocracy | Hive / Machine |
|---|---|---|---|---|
| **Bloc** | **party** | **faction / clique** | **sect / order** | (none — one will) |
| **Demand** | petition / platform plank | grievance / agitation | doctrinal call | (none / drift signal) |
| **Enact** | pass / legislate | grant / decree | sanctify / issue edict | self-correct |
| **Refuse** | veto / lose the vote | **suppress / purge** | declare heresy | overrule |
| **Legitimacy loss** | lose the election | coup risk / unrest | **schism** | (near-none — coherence) |
| **The cadence** | **elections** (periodic, scheduled reckoning) | continuous unrest you suppress/appease + coup risk | doctrinal cycles / religious calendar | minimal — rare drift events |

**The PROCESS shifts too, not just the words:**
- **Consent regimes (Authority→People):** demands arrive as petitions; refusing one costs you at the next **election** (a scheduled, recurring legitimacy reckoning unique to these regimes — lose it and the regime changes *peacefully*). The people leave or vote you out; they rarely shoot.
- **Command regimes (Authority→One Ruler):** there's no election to lose — demands are unrest you **suppress** (cheap short-term, stacks resentment) or **appease**, and the grave rung is a **coup** (a faction's loyalty flips the garrison) rather than a ballot. Legitimacy is held by force + delivery, not approval.
- **Theocracy:** demands are doctrinal; refusing the faithful risks a **schism** (a sect secedes with its own legitimacy — ties straight into the per-system local-collapse model above).
- **Hive / Machine:** the layer nearly **collapses to nothing** — minimal/no blocs, no demands, no elections; "discontent" is rare coherence-drift, not politics. This is the intended *thin* skin for a unity-stat empire (and the place the "what replaces morale" open question lands).

**Why it's cheap:** the engine still computes one legitimacy number, one bloc-support set, one demand list. The government type supplies (a) a **string table** (bloc→"party"/"sect", refuse→"purge"/"veto") read by the readout/UI, and (b) a small set of **process flags** already implied by the dials — `HasElections` (consent regimes), `RefusalMode` (emigration / suppress→coup / schism), `DemandVolume` (how loud/often) — all derivable from the Authority/Openness/Militarism notches the modulator already holds. No parallel system per government; one substrate, re-skinned and re-sequenced by the same modulator that already overrides the numbers.

### Cradle to grave (politics)
> a simulation **PRESSURE** (a real condition) → a **BLOC** voices a **DEMAND** (organised interest) → the player **ENACTS or REFUSES** (the lever) → **buff/debuff** to bloc support + legitimacy + morale (the effect) → sustained mishandling collapses **LEGITIMACY** → **regime change / revolt / secession** (the grave rung — you can lose your government or fracture your empire) → re-earn legitimacy to recover.
>
> *(The whole loop above is **re-skinned and re-sequenced by government type** — see "Government RE-SKINS the names AND the process": same machinery, different words and cadence, from party-petition-veto-election to faction-grievance-purge-coup.)*

### Connections (Prime Directive)
- **Morale/population** (built) — demands are generated from its factors; unrest is the per-colony manifestation; war-weariness is a morale input.
- **Government** (substrate built) — the modulator: which blocs are loud, how loud demands are, the refusal cost (emigration vs revolt), and legitimacy's weights; legitimacy-collapse triggers the locked regime-change. **It also re-skins the VOCABULARY and re-sequences the PROCESS of this layer** (party/petition/election vs faction/grievance/coup — see "Government RE-SKINS the names AND the process"), via a string table + a few process flags (`HasElections`, `RefusalMode`, `DemandVolume`) derived from the same dial notches.
- **Economy** (built) — most demands are economic (taxes/jobs); enacting spends money/resources.
- **Military** (built) — war demands; war outcomes feed legitimacy; "Confront [Rival]" bridges to EXTERNAL casus belli.
- **People** (M3 talent) — the Interior Minister is a delegate commander.
- **EXTERNAL politics** (designed 2026-06-30, `docs/DIPLOMACY-DESIGN.md` → "EXTERNAL politics — politics with teeth") — rival-facing demands hand off to diplomacy/casus belli; a "Confront [Rival]" demand IS a casus belli; war outcomes feed this layer's legitimacy; a trade treaty grows the Merchant bloc. The two layers share one engine and hand off every cycle.

### Build order (design now; build when scheduled)
legitimacy meter (derived, **per-system**) → blocs + emergent support → the emergent demand engine (reads the morale factors) → enact/refuse + buff/debuff + bloc-conflict → **the government re-skin/process layer** (string table + `HasElections`/`RefusalMode`/`DemandVolume` flags off the dials) → the Interior-Minister delegate → wire local legitimacy-collapse (system secession) + central collapse (capital / enough provinces) to the locked regime-change. *(EXTERNAL diplomacy — relations/treaties/casus belli/espionage/first-contact — is the larger, later half; see `docs/DIPLOMACY-DESIGN.md`.)*

### Open (decide when we build)
- Legitimacy's exact weight formula + the collapse threshold (calibration — feel).
- Bloc **support** dynamics (how fast support shifts; loyalty vs support split).
- Whether a 6th bloc (Faithful) appears only when the theocracy/tradition lean is high.
- Demand cadence (how often the people speak) + how many can be "open" at once.
- The re-skin **string table** + process-flag values per government type (the JSON column, moddable — same file as the dial classifier).
- Election mechanics for consent regimes (the scheduled legitimacy reckoning) — how often, and whether a lost election is a peaceful regime-swap vs. a soft game-over.

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
