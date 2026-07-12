# Exploration Content — the Field-Site Loop + Catalog (design)

**Status:** concepts + path forward (design conversation 2026-07-07; developer's-vision + incident-shape added 2026-07-11; **expanded catalog / "mine all of sci-fi" variety pass added 2026-07-12**; **X.1 ruins-tautology fix SHIPPED 2026-07-12** — build item 1's generation half is done, ruins now generate, the field-site consumer is next). Sits alongside `docs/DETECTION-DESIGN.md` (the survey/fog substrate this rides) and `docs/AI-SELF-PLAY-DESIGN.md` (scientists are leaders on the pipeline this gives a field career).

---

## What it does, in one line

Gives exploration something to **FIND** and scientists something to **DO** — one "field site" loop (survey to find → assign scientists → it yields → it depletes / persists / blows up) fed by a broad catalog of planetary and deep-space discoveries — so the galaxy has **prizes worth exploring, settling, and fighting over.**

## Why it matters (the three gaps it closes at once)

The 2026-07-07 current-state survey found three holes that are really one:
- **Exploration has nothing to find.** The survey→discover→reveal spine works, but `RuinsDB` never generates (a tautology bug) and nothing reads it; there are no anomalies, derelicts, or sites. Exploration is a reveal mechanic, not a decision.
- **Scientists barely matter.** They have exactly one job (sit in a lab), and even the lab competence bonus is near-unreachable through the default factory path.
- **Planets are undifferentiated.** Every world is roughly the same; nothing makes *this* planet a prize.

One system closes all three: **content for eXplore, a field career that makes scientist competence matter, and differentiated planet/system value.**

---

## The developer's vision — this IS science, and it should fill a home system for 250 years (2026-07-11)

