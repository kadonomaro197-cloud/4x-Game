# The Forces Window — a unified Order of Battle (Force Management, evolved)

**Status:** DESIGN + interactive prototype (`docs/combat/forceswindow.html`). Nothing wired into the engine yet.
**As of:** 2026-08-11.
**Grounded by:** a 6-agent read of the real client + engine (each claim below cited at `file:line`), then a synthesis pass.
**Prototype (clickable):** `docs/combat/forceswindow.html` — the window as it would look and behave, with every column/cell/order graded.

---

## 1. What this is, in one breath

You asked for the window that **lists, details, and controls every unit you own — military and civilian, in space and on the ground.** That window already has a name and a home: it's the **Force Management** window (the C# class is still called `FleetWindow`, kept that name on purpose so old saves don't break — `FleetWindow.cs:186`). Today it has two tabs — **Fleets** (your ships) and **Battalions** (your ground formations) — and they already share one way of giving orders.

So this is not a build-from-scratch. **About 70% of what you want is already sitting there.** The job is to grow those two separate tabs into **one order of battle**: a single list where a warship, a freighter, a tank battalion, and a colony are each *one row*, you can filter that list by "show me only my military / only my civilian / only space / only ground," click any row to read everything about it, and give it an order — all from the same place.

Think of it like the **status board in main control** on a ship: every pump, every valve, every tank on one board, color-coded by whether it's running, lined up, or tagged out. You don't walk to each space to check — the board brings them all to you. That's what this window becomes for your whole empire's forces.

---

## 2. The one hard truth: the engine can't tell a warship from a freighter

This is the single most important thing to understand before building, and it's the reason the work is an **engine** job before it's a **window** job.

**There is no "is this military?" flag that works.** The code *has* a field for it — `ShipInfoDB.IsMilitary` (`ShipInfoDB.cs:38`), plus siblings `Tanker`, `Collier`, `SupplyShip`, `Conscript` (`:21-29`). They're declared, and they're copied when a ship is cloned (`:81`) — but they are **never set and never read anywhere in the entire program.** Dead wiring. Ship *designs* carry no role or category either. Fleets carry no classification. On the ground, unit types are Infantry / Armour / Artillery only — all of them combat — and the design is **deliberately locked** against ever adding "civilian" unit *types* (`GroundCombat/CLAUDE.md:38-54`): a unit's job must come from what it carries, not a label stuck on it.

**So the window has to figure out military-vs-civilian for itself, by looking at what's bolted to the hull:**

- A ship with a **weapon** (combat firepower > 0) is a **Warship**. Strip its weapons in a fight and it stops being one — the classification is *live*, not a stored tag.
- No weapon, but a **logistics module** → a **Freighter**. A **survey sensor** → a **Survey** ship. **Cargo-transfer gear** → a **Tender** (refuels the fleet). A **troop bay** → a **Transport**.
- A ground unit with **attack ≤ 0** is **Support** (engineers, HQ) — the only "civilian on the ground" signal the engine has, and even that is derived, not a type.

**And here is the rule that decides *where* this logic lives:** the studio law is **one verb, both seats** — *if the AI can't do it the same way you can, it's built wrong.* If the window computes "warship" one way and the faction AI guesses it another way, you've built two truths and they'll drift. So the classifier must be **one small, tested helper in the engine** — call it `ShipRoleTools.ClassifyRole(ship)` — that **both the window and the AI call.** The ground side already has exactly this (`GroundRoleComposer.ClassifyRole`, `GroundRoleComposer.cs:45`); it's just never shown to the player. Build the ship one, surface the ground one, and both seats read the same answer.

> **The verdict:** do **not** revive the dead `IsMilitary` flag as the source of truth. Derive the class from components, engine-side, tested, shared with the AI.

---

## 3. What already exists (the 70%) — reuse, don't rebuild

| Piece | Where | What it gives the Forces window |
|---|---|---|
| The **host window** + two-tab bar | `FleetWindow.cs:186`, `:188` / `:1149` | Title "Force Management"; Fleets + Battalions are already sibling tabs. It's L14-safe (`End()` after the `if` block, `:243`). |
| The **space roster source** | `FleetWindow.cs:128`, `:1930` | Your faction's fleet tree — fleets *and* loose ships — already walked recursively. |
| The **cross-body Battalions table** | `FleetWindow.cs:1149-1269` | Every ground formation across every world, 8 columns, filters, and a full order surface — already built. |
| A **built cross-body helper** | `GroundForcesDB.cs:1147` (`AllFormationsFor`) | Its own doc comment names the Force Management window. Just point the tab at it. |
| **One order path for everything** | `OrderableDB.cs:9` → `StandAloneOrderHandler.cs:18` | Build a command → `HandleOrder` → validated → lands in the unit's order queue. Ships and ground both ride it. |
| The **ownership key** | `Entity.cs:35` (`FactionOwnerID`) | Every entity knows whose it is — the filter for "my units." |
| Reusable **detail panels** | `FleetWindow.cs:971` (combat sheet) · `ComponentInstancesDBDisplay.cs:13` (components) · `PlanetViewWindow.cs:1297` (formation panel) | The right-hand inspector is mostly assembling panels that already render. |
| The **ground role classifier** | `GroundRoleComposer.cs:45` | The "is this a support unit?" answer already exists — the AI reads it; the player never sees it. |

