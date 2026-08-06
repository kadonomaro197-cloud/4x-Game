# DESIGNER-DRIVER PLAYBOOK — how to turn a one-line brief into a working, honest build

> **What this is, in one sentence.** When the developer hands you a plain-English design brief — *"design a
> capital ship that's a carrier first and a ship-to-ship brawler second"* — this is the standard operating
> procedure for turning it into a **complete, verified, original** build using the 12 door-designers and the
> Entity Assembler (`docs/assembler/entityassembler.html`), at the level of scrutiny the developer signed off on.
>
> **Why it exists.** A brief is a *mission*, not a parts list. Left unstructured, "carrier first" turns into
> guesswork and, worse, into copying a ship you've seen. This playbook makes the process repeatable: interrogate
> the brief into a spec, derive the platform to meet it, size every supporting system until it closes, and back
> every line with engine source. Do that and you get the quality the developer called "the fallout of good
> designers" — every time, cold.
>
> **The mental model: you are the naval architect.** Real warship design doesn't start by bolting on guns. It
> starts with the mission, derives the requirements, then sizes the platform — hull, power, cooling, berthing,
> stores, endurance — until the whole thing *closes* (every requirement met, nothing hand-waved). That is exactly
> this procedure. The guns are the easy part; the ship is the supporting infrastructure that lets the guns fight.

---

## THE LAW — authenticity to limitations (read this first, it governs everything)

**A build "works" only through its LIVE wires.** Every number a component produces must be traced to a real reader
in the engine source (file:line). Three outcomes, and you must mark which one every capability is:

- **It reaches the sim (LIVE)** → the capability is real. Cite the reader.
- **The engine can't read it yet (PENDING)** → the capability is *aspirational*. Show it, mark it amber, and say
  what engine work would close it. **Never present it as working.**
- **It's written but nothing reads it (DEAD)** → don't sell it as a feature; flag it.

**The cardinal sin is a dial that writes to nothing** — the "pretty" disease (`docs/REALISM-VS-GAMEPLAY-AUDIT.md`).
Adding a component that produces an output no processor consumes is not "closing a gap," it's decoration. If the
gap needs an engine feature, say so; do not fake it with a part.

**Derive from the ROLE, never from a named ship.** See the two modes below.

---

## TWO MODES — know which one you're in

| Mode | Trigger | What you produce | Guardrail |
|------|---------|------------------|-----------|
| **SYNTHESIS** *(default)* | a **role** brief ("carrier first, brawler second") | an **original** design derived from the role | it must **NOT** come out as a franchise ship — if it resembles one, that's coincidence, not the method |
| **REPRODUCTION** *(explicit)* | a **named ship** ("build a Venator") | a canon-accurate match, sourced | franchise-litmus rules — see `docs/assembler/VENATOR-BUILD.md` |

If the brief *names* a franchise ship but asks for a *role* ("something like an Imperial carrier"), translate it
to a role spec first (Step 0), then run SYNTHESIS. Reproduction is only when the developer wants *that ship*.

---

## THE PIPELINE — nine steps, in order

### Step 0 — Restate the brief as an OBJECTIVE
Write the objective in one sentence, then the **role stack with sacrifices**. A stack is not a wish-list; each
rung *costs* the one below it.

> *Brief:* "carrier first, brawler second."
> *Objective:* a mobile airbase that can also stand in a capital gunline — **fighters win the battle, the guns buy
> time.** Stack: **① launch & sustain a large fighter wing → ② trade capital blows → ③ survive → ④ get there.**
> *Sacrifices:* deck space and command overhead come out of gun tonnage; it will out-carry and under-gun a pure
> battleship of its mass, and that's correct.

### Step 1 — The self-interrogation (turn the brief into a spec)
Resolve every axis below. **Default each from sensible engineering + the engine's real limits; state the
assumption; only escalate a genuine fork to the developer in prose** (the `AskUserQuestion` tool is broken here —
root `CLAUDE.md`). You are asking *yourself* the supplemental questions the brief left open.