**The bar (developer's call):** *"250 years in-game and never leaving Sol is completely fine — because the time was filled."* Filled with **what** is the whole point, and it's this system. The named picture:

- **Ancient ruins on Mars** — a dig that yields a windfall (the *ancient ruins* row).
- **Stabilizing Mercury after a bad solar flare hit** — a disaster your science corps has to *respond to* and bring back from the edge.
- **A hostile fish species that broke out of the ice during a mining incident on Europa** — an accident of your own making that unleashes a xeno-threat you now have to contain.
- **Jupiter's Great Red Spot is full of FTL fuel — but extracting it in a corrosive atmosphere is hell** — a rich prize gated behind a brutal environmental engineering problem (the *rare/exotic resource* row × the hazard geography).

**"Take the essence of that and fill science. It's essentially about four seasons' worth of Star Trek episodes."** Two design consequences fall out of that sentence:

1. **DENSITY is a first-class goal, not a "later" one.** Event-chains and episodic texture were parked as the last build item below — this note *elevates the intent* (not the build order): the catalog must be deep and dense enough that **a single home system is a campaign's worth of content.** Exploration is not the reward for leaving home; it's the substance of *being* somewhere. Science stops being a tech-tree grind and becomes **the episodes**. (Calibration — how many sites/incidents per body, how often chains fire — stays "open," but the target is now explicit: *dense enough to fill Sol.*)

2. **A THIRD site shape — the INCIDENT (reactive), alongside the two discovery shapes.** The catalog below is *discovery*-shaped: you survey, you find a prize, you choose to investigate. But *"stabilize Mercury after a flare"* and *"contain the Europa outbreak"* are **not things you go find — they happen TO your worlds and demand a science response.** That's a distinct shape:

   > **an event fires on a body you already hold (a hazard strike, a mining accident, an outbreak, a containment failure) → it degrades that world (population/industry/stability) on a clock → you assign scientist(s) to RESOLVE it (competence + funding = how fast, how safely) → resolved: the world recovers, maybe with a bonus learned; ignored/failed: it worsens, spreads, or is lost.**

   It **reuses the exact field-site loop** (assign scientists, funding dial, competence = rate/quality/risk, people-loss rung) — it's the same machine pointed at a *problem on your own turf* instead of a *prize on the frontier*. It's the reactive twin of exploration: the same science corps that digs the Mars ruin is the fire brigade for the Mercury flare. **The incident is also where hazards (which already exist) become gameplay** — a solar flare isn't just a number, it's an episode. And it ties the crisis seed in early: the Europa outbreak is a *small* version of the same "a thing got loose" shape the late-game ascension seed is the *galactic* version of.

   *(This makes the site-shape flag three-valued: **persistent study · one-shot expedition · incident-response.** Add it to the catalog-entry flag; the loop is unchanged.)*

---

## The spine — a "field site" is a lab on a location

Right now a scientist works a **lab** (`ResearcherDB` + assigned scientist + a 0–5 funding dial → points/day). A **field site** is that exact loop moved onto a discovered location:

> **survey finds a site → you assign scientist(s) → they work it, competence + funding setting rate/quality/risk → it yields (over time, or a one-shot resolve) → it depletes, persists, or blows up.**

Build the loop **once**; every site *type* below is **data** in a broad catalog (the same pattern as the covert-action and exchange catalogs). This is CONNECT, not a parallel system (`CONVENTIONS.md` §6) — it reuses survey, the scientist-assignment + funding machinery, the `Masked` per-faction fog, and the people-loss rung.

### Three scientist-at-a-site shapes (a flag on the catalog entry)

- **Persistent study** (a biosphere, an exotic star) — runs like a lab: yields *over time*, competence = the *rate*, the site *persists* as a standing bonus you hold.
- **One-shot expedition** (a ruin, a derelict) — resolves *once* with a success/quality roll, competence = the *odds*, the site *depletes*.
- **Incident-response** (a solar-flare strike, a mining accident, an outbreak — *reactive*, on a world you already hold) — an event **degrades a world on a clock**; you assign scientist(s) to *resolve* it, competence = how fast/how safely; resolved = the world recovers (maybe with a lesson learned), ignored = it worsens/spreads/is lost. The reactive twin of discovery — same loop, pointed at a problem on your own turf. See the developer's-vision note above.

### Multiple scientists per site — diminishing returns, self-balancing

A site accepts **more than one scientist**, but with **diminishing returns** as you pile them on (each additional scientist contributes a smaller share — a sublinear sum, not a straight multiply). Crucially, **no hard cap is needed**: because good scientists are scarce (the same talent pool as officers), the opportunity cost self-regulates — *if you have 4 scientists on one site, 3 of them could honestly be working 3 other sites or labs.* The game doesn't punish over-stacking with a rule; the math and the scarcity do it for you.

That makes **concentrate-vs-spread** a real, standing decision:
- **Concentrate** a team on one high-value site to finish it fast (accept the diminishing returns because *speed* matters — beat a rival to the ruin, close a dangerous anomaly before it spreads).
- **Spread** your scientists thin across many sites and labs to work the whole frontier at once (accept that each site progresses slowly).

Competence still stacks: a team's effect is the diminishing sum of its members' competence, so "one master + two juniors" vs "three mids" is a real composition choice.

---

## The catalog — PLANETARY sites (six reasons a planet is a prize)

Spread deliberately so each is a *distinct* payoff, not six flavors of "+research." A given world has **one, none, or (rarely) two** — scarcity is the rule (see below).

| Site | Scientist's job | Why the planet is a prize | Risk / hook |
|------|----------------|--------------------------|-------------|
| **Ancient ruins** | excavate (one-shot) | a tech windfall or a unique blueprint you can't research normally | guardians/traps kill the team — Stargate / Mass Effect / Halo |
| **Exotic anomaly** | study | research **+ an event chain** (can seed a story or a late-game crisis) | it can be dangerous and spread — the Expanse protomolecule |
| **Rare/exotic resource** | characterize it before you can mine it | a strategic material found on only a few worlds — economic power | worthless until studied; rivals covet it — Dune's spice |
| **Native biosphere** | xenobiology (persistent) | **Biology & Genetics tech** (an *empty* category today), population/medical/terraform bonuses | hostile life, plague outbreak — Star Trek |
| **Derelict / lost colony** | recover (one-shot) | salvage: materials, a ship design, **survivors** (population, even a stranded leader) | booby-trapped, something still aboard — BSG |
| **Dormant precursor device** | decipher & activate | a working strategic asset — a planetary shield, a superweapon | activation can backfire catastrophically — Halo / Stargate |

## The catalog — SPACE sites (same loop, system-scale prizes)

| Space site | Scientist's job | Why the *system* is a prize | Hook |
|------|----------------|----------------------------|------|
| **Derelict / drifting wreck** | recover | salvage, a ship design, survivors/a stranded leader, intel | Expanse / BSG |
| **Exotic star** (black hole, pulsar, neutron star) | study (persistent) | **Stellar Science tech** (thin today), exotic-physics unlocks | Star Trek / hard SF |
| **Asteroid/belt oddity** (hollow, artificial, exotic composition) | prospect + study | rare materials, or a ruin-in-disguise | Expanse (the Belt) |
| **Deep-space anomaly** (rift, gravimetric anomaly, subspace signal) | study | research, an event chain, **or a hidden route** (see below) | the mystery |
| **Precursor megastructure** (dormant gate, ring fragment, ancient shipyard) | decipher & activate | a working strategic asset — a **network shortcut** or unique build capacity | Halo / Stargate |

## What's special/additional about SPACE (beyond "same loop")

Four things planets structurally can't do:

1. **Transient / drifting finds.** A derelict on a decaying orbit, a comet that only swings by periodically — you have a *window* to reach it. Timing matters (reuses the hazard-transience seam).
2. **System-scale prizes.** A planet's value is its *surface*; a *system's* value is its *space* (a black hole to study, a gate to hold, a belt to strip). Planet-value and system-value **stack**.
3. **Network-reshaping finds — the headline.** Deep-space exploration can **change the map, not just reveal it** — which fixes the "flat random topology" gap and is unique to space. A precursor **gate** = a shortcut. A studied **anomaly** = a *hidden* jump point, a **wormhole** (risky, maybe one-way), or a route you can **stabilize**. Exploring becomes *building your own strategic geography* — the shortcut that flanks a rival, the back door into their core. (`JPFactory.CreateConnection` is a dead stub today — this is where it comes alive.)
4. **Hazards are the treasure map.** The dangerous places that already exist (gravimetric anomalies, gas clouds, ion storms, debris) become *where the good stuff hides* — a derelict in a gas cloud you can only find with counter-interference sensors; a precursor site inside an ion storm that fries the team. Almost pure CONNECT: the hazard system exists, and "gravitational anomaly" is *already* the name of the jump-point survey points, so "some anomalies are hidden jump points" is half-built.

---

## Scarcity — the rule that makes it work

**Not every planet or system has something — most are ordinary.** That is the whole point: scarcity turns a uniform galaxy into one with **hot spots** — a ruin world is a research objective, a spice world an economic chokepoint, a precursor-gate system a strategic must-hold. Rarity creates the map's geography of desire, and *that* is what makes planets and systems "more valuable."

## Earns its weight (the decisions) + Connect (the stack)

- **The decisions:** *where do you send scarce scientists* (concentrate vs spread; which prize to chase); *is this world/system worth the scientist-time, the risk, and defending it*; *is it worth taking from a rival.*
- **The stack (one system lights up six):** **exploration** (something to find) × **research** (fills empty tech categories) × **economy** (rare resources) × **population** (survivors, biospheres) × **military** (activate/hold devices, network shortcuts) × **diplomacy** (rivals covet your relic world). This is the connective payoff.

## Cradle to grave

> **survey** finds a site (per-faction `Masked` fog) → assign **scientist(s)** (a field posting, competence + funding = rate/quality/risk; diminishing returns on team size) → the site **yields** (research / blueprint / resource / population / strategic asset / route) → it **depletes** (one-shot) or **persists** (a standing bonus you hold) → **grave:** a dangerous site can **kill the assigned scientist** (rung 6); a conquered planet's site **transfers to the conqueror** (a reason to invade); bombardment can **destroy** a site (scorched earth); a network find can **collapse** (a destabilized wormhole, a lost gate).

Every rung is reachable and losable — not a parachuted-in "exploration points" abstraction.

## Connections (Prime Directive)

- **Survey / detection** (`GeoSurveyProcessor`, `JPSurveyProcessor`, `Masked`, `KnownSystems`) — finds the sites; the fog is per-faction. **Fix the ruins tautology bug** (`SystemBodyFactory.GenerateRuins` always early-returns) and wire a consumer.
- **Scientists / the leader pipeline** (`docs/AI-SELF-PLAY-DESIGN.md`) — a field site is a scientist's posting; makes competence matter; the Chief Scientist can delegate site assignment to a stance. Reuses the `ResearcherDB` + funding + assignment machinery.
- **Research / tech** — site yields feed tech (and can fill the *empty* Biology & Genetics and Stellar Science categories).
- **Economy / industry** — rare-resource sites; salvage materials; unique build capacity (ancient shipyard).
- **Population** — survivors from derelicts/lost colonies (a colonist source — ties to the colonist-transport gap); biosphere bonuses.
- **Jump network** — network-reshaping finds turn `CreateConnection` (dead stub) into real routes/gates/wormholes.
- **Hazards** — the risk geography; the treasure hides in the dangerous places; ties to the sensor flavors (counter-interference) and transience.
- **Ground combat / bombardment** — sites are on the surface, so they transfer on conquest and can be destroyed by bombardment.
- **People-loss rung (rung 6)** — dangerous sites kill assigned scientists.

## Locked vs. open

**Locked (2026-07-07):**
- **One universal field-site loop** for all exploration content — planetary AND space; a broad data-driven catalog, not a parallel system.
- **All catalog types exist** (six planetary + five space) — phasing is a build-effort question, not a scope one.
- **Three scientist-at-a-site shapes** — persistent study (lab-like, competence = rate), one-shot expedition (competence = odds), and **incident-response** (reactive — a hazard/accident/outbreak degrades a held world on a clock; competence = how fast/safely you resolve it). The developer's "fill Sol for 250 years" bar (2026-07-11) makes **content density a first-class goal** — deep enough that a single home system is a campaign's worth of episodes ("four seasons of Star Trek").
- **Multiple scientists per site with diminishing returns; no hard cap** — the scarcity/opportunity-cost self-regulates ("3 of the 4 could be doing something else"). Concentrate-vs-spread is the decision.
- **Scarcity** — not every planet/system has something; hot spots are the point.
- **Grave rung** — sites transfer on conquest, deplete/persist, can be destroyed, and can kill the team.

**Open (decide when we build):**
- The diminishing-returns curve (how fast the 2nd/3rd/4th scientist falls off) — calibration.
- Per-type yield magnitudes + rarity weights (how often each site type spawns).
- Which yields are one-time vs standing per type.
- How network-reshaping finds interact with the jump-network rework (route persistence, wormhole stability, gate control).
- Event-chain depth for anomalies (how far the story/crisis seed goes) — likely its own later system.

**Build order (after the survey/fog substrate, which exists):**
1. **Fix + wire ruins** (the tautology bug + a consumer) as the first catalog entry — proves the field-site loop end-to-end on the simplest one-shot expedition.
2. **The field-site loop** generalized (assign scientist(s), funding, diminishing-returns team math, yield-over-time vs one-shot).
3. **Broaden the planetary catalog** (rare resource, biosphere — fills empty tech categories).
4. **Incident-response shape** (wire a hazard/accident event → a resolvable field site on a held world; reuses the loop + the existing hazard system) — this is what makes a *home system* full, so it earns early priority once the loop exists.
5. **Space catalog + the four space-special dimensions** (derelicts/stars first; network-reshaping with the jump-network rework).
6. **Event chains / crisis seeds** (anomalies) — last, likely its own system.

---

## Expanded catalog — the VARIETY of "episodes" (mine all of sci-fi, 2026-07-12)

**Developer's ask (2026-07-12): draw on ALL of sci-fi media to widen the catalog — more variety of "episodes."** The density bar ("fill Sol for 250 years") is really a *variety* problem, and variety is **cheap if the AXES are right**. The locked rule holds: the one field-site loop never changes; an "episode" is a **combination** —

> **PAYOFF** (why it's a prize) × **SHAPE** (persistent / one-shot / incident) × **HOOK** (the twist that makes it a story) × **narrative skin** (flavor + which franchise it evokes).

A dozen payoffs × three shapes × a dozen hooks = **hundreds of distinct episodes from one machine** — no new engine per episode, exactly the "catalog is DATA" rule. This section widens the axes; it does **not** add machinery.

### Axis 1 — the PAYOFF (twelve reasons a place is a prize)

Each feeds a system that ALREADY exists (Connect). Grouped so no two are "+research in a hat."

| # | Payoff | The prize (which existing system it feeds) | Media touchstones |
|---|--------|--------------------------------------------|-------------------|
| 1 | **Knowledge windfall** | a lump of research points / an instant level in a category | Prothean beacon (ME), Ancient repository (SG-1), the Monolith (2001) |
| 2 | **Unique blueprint** | a component/ship design you CANNOT research normally (into the designer) | Forerunner tech (Halo), a captured alien hull (Independence Day/BSG) |
| 3 | **Strategic material** | a rare resource on few worlds — an economic chokepoint (economy/industry) | spice (Dune), Element Zero (ME), ZPM (SG-1), dilithium (ST), FTL fuel (dev's Red Spot) |
| 4 | **Population** | colonists / refugees / survivors — the colonist source (population) | lost colony (BSG), cryo-sleepers (Alien/Passengers), a generation ship |
| 5 | **Biology & genetics** | fills the EMPTY Biology tech category; medical/terraform/troop-quality bonuses | xenobiology (ST), Genesis device (ST II), the Shimmer (Annihilation) |
| 6 | **Strategic asset / superweapon** | a working device you hold — planetary shield, planet-killer, orbital gun (military) | Halo ring, Death Star tech, planet-killer (ST "Doomsday Machine"), the Great Machine (B5) |
| 7 | **Network / geography** | a shortcut — a new jump route / gate / wormhole that reshapes the MAP (jump network) | mass relays (ME), the Ring gates (Expanse), the Stargate, hyperspace lanes (SW) |
| 8 | **Intelligence** | a decoded signal, a rival's secret, a star chart (espionage/diplomacy) | Arrival, Contact (the Machine blueprint), a captured databank |
| 9 | **A leader / ally** | a stranded commander, an awakened benevolent AI, a defector (people/commanders) | a marooned hero (BSG), a friendly AI (Halo's Cortana), TARS (Interstellar) |
| 10 | **Hazard removed** | a standing threat on a held world neutralized → the world recovers (incident payoff) | disaster-of-the-week resolved (ST) |
| 11 | **A crisis unleashed** | you awaken/release a threat — the SEED of the late-game crisis (crisis system) | the Flood (Halo), protomolecule (Expanse), the xenomorph (Alien), the Reapers (ME) |
| 12 | **A choice with consequences** | no loot — a decision that swings relations/legitimacy/morale (diplomacy/politics) | the Prime Directive, "who gets the superweapon", uplift-or-don't |

### Axis 2 — the SHAPE (locked: three)

- **Persistent study** — yields over time, competence = the rate, persists as a standing bonus you hold. *(biosphere, exotic star, a living anomaly)*
- **One-shot expedition** — resolves once on a success/quality roll, competence = the odds, then depletes. *(ruin, derelict, beacon)*
- **Incident-response** — a thing happens TO a world you already hold, degrades it on a clock, competence = how fast/safely you resolve it. *(flare, outbreak, containment failure)*

### Axis 3 — the HOOK (the twist that makes it an episode, not a vending machine)

Modifiers on ANY payoff — the same "ancient ruin" becomes six different stories. Most are a **flag + a check**, not a new system.

| Hook | What it adds | Media |
|------|-------------|-------|
| **Guardian / trap** | the site fights back — kills the team unless you bring force or a specific counter | Stargate traps, Halo Sentinels, ME ruins |
| **Timed / transient** | a WINDOW — a comet, a decaying-orbit derelict, an alignment | Rama (Clarke), a passing rogue planet |
| **Contested / race** | a rival works it too — first to finish wins, or you fight over it | everyone wants the spice |
| **Gated by capability** | can't even start without a specific tech / sensor / hazard-suit | corrosive atmosphere (dev's Red Spot), hard radiation, deep pressure |
| **Cursed / backfire** | success can go WRONG — activation misfires, the dig wakes something | Event Horizon, the Monkey's-Paw device |
| **Reactive / sentient** | the site responds to how you treat it — observe vs. exploit changes the outcome | Solaris, a living ship (Farscape), a space-jellyfish (ST) |
| **Chained / breadcrumb** | finishing it reveals the NEXT site — a mystery arc | the Reaper trail (ME), the Ring mystery (Expanse) |
| **Moral fork** | the yield costs something — uplift-or-interfere, sacrifice the inhabitants for the tech | the Prime Directive |

### Signature episodes (a few fully-drawn, developer's-vision style)

- **The buried fleet** *(Mars/Luna · one-shot × unique-blueprint × guardian)* — a dig turns up a *dormant precursor warship*, a hull you could never research; its automated defenses wake as you excavate, so bring a ground force or lose the team. *(Connect: ruins → blueprint into the ship designer; a ground-combat guardian fight.)*
- **The singing star** *(a pulsar · persistent × knowledge × gated)* — only a hardened sensor can study it without frying the lab; it slowly fills the near-empty Stellar Science tree and holding the system is a standing research bonus. *(Connect: exotic-star site → Stellar Science; sensor-flavor gate; system-value.)*
- **Cold Lazarus** *(Europa · incident × crisis-seed × contain-or-spread)* — your mine cracks the ice and something *swims out*; contain it for a xenobiology windfall + a war-beast option, or fail and it spreads to the colony then to ships in orbit — a home-grown rehearsal of the late-game crisis. *(Connect: hazard/incident loop → biology tech OR the crisis system; ground/boarding defense.)*
- **The lighthouse** *(deep space · one-shot × network × chained)* — a decoded alien buoy reveals a *hidden jump point*, a back door into a rival's core and a breadcrumb to the next buoy. *(Connect: revive `JPFactory.CreateConnection`; the jump-network rework.)*
- **First words** *(a pre-warp world or a signal · persistent × choice × intelligence)* — study covertly for intel and a knowledge trickle, or make contact and pay the diplomatic price. The Prime Directive as a *lever*. *(Connect: diplomacy/first-contact; the espionage ledger.)*

### What's cheap CONNECT vs. what needs a small new hook

- **Pure DATA on the existing loop (cheap):** payoffs 1–5, 8, 9 land on systems that already exist (research, economy, population, the designer, people). Most hooks (guardian, timed, contested, gated, cursed, moral-fork) are a **flag + a check**, not a new system.
- **Needs a modest new hook (already in this doc's "open" frontier):** #7 network-reshaping (revive `CreateConnection`), #11 crisis-unleashed (ties to the ascension/crisis seed), and the **reactive/sentient** hook (a small "site reacts to your stance" rule).

**Bottom line:** widening the catalog is an *authoring* job (more DATA rows across these axes), not an *engineering* one. Build the loop + a handful of payoff wires once; then "more episodes" is content, and the four axes make the content combinatorial — which is exactly how one home system fills with four seasons of episodes.
