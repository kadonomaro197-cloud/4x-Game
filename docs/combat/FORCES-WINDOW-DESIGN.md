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

### 4.5 How the window knows which orders apply — the component scan
*(Answers the developer's question: "how will this know what orders apply to a fleet or battalion — could it scan the components?")* **Yes — that's exactly the mechanism, and the engine already does it for a handful of orders.** An order isn't a free-floating verb; it's powered by a **component**. So the window offers an order only when the unit actually carries the part. There are **three gates, cheapest first:**

1. **Kind gate (structural).** A colony can't "jump through a gate"; a battalion can't "warp to orbit." Some orders only make sense for a ship / fleet / formation / colony. Free, and it's how the prototype already filters.
2. **Component-scan gate (the one you asked about).** Walk the unit's installed parts (`ComponentInstancesDB`) once, collect the set of *abilities* they grant, and show a part-dependent order only if its ability is in that set. **This is already built** — `EntityExtensions.HasGeoSurveyAbility` (`:199`) is literally `entity.HasDataBlob<GeoSurveyAbilityDB>()`, and the Issue-Orders menu hides Geo-Survey / Grav-Survey / Embark-Troops on exactly this basis (`FleetWindow.cs:375-392`). Installing a survey sensor is what attaches `GeoSurveyAbilityDB`; the order appears with the part and vanishes when the part is destroyed. **Generalize the 3 hardcoded `Has*Ability` checks into a table:** order → required ability, and one `AbilitiesOf(entity)` that returns the set. Same pattern the classifier uses (warship = has-weapon) — read components, never a stored flag.
3. **Context / state gate (situational).** Even with the part, is there something to act on *now* — a jump gate in range, a colony to refuel at, a valid target? The Issue-Orders target list already does this per order type.

**A fleet or a battalion has no components of its own — it's a container.** So its order set is the **union of its members' abilities plus its own aggregate orders** (doctrine, formation-move). "Can this fleet survey?" = "does any ship aboard carry a survey sensor?" — which is why `HasGeoSurveyAbility` **recurses through `FleetDB.Children`** (`:203-209`). A battalion is the same one level down: its orders are its units' component-derived orders (a bombardment order needs a unit with an artillery piece — the ability rides the component, ground ruling #2) plus formation-level orders (stance, ROE, march).

**Why this is the right design, not just convenient:** it's cradle-to-grave and one-verb-both-seats in one move. The player never sees an order they can't execute; research → build → install a jump drive **unlocks** the Jump order; losing it in battle **removes** it; and because the AI reads the identical ability set, both seats drive the same verbs. The prototype demonstrates it live: select **TNS Meridian** (jump drive + weapons, no cargo hold) and Jump + fire-control are offered while Transfer-Cargo is hidden; select **TNS Warden** (weapons, no jump drive) and Jump drops out — click "⊘ N need a component this unit lacks" to see it greyed as *needs jump drive*; a freighter with a cargo hold gets Transfer-Cargo back.

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
| **S2** | ✅ **ENGINE HELPER BUILT (2026-08-13, OPERATION BLUEPRINT-TO-STEEL A3).** `ShipRoleTools.ClassifyRole` (`GameEngine/Ships/ShipRoleTools.cs`) + `ShipRoleToolsTests` — the `ShipRole` enum (Warship/Survey/Freighter/Transport/Tender/Hauler/Utility) derived from mounted components, with `ClassifyRole(design)` (AI) + `ClassifyRole(entity)` (window) + `IsWarship` + `IsMilitary`. **`ConquerResolver.IsWarship` + `DefendResolver.IsWarship` now DELEGATE to it** (one verb, both seats — byte-identical). The ground one (`GroundRoleComposer.ClassifyRole`) already existed. *Still window-side: surfacing the Class column (S5) — the forceswindow.html Class badge stays BUILD until then.* | ✅ engine · window pending |
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

---

## 10. The order catalog — 123 orders, graded

Grounded by a 4-agent survey of the engine's real order/command classes (each cited). **71 LIVE** (an order exists and there's a way to issue it — half are only reachable from *other* windows today, so a deep menu is mostly *routing* into one place), **23 DATA** (the capability/engine method exists, only a button is missing — the cheapest depth wins), **29 BUILD** (genuinely new). Two order systems sit under one menu: space commands queue on `OrderableDB.ActionList` → `HandleOrder`; ground orders queue on `GroundFormation.Orders`. The mid-battle levers (doctrine/EMCON/engagement/stance) are deliberately **direct calls**, not queued, so they still apply while a fleet is engagement-locked — the menu must preserve that path.

