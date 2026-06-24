# Diplomacy System — Design Survey and Plan

**What it needs to do:** Let factions have relationships with each other — allies, enemies, and everyone in between. Right now the game treats every non-player faction as hostile by definition, with no state machine, no treaties, and no way for factions to cooperate. This document maps what exists, what's missing, and what to build.

---

## Survey Findings — What Currently Exists

### The only inter-faction model: a three-way binary

`EntityFilter` (`Engine/DataStructures/EntityFilter.cs`) defines:

```csharp
[Flags]
public enum EntityFilter { None = 0, Friendly = 1, Neutral = 2, Hostile = 4 }
```

`EntityManager.GetFilteredEntities()` assigns these labels at query time:
- **Friendly** = same faction as the requester
- **Neutral** = `Game.NeutralFactionId` (-99) — stars, planets, asteroids
- **Hostile** = any other real faction you have a sensor contact for

That's it. "Hostile" is literally "a different faction and you can see them." There's no war flag, no treaty, no relationship value. You don't declare war — you just are at war with everyone who isn't you.

### FactionInfoDB — the hooks are stubs

`FactionInfoDB.cs` has two relevant fields:

```csharp
[JsonProperty]
public List<string> KnownSystems { get; internal set; } = new ();  // wired, actively populated

[JsonProperty]
public List<Entity> KnownFactions { get; internal set; } = new (); // declared, never populated
```

`KnownSystems` is real — it grows when a jump point survey completes, gates sensor visibility. `KnownFactions` exists but no code anywhere calls `.Add()` on it, reads from it, or checks it. It's a named field waiting for a system.

There's also a `FactionMaskIndex` / `FactionMask` bit-field system (32 slots) already used to gate sensor visibility. This could be the basis for access control (e.g., "allow this faction's ships into my logistics bases").

### Player vs. NPC: structural only

`FactionFactory.CreatePlayerFaction()` calls `owningPlayer.SetAccess(faction.Id, AccessRole.Owner)`. That's the only difference. A faction with no player attached is "NPC" in the sense that no human controls it — but there is no NPC AI processor, no doctrine data, no aggression flag, no faction type JSON field. The three scenario faction files (`uef.json`, `NutralsAndAllies.json`, `opposition.json`) all have identical schema with no behavioral attributes.

### Sensor contacts: functional

`SensorTools.GetDetectedEntites()` has a `filterSameFaction = true` flag that already skips your own ships. Cross-faction detection works: signal strength → inverse-square attenuation → threshold check → `SensorContact` created → self-registers into `FactionInfoDB.SensorContacts`. This gives you a list of detected foreign entities.

What the contact currently contains: the actual entity reference (live), last-known position, detection quality. At low detection quality, the original design intended a "sensor shadow" entity with partial info, but that was never implemented — any contact above threshold gives a direct reference to the real entity.

### Fire control: no IFF, no faction check

`SetTargetFireControlOrder.IsValidCommand()` checks:
- Does the commanding entity belong to the requesting faction?
- Does the target entity exist?
- Does the commanding ship have a fire control component?

No check that the target belongs to a different faction. A ship can legally be ordered to fire on a friendly. `GenericFiringWeaponsProcessor` also has no faction check before calling `FireWeapon()`. IFF is completely absent.

### Logistics: faction-blind (fixed as of this branch)

`LogiShipBidding()` previously iterated all `LogiBaseDB` entries in the system with no ownership check. Any faction's ship would freely bid on and service any base. Fixed: added `if(tbase.OwningEntity.FactionOwnerID != shippingEntity.FactionOwnerID) continue;` at line 124. Same-faction only until a diplomacy system exists to grant access.

### Commerce: no inter-faction money

`Ledger.cs` is a single-faction accounting book. It has `AddIncome()` and `AddExpense()`. No `Transfer(fromFaction, toFaction, amount)`. The only transaction categories are `InitialInvestment` and `Research`. `CargoTransferOrder` does not touch funds — cargo moves with no payment. Cross-faction cargo orders fail `IsCommandValid()` because it requires the commanding entity to be owned by the requesting faction.

### EventTypes: stubs only

`EventTypes.cs` has enum values for `TreatyAgreed`, `TechExchange`, `Communication`, `Diplomacy`, `Reparations` — copied from Aurora's database. Nothing fires them.

