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

`NPCDecisionProcessor` registers via the standard `IHotloopProcessor` auto-discovery. It targets entities with `FactionInfoDB`. However, **faction entities currently live in the GlobalManager, which MasterTimePulse does not iterate** — so this processor will not fire until either:
1. The GlobalManager is added to `MasterTimePulse.DoProcessing()`, or
2. The processor is triggered manually (e.g., from a monthly game-time event).

This is a known wiring gap. The processor itself compiles and will not crash startup.

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

3. **NPCDecisionProcessor wiring is incomplete.** See note above. Don't remove it — it's the intended integration point. Wire the global manager when implementing NPC turns.

4. **Every material a *starting* `ComponentDesign` needs must be in the colony blueprint's `StartingItems`.** `ColonyFactory.CreateFromBlueprint` unlocks `StartingItems` into `CargoGoods` first, then builds each `ComponentDesigns` entry — and `ComponentDesigner` (`Engine/Components/ComponentDesigner.cs:63`) looks up every `ResourceCost` material in the **unlocked** `CargoGoods`. A required-but-not-unlocked material crashes New Game / Quickstart in `ComponentDesigner` (`GUID object {id} not found`). This happened when `electronics`/`ree-magnetics` were added to the starting laser weapon and to the Ship Yard / Research Lab installations (commit `28967c7`) without being added to `earth.json` `StartingItems` — fixed by adding them. When you give a starting installation/weapon a new material cost, add that material to `StartingItems` too. (Related defensive change: `CargoDefinitionsLibrary.GetAny(string)` returns null instead of throwing `Sequence contains no elements`, so the failure names the missing id.) **Separate latent bug:** `TemplateFiles/ordnance.json` references `gallicite`, which is **not defined** as a mineral or material — harmless until that ordnance design is built, then it faults the same way.