> **⚠ Four DATA rows are stubs that look live** — they issue and complete but do nothing: fleet `RefuelAction`/`ResupplyAction` (empty `Execute`), `ServeyAnomalyAction` (throws `NotImplementedException`), `ShipLogisticsOrders` (empty). Finish them before surfacing, or they mislead the player.

> **⚠ One-verb-both-seats:** only the ground side tags an order with its issuer (`GroundOrderIssuer` Player/Ai, `GroundForcesDB.cs:307`). Space/fleet orders have no such marker — any new fleet order surfaced to both seats should add one.

> **⚠ Movement canon:** build ground movement on the single global-cylinder `March to Planetary Hex` (the coarse region-hop is scheduled for deletion, GroundCombat/CLAUDE.md M1); wire inter-system travel to `JumpOrder`/`ShipJumpCommand`, never the dead `InterSystemJumpProcessor.SetJump`.


### Movement — 16 (10L / 3D / 3B)

| Order | Kinds | Grade | Does | Source |
|---|---|---|---|---|
| **Move to Body** | fleet | LIVE | Warp the whole fleet to a chosen star-system body, slowest ship setting the pace so they arrive together. | `GameEngine/Movement/MoveToSystemBodyOrder.cs:146` |
| **Warp to New Orbit** | warship, civ-ship | LIVE | Order one ship to warp to a target and insert into a low circular orbit; the per-ship primitive under fleet moves. | `GameEngine/Movement/WarpMove/WarpMoveCommand.cs:74/140` |
| **Move Fleet Toward Target** | fleet | LIVE | Warp a fleet toward a target entity (the get-there-first leg for survey/refuel orders). | `GameEngine/Movement/WarpMove/WarpMoveCommand.cs:320` |
| **Change Orbit / Thrust Burn** | warship, civ-ship | LIVE | Apply a Newtonian delta-V burn at a scheduled time to raise/lower/change the ship's orbit. | `GameEngine/Movement/NewtonMove/NewtonThrustCommand.cs:15/48` |
| **Newton Simple Orbit Maneuver** | warship, civ-ship | LIVE | Simplified thrust command that sets up a target orbit; the maneuver-planner's execution primitive. | `GameEngine/Movement/NewtonMove/NewtonSimpleCommand.cs:11` |
| **Nav Sequence (Maneuver Plan)** | warship, civ-ship | LIVE | Append/replace an ordered list of delta-V maneuver nodes from the orbital planner as one plan. | `GameEngine/Movement/NavSequence/NavSequenceCommand.cs:10` |
| **Jump Through Gate** | fleet, warship | LIVE | Warp the fleet to a discovered jump gate and transit every ship to the destination system. | `GameEngine/JumpPoints/JumpOrder.cs:16 + ShipJumpCommand.cs:121` |
| **March Unit to Region** | g-unit | LIVE | Send one individual unit one adjacent-region ring-hop (coarse crossing-time move). | `GroundForcesDB.cs:755 (OrderMove)` |
| **Move Formation to Region** | g-formation | LIVE | March a whole formation one adjacent-region hop at the slowest member's pace so they arrive together. | `GroundForcesDB.cs:1030 (OrderFormationMove)` |
| **March to Planetary Hex** | g-unit, g-formation | LIVE | Path a unit across the one continuous global cylinder grid to a clicked hex, crossing region borders seamlessly. | `GroundForcesDB.cs:823 (OrderMoveToGlobalHex)` |
| **Intercept / Ram Target** | warship | DATA | Newtonian thrust-to-intercept pursuit onto a moving target; today only missile guidance issues it, no player button. | `GameEngine/Movement/NewtonMove/NewtonThrustCommand.cs:252 (ThrustToTargetCmd)` |
| **Move Unit to Hex within Region** | g-unit | DATA | A* march to a hex inside the unit's own region; engine method exists but no client caller (region layer scheduled for deletion). | `GroundForcesDB.cs:785 (OrderMoveToHex) - no client caller` |
| **Move Formation to Region-Hex** | g-formation | DATA | Block-march a formation to a per-region hex at shared pace; engine method exists, no UI issues it. | `GroundForcesDB.cs:864 (OrderFormationMoveToHex) - no client caller` |
| **Hold Position / Station-Keep** | fleet, warship | BUILD | Explicitly stop and hold a fixed point (not a body orbit); move orders only target bodies today, so free-point holding has no order. | `n/a - MoveToSystemBodyOrder targets bodies only` |
| **Fleet Waypoint Chain** | fleet, warship, civ-ship | BUILD | A move A-then-B-then-C waypoint list for ships; formations chain waypoints but fleets serialize single orders instead. | `n/a - only ground GroundOrder queue chains waypoints` |
| **Retreat to Nearest Friendly** | g-formation | BUILD | One-click fighting withdrawal toward the nearest friendly region; exists only as an AI brain intent, no player order. | `n/a - GroundTactics.cs:13 (GroundIntent.Retreat) is AI-only` |

