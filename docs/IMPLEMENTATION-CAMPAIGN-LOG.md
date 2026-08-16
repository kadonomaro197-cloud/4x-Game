# OPERATION BLUEPRINT-TO-STEEL — Implementation Campaign Log

**What this is:** the running ledger for the campaign that turns the 17 HTML design tools
(`docs/DESIGN-TOOLS-INDEX.md`) into real, shipping game features — engine code, client windows, game data.
This file is the **lifeline**: any session can pick up cold by reading this + `git log --oneline -30` and
continuing from **NEXT ACTION**. Updated and pushed with every slice.

**Started:** 2026-08-13. **Branch:** `claude/operation-blueprint-to-steel-6kxkhb` (all work here; push only here).

**The Prime Directive (from the campaign brief):** the HTMLs are the spec. An HTML beats every `.md`. The
HTMLs' own honesty grades (LIVE / DATA / BUILD) are the build orders. Implement the *design*, not the web page
(re-express in C# + ImGui). Build everything the HTMLs specify and nothing beyond them.

---

## NEXT ACTION

> **🧭 FRESH NEXT ACTION (2026-08-16, end of session) — CIVIC DOOR COMPLETE + D-units (loose-unit march) + E-env slice 1 ALL LANDED CI-GREEN.** `daae8f2` (civic Medical) ✅, `a897a9a` (E-env slice 1 = `CombatConditions` reader) ✅, `fb497cd` (D-units loose-unit march → queued formation verb) ✅; `de06459` (printf-trap hardening on `PlanetViewWindow._status`) ⏳CI (client-only, will pass). Two Agent-tool review agents (D-units logic + E-env-1 byte-identity) ran post-landing to catch what CI can't (D-units is runtime-blind) — fold any confirmed fix as a follow-up.
> **⚙ ENV LESSON (2026-08-16, re-confirmed):** the **Workflow tool's SUBAGENTS cannot use tools in this environment** — the permission handler STRIPS their tool-call parameters, so every Bash/Read/Grep returns nothing and the agents verify nothing (a review workflow returned "ZERO CONFIRMED DEFECTS" as a NON-result). **Use the Agent tool (Explore / general-purpose) for any parallel file-reading recon/review — those work; Workflow does not.** Do the deterministic orchestration in the main session.
> **The "Commerce civic dial" was a mis-derivation** — no such dial exists (see the C-civic row: Commerce is an academy leader-domain + the Economy door, not civic). **Pick the next FILE-DISJOINT buildable slice:**
> 1. **Phase D — planetary view (biggest unstarted chunk, north-star-aligned):** **D-units loose-unit march is DONE + CI-pending (2026-08-16** — developer ruled the formation is the unit of movement; the two bypass sites now auto-wrap → the queued `SetFormationOrder` verb both seats use). **NEXT:** (a) the flagged D-units follow-up — convert the direct `OrderFormationMove` formation-march buttons (`PlanetViewWindow.cs:1572` + FleetWindow `DrawBattalionOrders`) to the queued verb for full One-Verb consistency (immediate→queued, own slice); then **D-stockpile** (per-hex stockpile + hex-to-hex haul), then **D-planfn**.
> 2. **Phase E — E-env (smaller, self-contained, NEEDS NO MOVEMENT RULING):** `CombatConditions` into the shared combat kernel (space combat stops being environment-blind). ⚠ touches the shared damage/auto-resolve kernel (L10) → its own recon first.
> 3. **C-sensors** — the band-match fix (⚠ backlog file:line STALE + behaviour-changing → own focused slice).
> **PARKED for the developer (ADJUDICATION QUEUE — do NOT guess):** A4-ORDERS (4 order-stub behaviors) · C-GUIDED (missile ordnance-at-build) · C-MOBILITY (drive×frame combine) · B-ORDERS-MOVEMENT (Intercept/Ram) · C-deadknobs TIER 4 #6–8 + the capture-transfer ruling · **CIVIC space-habitat mass-pricing** (a 1M-colonist station costs the same 1 t as an empty one — price it? `01-IO-civic.md` §D). **Full slice board below reflects this session.** Historical NEXT ACTION detail preserved below.
>
> **(historical) 🧭 NEXT ACTION (2026-08-16) — the run-cost vector is COMPLETE; Phase C continues with the civic BUILD dials.**
> Phase A + B are DONE. Phase C **run-cost vector — ALL FIVE RUNGS LANDED:** Jobs (A1), **Staffing** (`7cdce65`), **Upkeep**
> (`77c8411`), **Power** (mechanism `d2ac424` + make-live `927deef` — reactor on Earth, throttle bites at 75 MW vs ~52 MW),
> **Food** (make-live `<this>` — 4 agri-complexes on Earth + demand on, food-positive 2.44×). The developer re-issued "for 1
> do whatever fits best / for 2 do your rec" → I un-parked both and BUILT the supply side onto the (formerly barebones)
> start colony, gauge-verified (`PowerThrottleTests` + `FoodDemandTests` assert Earth powered+fed on the real numbers; CI is
> the calibration net). **⏳ AWAITING CI on `927deef` (power) + the food commit before treating them green.** **The next
> buildable Phase C slice = the two civic BUILD dials from `01-IO-civic.md` — **BOTH NOW DONE (2026-08-16):****
> **✅ (1) Medical → health → morale — DONE 2026-08-16** (`MedicalAtbDB` mirrors `SecurityAtbDB`; summed via
> `GetTotalMedical` → the flag-gated `MoraleInputs.HealthStrength` term in `ColonyMoraleDB.ComputeMorale`, capped
> `MaxHealthBonus` 20, behind `PopulationProcessor.EnableMedicalMorale` set in BOTH morale gatherings + `NewGameMenu`-on;
> `medical-hospital` template + `default-design-hospital` registered buildable-not-installed on Earth; gauge
> `MedicalMoraleTests`; stations use the positional ComputeMorale overload so station-medical is a noted follow-up) and
> **✅ (2) Security → unrest → legitimacy — DONE 2026-08-16
> (developer: "start the civic security dial while we wait")**. The Security dial landed as a real BUILD (like A1's
> employment term): `SecurityAtbDB` (mirrors `FoodProductionAtbDB` — public parameterless + parameterized ctor, round-trips)
> summed via `ComponentInstancesDBExtensions.GetTotalSecurity` (health-scaled) → a NEW flag-gated `LegitimacyInputs.SecurityStrength`
> input on `LegitimacyDB.ComputeLegitimacy` (capped `MaxSecurityBonus` 15), behind `LegitimacyProcessor.EnableSecurityLegitimacy`
> (default OFF → byte-identical, guarded factor add so an unpoliced colony's breakdown is unchanged), `NewGameMenu`-on both
> paths. L6 six-point registration: `security-precinct` template (`installations.json`, single-dial `Security Rating` → `AtbConstrArgs`
> binds `SecurityAtbDB`) + `default-design-security-precinct` (rating 10) + Earth StartingItems/ComponentDesigns — **registered
> BUILDABLE but NOT auto-installed** (a policing decision, not a freebie; and it sidesteps the C-FOOD shared-fixture-baseline
> trap — nothing installed on Earth, so GetTotalSecurity is 0 pristine and no baseline test moves). Gauge `SecurityLegitimacyTests`
> (pure math + cradle-to-grave: register→install→flag-gated legitimacy rise→grave-rung destroy). Files: `SecurityAtbDB.cs` ·
> `ComponentInstancesDBExtensions.cs` · `LegitimacyDB.cs` · `LegitimacyProcessor.cs` · `installations.json` · `componentDesigns.json`
> · `earth.json` · `NewGameMenu.cs` · `SecurityLegitimacyTests.cs`. **Medical → health → morale is the remaining civic dial**
> (a new morale input on the CORE `ColonyMoraleDB.ComputeMorale` — health is not one of the six morale inputs, so it's a new
> consumer; same flag-gate-OFF + `MoraleTests` baseline + NewGameMenu-on pattern). After civic: the remaining door DATA dials (per `02-IO-MATRIX.md` —
> most left are new-atb BUILDs or dead-knob cuts, the cheap welds are spent), then Phase D (planetview units-on-map) + E
> (resolver environment). **Resume by re-reading this line + `git log --oneline -20`; do not restart landed work.**
>
> **DEVELOPER-REQUESTED WORK DONE (2026-08-13): the two base-red economy tests are fixed, the morale term is
> calibrated AND turned ON for the real game.** ✅ The base is greened (cargo + food test fixes, commit `c96dd5f`) —
> the PRE-EXISTING BASE RED carve-out is **retired**; any red is now real. ✅ The A1 employment→morale calibration
> (Option B per-capita demand, `JobsPerCapita = 7.0e-6`, commit `2e06464`) is resolved, and ✅ the term is **flipped
> ON for menu games** via `NewGameMenu` (engine default still OFF → tests byte-identical, commit `7ea23f4`). **⏳
> VERIFIED (2026-08-13):** the morale run `2e06464` completed **FULL SUCCESS (all 7 shards green)** — the base is
> green (cargo + food) and the employment homeworld-band test passed, so the calibration landed near-neutral (**no
> +15 snap**, `JobsPerCapita = 7.0e-6` confirmed); the flag-on run (`7ea23f4`) build-client is green (NewGameMenu
> compiles). The live morale feel is the developer's PC play-test (CLIENT-TEST-CHECKLIST). **NOW BUILDING: B-S5.**
>
> **🏁 PHASE B ROSTER LADDER S1→S9 IS COMPLETE + CI-VERIFIED GREEN (2026-08-15).** B-S8, B-S9a, B-S9b-1 (`271f987`, run
> 31858039704) and B-S9b-2 (`21b164f`, run 31859301590) are ALL fully green (7 shards + build-client). **B-S9 (all three
> parts) closes §S9:**
> **B-S9a** (`e2b10f7`, run 31857203852 GREEN) — engine `FactionAssets.OwnedColonies`/`OwnedStations`, the live-owner
> cross-check that filters the capture-stale colony/station registry by live `FactionOwnerID` (`FactionAssetsTests`).
> **B-S9b-1** (`271f987`) — colonies + stations as **Holding** roster rows (host/population/station-integrity + a Holding
> detail panel reusing `ComponentInstancesDBDisplay`). **B-S9b-2** (`21b164f`, build-client GREEN) — the assign-commander
> UI (`DrawHoldingAdminPosts`: per-`AdminSpaceDB.CommanderSeats` combo of `FactionInfoDB.Commanders` →
> `AssignAdministratorOrder`/`UnassignAdministratorOrder`; verified the order actually seats the officer — the holding
> carries `OrderableDB`, `HandleOrder` is try/catch-wrapped, `Clone()` never called). All four (B-S8/B-S9a/B-S9b-1/B-S9b-2)
> are flipped ✅ on the board. **NEXT: B-orders** (route the 23 button-only DATA orders + the deep
> categorized order menu — Phase B's last item, evolves `FleetWindow.cs`, whose compile is green through B-S9b-2), then
> Phase C/D/E per the slice board. B-orders shares `FleetWindow.cs` with B-S9b → build it once B-S9b is fully green.
>
> **Phase A + B-S1..S7 are CI-verified.** The Forces-window foundation is fully in place
> through the All-Forces roster. **B-S6** (`0e0faaa`, per-unit ground rows + engine `AllUnitsFor`): run `31852230485`
> **fully GREEN** → ✅. **B-S7** (`e894b12`, civilian-ship detail — cargo manifest via reused `CargoStorageDBDisplay` +
> `LogiShipperDB` route/state + a survey note): run `31853742806` **fully GREEN** (all 7 shards + build-client) → ✅.
> **B-S8 — aggregate Health + Fuel gauges**: the first push (`d369ebb`) went RED on the `rest` shard — the
> `HealthFraction_Defensive_NullAndComponentless` test fed a bare `Entity.Create()` (an UNMANAGED entity, `Manager ==
> null`), and `TryGetDataBlob` delegates to `Manager`, so it NRE'd instead of returning 1.0. The core invariant test
> (pristine → damaged → destroyed) PASSED, so the accessor's math was right; only the defensive edge was uncovered.
> Fixed in **`5e42d87`** with a `ship.Manager == null` guard (an unmanaged entity now returns 1.0 without throwing,
> honouring the "never throws" contract) — re-gating as run `31856896871`. The NEW engine accessor
> **`ShipHealth.HealthFraction(entity)`** sums a ship's living-component `HealthPercent` over its ORIGINAL design count
> (a destroyed/removed component honestly counts as 0 — not hidden by a mean-of-survivors), CI-gauged by
> `ShipHealthTests`; the roster gained a real **Health column** (ship via that accessor, battalion via `FormationHealth`,
> ground unit via `Health/MaxHealth`, colour-banded); and the ship detail shows **Health % + Fuel %** (Fuel reuses the
> existing `EntityExtensions.GetFuelInfo` fill fraction). **NEXT (once B-S8 is green): B-S9** — stations + colonies as roster rows (with the live-owner
> cross-check for the capture-stale-registry gap) + the assign-commander UI; then B-orders + Phase C/D/E. Phase B
> evolves `FleetWindow.cs` (keep the class name) and is client-heavy — CI compile-checks it but can't runtime-test, so
> each behavior gets a `docs/CLIENT-TEST-CHECKLIST.md` row.
>
> **Phase C recon (2026-08-15, done while B-S8's CI ran — §4 file-disjoint work):** the file-disjoint TIER 3 engine
> welds are mostly PARK items, now in the ADJUDICATION QUEUE — **C-guided (#3)** needs the "which ordnance at build"
> decision (rec: read a representative warhead at build; combat value is cached at build but a launcher's ordnance is a
> runtime loadout), **C-mobility (#5)** is the developer's call on the frame×drive combine (rec: multiply). **C-sensors
> (#4)** has a STALE backlog file:line (`SensorTools.cs:147` doesn't exist at HEAD) and is behaviour-changing (detection
> band re-tune) → its own focused slice, not a fill. So the clean next BUILD stays **B-S9** once B-S8 is green; the
> larger file-disjoint BUILDs (C-run-cost / C-staffing / C-civic, D-units, E-env) each want their own focused slice + CI
> cycle.
>
> **B-S9 SPLIT + B-S9b execute-ready ledger (2026-08-15).** B-S9a (engine live-owner cross-check `FactionAssets`) is
> built + pushed (`e2b10f7`, file-disjoint, re-gating alongside B-S8's fix). **B-S9b is the CLIENT half — purely
> `FleetWindow.cs` + the `ForceRef` selection struct, so it's blocked on B-S8 green (shares `FleetWindow.cs`).** Its
> reads are all confirmed to exist (EXISTS ledger): (1) **rows** — gather point is `DisplayAllForces()` (`:1756`, builds
> `List<RosterEntry>`); add a new `ForceDomain.Holding` + `ForceKind.Colony`/`.Station` (extend the `ForceRef` struct +
> `RosterEntry`/`ForceDomain`), iterate `FactionAssets.OwnedColonies(forceFaction)` / `OwnedStations(forceFaction)`
> (B-S9a), name via `Entity.GetDefaultName()`, location via the existing `ResolveEntityState` (B-S7), population via
> `ColonyInfoDB.Population` (Dict speciesId→count, summed) as the "strength" proxy; Health column = N/A for a holding
> (holdings aren't whole-or-dead combat units — show population, not a health %). (2) **assign-commander** — the ENGINE
> IS COMPLETE (no new engine work): a holding carrying an `admin-complex` has `AdminSpaceDB.CommanderSeats` (each an
> `AdminSpaceAbilityState` with `ComponentName` + seated `CommanderDB`/`CommanderID`, -1 = empty); the faction's
> commanders are `FactionInfoDB.Commanders` (`SafeList<Entity>`); the order is
> `AssignAdministratorOrder.Create(holdingEntity, commanderId, seat.ComponentName)` issued via
> `Game.OrderHandler.HandleOrder` (it auto-unassigns from a prior post). So B-S9b's detail panel adds a colony/station
> branch: population + installed components (reuse `componentsDB.Display`) + a per-seat commander dropdown. May split
> (rows first, assign-commander second). **D-units is NOT a CI-window fill** — the client's per-unit ground-move calls
> (`PlanetViewWindow.cs:581` `OrderMoveToGlobalHex`, `:1172` `OrderMove`) bypass the queue, but the AI's queued verb is
> a per-FORMATION `GroundOrder`, so D-units is knotted into the half-migrated M-track "one movement layer" collapse
> (M1/M9) — its own focused slice, needing the full movement rulings read first.
>
> **B-orders execute-ready ledger (2026-08-15, from `docs/combat/forceswindow.html`'s `ORDERS` array — 123 orders: 71
> LIVE / 23 DATA / 29 BUILD).** B-orders = **route the 23 DATA orders** (the engine order EXISTS with a file:line but has
> NO client UI) into the Force-Management window under a **deep categorized order menu** (by the HTML's 9 categories). ⚠
> **EXCLUDE the ~4 A4-parked STUBS** — routing them exposes no-op behavior: `RefuelAction.cs:26` (Refuel Self-Action),
> `ResupplyAction.cs:25` (Resupply Self-Action), `ServeyAnomalyAction.cs:19` (Survey Anomaly), `ShipLogisticsOrders.cs:7`
> (Ship Logistics State) — these were de-fanged in A4, their behavior parked in the ADJUDICATION QUEUE. The **~19 real
> DATA orders to route**, by category (slice by category, smallest/most-cohesive first — all share `FleetWindow.cs`, so
> slices SERIALIZE behind CI):
> • **Formation-ops (4)** — Nest Sub-Formation (`GroundForcesDB.cs:938` `SetParentFormation`), Detach Unit (`:989`
>   `UnassignUnit`), Set Formation Leader (`:999` `SetLeader`), Move Formation Tree (`:966` `OrderFormationTreeMoveToHex`)
>   → slot into `DrawBattalionOrders` (the battalion surface the roster already shows); needs a unit-picker + parent-picker.
> • **Standing-Conditional (4)** — Queue Waypoint: Move to Planetary Hex (`:342`), Queue Waypoint: Set Stance (`:345`),
>   Set Formation Order/Replace Queue (`:1060`), Pause-on-Action/Auto-Halt (`EntityCommand.cs:90`) → extend the existing
>   battalion queue panel.
> • **Movement (3)** — Intercept/Ram (`NewtonThrustCommand.cs:252`), Move Unit to Hex (`GroundForcesDB.cs:785`), Move
>   Formation to Region-Hex (`:864`).
> • **Logistics-Cargo (real ones only)** — Set Stockpile Min/Max (`LogiBaseDB.cs:17`), Resupply/Rearm Unit (`:670`
>   `ResupplyUnit`), Reload/Rearm Ordnance (via `CargoTransferOrder`).
> • **Fleet-ops (2)** — Toggle Inherit Orders (`FleetOrder.cs:121`), Dock/Undock Vessel (`DockTools.cs:148` — the new
>   Docking system).
> • **Combat (1)** — Set Target Priority (`FleetDoctrine.cs:33`).  • **Construction (1)** — Edit Production Job
>   (`IndustryOrder.cs:97`).  • **Special (1)** — Set Colony Tax Rate (`ColonyEconomyDB` TaxRate) → slots into the new
>   B-S9 Holding detail panel (a colony tax slider).
> **Recommended first slice: Formation-ops** (completes the battalion command surface the roster already exposes, one
> cohesive category). The **categorized-menu framework** (organizing all orders by the 9 categories) is the parallel UX
> deliverable — design it alongside slice 1 or as its own framework slice.
>
> **⭐ B-orders recon COMPLETE + source-verified (2026-08-15, `b-orders-recon` workflow, 7 agents, all claims re-checked
> against HEAD).** Full spec: `docs/BORDERS-RECON-SPEC.md` (below). Headlines — DONE so far: Formation-ops, tax,
> queue-stance, **Set Target Priority (engine SetTargeting + Combat-tab selector — CONFIRMED built, drop from list)**.
> **Three file-disjoint ENGINE setters buildable NOW (while `FleetWindow.cs` drains):** ① **RANK 1 `SetLogisticsOrder.
> CreateCommand_SetDesiredLevels`** (stockpile min/max write path + a `LogiBaseDB.Clone` bug fix — BUILDING NOW); ②
> **RANK 2 `CargoTransferOrder.CreateRearmFleetCommand`** (fleet ordnance-rearm helper, optional/clean); ③ **RANK 3
> `DockOrder.cs`** (new EntityCommand over `DockTools` — content-gated: no base-mod hull mounts a `DockBayAtb` yet, so
> byte-identical until a carrier hull lands). **Client-slice order (FleetWindow lane, serialized): C1 Toggle-Inherit
> (un-comment the dead wire at `FleetWindow.cs:436-447`, fix stale ids) → C2 Rearm-ground-unit (`GroundForces.
> ResupplyUnit`) → C3 Set-Formation-Order/replace-queue → C9 categorized-menu first cut → C6 Intercept/Ram (⚖ parked) →
> C7 Rearm-ordnance → C8 Dock/Undock (needs RANK 3).** `[PARALLEL]` (non-FleetWindow) client: **EP Edit-Production-Job**
> (`IndustryDisplay.cs`, engine fully built), **C4 Queue-move-to-global-hex** (`PlanetViewWindow`), **C5 Stockpile
> picker** (`LogisticsWindow`, needs RANK 1). **Corrections:** *Pause-on-Action* already has UI (`OrdersListWindow.cs:119`)
> — drop it; *Edit-Production-Job* `autoInstall` edit is a silent no-op (`IndustryTools.cs:69-73` commented) — offer
> count+repeat only. **New parked items → ADJUDICATION QUEUE** (below): the two region-local hex-move orders + Move-
> Formation-Tree (all M1-deletion-bound + queue-bypassing → AI can't drive → build the queued/global C4 instead), and
> **Intercept/Ram semantics** (it's a literal kinetic RAM, missile behaviour — ram vs match-orbit-intercept is the
> developer's call).

> **🟢 SESSION BATCH PUSHED (2026-08-15) — B-orders slices in flight.** Since resuming, seven B-orders slices are pushed
> and in CI (heavy runner contention — 7 runs queued, `rest` shards ~33 min): **RANK 1** `1736552` (stockpile write path,
> engine) · **EP** `fda7bd6` (edit-production-job) · **C1** `c8112be` Toggle-Inherit (build-client GREEN + 6/7 shards; rest
> pending) · **C4** `e57b39e` queued Move-to-hex (PlanetViewWindow) · **RANK 3** `adf189f` `DockOrder.cs` + `DockOrderTests`
> (engine) · **C2+C3** `e33487a` battalion resupply + replace-plan · **C9** `f22b419` categorized order-menu first cut.
> **NEXT (gated):** C7 Rearm-ordnance (FleetWindow — stack after C9 build-client greens) · C8 Dock/Undock (needs RANK 3
> green) · C5 Stockpile picker (LogisticsWindow — needs RANK 1 green). **C6 Intercept/Ram is PARKED** (ADJUDICATION QUEUE →
> B-ORDERS-MOVEMENT). RANK 2 (`CreateRearmFleetCommand`) judged optional + under-specified → **not built** (scope-guard;
> C7 uses the existing `CargoTransferOrder.CreateCommands`). Flip each 🔨→✅ on its green run.
>
> **UPDATE — C9 build-client GREEN + RANK 1 fully GREEN → two more slices landed.** **C9** `f22b419` build-client
> confirmed GREEN (whole client stack C1→C2/3→C9→C7 compiles). **RANK 1** `1736552` run 31879560294 **fully GREEN (all 7
> shards + build-client)** → ✅. That unblocked both: **C7** `f50e5cf` Rearm-ordnance (stacked on C9's green client base) and
> **C5** `<this commit>` Stockpile min/max picker (`ColonyLogisticsDisplay`, on the green RANK-1 engine base). **REMAINING
> B-orders: only C8** (Dock/Undock — needs RANK 3 `adf189f` green + a carrier hull to exercise; content-gated) **and C6**
> (parked). RANK 3's engine test (`DockOrderTests`) + C4/C2/C3/C7 rest-shards still finishing under contention.
>
> **UPDATE — C8 BUILT (RANK 3 engine compiles green: build-client + 6/7 shards, DockOrderTests in the last shard).** The
> C8 commit adds the Dock/Undock client UI (`FleetWindow` Logistics category, dimmed via `HasAnyCarrier`), issuing the
> RANK-3 `DockOrder`. **This completes docking cradle-to-grave** — the DockBay component is already in the Entity
> Assembler, so a player can design a carrier → build → dock/undock; content-gated/byte-identical until a hull carries a
> bay. **B-orders is now DONE except C6** (parked, ADJUDICATION QUEUE → B-ORDERS-MOVEMENT). Next: an adversarial
> runtime-review pass over the client slices (CI compiles but can't RUN the client), then Phase C.
>
> **REVIEW DONE → 2 CONFIRMED bugs fixed (adversarial workflow, 7 reviewers + verify).** **① C7 COMPILE BREAK (crash):**
> `baseStore.GetCargoables()` doesn't exist on `CargoStorageDB` — `GetCargoables()` is a member of the nested `TypeStore`
> class (the earlier grep saw the line but missed the enclosing class). C7's build-client was RED. **Fixed:**
> `baseStore.TypeStores.Values.SelectMany(ts => ts.GetCargoables().Values).OfType<OrdnanceDesign>()`. **② C1 display bug
> (minor):** `selectedFleetInheritOrders` was seeded only inside the `FlagShipID != -1` branch, so a flagship-less
> sub-fleet showed a stale checkbox. **Fixed:** seed it whenever `selectedFleetDB != null`. The other 5 reviewers found
> nothing (empty). Fix commit is FleetWindow.cs-only.

---

## HOW TO READ THE SLICE BOARD

Status: ⬜ not started · 🔨 building · ⏳CI in flight · ✅ landed (green). Each row names its owning HTML +
ladder row and, once landed, the commit sha.

---

## SLICE BOARD

### Phase A — the first-five welds (highest impact per line of code)

| Slice | What | Owning HTML / ladder | Status | Commit |
|-------|------|----------------------|--------|--------|
| A1 | Employment → morale producer (feed the dead morale term via `CrewReq`→`GetTotalJobs`, flag-gated) | `civicderived.html` / ENGINE-WIRING-BACKLOG TIER 2 | ✅ | `892924b` · calibration parked ⚖ |
| A2 | Ground `Penetration` + `PerShotEnergy` carry-through in the ground assembler path | `entityassembler.html` / ENGINE-WIRING-BACKLOG TIER 1 | ✅ | `a676efd` |
| A3 | `ShipRoleTools.ClassifyRole` + surface `GroundRoleComposer.ClassifyRole` (one helper, window+AI) | `forceswindow.html` / FORCES-WINDOW S2 | ✅ | `e91b722` |
| A4 | De-fang the 4 order stubs (no wedge/crash) + park their behavior: `RefuelAction`, `ResupplyAction`, `ServeyAnomalyAction`, `ShipLogisticsOrders` | `forceswindow.html` §10 | ✅ | `72dcc72` · 4 behaviors parked ⚖ |
| A5 | order→ability component-scan table + `AbilitiesOf(entity)` (generalize `Has*Ability`) | `forceswindow.html` §4.5 | ✅ | `761a017` |

### Phase B — the Forces window (evolve `FleetWindow.cs`, keep the class name) — ladder S1→S9

| Slice | What | Owning HTML / ladder | Status | Commit |
|-------|------|----------------------|--------|--------|
| B-S1 | Battalions tab → built `AllFormationsFor`, scope `PlayerFaction` | `forceswindow.html` / FORCES-WINDOW S1 | ✅ | `7f96ea1` (build-client green) |
| B-S3 | Make ship-combat-row + battalion-row reusable | FORCES-WINDOW S3 | ✅ | `d0f9df6` (build-client green) |
| B-S4 | One "selected unit" selection abstraction (the load-bearing refactor) | FORCES-WINDOW S4 | ✅ | `edd32d8` (build-client green) |
| B-S5 | New **All Forces** flat roster tab (filters + kind-swapping detail panel) | FORCES-WINDOW S5 | ✅ | `42d01c7` (all 7 shards + build-client green, run 31692300418) |
| B-S6 | Per-individual ground-unit rows + engine `AllUnitsFor` | FORCES-WINDOW S6 | ✅ | `0e0faaa` (all 7 shards + build-client green, run 31852230485) |
| B-S7 | Civilian-ship detail panel (promote logistics manifest/routes) | FORCES-WINDOW S7 | ✅ | `e894b12` (all 7 shards + build-client green, run 31853742806) |
| B-S8 | Aggregate Health + Fuel accessors (the missing ship gauges) | FORCES-WINDOW S8 | ✅ | engine `ShipHealth` + `ShipHealthTests` + roster Health column + Fuel readout; `d369ebb` rest RED (defensive test NRE on unmanaged `Entity.Create()`) → guarded `Manager == null` in `5e42d87` (all 7 shards + build-client green, run 31856896871) |
| B-S9a | Engine live-owner cross-check (`FactionAssets.OwnedColonies`/`OwnedStations`) + gauge | FORCES-WINDOW S9 | ✅ | engine `FactionAssets` + `FactionAssetsTests` (all 7 shards + build-client green, run 31857203852) |
| B-S9b-1 | Stations + colonies as roster ROWS (Domain "Holding") + Holding detail panel | FORCES-WINDOW S9 | ✅ | `271f987` (all 7 shards + build-client green, run 31858039704) |
| B-S9b-2 | Assign-commander UI (seats + `AssignAdministratorOrder`) | FORCES-WINDOW S9 §4.4 | ✅ | `21b164f` (all 7 shards + build-client green, run 31859301590) |
| B-orders | Route the 23 button-only DATA orders + deep categorized menu | `forceswindow.html` §10 | 🔨 | ledger in NEXT ACTION: ~19 real DATA orders (4 A4-stubs EXCLUDED) across 9 categories; slice by category, serialized behind CI (shares `FleetWindow.cs`) |
| B-orders-fops | Formation-ops (Nest / Set Leader / Detach) into the battalion surface | `forceswindow.html` §10 Formation-ops | ✅ | `0e04c9b` (run 31876370261 green); Move-Formation-Tree deferred (needs hex picker) |
| B-orders-tax | Set Colony Tax Rate — a tax slider on the Holding detail panel | `forceswindow.html` §10 Special | ✅ | `188f3b9` (run 31876531200 green) |
| B-orders-qstance | Queue a stance-change waypoint (Standing-Conditional) | `forceswindow.html` §10 Standing-Conditional | ✅ | `fb54aba` (run 31876634046 green); hex-move waypoint deferred (needs picker) |
| B-orders-targeting | Set Target Priority — engine setter (gauge-before-UI) | `forceswindow.html` §10 Combat | ✅ | `8c4d616` (run 31876849971 green) — `FleetDoctrine.SetTargeting` + `FleetDoctrineTests.SetTargeting_*` |
| B-orders-targetui | Set Target Priority — the Combat-tab client selector | `forceswindow.html` §10 Combat | ✅ | `f189805` (green) — `DisplayTargetPrioritySelector` (mirrors EMCON selector) → `FleetDoctrine.SetTargeting` |
| B-orders-inherit (C1) | Toggle Inherit Orders (sub-fleet) | `forceswindow.html` §10 Fleet-ops | 🔨 | `c8112be` (build-client GREEN + 6/7 shards; rest shard running) — client `FleetWindow.cs`: un-commented the dead Standing-Orders wire, repaired the stale `FleetDB.Parent.Guid` check (now the `TreeHierarchyDB.Parent` sub-fleet test) → `FleetOrder.ToggleInheritOrders` |
| B-orders-resupply (C2) | Resupply / rearm ground battalion | `forceswindow.html` §10 Special | 🔨 | client `FleetWindow.cs` `DrawBattalionOrders` — "Resupply battalion" button iterates `GroundFormationTools.MembersOf` → `GroundForces.ResupplyUnit(body, u)` (tops ammo to full on friendly-held ground, else 0); stacks on C1 (build-client green) |
| B-orders-replaceplan (C3) | Set Formation Order — replace vs append plan | `forceswindow.html` §10 Ground-move | 🔨 | client `FleetWindow.cs` `DrawBattalionOrderQueue` — "Replace plan" checkbox routes every plan button through a `Plan()` dispatcher: `GroundForces.SetFormationOrder` (replace whole queue) vs `QueueFormationOrder` (append); stacks on C1 (build-client green) |
| B-orders-catmenu (C9) | Categorized order menu — first cut | `forceswindow.html` §10 / `FORCES-WINDOW-DESIGN` §10 | 🔨 | client `FleetWindow.cs` — new `DrawCategorizedOrderList` reorganizes the flat Issue-Orders left list into Movement/Survey/Logistics CollapsingHeaders; capability-gated orders (Geo/Grav survey, troop lift) now DIM (BeginDisabled + greyed reason) instead of HIDE — cradle-to-grave made legible. `IssueOrdersDisplay` byte-identical (same `selectedIssueOrderType` targets). Engine untouched. Follow-ups F2/F3: re-shelve battalion orders + route every kind through the tree |
| B-orders-rearm (C7) | Rearm ordnance at a base | `forceswindow.html` §10 Logistics | 🔨 | client `FleetWindow.cs` — `IssueOrderType.RearmAt` + Logistics-category Selectable + `IssueOrdersDisplay` case: mirrors Refuel-at (warp fleet to base + `CargoTransferOrder.CreateCommands` WaitTillFull), but lets the player pick BASE **and** ORDNANCE (`OrdnanceDesign` is `ICargoable`; enumerate base stock via `GetCargoables().OfType<OrdnanceDesign>()`, per-ship free space via `GetFreeUnitSpace`) so a magazine never fills with rounds the launchers can't fire. Two-stage (fills the hold; launcher mag reloads from it). No engine change; RANK 2 helper NOT needed. Stations-as-base + auto-pick-launcher-ordnance are follow-ups. Stacks on C9 (build-client green) |
| B-orders-dock (C8) | Dock / undock vessels (client UI over RANK 3) | `forceswindow.html` §10 Carrier-ops | 🔨 | client `FleetWindow.cs` — `IssueOrderType.Dock` (dimmed in Logistics via `HasAnyCarrier`) + `IssueOrdersDisplay` case: per carrier ship (`DockTools.Capacity > 0`) shows berth used/door + `DockTools.DockedShips` (Undock) + the fleet's other ships (Dock, greyed with `CanDock`'s reason). Issues `DockOrder.Dock/Undock` (RANK 3). **Cradle-to-grave: the DockBay component is already offered in the Entity Assembler, so this makes docking fully playable** (design a carrier → dock → undock). Content-gated/byte-identical until a hull carries a bay. Stacks on C9 (build-client green) + RANK 3 (engine compiles green) |
| B-orders-hexmove (C4) | Queue Waypoint: Move to planetary hex (GLOBAL) | `forceswindow.html` §10 Ground-move | 🔨 | `[PARALLEL]` client `PlanetViewWindow.cs` `DrawOrderQueue` — "+ Move to hex (Q,R)" button reuses the globe's last-clicked `_selGQ/_selGR` → `GroundForces.QueueFormationOrder(f, GroundOrder.MoveHex(q,r))` (cylinder coords, One-Verb-Both-Seats queued path; region-local `OrderMoveToHex` deliberately not used). Also fixed the false "Shift-click a hex" hint. File-disjoint from `FleetWindow.cs` |
| B-orders-stockpile-eng | Set Stockpile Min/Max — engine write path (RANK 1) | `forceswindow.html` §10 Logistics | ✅ | `1736552` (run 31879560294 — ALL 7 shards + build-client GREEN) — `SetLogisticsOrder.CreateCommand_SetDesiredLevels` (writes `LogiBaseDB.DesiredLevels`) + `LogiBaseDB.Clone` bug fix + `SetLogisticsOrderTests` |
| B-orders-stockpile-ui (C5) | Set Stockpile Min/Max — client picker | `forceswindow.html` §10 Logistics | 🔨 | `[PARALLEL]` client `ColonyLogisticsDisplay.cs` — a "Stockpile Targets (min/max)" panel above the import/export columns: lists `LogiBaseDB.DesiredLevels` (each with a Remove → sets (0,0)) + a resource combo + min/max `InputInt`s + Set → `CreateCommand_SetDesiredLevels` (RANK 1, green). File-disjoint from `FleetWindow.cs` |
| B-orders-dockorder (RANK 3) | Dock / Undock vessel — engine order wrapper | `forceswindow.html` §10 Carrier-ops | 🔨 | `[PARALLEL]` new engine `GameEngine/Docking/DockOrder.cs` (`EntityCommand`, InstantOrder, mirrors `FleetOrder`) → `DockTools.TryDock`/`Undock`; own-the-carrier validity; non-throwing `Clone` + `DockOrderTests` (4 cases). Content-gated: no base-mod hull mounts a `DockBayAtb` yet, so byte-identical. Unblocks client C8. File-disjoint from `FleetWindow.cs`/`PlanetViewWindow.cs` |
| B-orders-editjob | Edit Production Job (count + repeat) | `forceswindow.html` §10 Construction | 🔨 | client `IndustryDisplay.cs` per-job row — inline count field + repeat toggle → `IndustryOrder2.CreateEditJobOrder` (engine fully built); `[PARALLEL]` (not `FleetWindow.cs`); autoInstall edit is a no-op (engine gap), so count+repeat only |

### Phase C — the designers + assembler (12 door HTMLs + `entityassembler.html`)

| Slice | What | Owning HTML / ladder | Status | Commit |
|-------|------|----------------------|--------|--------|
| C-run-cost | TIER 2.5 run-cost vector (power/jobs/food/upkeep) | ENGINE-WIRING-BACKLOG TIER 2.5 | ✅ | **ALL FIVE RUNGS LANDED (2026-08-16).** Jobs ✅ (A1); Staffing ✅ (C-staffing); Upkeep ✅ (`ColonyEconomyProcessor.BillInstallationUpkeep`, `77c8411`); **Power ✅** — mechanism `d2ac424` (`IndustryTools.PowerEfficiency` throttle) + make-live `927deef` (`PlanetInstallation` on the fission reactor mount + reactor on Earth → 75 MW vs ~52 MW, gauge `PowerThrottleTests`); **Food ✅** — make-live `0f088a1` (4 agri-complexes on Earth + `SustenanceProcessor.EnableFoodDemand` + `DefaultPerCapitaFoodDemand` 1e-6, food-positive 2.44×, gauge `FoodDemandTests`). Food-baseline test fixes: `65ab0dc`+`cf6638b` (6 pre-existing food tests needed `TestScenario.StripFoodProduction`). All CI-green through `cf6638b` |
| C-staffing | TIER 2.6 workforce→production staffing model | ENGINE-WIRING-BACKLOG TIER 2.6 | 🔨 | engine — `IndustryTools.StaffingEfficiency` = `min(1, ManpowerTools.AvailableWorkforce ÷ GetTotalJobs)`, a SECOND multiplier on the production rate at `ConstructStuff` (parallel to infra efficiency); flag `EnableWorkforceStaffing` default OFF (byte-identical) flipped ON by `NewGameMenu` (both start paths, A1 pattern); shares the ONE `GetTotalJobs` producer with the employment term; `ManpowerTools.AvailableWorkforce` (Manager-guarded, −1 = no pool → inert like the crew gate). Gauge `WorkforceStaffingTests` (full/half/zero/flag-off/no-pool). Engine-only; file-disjoint from the B-orders client lane |
| C-guided | Guided-weapon real warhead (item #3) | `entityassembler.html` / TIER 3 #3 | ⚖ parked | ordnance-at-build decision — ADJUDICATION QUEUE (rec: Option A) |
| C-sensors | Sensors band-match fix (item #4) | `sensorsderived.html` / TIER 3 #4 | ⬜ | ⚠ backlog file:line STALE + behaviour-changing (band re-tune) — needs a focused slice |
| C-mobility | `GroundMobility.SpeedMultForUnit` scale-not-replace (item #5) | `propulsionderived.html` / TIER 3 #5 | ⚖ parked | combine = developer's call — ADJUDICATION QUEUE (rec: multiply) |
| C-deadknobs | TIER 4 dead-knob adjudication (items 6–11 — most are STOP items) | ENGINE-WIRING-BACKLOG TIER 4 | ⬜ | |
| C-civic | New civic dials per IO census (`01-IO-civic.md`) | `civicderived.html` / 02-IO-MATRIX | ✅ | **CIVIC DOOR COMPLETE (2026-08-16).** All 9 civic jobs (`01-IO-civic.md` §A.1) map to a LIVE wire, and the three the census flagged "engine-pending" are now all built: **Jobs** = A1 employment (✅, producer = `GetTotalJobs` falls back to each installation's `CrewReq`, verified `ComponentInstancesDBExtensions.cs:37`); **Security → legitimacy** (`SecurityAtbDB` → `GetTotalSecurity` → flag-gated `LegitimacyInputs.SecurityStrength`, `security-precinct` buildable, gauge `SecurityLegitimacyTests`, `a7b9981`, CI-green); **Medical → health → morale** (`MedicalAtbDB` → `GetTotalMedical` → flag-gated `MoraleInputs.HealthStrength`, `medical-hospital` buildable, gauge `MedicalMoraleTests`, **`daae8f2` CI-green**). Both new dials flag-gated OFF + `NewGameMenu`-on + buildable-not-installed (out of the shared-fixture baseline). The other 6 jobs were already live: Food (SustenanceProcessor, this campaign), Life-support + Space-habitat (`PopulationSupportAtbDB`), Officers (`NavalAcademyAtb`), Residency + Recreation/"Amenity" (comfort → `GetHousingComfort`), Administration (`AdminSpaceAtb` span). **NO "Commerce" civic dial exists** — verified zero `Commerce` refs in GameData; per `01-IO-civic.md` A.1 group 2 + §C, Commerce is only an *academy leader-domain* (cosmetic school-naming that maps to the Command door) and trade→income belongs to the **Economy door**, not civic. The earlier "Commerce dial" was a mis-derivation. **One civic item PARKED for the developer:** space-habitat mass-pricing (the `fix`-checkbox proposal, `01-IO-civic.md` §D + §B) — a 1,000,000-colonist station costs the same 1 t as an empty one; pricing it is a design decision, → ADJUDICATION QUEUE |

### Phase D — planetary view (`planetview.html` → client planet view + engine) — ladder U1→T2

| Slice | What | Owning HTML / ladder | Status | Commit |
|-------|------|----------------------|--------|--------|
| D-units | U1→T2: select/read/move one unit via the ONE queued verb (kill direct-call bypass `PlanetViewWindow.cs:581`) | `planetview.html` / UNITS-ON-THE-MAP | ✅ loose-unit march | **DONE 2026-08-16 (developer ruling: the formation is the unit of movement — see ADJUDICATION QUEUE: D-UNITS RESOLVED).** The two loose-unit bypass sites (`MoveSelectedToGlobalHex`/`MarchSelectedTo`) now AUTO-WRAP the selection into a formation (`WrapSelectionIntoFormation`, reuse-or-create) → `GroundForces.SetFormationOrder(MoveHex/MoveRegion)` — the SAME queued verb the AI drives. Client-only, engine byte-identical (build-client is the gate; runtime is the developer's build). **Follow-up (own slice):** the formation-march buttons still call direct `OrderFormationMove` (`:1572` + FleetWindow) — convert to the queued verb for full One-Verb consistency (immediate→queued behavior change). <br>**(recon, kept)** Mechanism BUILDABLE, no new engine ruling: the queued MOVE verb both seats drive (`GroundFormation.Orders` → `QueueFormationOrder`/`SetFormationOrder` → `GroundForcesProcessor.cs:918-922` → `OrderFormationMoveToGlobalHex`; AI at `GroundTacticalBrain.cs:207`) is **formation-only**; the direct single-unit APIs (`GroundForcesDB.cs:755/785/823`) were the bypass. Mechanism BUILDABLE now, no new engine ruling: the queued MOVE verb both seats drive (`GroundFormation.Orders` → `QueueFormationOrder`/`SetFormationOrder` → `GroundForcesProcessor.cs:918-922` → `OrderFormationMoveToGlobalHex`; AI at `GroundTacticalBrain.cs:207`) is **formation-only**. The two bypass sites (`PlanetViewWindow.cs:581` `OrderMoveToGlobalHex`, `:1172` `OrderMove`) march **loose single units** via direct static calls (`GroundForcesDB.cs:755/785/823`). **BLOCKED on the loose-unit-move POLICY ruling** (auto-wrap into a one-member formation = AI's own `FormUpLoose` pattern / real per-unit queue / can't-move-loose) → **ADJUDICATION QUEUE: D-UNITS.** Rec: b1 auto-wrap. |
| D-stockpile | Per-hex stockpile + hex-to-hex haul (resource-locality ruling) | UNITS-ON-THE-MAP | ⬜ | |
| D-planfn | Remaining PLANETARY-FUNCTIONAL-PLAN slices the HTML badges call for | PLANETARY-FUNCTIONAL-PLAN-2026-07-27 | ⬜ | |

### Phase E — resolver environment hooks (`resolversim.html` + ENVIRONMENT-CONDITIONS-DESIGN)

| Slice | What | Owning HTML / ladder | Status | Commit |
|-------|------|----------------------|--------|--------|
| E-env | `CombatConditions` read into the shared kernel (space combat stops being env-blind) | `resolversim.html` / ENVIRONMENT-CONDITIONS-DESIGN | 🔨 | **Design-LOCKED (accepted resolver hooks). Slice 1 DONE 2026-08-16:** `Combat/CombatConditions.cs` — the value struct (Detection/Accuracy/Closing/Firepower/ShieldRegen/Cover ×mults + ambient DoT) + `FromHazard(HazardModifiers)` pure translation of the live `SpaceHazardTools` query + `ReadAt(system, position)` (the "wire" the design Part 2 named as the gap). **NO combat caller yet → byte-identical** (buildable-not-installed pattern). Gauge `CombatConditionsTests` (pure translation + a real gas cloud end-to-end). **NEXT (slice 2):** store on `FleetCombatStateDB` + read the Accuracy coefficient into the shared `CombatKernel.HitFraction` (the keystone — one edit moves BOTH ship + ground resolvers), flag-gated OFF + `NewGameMenu`-on; then slice 3 the space firepower/shield hooks (`CombatEngagement.cs:759`/`ApplyShield`) |

---

## ADJUDICATION QUEUE (items parked for the developer — §6 STOP conditions)

### ✅ RESOLVED — the two PRE-EXISTING base-red economy tests are FIXED (developer authorized "do the data design call", 2026-08-13)

**Plain English:** when this campaign branched, two tests were **already red** (proven: the docs-only opening commit
failed them identically). The developer authorized the data-design call, so both are now fixed. **Both turned out to
be TEST bugs — the engine and data were CORRECT** — where an older test expectation met a newer, deliberately-built
feature (a "merge-semantic break"). Both fixes are **test-only and byte-identical** (zero engine/data/JSON change),
verified by a parallel investigation workflow + independent source reads that agreed exactly.

1. **`CargoCompartmentTests.EveryResource_IsConsumedBySomething`** — *was Expected: not null, But was: null.* The
   failing assert was **not** the "unconsumed resource" check (that passes); it was the **grade-ladder shape check**
   (`:508-525`) which looks up four deliberately-unwired materials (`stainless-steel-d/-a`, `electronics-d/-a`) via
   the faction's **UNLOCKED** store `data.CargoGoods`. Those four sit on the test's own "awaiting-a-mechanic" list —
   they have no build-with-grade mechanic yet, so they are correctly **never unlocked**, so `CargoGoods.GetAny`
   returns null (a faction's `CargoGoods` starts empty; all materials live in `LockedCargoGoods` until unlocked —
   `FactionDataStore.cs:41/98-99`). **Fix:** the ladder lookup falls back to `LockedCargoGoods` so the shape check
   runs against the authored blueprint regardless of unlock state. **Data call: the four materials STAY LOCKED** —
   unlocking them would create "refining jobs you can queue forever for no reason", the exact thing this audit
   condemns.
2. **`FoodProductionTests.FoodProduction_GraveRung_DestroyingTheFarmReturnsStarvation`** — *was Expected: 1.0, But
   was: 0.0.* The engine is correct: while the farm ran (5000/day grown vs 2000/day eaten),
   `SustenanceProcessor.BankFoodSurplus` banked the ~90,000-unit surplus into the colony's cold store — a
   **deliberate food-supply-line buffer** (its own gauge: `Food_IsAShippableGood_…`). So a destroyed farm doesn't
   starve the colony *instantly*; it starves once reserves run out. The old test expected instant starvation. **Fix:**
   the grave-rung gauge now **drains the banked reserve** after destroying the farm, then asserts total shortage — the
   TRUE grave condition (no production AND no reserves). Food stays a losable capability; the buffer stays intact.

**Files:** `Pulsar4X.Tests/CargoCompartmentTests.cs` (grade-ladder lookup → locked-store fallback) ·
`Pulsar4X.Tests/FoodProductionTests.cs` (drain the banked food before the grave assert; `+using Pulsar4X.Storage`).

**THE CAMPAIGN VERIFICATION PROTOCOL — UPDATED (the base is now GREEN):**
> A slice is **CLEAN** iff its CI run is **fully green — `build-client` + all seven test shards, zero failures.**
> (Before this fix the protocol tolerated exactly two known `rest`-shard failures; that carve-out is retired — any
> red is now real.) Check with GitHub MCP `get_job_logs` on any failed job.

---

### ✅ A1-CALIBRATION — RESOLVED: the employment→morale denominator (was parked 2026-08-13, done 2026-08-13)

**Plain English:** A1 wired up the "do people have jobs?" morale term. The engine now counts a colony's jobs by
adding up every building's operating-crew requirement. The problem: those crew numbers were written as "how many
people it takes to RUN the building" (a factory might list a few thousand, a spaceport up to a million), while a
homeworld has **billions** of people. So "jobs ÷ workforce" comes out near zero — the game would read almost every
colony as **near-total unemployment** and dock up to −40 morale everywhere the moment the term is switched on.

That's why A1 shipped with the term **flag-gated OFF by default** (byte-identical — nothing changes in a current
game). The code works and is tested; what needs YOUR call is the *number*, before the flag is turned on for real.

**The question:** when we turn employment-morale on, what should "full employment" mean?
- **Option A — keep the full workforce as the denominator, and re-tune the building CrewReq numbers.** ❌ **REJECTED.**
  CrewReq is *shared* — the ship-crew gate (`ManpowerTools.ResolveBuild`) and talent draws read the same number, so
  inflating it to billions-scale corrupts ship crewing (Prime-Directive violation). And it can't scale: one factory
  can't employ 4.1 billion; you'd need absurd per-building numbers that still don't track population.
- **Option B — a per-capita job DEMAND denominator.** ✅ **CHOSEN.** Measure jobs against `population × JobsPerCapita`
  — the SAME shape the trusted `SustenanceProcessor` uses for food/power. The denominator now **scales with
  population**, so it isn't a magic number tied to one colony's size; a populous colony that under-builds industry
  reads a deficit, building more climbs toward the +15 bonus.
- **Option C — leave it OFF.** Superseded; the calibration is done (below), but the live on-switch stays the
  developer's (see the flag note).

**✅ RESOLVED (developer authorized "do the morale cal", 2026-08-13).** Chose **Option B**, verified by a parallel
investigation workflow + an adversarial review (which caught two defects in the first draft — a `0`-default that would
have red-lit the existing flag-gate test, and a fragile band assertion — both fixed). **The number:** the fully-built
start homeworld has **~52,000** installed jobs against 8.2 billion people; `JobsPerCapita = 7.0e-6` makes the per-capita
demand ≈ 57,400, so the homeworld reads a ratio just under 1 → a **mild employment deficit (near-neutral)** — lifted off
the −25 catastrophe, and NOT the **earned** +15 full-employment bonus (a thriving world is earned by over-building, per
the locked design). `JobsPerCapita` is a **mutable static** (`ColonyMoraleDB.JobsPerCapita`) so a scenario node / the
DevTools Society lever can retune it. **Files:** `ColonyMoraleDB.cs` (the coefficient) · `PopulationProcessor.cs` (both
morale sites) · `StationPopulationProcessor.cs` (the station site) · `EmploymentMoraleTests.cs` (the calibration band +
homeworld readout gauge). **✅ TURNED ON for the real game (developer-authorized 2026-08-13):** the engine flag
`PopulationProcessor.EnableEmploymentMorale` still **defaults OFF** (so the ENGINE test suite stays byte-identical — it
builds colonies via a factory, never the menu), but **`NewGameMenu` now flips it ON** for a menu-started game (both
`CreateGameCore` + Quickstart), the SAME default-off/menu-on pattern as `EnableGroundTacticalAI` /
`LegitimacyProcessor.ReadCurrentMorale`. So a real New Game runs the employment term live. **The live feel is the
developer's PC play-test** (CI can't run the client): read the `[a1-employment] HOMEWORLD …` gauge in `game_logs/` and
watch the homeworld's morale/population; tune `JobsPerCapita` (raise → more of a deficit; lower → nearer full employment;
too low snaps to the +15 bonus). One line in `NewGameMenu` reverts it if the live feel isn't right.

---

### ⚖ A4-ORDERS — what should the four "issue-and-do-nothing" orders actually DO? (parked 2026-08-13)

**Plain English:** the Forces window offers (or will offer) four orders that were never finished — they issue and do
nothing. A4 made them **SAFE** (they can no longer jam a fleet's order queue or crash the game clock — real bugs
that are now fixed). But making each one actually *work* needs a design call from you, because each is genuinely
ambiguous:

1. **Refuel a fleet in place** — WHAT is a "supply source" (a friendly colony/station you're parked at? a fleet
   tanker ship?), and must the fleet be physically close enough to receive fuel? (The cargo-transfer code does no
   distance check, so without a rule it would teleport fuel from anywhere.) **My recommendation:** refuel from a
   *co-located* friendly colony/station (same body) that holds the fuel — the natural "top off at the base you're
   at" — reusing the existing, proven `CargoTransferOrder.CreateRefuelFleetCommand`. Say the word and I finish it
   (also needs a one-line client fix so the menu passes the fleet to the order).
2. **Resupply a fleet** — this one has no defined meaning yet. Is "resupply" = reloading missile/ordnance
   magazines? Or an Aurora-style "maintenance supply point" resource that **doesn't exist in this engine at all**?
   **My recommendation:** define it as ordnance/magazine reload (the only thing the engine can actually move), and
   I'll build a `CreateResupplyFleetCommand` mirroring the refuel one.
3. **Survey an anomaly** — this is a near-duplicate of the already-working jump-point survey order (an "anomaly" is
   the same kind of surveyable point). **My recommendation:** DELETE this half-built duplicate and route "survey
   nearest anomaly" through the existing survey order — duplicating survey logic just invites the two to drift.
4. **Ship logistics (per-ship)** — the automated freight market already moves cargo (the base-side bidding loop
   does the real work), so this per-ship order is a *display shim*. **My recommendation:** leave it a shim (it's
   correct as-is); building a second per-ship state machine would duplicate the working market. Only revisit if you
   want manual per-ship logistics control.

**Meanwhile:** Refuel/Resupply currently complete as safe no-ops if issued. If you'd rather they NOT appear in the
menu until finished, I can pull them from the client's order list — your call (I recommend finishing #1 instead).

---

### ⚖ C-GUIDED — which ordnance does a missile ship's auto-resolve firepower assume? (parked 2026-08-15, §6 #3)
**Plain English:** Right now the auto-resolver rates every missile/torpedo launcher at a flat stub (100 kJ/s) instead
of the real warhead, so torpedo ships read far weaker than they are (`entityassembler.html` flags this outright; TIER 3
#3). The obvious fix — "read the mounted warhead" — hits a wrinkle: a launcher's ordnance
(`MissileLauncherAtb.AssignedOrdnance`) is a **runtime loadout** the player/AI assigns later, but a ship's combat value
(`ShipCombatValueDB`) is computed **once at build**, when no ordnance is loaded yet. So at the one read site there is
nothing to read.

**The decision (needs the developer):** which warhead should a missile ship's firepower use?
- **Option A (recommended): read a REPRESENTATIVE warhead at build** — the faction's default/available ordnance design
  for that launcher size — as a proxy for "what this launcher fires." Simple, one read site, no recalc machinery, and it
  fixes the under-count immediately. Downside: it doesn't track a mid-game loadout swap.
- **Option B: recalc combat value when ordnance is assigned** — honest per-loadout, but needs the parked
  "recalc-`ShipCombatValueDB`-on-change" hook (Combat gotcha #2), a bigger wire touching the whole combat-value caching
  model.
- Either way, warhead ENERGY = TNT-equiv mass × ~4.184e6 J/kg, then divided down to the beam kJ–MJ scale (the same open
  calibration `MissileImpactProcessor` carries — I'd mirror its divisor, not invent one).

**Recommendation:** Option A now (a representative-ordnance read at build, calibrated off the impact processor's scale),
with Option B flagged as the v2 loadout-accurate follow-up. Gauge: a torpedo ship's `ShipCombatValueDB.Firepower` scales
with its warhead choice, not the constant. Say the word and I build A.

### ⚖ C-MOBILITY — how should a designed drive combine with the frame's locomotion mode? (parked 2026-08-15, §6 #3)
**Plain English:** The propulsion door sells the four frame locomotion modes (Foot ×1 / Tracked ×2 / Walker ×1.5 /
Hover ×3) as "modes the simulation already reads." But `GroundMobility.SpeedMultForUnit` **replaces** the frame mode with
a mounted drive's `SpeedFactor` outright — so the moment a unit carries a designed drive, whether its frame is Foot or
Hover stops affecting speed at all (the four modes become dead weight; flagged in `GroundCombat/CLAUDE.md` + TIER 3 #5).
The backlog names the fix as **the developer's call on the exact combine.**

**The decision (needs the developer):** how do the frame mode and the designed `SpeedFactor` combine?
- **Option A (recommended): MULTIPLY** — `speed = frameMode × SpeedFactor`. A Hover chassis with a good drive is faster
  than a Foot chassis with the same drive; the frame choice stays a real decision. Simplest, matches "the frame matters."
- **Option B: a weighted blend** (e.g. `frameMode × (1 + w·(SpeedFactor−1))`) — lets the designed drive dominate but not
  erase the frame. More dials, needs a weight.

**Recommendation:** Option A (multiply). It's the smallest change (one line), makes frame choice matter again, and is
the natural reading of "modes the sim reads." Behavior change → re-baseline the mobility gauges. Say multiply (or a blend
weight) and I build it.

> **Note on TIER 3 #4 (sensors band-match):** the backlog cites `SensorTools.cs:147`, but that path/line is STALE — the
> band-match code isn't there at HEAD (grep before you trust). It's also a *behaviour-changing* detection fix (the ⚠:
> "expect to re-tune emitter/receiver bands so detection works by design not by bug"), so it warrants its own focused
> slice with a sensor-band data pass, not a rushed fill. Locating the real band-match site + checking whether the base
> data relies on the bug is the first step when Phase C reaches it.

### ⚖ B-ORDERS-MOVEMENT — Intercept/Ram semantics + the region-local hex-move orders (parked 2026-08-15, §6)
**Plain English:** three B-orders from `forceswindow.html` §10 have no safe default, so they're parked instead of guessed:

1. **Intercept / Ram Target** (C6, `NewtonThrustCommand.cs:252`). The existing engine verb is a **literal kinetic RAM** —
   it drives a ship at another to *collide*, missile-style. "Intercept" in most 4X games means *match orbits / close to
   weapons range and hold*, which is a different order entirely. **The decision:** does the player-facing "Intercept" wire
   to (a) the literal ram (a suicide/kinetic-weapon order — rare, needs a confirm), (b) a NEW match-orbit-and-hold intercept
   (close to a chosen range, stay there — the common meaning, needs a small new order), or (c) both, as two distinct
   buttons? **Recommendation:** build (b) match-orbit intercept as the default "Intercept", and expose the literal ram only
   as an explicit, confirm-gated "Ram" (it destroys your ship too). Don't wire the bare ram to a button labelled
   "Intercept" — that mislabels a suicide order. Say which and I build it.

2. **Move Unit to Hex within Region** (`OrderMoveToHex`, `GroundForcesDB.cs:785`) and **Move Formation to Region-Hex**
   (`OrderFormationMoveToHex`, `:864`). Both drive the **region-LOCAL hex layer slated for DELETION** under the 2026-07-28
   M1 "one movement layer" ruling, and both are **direct calls that bypass the order queue → the AI cannot issue them**
   (One-Verb-Both-Seats violation). **Practical resolution already taken:** C4 built the *queued GLOBAL* hex move
   (`GroundOrder.MoveHex` → `OrderFormationMoveToGlobalHex`), which both seats can drive. **The decision:** given M1 deletes
   the region-local layer, do you want *any* per-region hex-move UI, or should hex moves route **exclusively** through the
   queued global path (C4)? **Recommendation:** global-only (C4); leave the region-local orders unwired for the M1 deletion.

3. **Move Formation Tree** (`OrderFormationTreeMoveToHex`, `GroundForcesDB.cs:966`) — a **direct, immediate, non-queued**
   whole-tree march in **region-LOCAL** coords, so it both bypasses the queue (AI can't issue) AND uses a different
   coordinate space than the global queued MoveHex. **The decision:** is an immediate whole-tree march wanted at all, and
   if so should it be re-expressed as a *queued global* order so both seats can drive it? **Recommendation:** re-express as
   queued-global (a `GroundOrder.MoveTreeHex` twin) if wanted; don't wire the region-local direct version.

### ✅ D-UNITS — RESOLVED (developer ruling 2026-08-16): the FORMATION is the unit of movement
**The ruling (verbatim intent):** *"the battalion/fleets is how you move units; if you want to move multiple, split the
fleet/battalion."* So: **b1 = YES** (a loose unit you march AUTO-WRAPS into a one-member formation — my rec, "go with your
idea"); **b2 = NO** (no per-unit order queue); **b3 = YES** (to move a subset, split the formation). Unified: ground
movement mirrors space — you command FORMATIONS through the ONE queued verb, exactly like fleets.

**BUILT (2026-08-16, client-only, engine byte-identical):** the two loose-unit bypass sites in `PlanetViewWindow.cs`
(`MoveSelectedToGlobalHex` :581 `OrderMoveToGlobalHex`, `MarchSelectedTo` :1172 `OrderMove`) now route through a new
`WrapSelectionIntoFormation` helper → `GroundForces.SetFormationOrder(formation, GroundOrder.MoveHex/MoveRegion)` — the
SAME queued verb the AI drives (`GroundTacticalBrain` → `SetFormationOrder`), carrying the Player issuer marker. The
helper reuses the formation the selection already shares (no duplicate battalion per march) else auto-wraps a new one
(exactly as the "Form up" button does). One verb, both seats. **Follow-up flagged (own slice):** the formation-march
buttons still call the DIRECT `GroundForces.OrderFormationMove` (`PlanetViewWindow.cs:1572` + FleetWindow
`DrawBattalionOrders`) — a smaller, formation-level bypass; convert those to the queued `SetFormationOrder`/`QueueFormationOrder`
for full One-Verb consistency (a multi-window behavior change: immediate → queued, so its own slice + the developer's
nod on the timing shift). CLIENT-TEST-CHECKLIST: "D-units — loose-unit march auto-wraps + queues."

<details><summary>(historical) the parked policy question, for the record</summary>

**Plain English:** Phase D-units means "move one ground unit through the ONE queued verb both the player and the AI
use." The recon (file:line-verified) found the mechanism is **buildable now with no new engine ruling** — BUT it turns
on one policy the locked rulings *imply* yet never state, so it's parked instead of guessed.

**What the recon found.** The queued MOVE verb both seats already drive is **formation-only** — it lives on
`GroundFormation.Orders` (`GroundForcesDB.cs:416`), issued by `QueueFormationOrder`/`SetFormationOrder`
(`:1052`/`:1060`), popped by `GroundForcesProcessor` (`:918-922`) to `OrderFormationMoveToGlobalHex`. The AI drives it
per-battalion (`GroundTacticalBrain.cs:207`, after `FormUpLoose` packs loose units into battalions). A **single loose
unit has NO queued path** — the only single-unit move APIs (`OrderMove` `:755`, `OrderMoveToHex` `:785`,
`OrderMoveToGlobalHex` `:823`) are **direct static calls**, which are exactly the two client bypass sites the campaign
names: `PlanetViewWindow.cs:581` (`OrderMoveToGlobalHex`, loops loose units) and `:1172` (`OrderMove`). A compliant
queued FORMATION path already exists in that same window (`:1457` `QueueFormationOrder(GroundOrder.MoveHex(...))`).

**The decision:** when the player clicks a LOOSE unit (one not in any formation) and marches it, do we —
- **(b1) auto-wrap** the selected loose unit(s) into a one-member `GroundFormation`, then issue the existing queued
  `MoveHex` — keeps the "click a unit → march" UX but routes it through the One-Verb queued path; **this is exactly
  what the AI already does** (`FormUpLoose` → move battalion); or
- **(b2)** build a genuine **queued per-unit** `GroundOrder` queue on `GroundUnit` (heavier; cuts against the
  data-object model and M18's per-battalion order set; "per-unit orders" is already tagged a follow-up in
  `docs/CLIENT-TEST-CHECKLIST.md` + `forceswindow.html` "units have no such verb"); or
- **(b3)** the player simply **cannot** march a loose unit — the two bypass buttons are removed, and the player forms
  up first (exactly the AI's constraint), moving only formations.

**Recommendation: (b1) auto-wrap.** It satisfies One-Verb-Both-Seats (the player and AI now share the identical
primitive — form up, queue MoveHex), preserves the existing click-to-march feel, reuses live CI-tested code, and is
the literal application of the developer's own law ("the AI forms loose units into battalions, then moves the
battalion — the player does the same"). Cost: a march on a loose unit now creates a formation (it shows in the
Battalions roster and picks up formation stance/ROE) — the same thing that happens to the AI's units, so it's
consistent, but it *is* a save/roster-visible behavior change, which is why it wants your nod. **Say b1 / b2 / b3 and
I build it. Alternatively, Phase E (E-env) needs no movement ruling — I can take that first.**
</details>

### ✅ C-POWER (mechanism) — LANDED 2026-08-16 (developer: "do whatever fits best with what was planned") — colony POWER brownout throttle
**Built the recommendation below (a/b/c) as a byte-identical, tested MECHANISM.** `IndustryTools.PowerEfficiency(colony)` =
a THIRD production-rate factor (`ConstructStuff` rate = `infra × staffing × power`), `power = min(1,
EnergyGenAbilityDB.TotalOutputMax ÷ (GetTotalJobs × PowerDrawPerCrew_kW))`. (a) EXTENDS the existing energy system — supply
reads the SAME `TotalOutputMax` the fuel/warp code uses. (b) DERIVES demand from operating crew (the shared `GetTotalJobs`
producer × `PowerDrawPerCrew_kW` = 1.0 kW/crew) — **no new `*Atb`, no L6/L13 landmine.** (c) DISTINCT from infra (electrical
vs utility grid), flag-gated `EnablePowerThrottle` OFF (engine byte-identical) → `NewGameMenu`-on both paths. **The hard risk
is DISARMED by construction:** the start colony has **no power generation installed** (`earth.json` `Installations` has no
reactor/solar), so `TotalOutputMax` reads 0 → the throttle is **INERT** (returns 1.0 — the inverse of the food-supply-0
trap; supply-0 is SAFE, never bricks). Landed: `IndustryTools.cs` · `NewGameMenu.cs` (both paths) · `PowerThrottleTests.cs`
(calibration-independent gauge) · Industry CLAUDE.md · connection map (Power → production RATE row). Commit `d2ac424`.

### ✅ C-POWER-LIVE — RESOLVED 2026-08-16 (developer re-issued "for 1 do whatever fits best" → BUILD it): reactor on Earth
**Made the throttle BITE.** After thorough recon (reactor/turbine lack `PlanetInstallation` mount; solar ~10 kW/panel) the fix that "fits what was planned": **(1) added `PlanetInstallation` to the fission `reactor`'s `MountType`** (`energy.json` — additive, breaks no test: `PowerPlantGroundMountTests` only checks the GroundUnit/ShipComponent flags stay, `PowerJustificationTests` only pins solar's mount); **(2) installed ONE `default-design-fission-reactor` on Earth** (`earth.json` `Installations` — already registered in ComponentDesigns + `reactor` in StartingItems, so zero new registration; fixed 75 MW at install via `EnergyGenerationAtb`); **(3) gauge** `PowerThrottleTests.PowerEfficiency_StartColony_IsPoweredAndSafe` asserts supply(75 MW) ≥ demand(~52 MW) on the REAL New-Game colony (CI is the calibration net). Earth now reads 1.0 (not throttled, ~1.4× headroom) and browns out once industry grows past 75 MW; the grave rung is live (bombard the reactor → brownout). Blast radius verified clear (MiningTests dead; EconomyReadoutTests logs infra, doesn't assert it; employment band `(-9,0)` tolerates the +3 reactor crew). Files: `energy.json` · `earth.json` · `PowerThrottleTests.cs` · Industry CLAUDE.md · connection map. *(Original recon preserved below.)*

### ⚖ C-POWER-LIVE — the throttle can't BITE: the base mod has NO colony-mountable MW-scale power plant (parked 2026-08-16)
**Plain English:** the mechanism above is correct and safe, but on the stock start colony it does **nothing** — because
Earth generates no power, so supply reads 0 and the throttle stays inert. Making it BITE needs a real power plant installed
on the colony, and the recon found **the base mod has no power source that both (i) mounts on a colony AND (ii) makes
MW-scale power:**
- **Fission reactor** = 75 MW (`50 × 1500kg × 1`), but its `MountType` is `ShipComponent, ShipCargo, Fighter, GroundUnit,
  Station` — **no `PlanetInstallation`.** A colony can't build it.
- **Steam-turbine reactor** — its description literally says *"the station and colony plant,"* yet its `MountType` is
  `ShipComponent, ShipCargo, Fighter, GroundUnit` — **no `PlanetInstallation` AND no `Station`.** A mount/description
  contradiction that looks like a data bug.
- **Solar array** = the ONLY `PlanetInstallation`-mountable source, but a 100 m² panel makes **~10 kW** at 1 AU (Area 100 ×
  ~8% × 1361 W/m²). Earth's ~52 MW demand would need **~5,000 panels.** Absurd at start-colony scale. Solar output is also
  DYNAMIC (0 at install; the solar processor computes it per-tick from star light), so it's not an install-time-fixed number
  the way a reactor's is.

**So this is a design GAP, not a data flip.** The demand side (~52 MW, pinned by the A1 "~52k-job homeworld" note) is real;
the SUPPLY side has no colony-scale plant. **Recommendation (needs a developer call — it touches the Component Designer, and
the developer has strong designer opinions):** the cleanest fix is to **add `PlanetInstallation` (+ `Station`) to the
steam-turbine-reactor's `MountType`** — its own description says it IS the colony plant, so this aligns the mount with the
stated intent (a bug-fix, not a new invention) — then install one turbine on Earth's start (sized above ~52 MW) and add a
gauge asserting Earth-powered-and-safe on the real numbers. Alternatives: add `PlanetInstallation` to the fission reactor
instead; or bump the solar `Area` cap so a handful of panels reach MW-scale. All are economy/designer changes I did NOT take
autonomously. Say which (or "your rec — fix the turbine mount") and I install it + the gauge as the make-C-POWER-live slice.
*(Original recon + three-question adjudication preserved below.)*

### ⚖ C-POWER — the colony POWER run-cost needs a design call against the EXISTING energy system (parked 2026-08-15, TIER 2.5)
**Plain English:** the backlog calls Power "the only real build" of the run-cost vector — a generic component power draw
+ a colony power total vs reactor/solar supply → a brownout that throttles production, "same shape as infra." But the
recon found this is NOT a greenfield build; three things already exist and any Power slice must reconcile with them, and
none of the three reconciliations has a documented default:
1. **A power supply+demand system already exists.** `Energy/EnergyGenAbilityDB` carries the SUPPLY
   (`TotalOutputMax = MaxOutputFromReactor + MaxOutputFromSolar`) AND a `Demand` field, processed by `EnergyGenProcessor`.
   The start colony already installs reactors + solar (`earth.json`: `reactor`/`solarArray`/`default-design-fission-reactor`/
   `default-design_solarpanel`). **Question:** does the colony power run-cost EXTEND this blob (populate its `Demand` from
   installations, read its `TotalOutputMax`) or stand up a parallel colony-power model? Extending it is the honest
   "don't-duplicate" path, but I don't yet know if colonies even carry `EnergyGenAbilityDB` (ships do) or how its `Demand`
   is currently fed.
2. **There is NO generic installation power draw.** Only weapons have `WeaponSupply.PowerDraw_W`; installations have no
   power-demand value. So this needs EITHER a new `PowerDrawAtb` — which is the L6 six-point-registration + L13
   save-safe-ctor landmine chain AND authoring a watt number on every installation template (a real data effort) — OR a
   DERIVED proxy (from `CrewReq`/mass, like infra's `MassPerUnit/1000 + CrewReq`), which is arbitrary. **Question:** new
   authored dial, or derived proxy?
3. **Infrastructure already throttles production on a capacity grid** (`InfrastructureProcessor`, "the colony's utility
   grid: power, roads, comms, water"). A second power-vs-supply throttle risks DUPLICATING it. **Question:** is Power a
   distinct ELECTRICAL model (vs reactor/solar specifically, separate from the infra utility grid), or does it fold into
   infra?

**The hard risk:** if power demand is summed but colony supply reads 0 (colonies may not carry `EnergyGenAbilityDB`), the
throttle drives production to 0 — the exact "food-supply-was-hardcoded-0 → unwinnable" trap this codebase already hit once.

**Recommendation:** (a) EXTEND `EnergyGenAbilityDB` rather than build a parallel model; (b) DERIVE installation power draw
from `CrewReq` for the first cut (no new atb, no landmine, no template authoring — upgrade to an authored `PowerDrawAtb`
later if the developer wants per-installation tuning); (c) keep it DISTINCT from infra (electrical vs the general utility
grid) but flag-gate it OFF by default so it can't break the economy, and only turn it on after a local read confirms
colonies carry real power supply. This is a genuine multi-file extension of a live system + three design calls, so it is
parked rather than rushed. Confirm the three calls (or say "your recommendation") and I build it as the next run-cost
slice. The other three rungs (jobs/staffing/upkeep) needed no such call — they had clean existing bases.

### ✅ C-FOOD-DEMAND — RESOLVED 2026-08-16 (developer re-issued "for 2 do your rec" → BUILD it): farms on Earth + demand on
**Built the safe-by-construction make-live plan.** The recon (below) found the food loop built but Earth had NO food
supply — so turning on demand would starve it. Fix taken: **(1) registered + installed 4 `default-design-agri-complex` on
Earth** (`earth.json`: added `default-design-agri-complex` to ComponentDesigns + `food-production` to StartingItems + 4 to
Installations — the PROVEN DevTest recipe copied verbatim; food-production shares the same cost shape as `mine`/`factory`, so
it's buildable with no template surgery) → 20,000 food/day; **(2) added `SustenanceProcessor.EnableFoodDemand` flag +
`DefaultPerCapitaFoodDemand` = 1.0e-6** (a colony that hasn't authored its own demand uses the default; a DevTest strain
node keeps its own) → Earth demand = `8.2e9 × 1e-6` = 8,200/day, so supply/demand = **2.44×** (food-POSITIVE by
construction — both are constants, so supply > demand is an INVARIANT; `Shortage()` returns 0 when supply ≥ demand); **(3)
`NewGameMenu`-on both paths**; **(4) gauge** `FoodDemandTests` (asserts Earth food-positive on the real numbers; flag-off
byte-identical; the grave rung — a farmless colony fully starves). Morale-neutral: agri-complex quality is exactly 1.0
(`ColonyMoraleDB` only lifts morale when quality > 1.0). Blast radius verified clear (food byte-identical when off; +400 farm
crew stays inside the employment band `(-9,0)`; the DevTest self-sufficiency sim uses its own colonies, not `colony-earth`).
Files: `earth.json` · `SustenanceProcessor.cs` · `NewGameMenu.cs` (both paths) · `FoodDemandTests.cs` · Colonies CLAUDE.md ·
connection map. *(Original recon preserved below.)*

### ⚖ C-FOOD-DEMAND — TWIN OF C-POWER-LIVE: the food loop is built, but the start colony has NO food supply (parked 2026-08-15, findings updated 2026-08-16, TIER 2.5)
**Plain English:** the food loop is fully built (`SustenanceProcessor` reads farm output vs `pop × PerCapitaFoodDemand`,
banks a surplus, starves on a shortfall → morale), and turning it on is "one coefficient." But the 2026-08-16 recon (done
while resolving C-POWER) found this is the **SAME structural gap as C-POWER-LIVE — the start colony's SUPPLY side isn't
built:**
- Earth installs **NO food production** (`earth.json` `Installations` has no `agri-complex`/`hydroponics`), and stocks
  **NO `food` cargo** (the `Cargo` block has 20 minerals + fuels, no food). So its food supply is **zero.**
- So turning on ANY positive `PerCapitaFoodDemand` gives Earth an immediate **100% food shortage → −40 morale floor +
  population die-off.** Unlike C-POWER's throttle (supply-0 is INERT/safe), the food loop treats demand>supply as a REAL
  shortage — there is no inert guard. **Food's failure mode is CATASTROPHIC, not soft.**
- And installing farms is **not a 3-line data add** — the farm designs (`default-design-agri-complex`, 5000 food/day,
  `PlanetInstallation`-mountable) are **not in Earth's `ComponentDesigns` NOR is `food-production` in `StartingItems`**, and
  the `food-production` template has **no `ResourceCost` formula** (no base-mod colony builds a farm, so the build path is
  untested). So making it live is the full gotcha-10 six-point registration (unlock template + register design + install +
  verify materials + possibly author a ResourceCost) — landmine-dense (L6/L8), with `BaseModIntegrityTests` as the sensor.

**Why parked, not built autonomously:** the failure is catastrophic (starvation), the make-live is a real economy change
that changes Earth's start composition — which runs **counter to the developer's deliberate BAREBONES-New-Game philosophy**
(they stripped the start to minimal on purpose) — and it's landmine-dense + calibration-sensitive through 33-min CI. Exactly
the §6 park case, and the original adjudication's own conclusion ("guessing risks mass starvation — a developer-authorized
call").

**Recommendation (safe-by-construction make-live plan, ready on a "go"):** (1) unlock + register + install ~4 stock
`agri-complex` on Earth (4 × 5000 = 20,000 food/day); (2) add `SustenanceProcessor.EnableFoodDemand` + a static
`DefaultPerCapitaFoodDemand` = **1.0e-6** → demand = `8.2e9 × 1e-6` = 8,200/day, so supply/demand = **2.44×** (food-POSITIVE
by construction — `foodDemand = pop × coeff` and `farmOutput` are both constants, so supply > demand is an INVARIANT, never
a dynamic starvation; grave rung — destroy the farms — still returns the shortage); (3) NewGameMenu-on both paths; (4) a
gauge that asserts Earth is food-positive on the REAL numbers (CI is the safety net — a mis-size REDs before it ships) + the
flag-off byte-identity + the shortage/grave-rung math. Say "go — install farms + coeff 1e-6" (or name a coefficient) and I
build it as the make-C-FOOD-live slice, paired with C-POWER-LIVE (the two supply-side slices belong together).

*(Other slices with genuine ambiguity — two HTMLs contradict, a save-break with no safe pattern, a DECISION-PENDING
with no default, or an HTML number impossible without a redesign — will park here the same way.)*

Known future parks (from the backlog, not yet reached):
- **TIER 4 items 6–8** (Console Space on ship bridge · chassis √-law slider · Fighter Construction Points) are
  marked DECISION PENDING — likely STOP items when Phase C reaches them.
- **What colony capture transfers** (ground-side ruling) — a documented STOP.

---

## LANDED SLICES — detail log

*(Each landed slice gets a short plain-English entry here: what it does, the files touched, the gauge added,
and the CI run that turned it green.)*

### B-orders-fops — Formation-ops orders (Nest / Set Leader / Detach) — 🔨 built
**What it does (plain English):** the battalion command surface (the panel you get when you pick a battalion in the roster
or the Battalions tab) gains three "organize" actions it was missing: **Nest** this battalion under another (make it a
sub-formation), **make a chosen member the leader**, and **detach a unit** (pop it out of the formation to a loose unit).
Before this, those engine verbs existed but had no button — you could only rename / march / queue / set stance.

**Why it matters:** it's the first slice of **B-orders** — routing the design's DATA-graded orders (the ones where the
engine can do it but no screen let you). The Force-Management design (`forceswindow.html`) lists 123 orders; 23 are DATA
(engine-exists, no-UI). This wires the **Formation-ops** category's three real ones. It rounds out the battalion surface so
you can actually reorganize your ground order-of-battle from the window, the same way the fleet side lets you reparent
fleets.

**Files:** `Pulsar4X/Pulsar4X.Client/Interface/Windows/FleetWindow.cs` — new `DrawBattalionFormationOps(body, forces, f)`
(called from `DrawBattalionOrders`, before the region-map gate since these don't need it): a **Nest under** combo of the
faction's other formations → `GroundForces.SetParentFormation` (returns false on a cycle → safe no-op), and per member unit
a **Make leader** (`SetLeader`) + **Detach** (`UnassignUnit`) button. A `_formNestPick` dict holds the nest combo index.
Thin/defensive: direct CI-tested `GroundForces` calls on a click (like the existing march/stance surface), reads only,
`TextUnformatted` for user-renamable names, no hard-index; `MembersOf` returns a snapshot so detaching mid-loop is safe.
**Engine byte-identical** (client-only). **Deferred:** Move Formation Tree (`OrderFormationTreeMoveToHex`) needs a hex
target picker — a follow-up. Docs: campaign log, Client CLAUDE.md, CLIENT-TEST-CHECKLIST (B-orders row).

**Gauge:** client-only → the developer's PC play-test (CLIENT-TEST-CHECKLIST "B-orders — Formation-ops"): pick a battalion,
Nest it under another (it becomes a sub-formation), Make-leader a member (★ moves), Detach a member (it becomes loose and
shows in the roster's loose-unit list). Runtime is the developer's build.

### B-S9b-1 — colonies + stations as roster rows — 🔨 built (push gated on B-S9a green)
**What it does (plain English):** the All Forces roster now lists your **colonies and stations** as rows, alongside your
ships and battalions — so "everything I own, in one place" finally includes your *holdings*, not just your fighting
forces. A new **"Holding"** domain (filter it with the Domain dropdown). Each row shows the holding's name, the body it
sits on (Class column), its system (Location), and its **population** as the Strength number. A **station** also shows a
real **Health %** (its structural-integrity pool); a colony shows "—" for health (a colony has no single hit-point bar —
its strength is its population). Select one and the detail panel shows its host, population, (station) integrity, and its
**installed infrastructure** — reusing the exact components panel the Planetary window draws — plus an "Open planet view"
jump.

**Why it matters (the capture-stale trap it closes):** the game keeps a *list* of each faction's colonies/stations that
is written once at creation and **never cleaned up when a planet is captured** — so the raw list can name a colony you've
lost. This slice lists holdings through the B-S9a engine helper `FactionAssets`, which verifies each one's **live owner**
before showing it — so a colony captured away drops off your roster even while it lingers in the stale list. That's the
design's §S9 "live-owner cross-check," done in the engine (CI-tested) so the client just reads the honest set.

**Files:** `Pulsar4X/Pulsar4X.Client/Interface/Windows/FleetWindow.cs` — `ForceKind` gains `Colony`/`Station` (+
`ForceRef.OfColony`/`OfStation`), `ForceDomain` gains `Holding`; `DisplayAllForces` gathers holdings via
`FactionAssets.OwnedColonies`/`OwnedStations`; new `HoldingRow` (population/host/location) + `DrawHoldingDetail` (host +
population + station integrity + `ComponentInstancesDBDisplay.Display` reused, resolved via the B-S7 `ResolveEntityState`)
+ a `RowHealthFraction` Holding case (station integrity; colony —). `+ using Pulsar4X.Stations`. **Thin/defensive:**
reads only, `TextUnformatted` for every user-renamable name (the `%` printf trap), no hard-index; the holding entity rides
the `RosterEntry.Ship` field with dispatch keyed on `Domain==Holding` FIRST so no ship logic ever sees it. **Engine
byte-identical** (client-only; new draw methods + enum values). Docs: campaign log, Client CLAUDE.md (All Forces §S9),
CLIENT-TEST-CHECKLIST (B-S9 row).

**Split:** the **assign-commander** UI is **B-S9b-2** (below).

### B-S9b-2 — assign-commander UI — 🔨 built (stacks on B-S9b-1's `FleetWindow.cs`)
**What it does (plain English):** in a holding's detail panel, you can now **put an officer in charge** of it. A colony or
station that has an **admin complex** built on it has one or more administrator "seats." The panel lists each seat, shows
who's in it (or "empty"), and gives you a **dropdown of your commanders + an Assign button** to seat one — and an
**Unassign** button to clear it. Assigning an officer who's already running another post automatically moves them (the
engine handles that).

**Why it matters:** it's the last piece of §S9 (§4.4) and it closes a real gap — the engine has had the whole
assign-an-administrator machinery for a while (`AssignAdministratorOrder`), but **no screen ever let a player use it**.
Now the Force-Management roster does. It's the "play at your own altitude" delegation lever made reachable: hand a colony
to a governor from the same window you review your forces in.

**Files:** `Pulsar4X/Pulsar4X.Client/Interface/Windows/FleetWindow.cs` — new `DrawHoldingAdminPosts(holding)` (called from
`DrawHoldingDetail`): reads the holding's `AdminSpaceDB.CommanderSeats`, gathers `FactionInfoDB.Commanders` (name + type),
and per seat renders a commander combo + **Assign** (`AssignAdministratorOrder.Create(holding, commanderId,
seat.ComponentName)`) + **Unassign** (`UnassignAdministratorOrder.Create(...)`), both issued through
`_uiState.Game.OrderHandler.HandleOrder`. A `_seatCommanderPick` dict holds each seat's combo index. `+ using
GameEngine.People` / `Pulsar4X.People` / `Pulsar4X.People.Orders`.

**Verified the wire actually works (not just compiles):** the order's `EntityCommanding` is the holding, and both
`ColonyFactory` and `StationFactory` attach an `OrderableDB` to every colony/station — so `HandleOrder` enqueues the
order and `OrderableProcessor` runs `Execute` (the seat is really filled). The whole `HandleOrder` is try/catch-wrapped
(`[OrderError]`), so a bad click can never crash the client, and the order's `Clone()` (which throws `NotImplementedException`)
is **never called** on this path (no cloning — it executes directly). Engine byte-identical (client-only). Docs: campaign
log, Client CLAUDE.md (All Forces §S9), CLIENT-TEST-CHECKLIST (B-S9 row).

**Gauge:** client-only → the developer's PC play-test (CLIENT-TEST-CHECKLIST "B-S9"): select a colony/station with an
admin complex, pick a commander, click Assign → the seat shows that officer's name; Unassign clears it. A holding with no
admin post shows "build an admin complex to seat a governor"; with no commanders, "train officers at an academy."

**Gauge:** client-only → the developer's PC play-test (CLIENT-TEST-CHECKLIST "B-S9"): the roster shows a Holding row per
colony/station with population + (station) health; selecting one shows its host, population, installed infrastructure, and
a planet-view jump; a colony captured away no longer appears (the live-owner filter). The engine half (`FactionAssets`) is
the B-S9a CI gauge.

### B-S9a — engine live-owner cross-check — ⏳ CI (built file-disjoint while B-S8 re-gated)
**What it does (plain English):** the next roster slice (B-S9b) will add a faction's **colonies and stations** as rows in
the All Forces window. Before wiring any UI, this slice builds the engine number that tells the roster *which* holdings a
faction actually owns — and it fixes a real trap. A faction keeps a running list of its colonies and its stations (a
"registry"), but that list is **written once, at creation, and never cleaned up when a planet is captured**. When someone
takes a planet by ground invasion, the engine flips the planet's live owner flag but leaves it sitting in the *old*
faction's colony list. So the raw list can name a colony the faction has **lost**. `FactionAssets.OwnedColonies(faction)`
/ `OwnedStations(faction)` read the list as *candidates* but return only the ones whose **live owner** still matches — the
honest "what do you actually hold right now."

**Why it matters:** it's the "live-owner cross-check" the design (`FORCES-WINDOW-DESIGN.md` §S9) explicitly calls for, and
it's the **gauge-before-UI** discipline again (same as B-S8's Health accessor): build the number in the engine where CI
can test it, then the client just reads it. Building it now — as a **new file**, touching neither `ShipHealth.cs` (B-S8)
nor `FleetWindow.cs` (B-S9b) — was the file-disjoint work to do while B-S8's fix re-gated (§4: don't idle, don't stack on
an unverified base). It de-risks B-S9b: when B-S8 goes green, the client rows just call this already-CI-verified helper.

**Files:** `Pulsar4X/GameEngine/Factions/FactionAssets.cs` (NEW) — `OwnedColonies`/`OwnedStations`, pure/read-only/defensive
(the `Manager == null` guard is the same "never throws on an unmanaged entity" lesson B-S8 learned).
`Pulsar4X/Pulsar4X.Tests/FactionAssetsTests.cs` (NEW). Docs: Factions CLAUDE.md (FactionAssets row), Tests CLAUDE.md
(FactionAssetsTests row), this log.

**KNOWN LIMIT (flagged, deliberate):** the filter catches a **lost** colony (still in the registry, owner flipped away). It
does NOT catch a **gained** colony — capture also never *adds* the taken world to the captor's registry, so a colony you
just captured won't appear in *your* list for this filter to return. Surfacing a captured world for its new owner is a
**capture-side** fix (register the asset with the new owner at `GroundForcesProcessor.cs`, where the ownership flip's
comment already says "deeper transfer later"), a separate behaviour-changing slice — out of scope for this read-only check.

**Gauge:** `FactionAssetsTests` (CI, `rest` shard): a live-owned colony is returned; a **captured-away** colony is dropped
even though the registry still names it (the filter reads live ownership, not the stale list); restoring the owner
re-includes it; a station-less faction reads empty; null / unmanaged inputs return empty without throwing.

### B-S8 — aggregate Health + Fuel gauges — ⏳ CI (fix re-gating)

> **CI note (red → fixed):** first push `d369ebb` went RED on the `rest` shard —
> `HealthFraction_Defensive_NullAndComponentless` fed a bare `Entity.Create()`, which is an **unmanaged** entity
> (`Manager == null`); `Entity.TryGetDataBlob` delegates to `Manager`, so it NRE'd instead of returning 1.0. The core
> invariant test (pristine → damaged → destroyed) passed, confirming the accessor's math. Fixed in **`5e42d87`** by
> adding a `ship.Manager == null` guard — an unmanaged entity now returns 1.0 without throwing, honouring the accessor's
> "never throws" contract. Re-gating as run `31856896871`.

**What it does (plain English):** the All Forces roster gets a real **Health** column, and the ship detail panel shows a
ship's **Health %** and **Fuel %**. Before this, the roster had no health readout for ships at all — the game tracked a
ship's damage down at the individual-component level, but nothing added it up into a single "how beat-up is this ship"
number the window could show. This slice builds that number and wires it in. A ground battalion's health and a ground
unit's health already existed (the ground side computes them), so those show too — now every row has a health reading.

**Why it matters:** it's the design's decision #4 (`FORCES-WINDOW-DESIGN.md` §7 / §S8) — *"build the missing aggregate
ship accessors (they don't exist — a gauge-before-UI job) so those columns show real numbers."* The important discipline
here: **the gauge is built in the ENGINE first, where CI can test it**, then the client just reads it. And it's built
*honestly* — a ship's health = the summed integrity of its living components divided by the number the ship was **built
with**, so a component that got blown off in battle (which the damage system deletes) correctly counts as 0 rather than
being quietly ignored by an average of the survivors (which would make a half-wrecked ship read as pristine). Fuel needed
no new engine work — an accessor already existed (`GetFuelInfo`), it was just never surfaced in the roster.

**Files:** `Pulsar4X/GameEngine/Ships/ShipHealth.cs` (NEW) — `ShipHealth.HealthFraction(Entity)`, pure/defensive, reads
`ComponentInstancesDB.AllComponents` + the design's original component count. `Pulsar4X/Pulsar4X.Tests/ShipHealthTests.cs`
(NEW) — pristine = 1.0, a half-damaged component drops it by 0.5/count, a destroyed (removed) component counts as 0, and
null/component-less entities read 1.0 without throwing. `Pulsar4X.Client/Interface/Windows/FleetWindow.cs` — a **Health**
column on the roster table (8th column; `RowHealthFraction` dispatches by row kind, `HealthColor` green→red band) + a
**Health % / Fuel %** line in the ship detail (Fuel via the reused `GetFuelInfo`, faction/library read `TryGet`-guarded).
Docs: campaign log, Tests CLAUDE.md (ShipHealthTests row), CLIENT-TEST-CHECKLIST (B-S8 row). (No `GameEngine/Ships/CLAUDE.md`
exists, so the accessor is documented in its own XML docs + here + the Tests inventory.)

**Gauge:** engine `ShipHealth.HealthFraction` → `ShipHealthTests` (CI, `rest` shard). Client Health column + Fuel readout
→ the developer's PC play-test (CLIENT-TEST-CHECKLIST "B-S8"): the roster's Health column shows a % per row (colour-banded),
and a ship's detail shows Health % + Fuel %. Fuel only appears for a ship that burns fuel.

### B-S7 — civilian-ship detail panel — ⏳ CI in flight (rest shard) → flip ✅ when green
**What it does (plain English):** in the All Forces roster, clicking a **civilian** ship (a freighter, hauler, tender,
troop transport, survey ship — anything that isn't a warship) now shows what that ship is actually *carrying and doing*,
not just a combat line that reads "Firepower 0" for a ship with no guns. The detail panel gains: its **cargo manifest**
(what's in the holds, and how full each is), its **trade route + state** if it's running an automated logistics route
(what it's hauling, from where to where, and whether it's loading / en route / unloading), and a note if it's a **survey
vessel**. A warship is unchanged — it still shows its firepower/toughness/evasion combat sheet.

**Why it matters:** the design (`FORCES-WINDOW-DESIGN.md` §4.3) calls for the detail panel to *swap by kind* — a warship
shows combat, a civilian ship shows its manifest/route. This is graded **DATA** (not BUILD) because the numbers already
exist — the Logistics window renders them, they were just never surfaced in the roster. So this slice is almost pure
**reuse**: the cargo manifest is the *same* `CargoStorageDBDisplay` panel `EntityWindow` already draws (the exact call,
verbatim), and the route/state come straight off `LogiShipperDB.StateString` + `ActiveCargoTasks`. It makes the roster a
real order-of-battle for the *civilian* half of your fleet, not just the fighting ships.

**Files:** `Pulsar4X.Client/Interface/Windows/FleetWindow.cs` — `using Pulsar4X.Logistics`; in `DrawRosterDetail`'s ship
branch, `if(!e.Military) DrawCivilianShipReadout(ship)` (after the combat line, so a warship is untouched);
`DrawCivilianShipReadout` (cargo manifest via the reused `CargoStorageDBDisplay.Display` + a resolved `EntityState`;
`LogiShipperDB` route/state + active From→To tasks; a Survey-vessel note); `ResolveEntityState` (the `JumpToPlanetView`
walk-the-system-states idiom). Docs: campaign log, Client CLAUDE.md (All Forces §S7), CLIENT-TEST-CHECKLIST (B-S7 row).

**Gauge:** client-only → the developer's PC play-test (CLIENT-TEST-CHECKLIST "B-S7"): select a civilian ship (a start
freighter) in the roster and confirm its cargo manifest shows, plus route/state if it's on a logistics run; a warship
still shows only the combat sheet. Compile is gated by `build-client` (the reuse call is verbatim from `EntityWindow`).
Thin/defensive: reads only, `TextUnformatted` for user-renamable names (the `%` printf trap), no hard-index, and the
manifest degrades to a one-line note if the ship isn't in the active system view.

### B-S6 — per-individual ground-unit rows + engine `AllUnitsFor` — ⏳ CI in flight (rest shard) → flip ✅ when green
**What it does (plain English):** the All Forces roster (from B-S5) lists your battalions as single rows. This slice lets
you drill into a battalion to see the *individual soldiers/vehicles* inside it — and, importantly, it also surfaces the
**loose units** that aren't in any battalion yet (a freshly-raised garrison unit, a just-landed invader), which the old
"list of battalions" simply couldn't show. A new **"Show individual units"** checkbox on the roster: leave it off and you
get the tidy battalion-level view (unchanged); tick it and each battalion expands to show its member units as indented
child rows (`└`), with the loose units listed below (`•`). Click any unit row and the detail panel shows *that unit's*
stats — its type, its role, health, attack, defense, range, whether it's a veteran, and where it's standing.

**Why it matters:** the design's S6 closes "list *every* unit" (`FORCES-WINDOW-DESIGN.md` §S6). Before this, a unit that
hadn't been formed into a battalion was invisible in the Force-Management window — you couldn't even see it existed there.
The load-bearing new piece is an **engine** helper, `GroundFormationTools.AllUnitsFor(game, factionId)` — the unit-level
twin of the `AllFormationsFor` the Battalions/roster already use — which walks every world and returns *all* of a faction's
ground units, formed or not. It's an engine method, so it gets a real CI test (unlike the client, which CI can't run):
`EfGroundFormUpTests.AllUnitsFor_EnumeratesAcrossBodies_IncludesUnformed_FactionFiltered` proves it enumerates across
multiple worlds, includes a loose unit a formation-walk would miss, and excludes other factions' units.

**Files:** `Pulsar4X/GameEngine/GroundCombat/GroundForcesDB.cs` — new `GroundFormationTools.AllUnitsFor` (mirrors
`AllFormationsFor`, iterates `forces.Units`, read-only/defensive). `Pulsar4X/Pulsar4X.Tests/EfGroundFormUpTests.cs` — the
new gauge. `Pulsar4X.Client/Interface/Windows/FleetWindow.cs` — `ForceKind.GroundUnit` + `ForceRef.OfGroundUnit`; a
`GroundUnit` slot on `RosterEntry`; the `_rosterShowUnits` checkbox + the interleave (battalion → its `MembersOf` rows;
then loose units from `AllUnitsFor`); `UnitRow` (builds a unit row — Class via `GroundRoleComposer.ClassifyRole`, Mil/Civ
= `Attack > 0`); `DrawGroundUnitDetail` (the unit stats panel); plus a small printf-safe `RosterDetailHeader` and a
sweep routing user-renamable names/locations through `TextUnformatted` (the one runtime finding the B-S5 adversarial
verification surfaced — a ship/body named with a `%` could garble ImGui's `Text`). Docs: campaign log, Client CLAUDE.md
(All Forces §S6 note), GroundCombat CLAUDE.md (`AllUnitsFor`), CLIENT-TEST-CHECKLIST (B-S6 row).

**Gauge:** engine `AllUnitsFor` → `EfGroundFormUpTests` (CI, `rest` shard). Client per-unit rows → the developer's PC
play-test (CLIENT-TEST-CHECKLIST "B-S6"): tick "Show individual units", confirm a battalion expands to its members + loose
units appear, and clicking a unit shows its stat panel. Default-off keeps the S5 view byte-identical.

### B-S5 — the All Forces flat roster tab — ✅ `42d01c7` (all 7 shards + build-client green, run 31692300418)
**What it does (plain English):** the Force Management window gets a new sibling tab, **"All Forces"**, next to Fleets
and Battalions. It's a single flat list of *everything* you own — every ship AND every battalion, space AND ground —
in one table with the same columns for both: **Unit** (its name), **Domain** (Space or Ground), **Kind** (Ship or
Battalion), **Class** (Warship / Freighter / Survey… for a ship; Line / Artillery / Screen / Support for a battalion),
**Mil/Civ** (is it a fighting unit or a civilian one), **Location** (which system + body, or which world + region),
and **Strength** (firepower for a ship, formation strength for a battalion). So the question "what do I have, and
where is it?" is answered in ONE place instead of hopping between two tabs. Click any row and the panel below swaps to
the right tools for that KIND: a ship shows its combat readout (firepower / toughness / evasion) plus a "Select on
map" jump; a battalion shows the exact same march / queue / stance / ROE order surface the Battalions tab gives.

**Why it matters:** this is the design's "front door" (FORCES-WINDOW §4.2/§4.3) — the whole point of the Force
Management window. The earlier slices built the parts it needs (S2 the class classifier, S3 the reusable rows, S4 the
one-selection `ForceRef`); this slice assembles them into the unified roster a player actually reads. It reuses the
CI-tested engine brains rather than inventing new logic: `ShipRoleTools.ClassifyRole`/`IsMilitary` for a ship's class,
`GroundRoleComposer.ClassifyRole` for a battalion's plurality role, `GroundFormationTools.AllFormationsFor` for the
cross-body battalion list — the "one verb, both seats" classifiers the AI uses too, so the window and the AI agree.

**Files:** `Pulsar4X.Client/Interface/Windows/FleetWindow.cs` — the "All Forces" tab item (try/catch-wrapped, logs
`[RenderError]` once, still runs `EndTabItem`); `DisplayAllForces()` (gather ships by recursing `PlayerFaction`'s root
`FleetDB` with a fleet-id cycle guard + battalions via `AllFormationsFor`, fold each into a `RosterEntry`; Domain /
Mil-Civ / search filters; the common-column table over `_selRoster`); `DrawRosterDetail()` (kind-swapping — battalion
→ `DrawBattalionOrders`, ship → combat readout + Select-on-map); helpers `AllShipsUnder`/`CollectShips`,
`ShipLocation`, `BattalionLocation`, `FormationClass`; the `ForceDomain` enum + `RosterEntry` struct + `_selRoster`/
filter fields (added in the S4/S5 prep). Docs: `docs/CLIENT-TEST-CHECKLIST.md` (B-S5 roster row).

**Gauge:** compile-checked by `build-client` (the client can't run in CI). Runtime is the developer's PC play-test —
the CLIENT-TEST-CHECKLIST row: open Force Management → All Forces, confirm ships + battalions both list with the right
Class/Mil-Civ/Location/Strength, the Domain/Role/search filters narrow correctly, and clicking a ship vs a battalion
swaps the detail panel to the right tools. Defensive notes baked in: evasion renders as `F2` (not `P0`) to dodge the
ImGui `%` printf trap; every faction/entity read is `TryGet`-guarded; the fleet recursion is cycle-guarded.

### B-S4 — the unified "selected force" (`ForceRef`) — ✅ `edd32d8` (build-client green)
**What it does (plain English):** the Force Management window is going to grow one flat "All Forces" list that mixes
ships, fleets, and battalions in a single table (the next slice, S5). For that to work, the window needs ONE way to
say "this is the thing you have selected" that can point at *any* kind of force — not the three separate,
incompatible selection variables it has today (a selected fleet, a set of selected ships, and a selected battalion
tracked as two loose integers). This slice adds that one thing: a small value called **`ForceRef`** that records the
KIND of force selected (Fleet / Ship / Battalion) plus the id that pins it down — a ship or fleet by its entity id, a
battalion by its (world, formation) pair, exactly how a battalion has always been identified across worlds.

**Why it matters:** it's the "load-bearing refactor" the design calls out — the shared selection model the All-Forces
roster (S5) is built on, and the reason a click on a ship row and a click on a battalion row can live in one table. To
prove `ForceRef` actually works (not dead scaffold), this slice **migrates the Battalions tab's own selection onto
it** — the two loose `_selBattalionBodyId` / `_selBattalionFormationId` integers become one `_selBattalion` `ForceRef`.
That's a **byte-identical** swap: the selection test `IsBattalion(bodyId, formationId)` reproduces the old
`body.Id == … && formation.Id == …` check exactly, and the starting "nothing selected" value matches the old `-1`
defaults. The `OfFleet` / `OfShip` factories are the foundation S5 will use for the other two kinds.

**Files:** `Pulsar4X.Client/Interface/Windows/FleetWindow.cs` — new nested `ForceKind` enum + `ForceRef` readonly
struct (`None` / `OfFleet` / `OfShip` / `OfBattalion` + `IsNone` / `IsBattalion`); the Battalions tab's two selection
ints replaced by one `_selBattalion` `ForceRef` at its three sites (field, `isSel` test, click-set). Docs:
`docs/CLIENT-TEST-CHECKLIST.md` (B-S4 byte-identical selection check).

**Gauge:** compile-checked by `build-client` (the refactor's real risk is a type slip). Runtime is **byte-identical**
(same battalion selection behaviour), so no engine test moved and the local check is "battalion selection still works
exactly as before." The `OfFleet`/`OfShip` factories are deliberately not consumed yet — S5 wires them, the same
"foundation for the next slice" pattern as A5's ability scan.

### B-S3 — the ship-combat row + battalion row are now reusable methods — ✅ `d0f9df6` (build-client green)
**What it does (plain English):** the Force Management window draws two tables — the Combat tab's per-ship line, and
the Battalions tab's per-formation line. Until now each was written *inline*, tangled into its own loop, so the coming
"All Forces" tab (one flat list of everything you own) couldn't reuse them without copy-pasting. This slice lifts each
row out into its own small method — `DrawShipCombatRow(ship)` and `DrawBattalionRowColumns(body, forces, formation)` —
so the new roster can call the exact same row-drawing code and every table shows a ship (or a battalion) the same way.

**Why it matters:** it's plumbing for S4/S5 — one place that knows how to draw a ship row, one for a battalion row.
Nothing the player sees changes: the methods draw the exact same columns, from the exact same engine reads, in the
exact same order (a **byte-identical** refactor). The battalion row's *name + selection* deliberately stayed in the
caller, because unifying selection across ships and battalions is the next slice's job (S4).

**Files:** `Pulsar4X.Client/Interface/Windows/FleetWindow.cs` — extracted `DrawShipCombatRow(Entity, int)` from
`DisplayFleetCombatSheet`'s loop and `DrawBattalionRowColumns(Entity, GroundForcesDB, GroundFormation, int)` from
`DisplayBattalions`'s loop; both call sites now invoke the methods. Docs: `docs/CLIENT-TEST-CHECKLIST.md` (B-S3
byte-identical render check).

**Gauge:** compile-checked by the `build-client` CI job (the refactor's only real risk is a type/scope slip, which the
compile catches). No engine value changed and the draws are byte-identical, so no engine test moved; the "tables look
the same" confirmation is a local-build glance (CLIENT-TEST-CHECKLIST B-S3).

### B-S1 — Battalions tab reads the built cross-body helper — ✅ `7f96ea1` (build-client green)
**What it does (plain English):** the "Battalions" tab of the Force Management window lists every ground formation you
own across every world — the ground echo of the fleet list. It was gathering that list the hard way: walking every
star system the player currently knows and summing up each world's formations by hand, with a code comment admitting
"there's no engine helper for this yet." That engine helper *does* exist now (`AllFormationsFor` — its own comment
literally names this window as the thing it was built for), so this slice just points the tab at it.

**Why it matters:** it's the studio "one place, one way" discipline — the engine now owns "list all my battalions
across the galaxy" as ONE tested method, instead of the client re-deriving it. The hand-rolled walk could also *miss*
a battalion sitting on a world that had dropped out of the player's known-systems view; the engine helper walks the
real game, so your order of battle is complete. And it's scoped to **PlayerFaction** (the design's call), so the tab
shows YOUR battalions even while a Space-Master session is viewing another faction — before, SM mode showed the
viewed faction's (empty for the Game Master).

**Files:** `Pulsar4X.Client/Interface/Windows/FleetWindow.cs` (`DisplayBattalions` — the gather swapped from the
`StarSystemStates` walk to `GroundFormationTools.AllFormationsFor(game, myFaction)`; `myFaction` now
`PlayerFaction ?? Faction`; each returned body reconstructs its `(system, forces)` via `body.Manager as StarSystem` —
the same cast the position path already uses). Docs: `Pulsar4X.Client/CLAUDE.md` (Battalions-tab stale "no engine
helper yet" note corrected), `docs/CLIENT-TEST-CHECKLIST.md` (B-S1 runtime row).

**Gauge:** the engine helper is already CI-pinned by
`EfGroundFormUpTests.AllFormationsFor_EnumeratesAcrossBodies_FactionFiltered` (cross-body enumeration, faction-filtered,
each paired with its body) — the exact contract this tab now relies on — so no new engine test was needed. The client
change is compile-checked by the `build-client` CI job; its runtime look/feel is the developer's local build
(CLIENT-TEST-CHECKLIST B-S1). **Byte-identical in normal play** (PlayerFaction == Faction, and `AllFormationsFor`
returns the same formations the hand-walk did for known systems); only SM-mode scoping + completeness improve.

### A3 — `ShipRoleTools.ClassifyRole` (the ship role classifier) — ✅ `e91b722`
**What it does (plain English):** the engine now has ONE place that decides what KIND a ship is — warship,
freighter, survey ship, transport, tender, hauler, or bare utility — by reading the parts bolted to the hull
(a weapon → warship, a survey sensor → survey ship, and so on), exactly the way the ground side already reads a
unit's job from its stats. There is deliberately no stored "is this military?" flag (the engine has a dead one
that's never set); the class is DERIVED and live.

**Why it matters:** the Forces window will show this as each ship's "Class" column, and the faction AI already
needs to tell a warship from a freighter. Before this, the AI carried TWO separate copies of that test
(`ConquerResolver.IsWarship` + `DefendResolver.IsWarship`) that could drift apart. Now both **delegate** to the
one shared helper — the studio law "one verb, both seats": the window and the AI classify a ship the same way,
guaranteed.

**Files:** `GameEngine/Ships/ShipRoleTools.cs` (new — the `ShipRole` enum + `ClassifyRole(design)` /
`ClassifyRole(entity)` / `IsWarship` / `IsMilitary`); `ConquerResolver.cs` + `DefendResolver.cs` (their
`IsWarship` now one-line delegators — byte-identical by construction); `Pulsar4X.Tests/ShipRoleToolsTests.cs`
(new gauge). **Byte-identical:** the AI predicate is unchanged (delegation to identical code); the classifier is
otherwise a pure new read nothing consumes yet.

**Gauge:** `ShipRoleToolsTests` — every AI-warship design classifies Warship+Military and every other design
civilian (the byte-identity tripwire for the two delegators); a built ship classifies the same role as its
design; Mil/Civ maps only Warship to Military; null-safe.

**Note on the HTML badge:** the forceswindow.html "Class" column is graded BUILD because it's about the WINDOW
showing the column. The engine classifier (FORCES-WINDOW S2) is now built, but the column badge stays BUILD until
Phase B (S5) actually surfaces it in the window. The ground classifier (`GroundRoleComposer.ClassifyRole`)
already existed; "surfacing" it is window work, also Phase B.

### A2 — Ground `Penetration` + `PerShotEnergy` carry-through (the ground assembler path) — ✅ `a676efd`
**What it does (plain English):** a ground weapon you DESIGN in the Entity Assembler (a frame + weapon parts) now
carries its armour-piercing power. Before this, only the pre-built "monolithic" tank/infantry/artillery units
could crack armour — a *player-built* AP gun came out with zero penetration and bounced off plate. Now the
weapon part carries two dials: **Penetration** (how much of the target's armour the shot ignores) and
**PerShotEnergy** (whether it's one big alpha shot that punches through, or a spray of little shots that bounce).

**Why it matters:** it's the root-cause fix the backlog put first — the reason the ground-battle sim had to
hand-type the Tyranids' claw penetration. A player-designed anti-tank weapon now cracks plate a small-arms
weapon of equal firepower bounces off. And it's **per-weapon**: a unit carrying both a rifle and a cannon cracks
plate only with the cannon (the "honest home").

**Files:** `GroundWeaponAtb.cs` (2 new fields + 6th/7th ctor args + Clone); `GroundWeaponMount.cs` (per-mount
fields + copy-ctor); `GroundUnitAssembly.cs` (Result fields + weapon loop + `ToGroundUnitDesign`);
`GroundCombatant.cs:114` (the profile reads the mount's own pen/per-shot — the one behaviour edit);
`installations.json` (all 5 base-mod weapon templates → 7 `AtbConstrArgs` in lockstep, gotcha-6);
`Pulsar4X.Tests/GroundWeaponPenetrationAssemblyTests.cs` (new gauge).

**Flagged values (developer owns):** cannon **20/140** (= the monolithic Armor gun — parity), autocannon 6/40,
energy 10/90, rifle 0/10, claw 0/10. These reproduce the monolithic behaviour for the assembler path; the
developer can retune. Penetration is a **free dial this slice** (not costed in the Mass formula, so
`GroundWeaponAttackCostTests` stays byte-identical); costing it (CONVENTIONS §16) is a flagged follow-up.

**Not byte-identical (intended):** assembled cannon units now crack plate. No existing resolver test fields an
assembled unit, so nothing re-baselined. `GroundWeaponAttackCostTests` + `BaseModIntegrityTests` (the 7-arg JSON
bind sensor) stay green as tripwires.

**Gauge:** `GroundWeaponPenetrationAssemblyTests` — a cannon (authored 20/140) assembles a unit whose design +
mount + resolver profile carry 20/140; a rifle stays 0/10; on a mixed rifle+cannon unit each mount keeps its OWN
pen; and `GroundDamageMatrix.ArmourSoak` lands more with pen than without (AP cracks plate).

**HTML badge:** the entityassembler.html penetration badge already read "LIVE on ground" — A2 makes that claim
true for the assembler path too, so no HTML flip was needed. The backlog item #1 flipped ⬜→✅ (the engine caught
up to the badge).

### A1 — Employment → morale producer (feed the ±40 term "that can never fire") — ✅ `892924b` · calibration parked ⚖
**What it does (plain English):** the game has a morale rule for "do people have jobs?" — but it never actually
worked, because nothing in the game ever declared a single job, so the number was always zero. A1 wires it up: a
colony's jobs are now counted from its buildings' operating-crew requirement (a factory that needs 500 crew
provides 500 jobs), exactly as the civic-door design says.

**Why it's flag-gated (and parked):** turning this on is the single biggest live behaviour change in the backlog —
morale feeds migration, tax income, and legitimacy. And there's a calibration wrinkle: the crew numbers were
written as "crew to run the building," which against a billions-population homeworld reads as near-total
unemployment (up to −40 morale everywhere). So A1 ships the term **switched OFF by default**
(`PopulationProcessor.EnableEmploymentMorale`) — a current game is byte-identical — and the "should we turn it on,
and with what denominator?" decision is **parked in the ADJUDICATION QUEUE** above for the developer. The wire is
built and tested; only the on-switch waits.

**Files:** `ComponentInstancesDBExtensions.cs` (`GetTotalJobs` now sums `CrewReq`, `EmploymentAtbDB.Jobs` overrides
— keeps that attribute live, no dead code); `PopulationProcessor.cs` (the `EnableEmploymentMorale` flag + gates at
its 2 morale sites); `StationPopulationProcessor.cs` (the shared gate); `Pulsar4X.Tests/EmploymentMoraleTests.cs`
(new gauge).

**Gauge:** `EmploymentMoraleTests` — the producer now reads a non-zero jobs total on a staffed colony (was 0
before A1); the flag gates the morale term (OFF → neutral 0, byte-identical; ON → the term fires).

**HTML badge:** civicderived.html's "the morale term that can never fire" / "ZERO. Nothing produces it" / Jobs
grade "BUILD-NOW" flipped to reflect the honest state — the **producer is built** and the wire is complete, but the
term is **flag-gated pending the parked calibration** (not "live on every colony," which would mislead since it's
off by default). Backlog item #2 flipped ⬜→✅ (built, flag-gated).

### A4 — de-fang the four order stubs (no wedge, no crash) — ✅ `72dcc72` · 4 behaviors parked ⚖
**What it does (plain English):** the Forces window has four orders that were never finished and just do nothing
when issued. Two of them were worse than useless — a real BUG: "Refuel" and "Resupply," once issued, would **jam
the fleet's order queue forever** (the order never marked itself done, and a fleet won't take new standing orders
while its queue is stuck). The other two would **crash the game clock** if ever wired in (they threw an error on
the background thread when the game tried to copy them). A4 removes both landmines: the two now complete cleanly
instead of jamming, and the two crash-throwers are made safe.

**Why the behavior is parked, not finished:** making each order actually *work* needs a design decision from the
developer (what's a supply source, what "resupply" means, whether to finish-or-delete the duplicate anomaly-survey,
whether the per-ship logistics order should exist at all). Those four decisions are in the ADJUDICATION QUEUE with
my recommendation for each. The campaign's own STOP rule covers this: a stub with a real open question and no
documented default gets parked — but I fixed the live bugs (wedge/crash) either way.

**Files:** `RefuelAction.cs` + `ResupplyAction.cs` (de-wedged — `Execute` completes the order so it leaves the
lane); `ServeyAnomalyAction.cs` (was a raw throwing skeleton → now a safe inert shell, real non-throwing `Clone`;
the misspelled class name kept — it's embedded in saves, L3); `ShipLogisticsOrders.cs` (real non-throwing `Clone`,
left as the documented display shim); `Pulsar4X.Tests/OrderStubSafetyTests.cs` (new gauge).

**Gauge:** `OrderStubSafetyTests` — Refuel/Resupply complete after `Execute` (de-wedged, IsFinished flips true);
`ServeyAnomalyAction`/`ShipLogisticsOrders` `Clone()` doesn't throw; the survey order is a safe inert shell.
Engine-only, no JSON drift, no save-break (no class renamed).

### A5 — the order→ability component scan (`AbilitiesOf` + `CanIssue`) — ✅ `761a017`
**What it does (plain English):** an order in the game isn't a free-floating verb — it's powered by a part bolted
to the unit (a survey sensor lets you survey, a jump drive lets you jump, a troop bay lets you load troops). So the
Forces window should offer an order only when the unit actually carries the part. The engine already did this by
hand for three specific cases; A5 turns it into ONE shared tool: `AbilitiesOf(unit)` (the set of parts a unit
carries), an order→part table, and `CanIssue(unit, "GeoSurvey")` that checks them. A fleet's abilities are the
union of its ships' — "can this fleet survey?" = "does any ship aboard carry a survey sensor?"

**Why it matters:** it's the mechanism the deep order menu gates on (Forces-window §4.5), and it's read by the
window AND the AI (one-verb-both-seats). It's cradle-to-grave: install the part → the order appears; lose the part
in battle → the order vanishes. That last bit needed a subtle fix — when a component is uninstalled the engine
leaves an empty entry behind, so `AbilitiesOf` filters on a live part count, or a shot-off sensor would still
grant its order (the grave rung).

**Files:** `GameEngine/Ships/ShipRoleTools.cs` (extended the A3 file — added `AbilitiesOf` + `OrderAbilityTable` +
`CanIssue`); `Pulsar4X.Tests/AbilityScanTests.cs` (new gauge). **Byte-identical:** pure additive read-only helper;
nothing consumes it yet (the window/AI reroute onto it is a later slice, deliberately deferred to keep A5 green).

**Gauge:** `AbilityScanTests` — a surveyor reports GeoSurveyAtb + `CanIssue("GeoSurvey")`; a fleet holding it
reports the same (the union); a non-surveyor can't; clearing the component (the grave-rung stale state) removes the
ability (the Count>0 filter); a troop bay is covered too (it has no *AbilityDB — the case that forced the client's
hand-written scan).

**Phase A is COMPLETE.** All five first-five welds landed (A1 employment · A2 ground-penetration · A3
role-classifier · A4 order-stub safety · A5 component-scan). Two developer decisions parked (A1 calibration, A4
order behaviors). Next: **Phase B** — the Forces window (S1→S9), starting with the roster + the reuse of the
existing `AllFormationsFor`.

---

## SESSION NOTES

- **2026-08-13, session 1 open.** Fresh start (no prior log). Design layer already merged (PR #90). All 17
  HTMLs verified present. Read the four campaign-map docs (DESIGN-TOOLS-INDEX, BUILD-BACKLOG-INDEX,
  ENGINE-WIRING-BACKLOG, DESIGNER-NORTH-STAR method) + CONVENTIONS + the full FORCES-WINDOW-DESIGN. Launched a
  6-agent recon workflow (`wf_98ffad13-d66`) mapping the 5 Phase-A welds + the HTML badges against real source.
  Created this ledger.
