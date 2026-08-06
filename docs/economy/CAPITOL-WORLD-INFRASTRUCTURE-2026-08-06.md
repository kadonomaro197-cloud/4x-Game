# The Capitol World — build EVERYTHING the designers make, then find what's missing to run it

**As of 2026-08-06.** A whole-world stress test: take one homeworld, stand up **one of every planet-surface
infrastructure buildable the designers can make**, roll up everything it costs to *build* and everything it needs to
*function*, and produce the honest ledger of **what's missing**. Grounded in source (every number cited file:line),
built on the reconciled I/O docs (`docs/assembler/02-IO-MATRIX.md`, `06-OUTPUTS-BY-DOOR.md`, the engine backlog).

Scope, as locked with the developer: **planet surface + its ground layer only** — Civic, Industrial, Power, Sensors,
Command, Defense, Logistical, planetary Weapons, and the ground-unit chassis/parts. Ships, fighters, and orbital
space stations are out (stations are flagged separately at the end). "Everything built" = **one of each design** — the
cleanest way to exercise every consumer exactly once; a real capital would stack many of some (noted where it bites).

---

## 1. Plain English — what this is and why

You asked: *"simulate a Capitol world where everything from the designers is built, then determine what
resources/materials/whatever the builds will need to function."* This is that.

Think of it like commissioning a brand-new naval base and then asking the plant chief two questions:

1. **Can we even build all of it** from what's in the yard? (materials, parts, the templates themselves)
2. **Once it's all standing, will it actually run** — is there power for the pumps, people to stand the watches,
   food for the crew, money to keep the lights on?

The headline, up front, because it's the real finding:

> **The surface economy is a BUILD economy, not yet a RUN economy.** You can *construct* almost everything — the
> materials chain is closed and healthy. But most of the loops that decide whether a built world *keeps running and
> costs you something* — power draw, employment, food consumption, standing upkeep — are **unwired**. The world stands
> up; it doesn't yet breathe. That's the same "built, not connected" pattern the designer audit found, seen from the
> whole-world altitude.

---

## 2. The world — the Earth start baseline

From `GameData/basemod/ScenarioFiles/systems/sol/earth.json`:

| Fact | Value | Why it matters |
|------|-------|----------------|
| Population | **8.2 billion** (`StartingPopulation: 8.2E9`) | the workforce + food-demand denominator |
| Gravity / pressure | **9.8 m/s² · 1.0 atm · hydrosphere** | in-tolerance for every installation → gravity/pressure gates all PASS, so infrastructure/pop-support count at full value |
| Starting stock | **1,000,000 of every mineral + material** (20 goods, `earth.json:71-90`) | the build-material bank the whole construction pass draws on |
| Already built | **15 installation stacks** — infrastructure ×100, mine ×1, refinery ×1, factory ×1, shipyard ×1, warehouse ×10, research-lab ×2, city-hall ×1, spaceport ×1, launch-complex ×1, local-construction ×1, + 3 holds/tanks/sensor (`earth.json:53-69`) | the baseline the "build everything" pass adds onto |

So the world starts as a functioning industrial homeworld and we build the rest of the catalogue on top of it.

---

## 3. The build — one of every surface infrastructure buildable

From the source census (`installations.json` / `storage.json` / `energy.json` / `weapons.json` / `electronics.json` +
the bound `*Atb` classes). **43 distinct surface buildables**, grouped by designer door. ✅ = start-unlockable in a stock
game; 🔒 = template + design exist but are **NOT** stock-unlockable (a wall — see §4).