### Combat — 20 (13L / 1D / 6B)

| Order | Kinds | Grade | Does | Source |
|---|---|---|---|---|
| **Assign Weapons to Fire Control** | warship | LIVE | Link chosen weapons to a fire-control director so they fire together. | `GameEngine/Weapons/SetFireControlOrder.cs:13` |
| **Set Fire-Control Target** | warship | LIVE | Point a fire control at a detected enemy contact (resolves the sensor clone to the real entity). | `GameEngine/Weapons/SetFireControlOrder.cs:113` |
| **Open Fire / Cease Fire** | warship | LIVE | Toggle a fire control between engaging (weapons fire each tick) and holding fire. | `GameEngine/Weapons/SetFireControlOrder.cs:213` |
| **Assign Ordnance to Weapon** | warship | LIVE | Load a chosen missile/ordnance design into a launcher weapon. | `GameEngine/Weapons/SetFireControlOrder.cs:328` |
| **Set Fleet Doctrine** | fleet | LIVE | Pick the fleet's combat posture (firepower/toughness/speed multipliers); direct call, works mid-battle, cooldown-gated. | `GameEngine/Combat/FleetDoctrine.cs:52 (TrySetDoctrine)` |
| **Set EMCON Posture** | fleet | LIVE | Run Full/Cruise/Silent - scales every ship's emitted signature (how far off you can be detected). Direct call. | `GameEngine/Sensors/Emcon/FleetEmcon.cs:55` |
| **Set Engagement Posture (ROE)** | fleet | LIVE | Weapons Free / Hold Fire / Return Fire - whether the fleet opens a fight. Direct call, works mid-battle. | `GameEngine/Combat/FleetDoctrine.cs:39` |
| **Attack Nearest Hostile** | fleet | LIVE | Force this fleet to engage the nearest hostile now - clears retreat, sets Weapons Free, closes to weapons range. | `GameEngine/Combat CombatEngagement.OrderAttackNearestHostile` |
| **Land Troops (Invasion Drop)** | warship, civ-ship | LIVE | Drop loaded ground units from orbit onto a chosen region; blocked unless the ship holds orbital control (win the space first). | `GameEngine/GroundCombat/LandTroopsOrder.cs:22` |
| **Set Combat Stance (Ground)** | g-formation | LIVE | Switch the formation's doctrine stance from the moddable catalog (+/-25% attack/damage-taken trade), cooldown-gated. | `GroundFormationDoctrine.cs:43 (TrySetStance)` |
| **Set Rules of Engagement (Ground)** | g-formation | LIVE | Set the maneuver intent each tick - stand and fight, close the gap (brawler), or kite at range (auto-back-away). | `GroundForcesDB.cs:275 + GroundFormationDoctrine.cs:61` |
| **Raze Infrastructure** | g-formation | LIVE | Order a formation to bombard/raze the building(s) on a held or contested hex (Attack-scaled, staged drain). | `GroundForcesDB.cs:347 + processor:941` |
| **Capture Infrastructure** | g-formation | LIVE | Seize a hex - flips the hex owner so its buildings stop fortifying the defender (instant in v1). | `GroundForcesDB.cs:348 + processor:948` |
| **Set Target Priority** | fleet | DATA | Heaviest / BiggestThreat / Balanced targeting steers the casualty step, but is set only via a whole-doctrine swap, no standalone control. | `GameEngine/Combat/FleetDoctrine.cs:33 (FleetDoctrineDB.Targeting)` |
| **Dig In / Entrench** | g-unit, g-formation | BUILD | A stationary unit digs in for a defensive multiplier over time; no such order exists - HoldFor only pauses, only buildings fortify. | `n/a - closest GroundForcesDB.cs:344 HoldFor grants no defense bonus` |
| **Set Ambush** | g-formation | BUILD | Go hidden/hold-fire in cover and strike an enemy that enters range; no ambush order or hidden-posture exists. | `n/a - no ambush order` |
| **Call Orbital Bombardment** | g-formation, g-unit | BUILD | A ground formation calls in orbital fire on an enemy hex; space-to-ground weapons exist but no ground-side call-for-fire order. | `n/a - SpaceWeaponGround.cs exists ship-side` |
| **Blockade** | fleet | BUILD | Deny an enemy body's orbit or a jump point to hostile traffic; no blockade concept/order. | `n/a - orbital-dominance check noted missing in Fleets/CLAUDE.md` |
| **Retreat / Withdraw (Explicit)** | fleet | BUILD | A first-class break-off-and-run order issuable mid-engagement; today withdrawal is only a doctrine flag, not a standalone order. | `n/a - hook reserved at GameEngine/Engine/Orders/EntityCommand.cs:39 (IsAllowedDuringEngagement)` |
| **Attack Specific Target** | fleet | BUILD | Pick a specific enemy fleet by map-click to engage; only attack-nearest-hostile exists today. | `n/a - follow-up flagged at FleetWindow.cs:652` |