---

## 4. The unified design

### 4.1 Shape
Keep `FleetWindow` as the host (same class name, same title, same safe window wrapper). Evolve the tab bar into **a small set of views over one shared roster:**

- **All Forces** *(new — the front door)* — one flat, sortable, filterable table; every owned unit is one row; one selection drives one detail panel on the right.
- **Fleets** *(keep)* — the existing recursive fleet tree + per-fleet Summary / Combat / Issue-Orders / Standing-Orders sub-tabs.
- **Battalions** *(keep, one-line change)* — the existing ground table; switch its gather to the built `AllFormationsFor` helper.
- **Logistics** *(new)* — your civilian ships (freighters, tenders, transports, survey), with manifests/routes.
- **Holdings** *(new, scope-gated)* — colonies + stations as owned-asset rows.

The tabs are **filter presets over the same roster** — that's the whole design in one move: one list, many lenses.

### 4.2 The roster (the "All Forces" table)
One row per unit. Columns, each graded by whether the code does it today:

| Column | Grade | Source |
|---|---|---|
| **Unit** (name + group/fleet) | LIVE | `Design.Name` / formation name |
| **Domain** (Space / Ground / Holding) | LIVE | which tree it came from |
| **Kind** (Fleet / Ship / Formation / Unit / Station / Colony) | LIVE | entity shape |
| **Class** (Warship / Freighter / Survey / Tender / Transport / Infantry / Armor / Artillery / Support …) | **BUILD** | the new `ClassifyRole` helper (ground: DATA — exists, unsurfaced) |
| **Mil / Civ** | **BUILD** | derived from Class — no stored field |
| **Location** (system·body / world·region) | LIVE | `PositionDB` / `LeaderRegion` |
| **Strength** | LIVE | `ShipCombatValueDB.Firepower` / `FormationStrength` |
| **Health** | **BUILD** space / LIVE ground | ships need a new aggregate accessor; formations already compute it |
| **Order / Status** | LIVE | `OrderableDB.ActionList` / `GroundOrder.Describe` / `LogiShipperDB.StateString` |
| **Commander** | DATA | `ShipInfoDB.CommanderID` exists, read-only; no assign UI |

