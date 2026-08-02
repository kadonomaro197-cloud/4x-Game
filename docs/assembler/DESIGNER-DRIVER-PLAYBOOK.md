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
| **Megastructure / Death Star** | station (mega tier) — *add a host tier + scale budgets when we build it* | a **superweapon** capability (a new door-derived component — verify its output has a reader before selling it); an internal **economy + crew town** at colony scale; its own defensive fleet-in-a-hull. Expect much of the superweapon to land PENDING — flag it, that's the honest answer. |

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