### Survey — 3 (2L / 1D / 0B)

| Order | Kinds | Grade | Does | Source |
|---|---|---|---|---|
| **Geological Survey** | fleet, civ-ship | LIVE | Survey a body over game-time to reveal its minerals (fleet must carry a geo-survey component). | `GameEngine/GeoSurveys/GeoSurveyOrder.cs:8/84` |
| **Jump-Point (Grav) Survey** | fleet, civ-ship | LIVE | Survey a survey-point to discover/reveal a jump point (fleet must carry a JP-survey component). | `GameEngine/JumpPoints/JPSurveyOrder.cs:11/95` |
| **Survey Anomaly** | fleet | DATA | Investigate a located anomaly; class exists but Execute/IsValid/Clone all throw NotImplementedException. | `GameEngine/Fleets/ServeyAnomalyAction.cs:19 (throws NotImplementedException)` |

### Logistics-Cargo — 16 (8L / 6D / 2B)

| Order | Kinds | Grade | Does | Source |
|---|---|---|---|---|
| **Transfer Cargo** | warship, civ-ship, colony, station, any | LIVE | Move any cargo item (minerals, refined goods, components, fuel, ordnance) between ship and colony/station/other-ship by drag-drop. | `GameEngine/Storage/CargoTransferOrder.cs:17/72` |
| **Refuel Fleet at Base** | fleet, warship, civ-ship | LIVE | Warp a fleet to a friendly colony/station and top every ship's fuel tank from that base's stores. | `GameEngine/Storage/CargoTransferOrder.cs:136 (CreateRefuelFleetCommand)` |
| **Enable/Disable Trade Ship** | civ-ship | LIVE | Add or remove a LogiShipperDB so the ship joins or leaves the automated freight bidding market. | `GameEngine/Logistics/SetLogisticsOrder.cs:58` |
| **Set Trade Ship Cargo Allocation** | civ-ship | LIVE | Choose how much per-cargo-type volume the trade ship dedicates to freight and its max trade mass (shapes which runs it bids on). | `GameEngine/Logistics/SetLogisticsOrder.cs:95` |
| **Set Base Imports & Exports** | colony, station | LIVE | Mark goods this base wants imported or has surplus to export - the demand signal the trade-ship bidding market reads. | `GameEngine/Logistics/SetLogisticsOrder.cs:70` |
| **Enable/Disable Trade Hub** | colony, station | LIVE | Remove the LogiBaseDB so a base stops acting as a logistics trade hub (created by installing a LogiBaseAtb component). | `GameEngine/Logistics/SetLogisticsOrder.cs (RemoveLogiBaseDB)` |
| **Embark / Load Troops** | warship, civ-ship | LIVE | Load the ground units standing on the orbited body into a troop/vehicle bay ship (capacity-gated). | `GameEngine/GroundCombat/LoadTroopsOrder.cs:21` |
| **Move Component To/From Cargo** | any, colony, station, warship, civ-ship | LIVE | Stow an uninstalled component into the cargo hold, or pull one out ready to install. | `GameEngine/Storage/AddComponentToStorageOrder.cs:30 + RemoveComponentFromStorageOrder.cs:30` |
| **Refuel (Fleet Self-Action)** | fleet | DATA | Standing/issued fleet refuel-in-place action; registered and selectable but Execute() is an EMPTY stub - transfers no fuel. | `GameEngine/Fleets/RefuelAction.cs:26 (empty Execute)` |
| **Resupply (Fleet Self-Action)** | fleet | DATA | Standing fleet resupply action; selectable but Execute() is an empty stub - moves no ordnance/supply. | `GameEngine/Fleets/ResupplyAction.cs:25 (empty Execute)` |
| **Set Stockpile Min/Max Target** | colony, station | DATA | Define a per-good min/max the base should auto-maintain; the LogiBaseDB.DesiredLevels field exists but no UI or processor writes/reads it. | `GameEngine/Logistics/LogiBaseDB.cs:17 (DesiredLevels` |
| **Ship Logistics State (Start/Abort Job)** | civ-ship | DATA | Per-ship logistics lifecycle order; Execute() is an empty stub - the ship state machine past Bidding is undriven. | `GameEngine/Logistics/ShipLogisticsOrders.cs:7 (empty Execute)` |
| **Resupply / Rearm Unit (Manual)** | g-unit | DATA | Top a unit's ammo pool to full when it sits on friendly-held ground; the processor auto-resupplies but no manual player button. | `GroundForcesDB.cs:670 (ResupplyUnit)` |
| **Reload / Rearm Ordnance at Magazine** | warship, fleet, civ-ship | DATA | A one-click rearm-from-base for missile ships (the fleet analog of Refuel); ordnance moves via generic Transfer Cargo but no dedicated reload order. | `ordnance moves via CargoTransferOrder.cs` |
| **Set Trade Route / Auto-haul A to B** | civ-ship, colony, station | BUILD | A directed, explicit haul-between-two-named-bases route; doesn't exist - logistics is emergent bid-based, no pinned A->B loop. | `n/a - emergent path GameEngine/Logistics/LogisticsProcessor.cs LogiShipBidding` |
| **Ferry Population / Load Colonists** | civ-ship, colony | BUILD | Load colonists into a ship and move population between worlds; no colonist cargo type - population is not ICargoable. | `n/a - population in ColonyInfoDB.Population, not modeled as cargo` |

