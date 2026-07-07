# Exploration Content — the Field-Site Loop + Catalog (design)

**Status:** concepts + path forward (design conversation 2026-07-07). No engine code yet. Sits alongside `docs/DETECTION-DESIGN.md` (the survey/fog substrate this rides) and `docs/AI-SELF-PLAY-DESIGN.md` (scientists are leaders on the pipeline this gives a field career).

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

## The spine — a "field site" is a lab on a location

Right now a scientist works a **lab** (`ResearcherDB` + assigned scientist + a 0–5 funding dial → points/day). A **field site** is that exact loop moved onto a discovered location:

> **survey finds a site → you assign scientist(s) → they work it, competence + funding setting rate/quality/risk → it yields (over time, or a one-shot resolve) → it depletes, persists, or blows up.**

Build the loop **once**; every site *type* below is **data** in a broad catalog (the same pattern as the covert-action and exchange catalogs). This is CONNECT, not a parallel system (`CONVENTIONS.md` §6) — it reuses survey, the scientist-assignment + funding machinery, the `Masked` per-faction fog, and the people-loss rung.

### Two scientist-at-a-site shapes (both exist — a flag on the catalog entry)

- **Persistent study** (a biosphere, an exotic star) — runs like a lab: yields *over time*, competence = the *rate*, the site *persists* as a standing bonus you hold.
- **One-shot expedition** (a ruin, a derelict) — resolves *once* with a success/quality roll, competence = the *odds*, the site *depletes*.

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
- **Both scientist-at-a-site shapes** — persistent study (lab-like, competence = rate) and one-shot expedition (competence = odds).
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
4. **Space catalog + the four space-special dimensions** (derelicts/stars first; network-reshaping with the jump-network rework).
5. **Event chains / crisis seeds** (anomalies) — last, likely its own system.