| Axis | The question it answers | How to default it |
|------|-------------------------|-------------------|
| **Role stack** | what wins, what it sacrifices | from the brief's ordering; make the sacrifice explicit |
| **Host & scale** | ship / ground / station / megastructure; size class | the noun in the brief ("capital ship" → large warship host); scale the budget tier to match |
| **Threat model** | what it actually fights (fighters? capital ships? planets? forts?) | infer from role; a carrier fights fighters + capitals, so it needs flak **and** capital guns |
| **Doctrine** | stand-off vs knife-fight; alpha vs sustained; who-shoots-first | from role; a carrier fights at stand-off (its fighters are the reach) → long detection, long-range guns |
| **Endurance intent** | patrol days; self-sufficiency; logistics tail | capital → long deployment → hydroponics closed loop + deep reactor core |
| **Mobility intent** | FTL reach; sublight agility; or fixed | capital ships are FTL-capable, slow sublight (mass); stations are fixed |
| **Crew & command** | flagship? how automated? | capital → flagship (flag command suite) + full crew sustainment |
| **Survivability** | armour / shields / evasion / ECM mix | from doctrine; a slow brawler leans armour+shields+ECM, not evasion |
| **Cost & tech ceiling** | budget, tech tier | default to "not a constraint unless the brief sets one"; note the cost, don't cap it silently |

### Step 2 — Objective → CAPABILITY LIST
Translate the resolved spec into the capabilities the design **must** have, ranked by the role stack. Name each as
a job, not a part.

> ① carry & launch ~N fighters · run the wing (deck sensors, flag command) → ② capital main battery + a
> flak/PD screen + torpedoes → ③ heavy armour + deflector shields + ECM → ④ FTL drive + sublight engines + fuel.