### Fleet-ops — 7 (5L / 2D / 0B)

| Order | Kinds | Grade | Does | Source |
|---|---|---|---|---|
| **Create Fleet** | fleet | LIVE | Create a new named empty fleet under the faction root. | `GameEngine/Fleets/FleetOrder.cs:55 (CreateFleetOrder)` |
| **Disband Fleet** | fleet | LIVE | Dissolve a fleet, re-homing its ships to the faction root and sub-fleets to the parent. | `GameEngine/Fleets/FleetOrder.cs:67 (DisbandFleet)` |
| **Assign Ship to Fleet** | fleet, warship, civ-ship | LIVE | Add a ship to a fleet (first ship becomes the flagship). | `GameEngine/Fleets/FleetOrder.cs:88 (AssignShip)` |
| **Unassign Ship from Fleet** | fleet, warship, civ-ship | LIVE | Remove a ship from a fleet (detaches to root; clears flagship if it was the flag). | `GameEngine/Fleets/FleetOrder.cs:99 (UnassignShip)` |
| **Set Flagship** | fleet, warship | LIVE | Make a chosen ship the fleet's flagship (its map icon + representative). | `GameEngine/Fleets/FleetOrder.cs:110 (SetFlagShip)` |
| **Toggle Inherit Orders** | fleet | DATA | Flip whether a sub-fleet follows its parent's orders; order class + field are live but the client checkbox is commented out. | `GameEngine/Fleets/FleetOrder.cs:121` |
| **Dock / Undock Vessel (Carrier Berthing)** | warship, civ-ship | DATA | Berth a whole vessel inside a carrier's dock bay (re-parents so it travels with the carrier); full capability + gates exist but NO order/UI is wired. | `GameEngine/Docking/DockTools.cs:148 (TryDock)/:178 (Undock)` |

### Formation-ops — 12 (4L / 4D / 4B)

| Order | Kinds | Grade | Does | Source |
|---|---|---|---|---|
| **Reparent / Reorganize Fleet** | fleet | LIVE | Move a fleet or ship under another fleet in the command tree (drag-drop), with a cycle guard. | `GameEngine/Fleets/FleetOrder.cs:77 (ChangeParent)` |
| **Create Formation** | g-formation | LIVE | Create a named empty formation on a body (the ground echo of forming a fleet); first unit assigned becomes the leader. | `GroundForcesDB.cs:917 (CreateFormation)` |
| **Assign Unit to Formation** | g-unit, g-formation | LIVE | Add a same-faction unit to a formation (ground echo of AssignShip); first in becomes leader. | `GroundForcesDB.cs:978 (AssignUnit)` |
| **Disband Formation** | g-formation | LIVE | Dissolve a formation - its members become unformed and the record is removed; the units survive. | `GroundForcesDB.cs:1018 (DisbandFormation)` |
| **Nest Sub-Formation** | g-formation | DATA | Re-parent one formation under another to build a battle-group tree (cycle-guarded); engine only, no UI wired. | `GroundForcesDB.cs:938 (SetParentFormation) - no client caller` |
| **Detach Unit from Formation** | g-unit, g-formation | DATA | Remove a unit from its formation; leadership passes to a survivor. Engine method exists, no client button. | `GroundForcesDB.cs:989 (UnassignUnit) - no client caller` |
| **Set Formation Leader** | g-formation | DATA | Designate a member unit as the formation's leader/flagship; engine method exists, no UI. | `GroundForcesDB.cs:999 (SetLeader) - no client caller` |
| **Move Formation Tree** | g-formation | DATA | March a formation AND every nested sub-formation under it with one order (command-hierarchy payoff); engine only, no UI. | `GroundForcesDB.cs:966 (OrderFormationTreeMoveToHex) - no client caller` |
| **Merge Formations** | g-formation | BUILD | Absorb one formation's units into another as a single verb; only nesting and per-unit reassignment exist, no true merge. | `n/a - no merge verb (nest is GroundForcesDB.cs:938)` |
| **Split Formation** | g-formation | BUILD | Detach a subset of units into a new named formation in one action; today needs Unassign-each + Create + Assign. | `n/a - no split verb` |
| **Escort / Guard Unit** | fleet, warship, g-formation | BUILD | Station-keep on and shadow another friendly unit, interposing against threats; only an AI-internal helper exists, no player order. | `n/a - GameEngine/Fleets/FleetAssembly.cs FindEscortableFleet is AI-only` |
| **Screen (Interpose Escorts)** | fleet | BUILD | Order a sub-fleet to screen the main body; FleetRole.Screen exists as an auto-composition role but is not an issuable order. | `n/a - FleetRole.Screen (GameEngine/Fleets/FleetRoleComposer.cs) is a role tag` |

