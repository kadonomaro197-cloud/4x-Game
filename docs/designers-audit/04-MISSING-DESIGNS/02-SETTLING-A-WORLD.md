# Missing design 2 — SETTLING A WORLD (colonists that move and arrive)

> **What it is, in one breath:** you can build a passenger hold and a cryo hold, but **you can't actually
> move people with them.** Nothing turns "colonists carried on a ship" into "colonists added to a colony,"
> and nothing lets a ship *found* a new colony with the people it's carrying. This design makes population a
> thing you can *transport* — the missing half of "take a planet" (you can invade, but you can't settle) and
> the whole of "found a new colony out at the frontier." It answers crack **C3** (passengers never reach
> population) and build **E-build-1**.
>
> **Honest scoping:** the *designer door and dial already exist* — the Logistical designer's carry-mode door
> (Passenger vs Cryo) and the hold-capacity dial. What's missing is the **mechanism** on both ends: a
> colonist cargo good to put *in* the hold, and the orders that load/unload/found. So this design is mostly
> "wire the consumer," at the same rigor as a door — because without it two whole player verbs don't exist.

---

## 1. The input surface (verified in source)

| Piece | State | Evidence |
|-------|-------|----------|
| Passenger / cryo holds | **exist** | `CargoStorageAtb('passenger-storage'/'cryogenic-storage')`; priced per-person by volume — `PassengerPacking.MassPerPerson_kg = 100.0`, cryo packs denser (`CryoPodVolume_m3` < `BerthVolume_m3`, `PassengerPacking.cs:32-46`) |
| A colonist **cargo good** | **MISSING** | no `ICargoable` colonist good exists; the only reader of passenger-storage is `TeamObject.cs:66` (scientist/commander *teams*, not colonists) |
| Population store | **exists** | `ColonyInfoDB.Population`; every write is a non-transport path — `PopulationProcessor` (growth), `GameStageFactory` (init), `DamageProcessor` (casualties), `StationPopulationProcessor` |
| Found-a-colony path | **exists, unsourced** | `ColonyFactory.CreateColony(faction, species, planet, initialPopulation=0)` — takes a founding population, but nothing sources it *from a ship* |
| The transport pattern to mirror | **exists** | troops: `LoadTroopsOrder.cs:62 → GroundTransport.TryLoadUnit → CanLoad → BayCapacity` — the exact load/capacity/arrive chain, on `GroundBayAtb` |

**The gap in one line:** the hold exists, the population store exists, the founding path exists — but there
is **no colonist good to carry, and no order to move a carried colonist into a colony's population.**

---

## 2. What already exists (extend, don't rebuild)

- The **Logistical designer** already has the carry-mode **door** (Passenger cabin awake vs Cryogenic) and
  the hold-capacity **dial** — with a real price difference (awake berths take more volume per person; cryo
  packs dense but, by design, will want power to thaw — ties to design 1).
- The **troop transport chain** is the working template: load from source → capacity gate → travel → arrive.
  Colonist transport is the *same shape* with a different payload and a different destination (a colony's
  population instead of a battlefield).
- `CreateColony` already accepts a founding population.

**So this is ~80% mechanism, and the mechanism already exists once — for troops.** We mirror it.

---

## 3. The design — three orders and one cargo good (no new designer door needed)

The door/dial are done; the missing pieces are all **mechanism**, each mirroring something that already works:

1. **A colonist cargo good** — an `ICargoable` representing *N people* that rides a passenger/cryo hold at
   `MassPerPerson_kg` (and the cryo volume when frozen). This is the payload the hold was built for.
   *Intrinsic test:* the hold's *capacity* (how many people fit) is a Logistical **dial** — settable knowing
   only the hold (already exists). The *good itself* is data (a cargo type), like fuel or ore.

2. **Load-colonists order** — pulls people from a colony's `ColonyInfoDB.Population` into the ship's
   passenger/cryo hold, capacity-gated. Mirrors `LoadTroopsOrder`/`TryLoadUnit`/`CanLoad`.

3. **Unload-colonists order (the C3 fix)** — on arrival at a colony, moves carried colonists out of the hold
   and **adds them to `ColonyInfoDB.Population`**. This is the single missing consumer that makes the whole
   passenger-storage subsystem non-dead. Mirrors the troop-unload / invasion arrive step.

4. **Found-colony-from-ship** — the unload order, when the destination has *no* colony yet, calls
   `CreateColony(..., initialPopulation = carried)` instead of adding to an existing one. This is the
   frontier-settlement verb, built on the founding path that already exists.

**Why no new designer door:** the *decision* the player makes — awake vs cryo, how big a colony hold —
is already expressed by the Logistical door and dial. Adding a parallel "colony ship" component would be the
"don't invent parallel systems" anti-pattern; a colony ship is just a hull with big passenger holds.

---

## 4. The awake-vs-cryo decision (the door that already exists, made real by the consumer)

Once colonists actually arrive, the Logistical carry-mode door finally *means* something — it becomes a real
tradeoff instead of a dead dial:
- **Awake (passenger berth):** more volume per person, no thaw, but the people consume food/life-support en
  route (ties to the food economy) and arrive ready.
- **Cryo (cryogenic pod):** dense (many more per hull), no en-route upkeep, but needs **power to hold the
  freeze** (ties directly to design 1 — a cryo hold is a *standing power draw*; lose power and you lose the
  colonists). That is the decision the consumer creates: pack them dense and gamble on your reactor, or fly
  them awake and pay the life-support.

This is the payoff of designing the consumer: a door that was inert becomes a genuine risk/space tradeoff,
and it *stacks* with the power economy (design 1) and the food economy (A-flip-2).

---

## 5. Cradle to grave

**people** (grown at a colony, `PopulationProcessor`) → **loaded** into a passenger/cryo hold (Logistical
component, designed/researched/built) → **flown** (fuel, range — the propulsion/logistics chain) → **arrive**
→ **unloaded into a colony's population, or found a new colony** (the decision — where do these people go, and
do I settle a fresh world?) → **the ship is destroyed en route** → the colonists are lost, and a cryo hold
that loses power loses them too (the grave rung, shared with design 1). Every rung exists except the arrive
step — which is exactly this design.

---

## 6. Gauge & blast radius

- **Gauge:** load N colonists at colony A, fly to B, unload → B's `ColonyInfoDB.Population` rose by N (and A's
  fell by N); load at A, fly to an *empty* world, unload → a new colony exists with founding population N.
- **Blast radius:** small and additive — the colonist good and the three orders don't touch any existing
  path until a player issues them; `ColonyInfoDB.Population` gains one more writer alongside growth/casualties.
  The one interaction to watch is the awake-cryo upkeep (food en route, power for cryo) — those ride the food
  and power economies (A-flip-2, design 1), so settling stacks on them rather than duplicating them.

---

*Design 2 of 5. Next: the employment model — making the jobs→morale loop coherent.*