### Step 3 — Capability → COMPONENTS (pick, or derive)
For each capability:
- **Exists in the catalog?** → compose it (choose the count from Step 4's sizing).
- **Needs a NEW component type?** → derive it through the relevant **door's** method: the **intrinsic test**
  (`docs/economy/DESIGNER-NORTH-STAR.md`) — a dial is real only if it writes a sim variable the engine reads; find
  that variable in source first. Then add it to the catalog with its honesty marker.
- **Which door owns it?** → Weapons / Defense / Chassis(=host) / Civic(crew) / Command / Enhancers / Industrial /
  Logistical / Power / Propulsion / Sensors / Aura. The per-door I/O is in `docs/assembler/02-IO-MATRIX.md`.

### Step 4 — Build the SUPPORTING INFRASTRUCTURE that closes (the heart of the job)
Every capability drags supporting systems. **Iterate — add parts until every gate below passes.** This is the
step that turns a fantasy sketch into a ship that can actually fight.

| Gate | The rule | Fix if it fails |
|------|----------|-----------------|
| **Mass / volume** | Σ ≤ host budget | trim, or step up the host scale |
| **Power supply ≥ draw** | reactors ≥ energy-weapon + system draw | add reactors |
| **Heat** | radiators ≥ energy-weapon heat | add radiators, or the guns throttle |
| **Crew berths ≥ crew** | quarters house everyone aboard | add Crew Quarters |
| **Life support ≥ crew** | air/water for the crew | add Life Support Plants |
| **Ammo feed** | magazines present for kinetic/explosive guns | add Magazines / Ammo Bunkers |
| **Ammo endurance** | minutes of sustained fire is *sane* (not 5) | add Ammo Bunkers |
| **Deployment** | reactor fuel core + provisions cover the patrol | Hydroponics (closed food loop) + reactor sizing |
| **Detection ≥ longest weapon** | it can see as far as it shoots | add Long-Range Sensors |
| **Mobility** | accel / Δv / FTL match the doctrine | drives, fuel, warp |
| **Two clocks are distinct** | Deployment (stay on station) ≠ Maneuver fuel (Δv, spent in bursts) | never conflate them |

### Step 5 — The AUTHENTICITY PASS (verify every wire against source)
For every output the design produces, grep the engine (`Pulsar4X/GameEngine/`) and mark it: **LIVE** (file:line of
the reader) · **LIVE-gated** (reader behind a default-off flag) · **host-split** (live on one host, inert on
another) · **READ** (owned by another door) · **EMERGENT** (computed from the whole entity) · **PENDING** (no
reader) · **DEAD** (written, unread). The verified ledger already exists in `docs/assembler/06-OUTPUTS-BY-DOOR.md`
— consult it first; only re-grep for a new component type. **A capability whose output is PENDING/DEAD is flagged,
not sold.**

### Step 6 — CRADLE-TO-GRAVE check
Every capability must be reachable through the whole chain: **mineral → material → component → research → unit →
decision → loss** (root `CLAUDE.md`). Name the resource bill (the tool's Resource Bill panel does this; the supply
is verified in `docs/assembler/05-MATERIAL-INPUTS-BY-DOOR.md`). A capability the player can't research/build/lose
fails cradle-to-grave.

### Step 7 — VERIFY (the gauges — you cannot ship what you haven't seen)
- `node --check` the extracted `<script>` (syntax).
- DOM-stub harness: drive the new preset across all applicable hosts + the steppers → **0 throws**.
- Playwright screenshot **both themes** and *look at it* (layout, overflow, the honesty markers rendering).
- Confirm every Step-4 gate reads green (or is an honestly-flagged PENDING).

### Step 8 — DELIVER
1. **A build doc** — `docs/assembler/<NAME>-BUILD.md`, same shape as `VENATOR-BUILD.md`: the brief, the resolved
   axes, the capability→component→count mapping, the computed readouts, the **honesty ledger** (LIVE/PENDING/DEAD),
   and the **deferred backlog** (what needs engine work, with the grep that proves it).
2. **A live preset** in `entityassembler.html` so the developer can click it (default the artifact to open on the
   newest build, active-preset highlighted).
3. **Upkeep in the same commit** — flip the row in `docs/DOCS-INDEX.md`; add the doc to the root `CLAUDE.md`
   reference table if it's a keystone.
4. **Commit + push**, then republish the artifact (same URL).

*(A separate showcase artifact is only for a capstone like the Death Star; ordinary builds ride the shared tool.)*

---

## THE MIN/MAX PASS — squeeze maximum performance from the residual budget

*A build that closes (Step 4) has leftover budget. This is the optional last pass: pour that residual into the most
performance you can buy, without breaking a single gate. It is an **add-to-the-leftover** pass — you keep the build's
identity and fill the slack, you do NOT gut it. Worked live on the Venator / Acclamator / Sovereign, 2026-08-03.*

**The seven steps:**

1. **Read the residual.** For every budget — mass AND volume — compute `cap − used`. (Venator: 1,042 t / 2,794 m³ left.
   Acclamator: 2,875 t / 418 m³ left. Sovereign: 3,851 t / 4,674 m³ left.)

2. **Find the BINDING constraint** — the budget with the highest % used. That is the scarce resource, and you optimize
   **per unit of it**. The other budget's slack is **stranded**: you literally cannot spend it. *This is the whole game.*
   The Acclamator had **1,751 t of mass it could not use** because volume ran out first — a transport is volume-bound,
   so its mass headroom is a mirage. Name the binding constraint before you add anything.

3. **Name the metric.** What is this ship FOR? Firepower for a gunship, shield pool for a shield-tank, ground capacity
   for a transport. That single number is what you maximize — everything else is support.

4. **Rank fillers by metric-per-binding-unit, IN-FAMILY.** For each component the ship's role allows, compute
   *(metric gained) ÷ (binding-constraint cost)*. When mass binds, that's metric **per ton**; when volume binds, metric
   **per m³**. Pick the top of the list — but stay in the ship's weapon family so you don't erase its identity
   (a phaser cruiser gets more phasers, not turbolasers). *In this catalog the **heavy turbolaser is the firepower-density
   king** — the most MJ/s per ton AND per m³ (0.024 MJ/s·t⁻¹, 0.067 MJ/s·m⁻³) — which is why it's the default firepower
   filler whenever a turbolaser ship has room; a **shield generator** is the survivability-density pick.*

5. **Add greedily — but pay every FORCED cost in the same breath.** A weapon is never just its own mass. It **draws
   power, makes heat, and needs a crew berth + life-support**. After each addition re-check EVERY gate; when a weapon is
   about to trip one, add the cheapest fix alongside it — a **Radiator** for heat, **Crew Quarters** for berths, a
   **Reactor** for power — and that fix spends the binding budget too, so it is part of the weapon's *true* cost. (The
   Venator's real price for 2 heavy turbolasers was 2 turbolasers **+ 2 radiators + a berth** — budget the whole train.)

6. **Stop at the wall.** When the binding constraint is within a hair of its cap (Venator 19,986/20,000 t; Acclamator
   13,998/14,000 m³), or the next add trips a gate you can't cheaply fix, you're done.

7. **Verify — and heat margin ≥ 0 is part of "max."** All requirement gates green, warp battery ≥ jump cost, AND the
   **heat margin non-negative** — a ship whose guns out-heat its radiators is *throttling*, which is not maximum
   performance no matter how many guns it mounts. Then screenshot + 0 throws.

**What the pass bought (residual → firepower), each hitting a different wall:**

| Ship | Binding wall | Residual poured in | Firepower | The lesson it teaches |
|---|---|---|---|---|
| **Venator** | MASS 99.9% | +2 heavy turbolasers +2 radiators +1 berth +armour | 120 → **143 MJ/s** | mass-bound; 2,272 m³ volume left stranded |
| **Acclamator** | VOLUME 99.99% | +3 heavy turbolasers +1 radiator (densest firepower/m³) | 61 → **95 MJ/s** | volume-bound; **1,751 t mass stranded** |
| **Sovereign** | MASS 98% | +6 phaser banks +10 shields +reactor/radiators/berths | 59 → **84 MJ/s**, shields **70 → 120 MJ** | in-family: a phaser/shield ship, scaled up — not re-armed |

**The four durable lessons:** ① the **binding constraint decides everything** — optimize the scarce budget, ignore the
roomy one; ② **weapons are never free** — firepower drags power + heat + crew, budget the whole train; ③ **density is
king when a budget binds** — max metric-per-ton if mass-bound, per-m³ if volume-bound; ④ **min/max ≠ erase identity** —
fill with more of what the ship already IS.

---

## DOCUMENTED GAPS — the assembler mounts it, but no door builds it yet (deferred, not blocking)

*Surfaced 2026-08-03 by the cradle-to-grave check — every mounted component must be designable in a door. Three fail
that test today. Left DOCUMENTED, not built, so they aren't lost when work resumes.*

- **Tractor beam → belongs in an ENHANCERS / utility dial.** Mounted on the Venator (×6) and Death Star (×768), but
  **zero doors produce it** (it deals no damage, so it's not a weapon; there is no utility door for it to live in). Its
  home is an Enhancers/utility dial — grip-and-drag. Its battlefield payoff (docking / salvage / capture) is *also*
  engine-PENDING, so it's a gap at both the design and the engine layer.
- **Superlaser → a megastructure superweapon BEYOND the weapons door.** The weapons door tops out at a "Spinal Lance"
  (8 MJ/s, ship-to-ship). A planet-cracker is a category the door doesn't model, and the engine has no
  planetary-destruction reader either — PENDING at both layers. Death Star only.
- **Capital-beam + missile RANGES exceed the weapons-door range ceilings.** The door caps beam range at 60 km and
  guided at 100 km, but the assembler mounts Medium Turbolaser 140, Ion Cannon 160, Heavy Turbolaser 220, Missile
  400 km. Same shape as the (already-fixed) damage ceiling. Fix when picked up: raise the door beam ceiling to ~250 km
  and guided to ~450 km — closes the gap AND is more physically honest (space beams/missiles reach far), keeping
  laser ≥ plasma (correct vacuum physics — a plasma bolt disperses faster than a laser diffracts).

*A weapon-NAMING note (not a gap): the assembler uses franchise labels (Turbolaser, Laser Cannon); the weapons door
deliberately uses generic ordnance names, so a beam-energy weapon it designs is a Laser / Beam Projector / **Lance** —
same weapon, franchise skin. The Sovereign's "turbolasers" are the door's Lances.*

---

## THE HONESTY VOCABULARY (mark every output as exactly one)
**LIVE** · **LIVE-gated** (default-off flag, client-on) · **host-split** (live on ground, inert on ship, or vice
versa) · **READ** (this door only displays it; another owns it) · **EMERGENT** (computed from the finished entity,
e.g. Evasion) · **PENDING** (no engine reader yet) · **DEAD** (written, zero readers). Sources of truth:
`06-OUTPUTS-BY-DOOR.md` (per-door reader ledger) and `AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md` §6.1 (the totals
the resolver reads = the Assembler's output contract).

---

## THE SCALING LADDER — corvette → capital → station → megastructure (→ Death Star)
The method is scale-invariant; only these change:

| Rung | Host | What's new vs the rung below |
|------|------|------------------------------|
| Escort / corvette | ship | small budget; often provisions-limited (no room for hydroponics) |
| Capital ship | ship (capital tier) | flagship command, full crew sustainment, closed food loop, capital reactors |
| Station | station | **immobile** (Evasion = 0); immense structure; can host industry + a population town |
| **Megastructure / Death Star** | station (mega tier) — *Battle Station host tier + scaled budgets, **BUILT** 2026-08-02 (`deathstar` preset)* | a **superweapon** capability (a new door-derived component — verify its output has a reader before selling it); an internal **economy + crew town** at colony scale; its own defensive fleet-in-a-hull. Expect much of the superweapon to land PENDING — flag it, that's the honest answer. |

When a rung needs a capacity the host can't express (a megastructure's superweapon, a planet-cracker), that's a
**Step 3 derive** — and if the engine has no reader, a **Step 5 PENDING**, not a fake.

---

## ANTI-PATTERNS — if the build does any of these, it's wrong
- **Reproducing a franchise ship in SYNTHESIS mode.** "Carrier first" must not converge on a Venator.
- **A dial that writes to nothing** (the "pretty" disease). If it needs engine work, flag it; don't add a part.
- **Selling a PENDING capability as working.** The carrier that can't launch its fighters is honest amber, not green.
- **Skipping the supporting-infrastructure sizing.** A ship that can't see / feed / power / cool / berth itself is
  not a build — it's a sketch.
- **Hand-waved counts.** Every count is derived from the role + the Step-4 gates, not vibes ("~52" is not a number).
- **Shipping without the gauges.** No `node --check`, no screenshot → not delivered.
- **Conflating the two endurance clocks.** Deployment (reactor+stores) is not maneuver fuel (Δv).

---

## WORKED SKETCH — "carrier first, brawler second" (the shape, not the full build)
0. **Objective:** mobile airbase that can stand in a gunline; stack ①wing ②guns ③survive ④move.
1. **Axes:** capital ship host · fights fighters+capitals · stand-off doctrine · long deployment · FTL+slow
   sublight · flagship · armour+shield+ECM survivability.
2. **Capabilities:** big fighter wing + deck command → capital main battery + flak screen + torpedoes → armour +
   shields + ECM → FTL + sublight + fuel.
3. **Components:** Fighter Bays (many — the role leader) · Flag Command Suite + Long-Range + Fire-Control Sensors ·
   a *modest* main battery (Heavy + Medium Turbolasers — fewer than a battleship, because ①>②) · a heavy PD screen
   (fighters are the threat) · Torpedoes · Armour + Shields + ECM · Capital Reactors + Radiators + Capacitors ·
   Crew Quarters + Life Support + Medical + Hydroponics · Ion Drives + Fuel + Warp.
4. **Close the gates:** size reactors to the guns+deck, radiators to the energy heat, berths+life-support to the
   (large, wing-inflated) crew, deep ammo, detection past the torpedoes, a closed food loop for a long cruise.
5. **Honesty:** guns/armour/shields/ECM/detection/sensors/crew-gates = LIVE; **the fighter wing's *launch* =
   PENDING** (the one output that makes it a carrier has no reader — `ParasiteLauncherReady`, 0 emitters). So the
   headline is honest: *this hull is a superb carrier by design and a competent brawler, and the carrier half
   can't yet sortie in the sim — an engine job, flagged.*
   → The result carries far more fighters and fewer guns than a Venator, at a different size and shape. **Not a Venator.**

---

## CONNECTED DOCS (the design layer this playbook drives)
- `docs/economy/DESIGNER-NORTH-STAR.md` — the intrinsic test (deriving a real dial). **Read before deriving any new component.**
- `docs/assembler/02-IO-MATRIX.md` — every sim-reaching variable per door (the wiring).
- `docs/assembler/06-OUTPUTS-BY-DOOR.md` + `05-MATERIAL-INPUTS-BY-DOOR.md` — the verified reader/supply ledgers (what's LIVE).
- `docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md` §6.1 — the totals the resolver reads (the output contract).
- `docs/assembler/VENATOR-BUILD.md` — the worked **reproduction** example + the per-door capital-ship audit.
- `docs/assembler/entityassembler.html` — the tool the build lives in.
- Root `CLAUDE.md` — the Prime Directive (map connections), Cradle-to-Grave, One-Verb-Both-Seats, the Visibility Gate.

*Playbook written 2026-08-02. Default mode is SYNTHESIS; the standard is authenticity to limitations.*
