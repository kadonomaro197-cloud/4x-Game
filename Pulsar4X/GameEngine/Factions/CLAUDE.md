# Factions — Subsystem Reference

Faction state, tech database, ability store, and NPC decision engine. Lives in `GameEngine/Factions/`.

---

## File Map

| File | Purpose |
|------|---------|
| `FactionInfoDB.cs` | Core DataBlob for a faction entity. Holds faction name, money ledger, tech/cargo stores, known systems, colonies, ship/component/industry designs, sensor contacts. |
| `FactionDataStore.cs` | The data layer inside FactionInfoDB (`FactionInfoDB.Data`). Holds `Techs`, `CargoGoods`, `LockedCargoGoods`, `ComponentTemplates`, and `Unlock()` / `IncrementTechLevel()` logic. |
| `FactionFactory.cs` | Creates faction entities and populates `FactionInfoDB`. `CreateFaction()` for code-created factions; `LoadFromJson()` for scenario-file-created factions (reads "name", "componentDesigns", "ordnanceDesigns", "shipDesigns", "species", "colonies", "isNPC", "doctrine"). |
| `DoctrineVector.cs` | **NEW** Struct: four float weights (Economic, Military, Tech, Expansion) describing NPC strategic priorities. Serialized with `[JsonProperty]`; lives on `FactionInfoDB.Doctrine`. |
| `NPCDecisionProcessor.cs` | **NEW SKELETON** `IHotloopProcessor` on `FactionInfoDB`, monthly. Only acts on factions where `IsNPC == true`. Evaluates `DoctrineVector` to pick the dominant goal each cycle. Decision implementation is a TODO. See wiring note below. |
| `RelationshipState.cs` | **NEW (substrate, 2026-06-30)** The per-PAIR diplomatic record + `DiplomaticStance` enum (War/Hostile/Neutral/Friendly/Allied). A relationship is a NUMBER on a track (`RelationScore` −100..+100) that events nudge via `AdjustScore(delta)`; the headline `CurrentStance()` is DERIVED from the score by named thresholds, with `AtWar` a latched override (`DeclareWar`/`MakePeace`). Carries the treaty flags (`TradeAgreement`/`LogisticsAccess`/`MilitaryAccess`) and `LastContact`. Plain value object (not a DataBlob). Tests: `DiplomacyTests`. See `docs/DIPLOMACY-DESIGN.md`. |
| `DiplomacyDB.cs` | **NEW (substrate, 2026-06-30)** A faction's whole diplomatic ledger: `Dictionary<int, RelationshipState>` (other-faction Id → standing). Attached to EVERY faction by `FactionFactory` (`CreateFaction` + `CreateBasicFaction` — so all four creation paths, incl. `LoadFromJson`/`CreatePlayerFaction`). Each faction keeps its OWN view (A↔B can disagree). `GetRelationship(id)` returns existing-or-fresh-Neutral WITHOUT storing (looking ≠ a relationship); `GetOrCreateRelationship(id)` persists the row for an actual event. **First behavior wiring landed 2026-07-01:** combat `CombatEngagement.AreHostile` now reads this — a MUTUAL Friendly/Allied stance suppresses hostility (the v1 "different faction = hostile" default is otherwise preserved). Gauge `DiplomacyIffTests`. Remaining behavior slices (first-contact event, commerce/logistics gating) are `TESTING-TRACKER.md` C6. |
| `Treaties.cs` | **NEW (teeth, 2026-07-01, #33)** The treaty levers. `TreatyType` enum (NonAggression/TradeAgreement/LogisticsAccess/MilitaryAccess/DefensivePact/Peace) + `Treaties.Propose(...)` — a treaty is proposed → considered (`WouldAccept`: the target's relation score vs. a per-treaty **trust threshold** — deeper treaty needs a higher score; no ordinary treaty mid-war; Peace only ends an actual war) → on acceptance the flag is set on BOTH `RelationshipState`s + both scores warm by `SigningBonus`. Adds `NonAggressionPact`/`DefensivePact` flags to `RelationshipState`. Pure on two `DiplomacyDB`s + an entity-level overload. **v1: accept is score-gated; the design's "archetype also decides" folds in when NPC personality data exists (threshold tweak, not a rebuild). Breaking a treaty = the betrayal penalty is a later slice.** Nothing calls Propose yet → no behavior change. Tests: `DiplomacyTreatyTests`. |
| `ReactiveDiplomacy.cs` | **NEW (#35, 2026-07-01)** The reactive "Are we good?" engine — the world acts on what it OBSERVES (the developer's marquee example: a rival sees your fleet near its space → an intent probe). `ExternalStimulus` enum (FleetNearBorder / AtWarWithTheirEnemy / YouAppearWeak / CrisisOnTheirBorder / YouBrokeATreaty / …) → `Overture(obs, theirViewOfYou)` returns a `DiplomaticOverture` (AreWeGoodProbe / AllianceOffer / WarningToStop / RequestDefenseFleet / DistrustGuardRises / …), gated by their current stance; `RelationDelta(obs)` is the direct needle-nudge for the trust/distrust stimuli. Pure/stateless — the INTERNAL demand engine pointed outward (big reuse). **The loop that feeds it observations (fleet-vs-contact each cycle) and turns an overture into a message/commitment is the wiring step; nothing calls it yet.** Fog cuts both ways (feed only what the observer detects). Tests: `DiplomacyReactiveTests`. |
| `ExchangeCatalog.cs` | **NEW (#35, 2026-07-01)** The data-driven catalog of everything two factions can trade (`ExchangeDef` = key + `ExchangeCategory` [7 families] + `ExchangeKind` Instant/Standing/Event/State + `ExchangeRoute` [which existing system it drives: Ledger/Logistics/Fleets/CombatIFF/Sensors/Research/Movement/GroundCombat/People/Espionage/Galaxy/DiplomacyDB]). The catalog IS the connection map — each row is a wire into a system that already exists. Broad representative in-code set (all 7 categories); meant to GROW and can move to JSON later. `ByCategory`/`ByKey` lookups. The commitment model that EXECUTES a chosen exchange (Instant → transfer now, Standing → emit orders each cycle) is the next step. Tests: `ExchangeCatalogTests`. |
| `CasusBelli.cs` | **NEW (teeth, 2026-07-01, #33)** The militarism GATE on war. `CasusBelli` enum (None/BorderDispute/BrokenTreaty/AllyDefense/ConfrontRival/Retaliation) + `CasusBelliRules.WarDeclarationMoraleImpact(gov, cb)` — the one-time morale/legitimacy delta of declaring war: justified-vs-naked sets the baseline, the `GovernmentDB` **Militarism** dial swings it (High → +, Low → −). The four corners: militarist+justified = morale BONUS; pacifist+naked = regime-threatening hit. Pure/static. Wiring the delta into legitimacy/morale is #31; where a casus belli comes from (broken-treaty event, Confront-Rival demand) is the INTERNAL⟷EXTERNAL handoff. Tests: `DiplomacyCasusBelliTests`. |
| `FirstContact.cs` | **NEW (behavior, 2026-07-01)** The diplomacy "front door": `FirstContact.OnDetection(detectorFactionEntity, detectedEntity, when)` — called from `SensorScan` on a real detection. When a faction first detects an entity of another non-neutral faction, records a MUTUAL Neutral relationship row on both `DiplomacyDB`s (stamped `LastContact`) and fires a first-contact `EventType` (Neutral by default — no auto-hostility). `DiplomacyDB.HasMet` guards it to fire once per pair. Defensive/no-throw (runs in the sensor hot loop); skips neutral/own-faction targets. Liveness counter `FirstContact.ContactCount`. Tests: `DiplomacyFirstContactTests`. |
| `GovernmentDB.cs` | **NEW (substrate)** The empire-wide regime as a MODULATOR (`docs/GOVERNMENT-AND-POLITICS-DESIGN.md`). Four 3-notch dials (Authority/Economy/Openness/Militarism) that derive **rule overrides** (`CrewPolicy()` → the M3-2 crew rule; `Discontent()`) and **coefficient overrides** (`TaxCeiling()`/`MoraleWeight()`/`ResearchMultiplier()`/`MilitaryBuildMultiplier()`/`WarMoraleFactor()`), plus a live classifier (`Name()`/`Description()` — iconic combos table + auto-fallback). **Substrate only — not attached to factions and not yet read by any processor;** the per-lever wiring is the next step. Tests: `GovernmentTests`. |

---

## Key FactionInfoDB Fields

```csharp
FactionInfoDB
  Abbreviation        string            — short faction code
  Money               Ledger            — credit balance with transaction log
  Data                FactionDataStore  — tech/cargo/locked-cargo store
  Species             List<Entity>      — faction's species entities
  KnownSystems        List<string>      — system IDs this faction has surveyed
  Colonies            List<Entity>      — colony entities owned by this faction
  ShipDesigns         Dict<string, ShipDesign>
  MissileDesigns      Dict<string, OrdnanceDesign>
  ComponentDesigns    ReadOnly<Dict<string, ComponentDesign>>  — all designs including unresearched
  IndustryDesigns     Dict<string, IConstructableDesign>       — only what can be built right now (includes refined materials)
  SensorContacts      Dict<int, SensorContact>
  IsNPC               bool              — true = AI-controlled; NPCDecisionProcessor acts on these
  Doctrine            DoctrineVector    — NPC priority weights (ignored for player factions)
```

---

## FactionDataStore — the locked/unlocked split

`FactionInfoDB.Data` holds two parallel inventories:

- **`CargoGoods`** — items available to the faction right now (mineable minerals, buildable materials, unlocked component designs).
- **`LockedCargoGoods`** — items known to exist but not yet available (locked behind unresearched techs).

`Unlock(id)` moves an item from LockedCargoGoods to CargoGoods.
`IncrementTechLevel(tech)` increments the level, then calls `Unlock()` for each entry in `tech.Unlocks[tech.Level]`.

**IndustryDesigns sync gotcha:** `FactionInfoDB.IndustryDesigns` (the "things we can build" list) is populated at startup by `SetIndustryDesigns()`. When a `ProcessedMaterial` is unlocked mid-game via tech research, `Unlock()` moves it to `CargoGoods` but `IndustryDesigns` is NOT updated automatically — so colonies can't queue the material even though it's now available. **Fix in place (`ResearchProcessor.cs`, this branch):** after `AddTechPoints` causes a level-up, `DoResearch()` iterates `tech.Unlocks[tech.Level]` and syncs any materials into `IndustryDesigns`.

---

## NPC Doctrine

`DoctrineVector` has four float weights. They are relative, not normalized — whichever is largest drives that cycle's decision. Defaults to all zeros (no decision is made).

`NPCDecisionProcessor` registers via the standard `IHotloopProcessor` auto-discovery. It targets entities with `FactionInfoDB`. **The GlobalManager wiring gap is FIXED (keystone, 2026-06-30):** `MasterTimePulse.SimulateTimeUntil` now processes `GlobalManager.ManagerSubpulses` after the per-system block, so faction-level processors fire on their schedule (NPCDecisionProcessor: `FirstRunOffset` 5d, `RunFrequency` 30d). It still no-ops on player factions (`IsNPC == false`) and its `Tick` decision body is still a TODO — but the *loop now runs*, which is the prerequisite every faction-level autonomous system (NPC turns, the internal/external politics engines) was waiting on. Liveness gauge: `NPCDecisionProcessor.TickCount` (static; climbs each ProcessManager call) — asserted by `FactionEconomyTests.FactionLevelProcessors_FireOnceGlobalManagerIsIterated`.

**Scenario JSON keys (FactionFactory.LoadFromJson):**
```json
{
  "name": "My NPC Faction",
  "isNPC": true,
  "doctrine": {
    "economic": 0.15,
    "military": 0.55,
    "tech": 0.15,
    "expansion": 0.15
  }
}
```

---

## Gotchas

1. **`FactionDataStore` is not `FactionInfoDB`.** `FactionInfoDB.Data` is the DataStore. Code that gets the data store via `factionInfoDB.Data` and then calls `Data.CargoGoods[id]` is correct. Don't confuse the two.

2. **`IndustryDesigns` must be refreshed after any mid-game unlock.** The startup path (`SetIndustryDesigns`) and the startup-item path (`ColonyFactory.cs:43`) both do this correctly. Any new unlock call site must also update `IndustryDesigns` — either by calling the sync loop or by calling `SetIndustryDesigns()`.

3. **NPCDecisionProcessor now FIRES (GlobalManager wired, 2026-06-30) but its decision body is still a stub.** The loop runs (see note above); the `Tick` that turns doctrine weights into actual orders is the TODO. So it's safe and live but does nothing yet — implement the decision body when building NPC turns.

4. **Every material a *starting* `ComponentDesign` needs must be in the colony blueprint's `StartingItems`.** `ColonyFactory.CreateFromBlueprint` unlocks `StartingItems` into `CargoGoods` first, then builds each `ComponentDesigns` entry — and `ComponentDesigner` (`Engine/Components/ComponentDesigner.cs:63`) looks up every `ResourceCost` material in the **unlocked** `CargoGoods`. A required-but-not-unlocked material crashes New Game / Quickstart in `ComponentDesigner` (`GUID object {id} not found`). This happened when `electronics`/`ree-magnetics` were added to the starting laser weapon and to the Ship Yard / Research Lab installations (commit `28967c7`) without being added to `earth.json` `StartingItems` — fixed by adding them. When you give a starting installation/weapon a new material cost, add that material to `StartingItems` too. (Related defensive change: `CargoDefinitionsLibrary.GetAny(string)` returns null instead of throwing `Sequence contains no elements`, so the failure names the missing id.) **Separate latent bug:** `TemplateFiles/ordnance.json` references `gallicite`, which is **not defined** as a mineral or material — harmless until that ordnance design is built, then it faults the same way.
