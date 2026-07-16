# Construction — Subsystem Reference

**Build the pieces somewhere, carry them, put them together on site.** Lives in `GameEngine/Construction/`. New 2026-07-15.

> Read `docs/economy/UNIVERSAL-ASSEMBLY-DESIGN.md` (the ONE-designer / chassis-gives-budget frame) and `GameEngine/Stations/CLAUDE.md` (the station host + the `DeployStationOrder` sibling path) before touching this.

---

## The one idea (the developer's model "A")

A designed station is a **recipe** — a chassis plus modules (a `Pulsar4X.Stations.StationDesign`, authored in the Entity Assembler). You don't build the whole station in one place. Instead:

1. You **build the COMPONENTS** (the chassis, the reactor, the research lab…) at a factory — a colony or another station, anywhere that can build components. A component built with **no install target** drops into cargo as a stored item (that path already existed: `ComponentDesign.OnConstructionComplete` → `storage.AddCargoByUnit`).
2. You **pack them in a cargo hold** and haul them — on the constructor itself, or spread across a fleet.
3. A unit that carries a **field constructor** (`ConstructorAtb`) flies to the spot and **assembles the station on site** out of those carried parts.

Analogy: a shipyard doesn't ship you a finished submarine — it ships the sections, and they're welded together in a graving dock. The constructor is the graving dock you can fly to a star.

This closes the vertical **cradle-to-grave** chain end to end: mineral → material → **component built at a factory** → packed in cargo → **hauled by a fleet** → **assembled on site by a constructor** → a working station → destroyed (the parts die with it).

---

## File Map

| File | Purpose |
|------|---------|
| `ConstructorAtb.cs` | **The field-constructor ability — a COMPONENT** (`IComponentDesignAttribute`, CONVENTIONS §6): researched → built → mounted on a ship → lost when it dies. Carries `ConstructionCapacity` (m³) — the largest recipe (total component volume) it can raise, so a bigger constructor (which costs more) builds a bigger station. INERT on install (the order reads it at build time via `TryGetComponentsByAttribute<ConstructorAtb>`); double-arg ctor for the JSON/NCalc binder (L7). SIX-point registered base-mod `constructor` (works from turn one). |
| `ConstructionCargo.cs` | **Shared fleet-pooled cargo helpers**, factored out of `DeployStationOrder` so the bare-frame deploy and the recipe-driven build pool holds the SAME way (no drift). `GatherPooledHolds(ship)` (own hold + every fleet-mate's — there is NO ship→fleet back-reference, so it searches the system's `FleetDB`s for the one whose children include the ship), `CountPooled(holds, item)`, `TryConsumePooled(holds, item, units)` (**check-then-consume** — sums the pool first, consumes nothing if short, so a refused build never half-drains a hold). Pure + defensive; never throws. |
| `OnSiteConstructionOrder.cs` | **The build-on-site order — the recipe-driven twin of `DeployStationOrder`.** `CreateCommand(constructorShip, stationDesignId)`. `IsValidCommand`: the ship carries a `ConstructorAtb` and is parked somewhere it can anchor. `Execute`: resolve the site (`GetSOIParentEntity`) → resolve the `StationDesign` recipe → read the constructor's capacity → resolve each `ComponentDesignIds` entry to a real `ComponentDesign`, total the volume, refuse if it exceeds capacity → **verify the pooled holds hold every component (check), then CONSUME them** → `StationFactory.CreateStation` at the site + `AddComponent` each module. FULLY GUARDED — a missing recipe / site / faction / short pool refuses cleanly (an event, nothing consumed) and never throws. `InstantOrder`, so the handler runs it synchronously. |

---

## Connections (Prime Directive)

- **Stations / `StationDesign`** — the RECIPE this order builds (`ComponentDesignIds` + `InitialPopulation`). Authored by `StationDesign.RegisterStationDesign` (the Entity Assembler). The sibling `DeployStationOrder` is the *bare-frame-for-one-material* path; this is the *assemble-from-real-parts* path. Both end at `StationFactory.CreateStation`.
- **Storage / `CargoStorageDB` + `CargoMath`** — components ride as cargo keyed by `ComponentDesign.CargoTypeID`. **🧨 A component only stores if its `CargoTypeID` names a real TypeStore** (`CargoMath.AddCargoByUnit` silently no-ops otherwise, and the error logging is commented out). `station-chassis` was `"None"` → un-haulable; fixed to `general-storage` + a `ShipCargo` mount in the same slice. Any module meant to be carried needs a stockable `CargoTypeID` and a hold with the matching TypeStore.
- **Fleets / `FleetDB`** — the pool. Direct children only (sub-fleet recursion is a documented refinement).
- **Components / `Entity.AddComponent(design, count)`** — installs each module on the freshly-created station, firing its `OnComponentInstallation` (so a reactor/research/factory module lights up its host-agnostic economy processor for free).
- **Client / `EntityContextMenu`** — a **"Construct Station Here"** submenu on a constructor ship lists the faction's station recipes; picking one issues this order through `Game.OrderHandler.HandleOrder` (the CI-tested player path). Compile-checked only — runtime is the developer's local build.

## Gotchas

1. **`OnSiteConstructionOrder` must never throw** — it runs through the order handler (which logs, not crashes), but keep it guarded: a bad recipe/site/pool refuses via `Refuse(...)`, never an exception.
2. **Check-then-consume is load-bearing.** `RemoveCargoByUnit` clamps silently, so always verify the whole pool covers the bill BEFORE removing anything (`TryConsumePooled` does this). A spend-then-discover-short path would leave a half-drained hold and no station.
3. **A component is only haulable if its `CargoTypeID` is a real store** (not `"None"`). See the Storage connection above — this is the flagged landmine.
4. **Tests submit through the real handler** (`Game.OrderHandler.HandleOrder`), never `Execute()` directly — the standing station-order convention. Gauge: `Pulsar4X.Tests/ConstructorTests.cs`.