### Construction-Industry — 20 (15L / 1D / 4B)

| Order | Kinds | Grade | Does | Source |
|---|---|---|---|---|
| **Deploy Station Here** | warship, civ-ship | LIVE | A construction ship anchors a bare station platform at its current location, paying frame materials from its own + fleet-mates' holds. | `GameEngine/Stations/DeployStationOrder.cs:43/73` |
| **Construct Station Here (from Parts)** | civ-ship | LIVE | A constructor ship assembles a designed station on-site from the actual components it and its fleet hauled, installing each module. | `GameEngine/Construction/OnSiteConstructionOrder.cs:46` |
| **Colonize Body** | civ-ship, colony, any | LIVE | Found a new colony on a surveyed, colonizeable body for a species. | `GameEngine/Colonies/CreateColonyOrder.cs:9/25` |
| **Launch Ship from Pad** | colony, station | LIVE | Launch a completed ship off a colony's launch pad / shipyard into space. | `GameEngine/Ships/LaunchShipCommand.cs:7/21` |
| **Queue Production Job** | colony, station | LIVE | Add a job to a production line to refine materials, build components, build ships, or build ground units - with batch count, repeat, auto-install. | `GameEngine/Industry/IndustryOrder.cs:55 (IndustryOrder2 NewJob)` |
| **Cancel Production Job** | colony, station | LIVE | Remove a queued/in-progress job from a production line. | `GameEngine/Industry/IndustryOrder.cs:71` |
| **Change Production Job Priority** | colony, station | LIVE | Move a job up or down in a production line's queue. | `GameEngine/Industry/IndustryOrder.cs:83` |
| **Add to Local Construction Queue** | colony, station | LIVE | Queue a design on the second, on-site LocalConstructionDB build path (points-per-day queue that installs the component on completion). | `GameEngine/Industry/Orders/AddToConstructionQueueOrder.cs:8` |
| **Remove/Reorder Local Construction Queue** | colony, station | LIVE | Remove a job from, or move it up/down within, the LocalConstructionDB build queue. | `GameEngine/Industry/Orders/RemoveFromConstructionQueueOrder.cs + MoveUp/MoveDownInConstructionQueueOrder.cs` |
| **Install Facility from Stockpile** | colony, station, any | LIVE | Install a component held in a cargo hold onto a target entity, consuming it from storage and recalculating abilities. | `GameEngine/Storage/CargoInstallOrder.cs:52` |
| **Install Component Instance** | any, colony, station, warship, civ-ship | LIVE | Move a component from a cargo hold into an installed slot on the entity (closest thing to a manual refit-one-module step). | `GameEngine/Engine/Components/InstallComponentInstanceOrder.cs:26` |
| **Uninstall Component Instance** | any, colony, station, warship, civ-ship | LIVE | Remove an installed component from an entity (module removal; pairs with Add-to-storage for a manual swap). | `GameEngine/Engine/Components/UninstallComponentInstanceOrder.cs:27` |
| **Place Installation in Region** | colony | LIVE | Place a PlanetInstallation design at a surface region of the colony's world (draws as a building on the tactical map). | `GameEngine/Galaxy/PlaceInstallationInRegionOrder.cs:30` |
| **Build Mine on Deposit Hex** | colony | LIVE | Place a mine installation on a specific surface hex that holds a scanned mineral deposit. | `GameEngine/Galaxy/PlaceInstallationOnHexOrder.cs:31` |
| **Build / Raise Ground Unit** | colony, g-unit | LIVE | Build a ground unit through normal industry (a component carrying GroundUnitAtb; on install it raises the unit). DevTools can raise directly. | `GroundForcesDB.cs:589 (RaiseUnit) via GroundUnitAtb OnComponentInstallation` |
| **Edit Production Job** | colony, station | DATA | Change an existing job's quantity/repeat/auto-install; the order type exists but the client sets those at queue-time instead, so no wired caller. | `GameEngine/Industry/IndustryOrder.cs:97 (CreateEditJobOrder` |
| **Reinforce / Rebuild Formation** | g-formation, colony | BUILD | Queue production of replacement units to bring an under-strength garrison back to target; the AI helper exists but no player order. | `n/a - GroundReinforcement.cs is an AI-read helper, no order verb` |
| **Refit Ship at Yard** | warship, civ-ship | BUILD | Retool a built ship to a different design at a shipyard; no refit order exists, only piecemeal Install/Uninstall of component instances. | `n/a - grep refit in GameEngine returns only EventTypes.cs` |
| **Scrap / Decommission Ship** | warship, civ-ship | BUILD | Break a ship for material recovery at a colony/yard; no order exists, ShipFactory.DestroyShip has no client caller and no reclaim path. | `n/a - DestroyShip not reachable from client` |
| **Mothball / Reactivate** | warship, civ-ship | BUILD | Lay a ship up in reserve (reduced upkeep, cannot fight) and later reactivate it; no such order. | `n/a - no mothball order in GameEngine` |