### Installations (`MountType = PlanetInstallation`)
| Door | Buildable | Stock? | What it provides |
|------|-----------|:---:|------------------|
| **Civic** | Infrastructure | ✅ | pop-support 500 · **infra-capacity 1000** · housing comfort 5 · storage 500 |
| Civic | Space Habitat | ✅ | same, sealed (no gravity/pressure limit) |
| Civic | Research Lab | ✅ | 10 research pts + a Researcher; **costs 10,000 cr/day** |
| Civic | Research Academy | ✅ | graduates 10 scientists / 24 mo |
| Civic | Food Production (agri-complex) | 🔒 | 5000 food/day, quality→morale |
| Civic | Naval Academy | 🔒 | graduates naval officers |
| **Command** | Administrative Complex (City Hall) | ✅ | an admin seat (`AdminSpaceAtb`) |
| Command | Command Berth | ✅ | a seated officer (Role/Grade/Survivability) |
| Command | Intelligence Directorate | ✅ | 2 covert-op capacity |
| **Industrial** | Mine · RoboMiner | ✅ | mines all 15 minerals |
| Industrial | Refinery | ✅ | 500 refining pts/day (minerals→materials) |
| Industrial | Factory | ✅ | 500 each component/installation/ordnance pts/day |
| Industrial | Ship Yard | ✅ | ship-assembly + component pts |
| Industrial | Construction Services · Launch Complex | ✅ | on-site build pts · a launch pad |
| **Power** | Solar Array | ✅ | power output — **but nothing at colony scale consumes it** (§5) |
| **Sensors** | Passive Sensor · Barrage Jammer | ✅ | detection reveal · enemy-detection jam |
| **Defense** | Bunker | ✅ | fortification (LocalFortify 0.25) + capture/bombard target |
| Defense | Sensor-Hardening · Warp-Stabilizer · Drive-Reinforcement · Heat-Radiator · Deflector-Array | ✅ | hazard resist · heat cap · planetary shield |
| Defense | Point-Defense Mount | ✅ | planetary PD intercept |
| **Weapons** | EMaser · Railgun · Siege Railgun · Flak · Pulse-Laser · Ion-Disruptor · Plasma-Repeater | ✅ | planetary weapon emplacements |
| **Logistical** | Warehouse · Fuel Tank · Ammo Magazine · Passenger Cabin · Refrigerated/Containment Hold | ✅ | typed storage |
| Logistical | Spaceport · Logistics Office | ✅ / 🔒 | cargo transfer · import-export slots (office not stock) |
| **Chassis** | Building Foundation | ✅ | a footprint budget (a plot to build on) |
| Chassis | Infantry · Armor · Artillery (prebuilt units) | ✅ | raises a ground unit on completion |

### Ground-unit parts (`MountType = GroundUnit`, assembled into a designed unit)
Chassis frames (Human/Swarm/Vehicle/Walker) · weapons (rifle/autocannon/tank-cannon/plasma/claw) · armour
(composite/ablative/reactive) · augments (power-armour/reflex/shield-gen/ward) · training cadre · sealed systems ·
locomotion · ground radar · ammo magazine · combat-engineer kit · **unit reactor / RTG / steam-turbine** (the *only*
buildable power sources — and they mount on a **unit, not the colony**; see §5). All ✅ except RTG (template-only).

---

## 4. Can we BUILD it all? — the construction-side ledger

**Materials: YES, cleanly.** Every distinct material and mineral any surface buildable consumes to build is **defined
and producible** — a mined mineral, or a material the `refinery` refines from mined minerals (verified: the whole
surface build chain closes, `materials.json` recipes all resolve). Earth stocks 1,000,000 of each, which dwarfs a
one-of-each pass. **No undefined-material landmine touches the surface layer** — the notorious
gallicite/duranium/mercassium hole lives only in *ordnance/ship* data (and was itself fixed 2026-08-02).

But three things block a literal "build **everything**":