---

## Gap Analysis

| Feature | Status |
|---------|--------|
| Relationship state (allied/neutral/hostile/war) | ❌ Missing — binary Friendly/Hostile only |
| Faction discovery (first contact event) | ❌ Missing — `KnownFactions` stub never populated |
| Faction types / NPC doctrine | ❌ Missing — no aggression, no archetype, no behavioral flags |
| IFF in fire control | ❌ Missing — can target own ships |
| NPC AI processor | ❌ Missing — no automated faction behavior |
| Inter-faction trade / payment | ❌ Missing — Ledger is single-faction |
| Logistics access control | ⚠️ Fixed to same-faction — alliance exemption needs diplomacy system |
| Sensor detection cross-faction | ✅ Functional |
| Combat mechanics (once ordered) | ✅ Functional — player must issue orders |
| Diplomatic event types | ✅ Enum stubs — nothing behind them yet |
| `FactionMask` system for access gating | ✅ Functional infrastructure, unused for diplomacy |

---

## Design Plan

### Step 1 — Relationship State (foundation for everything else)

**New class:** `RelationshipState` (simple value class, not a DataBlob)

```csharp
public enum DiplomaticStance { War, Hostile, Neutral, Friendly, Allied }

public class RelationshipState
{
    public DiplomaticStance Stance { get; set; } = DiplomaticStance.Hostile;
    public int RelationScore { get; set; } = 0; // -100 to +100, drives stance thresholds
    public bool TradeAgreement { get; set; } = false;
    public bool LogisticsAccess { get; set; } = false;  // can use each other's bases
    public bool MilitaryAccess { get; set; } = false;   // can move through territory
    public DateTime? LastContact { get; set; } = null;
}
```

**New DataBlob:** `DiplomacyDB` — attached to the faction entity

```csharp
public class DiplomacyDB : BaseDataBlob
{
    [JsonProperty]
    public Dictionary<int, RelationshipState> Relationships { get; set; } = new();
    // Key = other faction's FactionOwnerID

    public RelationshipState GetRelationship(int otherFactionId)
    {
        if (Relationships.TryGetValue(otherFactionId, out var rel)) return rel;
        return new RelationshipState(); // default: Hostile
    }
}
```

This is the single source of truth for all inter-faction relations. Anything that needs to know "can Faction A do X to Faction B" queries this.

### Step 2 — Wire KnownFactions / First Contact

When `SensorScan` creates a new `SensorContact` for an entity belonging to an unknown faction:
- Check if that faction ID is in `FactionInfoDB.KnownFactions`
- If not: add the faction entity to `KnownFactions`, fire `EventTypes.Communication` event, create a default `RelationshipState` (Hostile) in `DiplomacyDB`
- This is first contact — the moment you know they exist

### Step 3 — Logistics Access Gate

`LogiShipBidding()` already has the faction check. Change it from a hard same-faction-only gate to a diplomacy query:

```csharp
// Allow if same faction OR if the relationship grants logistics access
var diplomacy = shippingEntity.GetFactionOwner.GetDataBlob<DiplomacyDB>();
var rel = diplomacy.GetRelationship(tbase.OwningEntity.FactionOwnerID);
if (tbase.OwningEntity.FactionOwnerID != shippingEntity.FactionOwnerID && !rel.LogisticsAccess)
    continue;
```

Same result now (access denied to all foreign factions), but the gate is now diplomacy-driven rather than hardcoded.

### Step 4 — IFF in Fire Control

`SetTargetFireControlOrder.IsValidCommand()` — add a check before accepting the order:

```csharp
// Prevent targeting own faction ships
if (targetEntity.FactionOwnerID == requestingFaction.FactionOwnerID) return false;
```

Separate question: should you be able to target an "Allied" faction's ships? Aurora allows it (friendly fire is on you). Same here — allow targeting anyone except your own faction.

### Step 5 — Faction Types and NPC Doctrine

Add to faction JSON (new optional fields, backward-compatible):

```json
{
  "IsNPC": true,
  "DoctrineWeights": {
    "Economic": 0.4,
    "Military": 0.2,
    "Tech": 0.2,
    "Expansion": 0.2
  },
  "DefaultStance": "Hostile"
}
```