### Special — 12 (5L / 1D / 6B)

| Order | Kinds | Grade | Does | Source |
|---|---|---|---|---|
| **Rename Entity** | warship, civ-ship, fleet, colony, station, any | LIVE | Rename any owned entity (ship, fleet, colony, station) that has a NameDB. | `GameEngine/Names/RenameCommand.cs:7/22` |
| **Rename Formation** | g-formation | LIVE | Rename a formation (a data object, so it uses this setter instead of the entity-only RenameWindow). | `GroundForcesDB.cs:1009 (RenameFormation)` |
| **Assign/Unassign Governor** | colony, station | LIVE | Seat or remove a commander in an administration post on a colony/station (governance delegate). | `GameEngine/People/Orders/AssignAdministratorOrder.cs:10 + UnassignAdministratorOrder.cs:10` |
| **Set Post Funding** | colony, station | LIVE | Set the funding level of an admin/research post (drives its output rate). | `GameEngine/Tech/Orders/FundingChangedOrder.cs:8` |
| **Research: Queue Tech / Assign Scientist** | colony, station | LIVE | Add or remove a tech in a research lab's queue and assign/unassign a scientist to lead it. | `GameEngine/Tech/Orders/AddTechToQueueOrder.cs + RemoveTechFromQueueOrder.cs + AssignScientistOrder.cs` |
| **Set Colony Tax Rate** | colony, station | DATA | Player sets the tax rate (drives monthly income scaled by morale); TaxRate has a public setter but NO order class - a direct field write. | `GameEngine/Colonies/ColonyEconomyDB.cs (TaxRate` |
| **Salvage / Recover Wreck** | warship, civ-ship | BUILD | Recover materials/components from a destroyed hull or debris; no salvage order or wreck entity. | `n/a - no salvage order` |
| **Self-Destruct / Scuttle** | warship, civ-ship | BUILD | Destroy your own ship (deny capture / clear a wreck); no such order. | `n/a - no scuttle order` |
| **Rename / Disband Individual Unit** | g-unit | BUILD | Rename a single unit or scrap/disband it to recover crew/cost; only formations can be renamed/disbanded, units have no such verb. | `n/a - RenameFormation/DisbandFormation are formation-only (GroundForcesDB.cs:1009/1018)` |
| **Set Capital / Abandon Colony** | colony | BUILD | Designate a capital colony, or abandon/disestablish one; neither exists - only CreateColonyOrder, no set-capital and no abandon order. | `n/a - no set-capital or abandon order` |
| **Set Readiness / Crew Condition** | warship, fleet, g-unit | BUILD | A readiness/condition state (Green/Yellow/Red, shore-leave, refit-state) to display and toggle on a command card; entirely new. | `n/a - no readiness concept on ships or units` |
| **Cancel / Reorder Live Queued Order** | fleet, warship, civ-ship, any | BUILD | Cancel or re-sequence orders already in the live ActionList; today the Orders table only DISPLAYS the queue, no cancel or drag-reorder. | `n/a - FleetWindow.cs:1823 Orders table is display-only` |