**Filters:** Domain (Space/Ground/Holdings) · Role (Military/Civilian) · Class · Location · search. Ground **formations** are rows; expand one to see its **individual units** as child rows (the per-unit render is the one genuinely-new piece here — the data's all on the unit already).

### 4.3 The detail panel (swaps by unit kind)
- **Warship** → combat sheet (firepower, toughness, evasion, beam reach) + installed-components table. *(reuses `FleetWindow.cs:971` + `ComponentInstancesDBDisplay.cs:13`)*
- **Civilian ship** → manifest / route / trade-space / survey progress. *(promotes `LogisticsWindow.cs:74-173`)*
- **Ground formation** → strength / health / stance / ROE / member units + order queue. *(reuses the Battalions surface + `PlanetViewWindow.cs:1297`)*
- **Ground unit** → its own stats (type, health, attack, defense, range, speed, ammo, veterancy, location). *(data on the unit; render is new)*
- **Station / colony** → host body + installed ability categories, with a **live-owner cross-check first** (see §6 grave rung).

Every panel shows the **"why this class?"** derivation up top — the teaching moment that makes the derived classification legible instead of magic.

### 4.4 Orders & control
Everything routes through the **one shared order path** (`OrderableDB` → `HandleOrder`). Reuse the existing Issue-Orders surface for ships (Move / Refuel / Geo-Survey / Grav-Survey / Jump / Embark-Land, each capability-gated — `FleetWindow.cs:357-399`) and the ground order calls for formations (March / Stance / ROE / Raze / Capture / Embark — `GroundForcesDB.cs:755-1069`). Keep the mid-battle direct-call levers (doctrine / EMCON / engagement) — they deliberately bypass the queue so they work during a fight. **One new order worth adding:** assign a commander to a fleet flagship or a formation (the data + the order pattern exist — `CommanderDB.cs:29`, `AssignAdministratorOrder.cs:65` — no UI does it yet).

---

## 5. The classification rules (what the engine helper computes)

**Ship** (`ShipRoleTools.ClassifyRole`, proposed — reads components, never a flag):
1. firepower > 0 → **Warship** · *Military*
2. survey sensor → **Survey** · *Civilian*
3. logistics module → **Freighter** · *Civilian*
4. troop bay → **Transport** · *Civilian hull, military cargo* ⚠ *(open edge — see §7)*
5. cargo-transfer, unarmed → **Tender** · *Civilian*
6. cargo hold, unarmed → **Hauler** · *Civilian*
7. else → **Utility** · *Civilian*

**Ground** (`GroundRoleComposer.ClassifyRole`, exists — `GroundRoleComposer.cs:45`):
- attack ≤ 0 → **Support** · *non-combat*
- else → the unit's type (Infantry / Armor / Artillery) · *Military*

---

## 6. Cradle to grave

The window is a **readout + control surface**, so its chain is the chain of the units it lists, plus the one capability it adds (classification):

> mineral → material → production → **component** (weapon / cargo bay / survey sensor / troop bay / logi module, designed + research-gated) → installed on a **unit** → the unit's **class emerges from those components** (this is *why* the classifier must read components, not a flag) → the unit is a **row** in the roster, filterable by that class → the player issues the **decision** (order / stance / doctrine / assign-commander) through the row → when the unit is **damaged/destroyed**, the component-level loss changes its class live (a warship that loses all weapons stops reading as a warship) and **capture flips its owner** so it leaves your roster and joins the enemy's.

The classifier itself has a cradle: a small pure engine helper both the window and the AI read — satisfying one-verb-both-seats.

> **⚠ The one grave-rung gap to honor:** a colony/station registry (`FactionInfoDB.Colonies/.Stations`) is added-to on creation but **not cleared on capture** (`SYSTEM-CONNECTION-MAP.md:155`). Until that's fixed engine-side, the window must **verify each asset's live `FactionOwnerID` before showing it**, or you'll see worlds you've lost still listed as yours. Cheap window-side check now; deeper engine fix later.

---

## 7. Open decisions (defaults picked for the prototype — say the word to change)

1. **Scope** — *default: true order of battle* (holdings/colonies/stations included, not just mobile forces). The alternative is mobile-forces-only.
2. **Ground granularity** — *default: formations are rows, individual units expand underneath.*
3. **Classification home** — *default: a derived, tested engine helper* (`ShipRoleTools.ClassifyRole`), **not** the dead `IsMilitary` flag. (Unanimous survey recommendation.)
4. **Health/Fuel gauges** — *default: build the missing aggregate ship accessors* (they don't exist — a gauge-before-UI job) so those columns show real numbers.
5. **Capture-stale registries** — *default: cheap window-side live-owner check now*, engine removal-on-capture later.
6. **Commander assignment** — *default: include it* (data + order pattern exist; no UI does it today).
7. **Troop transport** — *is an unarmed troopship "military"?* The prototype tags the **hull** Civilian and flags "carries military units." Your call.

---

## 8. Build order — cheapest reuse first

| Slice | What | Grade |
|---|---|---|
| **S1** | Point the Battalions tab at the built `AllFormationsFor`; scope to `PlayerFaction`. Pure reuse. | DATA |
| **S2** | The engine classifier `ShipRoleTools.ClassifyRole` (+ test), reusing the emergent predicates that already exist; surface the ground one. **Read by window *and* AI.** | BUILD |
| **S3** | Make the two existing table rows reusable (extract the ship combat row; confirm the battalion row is callable). | DATA |
| **S4** | One "selected unit" abstraction unifying the fleet + battalion selection. The load-bearing refactor. | BUILD |
| **S5** | The new **All Forces** flat roster tab — one table over S2/S3/S4, with the Domain/Mil-Civ/Class/Location filters and a kind-swapping detail panel. | BUILD |
| **S6** | Per-individual-ground-unit rows + the engine sibling `AllUnitsFor` (includes unformed units). Closes "list *every* unit." | BUILD |
| **S7** | Civilian-ship detail panel — promote the logistics manifest/routes/dV into the roster. | DATA |
| **S8** | Aggregate Health + Fuel accessors (the missing gauges) so those columns are real. | BUILD |
| **S9** | Stations + colonies as rows (live-owner cross-check) + assign-commander UI. Scope-gated, last. | BUILD |

Each slice is one CI-gated step. S2 is the keystone — it's the classification both the window and the AI depend on.

---

## 9. Connections this window adds (for `SYSTEM-CONNECTION-MAP.md`)

- **Force Management → `ShipCombatValueDB`** — reads Firepower > 0 as the warship/military key (already the proxy at `FleetWindow.cs:1077`; formalize it).
- **Force Management → new `ShipRoleTools.ClassifyRole`** — the Class column, shared with the AI.
- **Force Management → `GroundFormationTools.AllFormationsFor`** (`GroundForcesDB.cs:1147`) — the cross-body ground source; add sibling `AllUnitsFor`.
- **Force Management → `GroundRoleComposer.ClassifyRole`** — new consumer (today only maneuver AI reads it).
- **Force Management → `LogiShipperDB` / `LogiBaseDB`** — civilian-ship status + manifest.
- **Force Management → `FactionInfoDB.Colonies/.Stations`** — holdings rows, with a live-`FactionOwnerID` cross-check (capture-stale).
- **Force Management → GlobalManager iteration** (L5) — any "all owned" sweep must include the GlobalManager, where the root fleet + faction entity live.
- **Force Management → `CommanderDB.AssignedTo`** — new write-connection if assign-commander is added.

---

*Prototype: `docs/combat/forceswindow.html`. Seed window: `FleetWindow.cs` ("Force Management"). This doc records the design; the engine is untouched.*