`FactionInfoDB` gets:
```csharp
[JsonProperty] public bool IsNPC { get; set; } = false;
[JsonProperty] public DoctrineVector Doctrine { get; set; } = new();
[JsonProperty] public DiplomaticStance DefaultStance { get; set; } = DiplomaticStance.Hostile;
```

`DoctrineVector` is a simple struct with four `double` weights summing to 1.0. The NPC decision processor (Stage 5 of the economy plan) uses these weights to prioritize goals.

### Step 6 — Inter-Faction Commerce

When `CargoTransferOrder` executes a cross-faction transfer (both logistics access AND trade agreement are true):

1. Calculate payment: `amount × item.CreditValue × trade_modifier`
2. `sourceColony.GetFactionOwner.GetDataBlob<FactionInfoDB>().Money.AddIncome(TransactionCategory.Trade, payment)`
3. `destColony.GetFactionOwner.GetDataBlob<FactionInfoDB>().Money.AddExpense(TransactionCategory.Trade, payment)`

`TransactionCategory` gets a new `Trade` value. The Ledger already has the infrastructure — it just needs the new category and a transfer helper.

---

## Implementation Order

| Priority | Item | Depends On |
|----------|------|-----------|
| 1 | `DiplomacyDB` + `RelationshipState` data structures | Nothing |
| 2 | Attach `DiplomacyDB` to faction entity at creation | Step 1 |
| 3 | Wire `KnownFactions` / first contact via `SensorScan` | Step 1 |
| 4 | Logistics gate → diplomacy query | Step 2 |
| 5 | IFF check in fire control | Nothing (independent) |
| 6 | Faction type fields in JSON + `FactionInfoDB` | Nothing (independent) |
| 7 | NPC decision processor | Economy Stage 5 + Step 6 |
| 8 | Inter-faction commerce via Ledger | Logistics working + Step 1 |

Steps 1–3 are the foundation. Steps 4–6 can be done independently once the foundation is in.

---

## Key Files for Implementation

| File | What to Change |
|------|---------------|
| `Factions/FactionInfoDB.cs` | Add `IsNPC`, `Doctrine`, `DefaultStance`; populate `KnownFactions` |
| `Factions/FactionFactory.cs` | Attach `DiplomacyDB` at faction creation |
| `Engine/DataStructures/EntityFilter.cs` | Add `Allied = 8` flag if needed |
| `Sensors/SensorRecever/SensorScan.cs` | Wire first-contact detection to `KnownFactions` |
| `Logistics/LogisticsProcessor.cs` | Change hardcoded same-faction gate to diplomacy query |
| `Weapons/SetFireControlOrder.cs` | Add IFF check to `IsValidCommand()` |
| `Factions/Ledger.cs` | Add `Trade` to `TransactionCategory`; add transfer helper |
| `GameData/basemod/ScenarioFiles/*.json` | Add `IsNPC`, `DoctrineWeights`, `DefaultStance` |
| *(new)* `Factions/DiplomacyDB.cs` | `DiplomacyDB` DataBlob + `RelationshipState` class |

---

## Notes on NPC Combat Decision Loop

Once `DiplomacyDB` exists, the NPC processor can make combat decisions without guessing:

```
// Monthly (or on-demand trigger)
1. GetFilteredEntities(Hostile, npcFactionId) → detected enemy list
2. Skip any entity where DiplomacyDB.GetRelationship(target.FactionOwnerID).Stance >= Neutral
3. Score remaining targets by threat (nearest, heaviest sensor return)
4. Reuse SetWeaponsFireControlOrder + SetTargetFireControlOrder + SetOpenFireControlOrder
```

Steps 4 are the same order classes the player uses. The NPC sets `RequestingFactionGuid` to its own faction ID and the order system handles the rest. No special NPC code path needed — the existing order infrastructure is faction-agnostic.

---

## What NOT to Build Yet

- Complex treaty negotiation UI — the data model (Step 1) is sufficient for now; treaties are just setting boolean flags
- Espionage / infiltration — entirely new system, flag for later
- Reputation with minor factions — needs faction archetypes first (Step 5)
- Population happiness from foreign relations — hooks into colony system, flag for later