### Standing-Conditional — 17 (9L / 4D / 4B)

| Order | Kinds | Grade | Does | Source |
|---|---|---|---|---|
| **Move to Nearest Colony** | fleet | LIVE | Auto-move the fleet to the closest friendly colony; selectable as a standing-order action. | `GameEngine/Movement/MoveToNearestColonyAction.cs` |
| **Move to Nearest Geo-Survey Target** | fleet | LIVE | Auto-move the fleet to the closest un-surveyed body; standing-order action. | `GameEngine/Movement/MoveToNearestGeoSurveyAction.cs` |
| **Move to Nearest Anomaly** | fleet | LIVE | Auto-move the fleet to the closest anomaly; standing-order action. | `GameEngine/Movement/MoveToNearestAnomalyAction.cs` |
| **Create Standing / Conditional Order** | fleet | LIVE | Build an if-condition-then-actions rule on a fleet (e.g. fuel < 30% -> move to nearest colony), evaluated hourly. | `GameEngine/Engine/Orders/ConditionalOrder.cs:6 + FleetOrderProcessor` |
| **Condition: Fleet Fuel** | fleet | LIVE | The only wired standing-order condition - fires when fleet average fuel crosses a comparison threshold (framework supports AND/OR of more). | `GameEngine/Engine/Orders/Conditions/FuelCondition.cs:13` |
| **Queue Waypoint: Move to Region** | g-formation | LIVE | Append a then-move-to-region-N step to the formation's sequential order queue (a real then-waypoint chain). | `GroundForcesDB.cs:343 + :1052 (QueueFormationOrder)` |
| **Queue Waypoint: Hold / Pause** | g-formation | LIVE | Queue a timed wait (default 6h) at the current position before the next queued order runs. | `GroundForcesDB.cs:344 (Hold) + processor:926` |
| **Queue Waypoint: Set ROE** | g-formation | LIVE | Queue an ROE switch (Stand-off / Close) as a step in the order chain, applied when that waypoint executes. | `GroundForcesDB.cs:346 (Roe) + processor:936` |
| **Clear Plan (Cancel Queue)** | g-formation | LIVE | Empty a formation's queued waypoint chain; in-transit units finish their current march but no further orders run. | `GroundForcesDB.cs:1069 (ClearFormationOrders)` |
| **Queue Waypoint: Move to Planetary Hex** | g-formation | DATA | Queue a formation march to a GLOBAL cylinder hex as a waypoint; order type + handler exist but no client button queues it. | `GroundForcesDB.cs:342 (MoveHex) + :893 + processor:918 - no client caller` |
| **Queue Waypoint: Set Stance** | g-formation | DATA | Queue a doctrine-stance switch as a sequenced order step; order type + handler exist but the client sets stance directly, never queues it. | `GroundForcesDB.cs:345 (Stance) + processor:931 - no client caller` |
| **Set Formation Order (Replace Queue)** | g-formation | DATA | Replace a formation's entire queue with a single do-this-now order; engine method exists, client uses Queue+Clear instead. | `GroundForcesDB.cs:1060 (SetFormationOrder) - no client caller` |
| **Pause-on-Action (Auto-Halt)** | fleet, warship, civ-ship, any | DATA | An order with PauseOnAction=true stops the clock when it fires so the player is notified; the field + event exist but no UI sets the flag. | `GameEngine/Engine/Orders/EntityCommand.cs:90 (PauseOnAction` |
| **Patrol / Loiter Area** | fleet, warship, g-formation | BUILD | Sweep or hold a designated area/route and auto-engage intruders; no such order exists anywhere. | `n/a - no Patrol order in GameEngine (would extend GroundOrderType / add a fleet order)` |
| **Picket / Sentry** | fleet, warship | BUILD | Hold a chokepoint/jump-point and report contacts, alerting on intrusion; no such order. | `n/a - no picket order` |
| **Repeat / Loop Order** | fleet, civ-ship, g-formation | BUILD | A cyclic order that re-runs forever (patrol loop, repeating cargo run, cycle-these-waypoints); no cyclic primitive - orders run once and are removed. | `n/a - RemoveAll(IsFinished) drops completed orders` |
| **Richer Standing-Order Conditions** | fleet | BUILD | New triggers - hull-damage %, cargo-full, enemy-detected, at-location, ammo-low, arrived; the CompoundCondition AND/OR framework supports them, none authored. | `n/a - only FuelCondition exists` |

*Prototype: `docs/combat/forceswindow.html`. Seed window: `FleetWindow.cs` ("Force Management"). This doc records the design; the engine is untouched.*