| # | Blocker | Detail | Severity |
|---|---------|--------|----------|
| **B1** | **3 templates aren't stock-unlockable** | `food-production`, `naval-academy`, `logistics-office` have complete templates *and* default designs, but their ids are **absent from Earth's `StartingItems`/`ComponentDesigns`** — so a stock New Game literally cannot build them (gotcha #10, the two-unlock-ends rule). Food is the sharp one: **the entire food supply side is un-buildable out of the box.** | **HIGH** — you cannot build the full set without a data unlock |
| **B2** | **No `ammo` material** | projectile/guided weapons "eat ammo," but there's no `ammo` material — the resolver's ammo pool is **mass-based**, not a stocked good. Not a crash; a conceptual gap (you can't stockpile "ammo" as a resource). | LOW |
| **B3** | **`spaceport` id collision** | defined twice — `installations.json:935` (transfer + storage) and `storage.json:108` (transfer only). `default-design-spaceport` binds whichever loads last. Data hygiene, resolve before relying on spaceport behaviour. | LOW |

**Fix for B1 is pure data** (add the three ids to `earth.json` `StartingItems` + `ComponentDesigns`, or gate them behind a
starting tech) — no engine work. It's the one thing standing between "build most of it" and "build all of it."

---

## 5. Will it RUN? — the function-side ledger (the real question)

Once every building is standing, what does the world *demand to keep running*, and does the game supply it? Nine run
inputs, summed across the full build. **This is where the holes are.**

| Run input | Demand of the built world | Supplied? | Verdict |
|-----------|---------------------------|-----------|---------|
| **Infrastructure support** | Σ `(mass_t/1000 + CrewReq)` over every non-infra building | **Σ `InfrastructureCapacityAtb` = 100,000** (100 × infrastructure) | ✅ **WIRED — the one live balance.** See §5.1 |
| **Power** | *the built colony draws ≈ 0* | Solar Array produces power | 🕳 **HOLE — no colony power consumer** |
| **Jobs / employment** | the morale model wants an employment ratio | every building declares **0 jobs** | 🕳 **HOLE — dead wire** (backlog #2) |
| **Food** | 8.2 B people | `PerCapitaFoodDemand = 0` → demand 0 | 🕳 **HOLE — food loop inert** |
| **Crew / workforce** | Σ CrewReq ≈ **46,000** (see §5.1) | workforce is a fraction of 8.2 B → millions | ✅ **surplus** (people-draw is the M3 story, not binding here) |
| **Upkeep / credits** | research labs 10,000 cr/day each; ground units Mass×0.1/mo | billed | ⚠ **PARTIAL — only labs + ground units cost anything**; factories/mines/shipyards run free |
| **Ammo** | ground ammo-weapons + planetary magazines | `GroundMagazineAtb` / `ShipMagazineAtb` | ✅ wired |
| **Population support (life-support)** | 8.2 B | Σ `PopulationSupportAtbDB` (tolerance-gated) | ✅ **doesn't bind on Earth** (habitable); would bind on a hostile world |
| **Mass / build-points** | the construction queue | factory/construction/shipyard points | ✅ wired |

### 5.1 The one balance that's real — the infrastructure grid

This is the only run-input that actually *pushes back*, so it's worth doing exactly. The engine
(`InfrastructureProcessor.cs:17,86-100`) models the colony's "utility grid":

- **Supply** = Σ `InfrastructureCapacityAtb.Capacity` over in-tolerance infra. Earth's 100 `infrastructure` × 1000 =
  **100,000** (a Space Habitat adds another 1000 each).
- **Demand** = Σ over **every other** building of `MassPerUnit/1000 + CrewReq` (1 unit per tonne + 1 per crew).
- **Efficiency** = `min(1.0, supply/demand)`, and **over-demand throttles ALL production and mining** (read live by
  `IndustryTools.cs:115` and `MineResourcesProcessor.cs:64`).

Running the numbers on the full one-of-each build (dominant terms):

| Building | mass_t/1000 | + CrewReq | = demand |
|----------|---:|---:|---:|
| **Factory** | 5,000 | 25,000 | **30,000** |
| Ship Yard | 80 | 10,000 | 10,080 |
| Mine | 50 | 5,000 | 5,050 |
| Refinery | 5 | 500 | 505 |
| City Hall | 100 | 250 | 350 |
| everything else (≈35 buildings) | — | — | ≈ 1,000 |
| **TOTAL demand** | | | **≈ 46,000** |

**Supply 100,000 vs demand ≈ 46,000 → efficiency = 1.0, with ~2× headroom.** The stock world runs the whole catalogue
at full output. **But the grid is the real constraint the moment you scale heavy industry:** each additional Factory adds
**30,000** demand, so **a 4th factory tips demand past the 100 infrastructure's 100,000 supply and throttles the entire
colony.** The rule a Capitol builder must live by: **build ~1 infrastructure per (tonne + crew) of industry you add.**
This is the single genuinely-wired "resource you can run short of" on the surface — and it's a good one.

### 5.2 The four holes (why the world doesn't fully "run")

1. **Power is a non-resource at colony scale.** No colony installation draws generic power (`06`'s C12 finding). The
   only surface power *plant* is the Solar Array — and there's no colony consumer for its output. Worse, the three real
   reactors (`reactor`/`rtg`/`steam-turbine`) mount **on a ground unit, not the colony** (their `MountType` has
   `GroundUnit`, not `PlanetInstallation`). So a fully-built Capitol has **no meaningful power economy** — power only
   becomes a real resource *inside a ground unit* (an energy weapon drawing its reactor). Expectation "everything needs
   power" is **unmet by design today**, not merely short.
2. **Employment is a dead wire.** The morale model *wants* an employment ratio and the reader is live
   (`PopulationProcessor.cs:74` → a ±40 morale band), but **no building declares a job** — nothing writes
   `EmploymentAtbDB.Jobs`, so a fully-industrialised Capitol employing (notionally) millions reports **0 jobs** and the
   employment-morale term sits at 0 forever. **backlog #2** — the cheapest high-impact fix in the whole project.
3. **Food is an inert loop.** `PerCapitaFoodDemand` defaults **0.0** (`ColonySustenanceDB.cs:23`), so 8.2 B people eat
   nothing; food production is decorative. And the food building itself is un-buildable in a stock game (B1). So even if
   you set the coefficient, you'd first have to unlock the complex.
4. **Most installations have no standing cost.** Only research labs (10,000 cr/day) and ground units (Mass×0.1/mo)
   bill upkeep. A Capitol's factories, mines, shipyards, spaceport, and defenses cost **nothing** to keep running —
   there's no "an idle industrial base still drains the treasury" pressure. (Ship upkeep is a separate, out-of-scope
   station system.)

---

## 6. THE MISSING LEDGER — the answer

Everything the fully-built Capitol needs that the game doesn't supply, split into **to build** and **to function**.

### To BUILD (construction-side)
| Missing | What it is | Fix | Cost |
|---------|-----------|-----|------|
| **Food / Naval-Academy / Logistics-Office unlock** | 3 complete templates absent from Earth's start unlocks — can't be built at all | add the 3 ids to `earth.json` `StartingItems` + `ComponentDesigns` (or a starting tech) | **DATA, low** |
| **`ammo` as a material** | no stockable ammunition good; ammo is mass-based | design call: keep mass-based, or define an `ammo` material chain | design |
| **`spaceport` id collision** | defined twice across two files | dedupe the template | data hygiene |
| *(everything else)* | **materials chain is closed and healthy** | — | ✅ nothing missing |

### To FUNCTION (run-side)
| Missing | What it is | Fix | Backlog |
|---------|-----------|-----|---------|
| **Colony power consumption** | nothing on the colony draws power; no colony reactor building; solar output unused | add a generic component power draw + a colony-scale reactor installation (or accept power = unit-only) | (C12 — new) |
| **Employment / jobs** | consumer live, **producer absent** → 0 jobs, morale term dead in every colony | publish each industry building's `CrewReq` as `EmploymentAtbDB.Jobs` (the civic door's intended design) | **#2** |
| **Per-capita food demand** | defaults 0 → population eats free, food loop inert | set `PerCapitaFoodDemand` (flag-gated, baseline against `MoraleTests`) | (new) |
| **Installation upkeep** | factories/mines/etc. cost nothing to run | extend the monthly biller (like ground/station upkeep) to installations | (new) |
| **Infrastructure scaling** | ✅ *this one works* — but the player must build infra in step (~1 per tonne+crew); a 4th factory throttles the colony | (working as designed — document it for the player) | — |

**The through-line:** every "to build" gap is **cheap data**; every "to function" gap is a **missing producer or a
missing draw** — the engine has the *readers* (morale, efficiency, upkeep billers) but not the *writers* that would make
the world cost something to run. That is exactly the designer audit's finding — *the readers are built, the producers
aren't* — now confirmed at whole-world scale.

---

## 7. What this proves

- **You can build a Capitol world today** — the construction economy (mine → refine → build → install) is real and
  closed, and the infrastructure grid gives it one honest constraint that scales sensibly.
- **You cannot yet make that Capitol *cost* you** — power, jobs, food, and upkeep are the four loops that would turn a
  built world into a living one, and three of the four are unwired (the fourth, food, is inert-by-coefficient).
- **The cheapest, highest-impact single fix is employment (backlog #2):** the whole chain is built except the one line
  that declares a job. Wire it and every colony's morale starts moving on how well it's employed — the first of the four
  loops to come alive.

*Out of scope but adjacent: orbital **space stations** are the parallel off-world host (`docs/economy/OFF-WORLD-INFRASTRUCTURE-DESIGN.md`)
— they'd add the same buildings in orbit and carry their own upkeep biller (`StationUpkeepProcessor`), which is why
"installation upkeep" above is a surface-only gap. Companions: `docs/assembler/05-MATERIAL-INPUTS-BY-DOOR.md` (per-door
supply), `docs/assembler/06-OUTPUTS-BY-DOOR.md` (per-door readers), `docs/assembler/ENGINE-WIRING-BACKLOG-2026-08-06.md`
(the engine to-do), `docs/REALISM-VS-GAMEPLAY-AUDIT.md` (the built-not-connected pattern).*
