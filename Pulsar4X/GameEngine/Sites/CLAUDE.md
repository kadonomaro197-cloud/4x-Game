# Sites — the Site Engine (subsystem reference)

**What it is, in one line:** the **one** data-driven engine every mid-game *episode* is a row in — a ruin, an anomaly, an outbreak, a derelict, a resource node, a first-contact, a late-game crisis. Design: `docs/SITE-ENGINE-DESIGN.md` (parent doc; `docs/EXPLORATION-CONTENT-DESIGN.md` is its catalog chapter).

The whole mid-game reduces to one shape:

> **a LOCATED thing → a berth-seated LEADER works it → it can be FOUGHT OVER at that location → it RESOLVES down one of several branches → a yield, or nothing.**

Build it once and exploration, ground combat, diplomacy, research, economy, and the crisis system all become **consumers of one engine** instead of six islands (the Prime Directive's "connect" made into architecture).

---

## Build sequence (anomaly-first — the engine, then content)

The space anomaly is the engine minus the surface bits, so it's the cheapest first build.

| Slice | What it builds | Status |
|-------|----------------|--------|
| **SE-1** | the engine on a space anomaly — `FieldSiteDB` + state machine + presence detection + accrue + resolve + a research yield | ✅ done (SE-1a + SE-1b + SE-1c) |
| SE-2 | the Command Berth as a real component (Role/Grade/Support/Survivability/Span) + the posting-danger incident roll | 🔨 building (SE-2a gear + SE-2b seat/work-rate done; SE-2c incident roll next) |
| SE-3 | surface sites — locate ruins on a hex, the unit-worker path, Load/Land orders + hex landing, the guardian hook | planned |
| SE-4 | the incident shape (Europa) — bleeds-you pressure + hold-while-you-work + a menace faction + the spawn event | planned |
| SE-5 | branches + the ruptured edge (the crisis) — composable multi-path resolve, Diplomat/Intelligence roles, persistent→crisis | planned |
| SE-6+ | content — author catalog rows across the dials as data | planned |

Each slice is CI-gated and byte-identical-first (new code neutral/unattached until wired).

---

## File map

| File | Role |
|------|------|
| `FieldSiteDB.cs` | **SE-1a (2026-07-12)** The SITE RECORD + the enums. The §3 dials — `SiteRole` (Science/Tactical/Diplomatic/Intelligence/Engineering), `SiteShape` (OneShot/Persistent/Incident), `SiteHook` (Benign/Guardian/Cursed/…), `SiteYield` (Research/Blueprint/…) — plus the live §4 state: `SiteStatus` (Discovered→Worked→Depleted/Persistent/Ruptured), `Progress` (banked yield magnitude), `Understanding` (the branch gate), `UnderstandingToResolve`, `YieldDelivered`. A DataBlob with `[JsonProperty]` fields + deep-copy `Clone()`. **Not attached to any entity yet → byte-identical.** |
| `SiteMachine.cs` | **SE-1a (2026-07-12)** The PURE state machine (§4, agency-preserving, no timers). `Accrue(site, work, understanding)` — first work begins the study (Discovered→Worked), banks progress + understanding; a resolved site ignores further work. `BranchUnlocked(site)` — Understanding ≥ threshold (knowledge unlocks, never a timer). `Resolve(site)` — a worked+unlocked site transitions to Depleted (OneShot) / Persistent by Shape. Pure/static/deterministic (no clock/RNG/game state) so SE-1b's processor is a thin driver over it. Gauge: `FieldSiteTests`. |
| `FieldSiteFactory.cs` | **SE-1b (2026-07-12)** Puts a `FieldSiteDB` into the world as a NEUTRAL, point-in-space anomaly — NameDB + a fixed `PositionDB` (`MoveType.None`) + the site record, added via `system.AddEntity` (mirrors `JumpPoints.JPFactory`). `CreateAnomalySite(system, atPosition, name, …dials)`. **Not called in the live New-Game path → byte-identical** (no anomaly exists until an exploration-discovery slice spawns one). |
| `SiteWorkProcessor.cs` | **SE-1b + SE-1c + SE-2b (2026-07-13)** The DRIVER: a daily `IHotloopProcessor` keyed to `FieldSiteDB`. For each site, if a worker is present (`TryFindWorker` = the nearest non-neutral **ship** within `PresenceRadius_m` = 1000 km), it feeds `WorkPerDay`/`UnderstandingPerDay × multiplier` into `SiteMachine.Accrue` and records `WorkedByFactionId`. **SE-2b:** `BerthWorkMultiplier(worker, role)` = the worker's best MANNED, Role-matching Command Berth → `Grade × (1 + leaderSkill + Support/100)`; no manned matching berth → 1.0 (the SE-1b flat rate, additive/byte-identical). **SE-1c:** once understanding fills, it `Resolve`s the single-branch anomaly and calls `DeliverYield` **once** (`YieldDelivered` guard) to route the banked Progress into the Yield's consumer. No worker → no advance (pressure, not a timer). Sleeps on an empty system → byte-identical live. Gauge: `SiteWorkTests` + `SiteYieldTests` + `BerthWorkTests`. |
| `SiteYields.cs` | **SE-1c (2026-07-13)** The YIELD ROUTER (§3 Yield dial) — the "connect" that pays a resolved site's Progress into an existing consumer system. `DeliverResearch(game, factionId, points)` adds research points to the working faction's NEAREST BREAKTHROUGH (most-progressed researchable tech, ties by id) via `FactionDataStore.AddTechPoints`. Pure over game state. The other yields (Blueprint/Resource/Population/Leader/StrategicAsset/NetworkRoute) route into their own systems in later slices. Gauge: `SiteYieldTests`. |
| `CommandBerthDB.cs` | **SE-2a (2026-07-13)** The berth GEAR — one host's roster of installed Command Berths (`List<CommandBerth>`; a host can carry several, like an Enterprise-D). Each `CommandBerth` carries the §5 dials — `Role` (which `SiteRole` it works), `Grade` (work-rate tier), `Support` (leader competence boost), `Survivability` (incident-risk reduction), `Span` (force size) — plus its `ComponentName` identity. **SE-2b added occupancy** — `CommandBerth.CommanderID` (-1 = empty) + `BestOccupiedBerthFor`/`BestEmptyBerthFor`/`FindBerthOfCommander` (the reads seating + the work-rate use). `[JsonProperty]` + deep-copy `Clone()`. Byte-identical (nothing seats a leader until an order/NPC does). Gauge: `CommandBerthBaseModTests` + `BerthWorkTests`. |
| `BerthOps.cs` | **SE-2b (2026-07-13)** Seating + competence read. `SeatLeader(host, commander, role)` fills the best empty matching berth (`CommandBerth.CommanderID`) AND sets the leader's `CommanderDB.AssignedTo` back-reference (the shared convention the admin/scientist assignments use); `VacateBerth(host, commanderId)` frees both (reassignment / the grave rung, wired to death in SE-2c). `LeaderSkill01(manager, commanderId)` = the seated leader's `Experience / MaxLeaderExperience` (200), clamped 0..1 — the SE-2b site-work skill proxy (a dedicated Site-work BonusCategory is a later refinement). Gauge: `BerthWorkTests`. |
| `CommandBerthAtb.cs` | **SE-2a (2026-07-13)** The component that MAKES a berth buildable (the Site-Engine twin of `ResearchAcademyAtb`/`IntelDirectorateAtb`). On install adds a `CommandBerth` to the host's `CommandBerthDB` (seeding the blob if absent); on uninstall removes THIS component's berth by name (save/load-robust) and drops the roster when the last berth goes (the grave rung). `[JsonProperty]` dials + parameterless ctor (save/load). Six-point base-mod registration: `command-berth` template (`installations.json`, dials Role/Grade/Support/Survivability/Span, Role authored as a `GuiEnumSelectionList` over `Pulsar4X.Sites.SiteRole` like the ship bridge's Admin Level) + `default-design-command-berth` (`componentDesigns.json`) + Earth's start colony lists both (StartingItems + ComponentDesigns), so it's buildable from turn one. AttributeType FQN `Pulsar4X.Sites.CommandBerthAtb`. Gauge: `CommandBerthBaseModTests` (JSON→atb binding + install/seed/drop); `BaseModIntegrityTests` covers the material/unlock end. |

---

## Key locked decisions (design)

- **One engine, data-driven** — every episode is a row across ~7 dials (Location · Discovery · Worker · Role · Shape · Hook · Yield). Not parallel systems.
- **No timers** — an unresolved incident/persistent site applies steady PRESSURE; the player chooses when/how to act. The pressure is the clock (protects agency).
- **Branches compose** — accrued knowledge UNLOCKS branches, never closes them; understand → *then* choose (seal vs. ally), never railroaded; different rewards *or none*.
- **The persistent site can RUPTURE** into a crisis (the reward carries the risk) — the small-scale seed of the late-game crisis (SE-5).
- **Where a leader is posted sets their incident/death risk**, mitigated by the Command Berth's Survivability dial — one incident roll across all pillars (SE-2).
- **The Command Berth IS the delegate seat** (`docs/GOVERNANCE-AND-DELEGATION-DESIGN.md`) — built on the one `AdminSpaceAtb` seat mechanism; a ship fits several (Enterprise-D).

---

## Connections (Prime Directive)

- **Exploration/survey** (`GeoSurveyProcessor`, fog) — discovers sites.
- **People/commanders + academies** (X.0) — fill the berths; the grave rung (incident roll) is theirs.
- **Ground combat + the hex grid** (`GroundForcesProcessor`, `SurfaceGrid`, `RaiseUnit`, conquest) — the located-combat hook + surface substrate (SE-3/4).
- **Ships/fleets/movement** (`MoveToSystemBodyOrder`, `GroundTransport`) — reach + deploy.
- **Research / economy / population / jump-network / diplomacy / espionage** — the yield routes + branch consumers.
- **Delegation/governance** (`AdminSpaceAtb`, `AssignAdministratorOrder`) — the berth is the seat.
