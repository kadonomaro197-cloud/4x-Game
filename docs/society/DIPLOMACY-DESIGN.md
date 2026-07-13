# Diplomacy System — Design Survey and Plan

**What it needs to do:** Let factions have relationships with each other — allies, enemies, and everyone in between. Right now the game treats every non-player faction as hostile by definition, with no state machine, no treaties, and no way for factions to cooperate. This document maps what exists, what's missing, and what to build.

> **Two halves of this doc.** The **Survey + mechanical Design Plan** below (Steps 1–6) is the *substrate* — the data model (`DiplomacyDB`, `RelationshipState`), IFF, first-contact wiring, commerce. It's correct and stays. Layered ON TOP of it is **"EXTERNAL politics — politics with teeth"** (near the end, added 2026-06-30) — the *decision-engine* design that makes diplomacy a thing a player can MAIN or delegate, to the same **Every-Layer-a-Complete-Game** bar as the INTERNAL politics layer (`docs/society/GOVERNMENT-AND-POLITICS-DESIGN.md`). The substrate is the plumbing; the teeth section is what makes it a game. Read both; build the substrate first.

> **STATUS UPDATE 2026-07-07 — the keystone wall is down; the Foreign Minister is buildable now.** A code survey found the three "hard prerequisite" keystones (see the blast-radius section) essentially cleared: **(1) the GlobalManager trap is FIXED** — `NPCDecisionProcessor` fires monthly; **(2) detection-quality is NO LONGER a prerequisite** — `SignalQuality` was CUT (`docs/combat/DETECTION-DESIGN.md`) and the hidden-info gradient now lives in the **Information Ledger** (`docs/society/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md`), not a sensor field; **(3) hostility-from-diplomacy is substantially DONE** — `CombatEngagement`/`AreHostile` reads `DiplomacyDB` (treaties + stance suppress a fight; fire-control IFF may still lag). So the substrate — relationship track (`RelationshipState`/`DiplomacyDB`), first-contact wired into the live sensor scan (`FirstContact`), treaty *effects* gating combat + logistics, war→legitimacy — is **built and wired.** What's missing is the **DECIDER**. The Foreign Minister (and its NPC mirror) is that decider; it drives the built-but-**DARK** machinery: the reactive **"Are we good?" overture engine** (`ReactiveDiplomacy.Overture` — no observation feeder yet), autonomous **treaty proposal** (`Treaties.Propose` — only DevTools/scaffold calls it), the **exchange-catalog commitment executor** (`ExchangeCatalog` — inert data, 27 levers, nothing consumes them), and the **casus-belli morale gate** (`CasusBelliRules` — computed, never applied). The FM is seated in the leader pipeline (`docs/society/GOVERNANCE-AND-DELEGATION-DESIGN.md`) — see "The delegate — the Foreign Minister" below.

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

## EXTERNAL politics — politics with teeth (the decision-engine layer) — LOCKED 2026-06-30

The substrate above (Steps 1–6) gives factions *relationships*. This section makes those relationships a **game** — a pillar a player can MAIN (a whole playthrough won by diplomacy, never firing a shot) or **delegate and ignore** (a Foreign Minister runs it while you play conqueror). Same bar as the INTERNAL layer in `docs/society/GOVERNMENT-AND-POLITICS-DESIGN.md`; this is its outward-facing twin. **Two theaters: INTERNAL = hold your own house; EXTERNAL = deal with the neighbours.** They share one engine and constantly hand off to each other.

> **The developer's framing (why this matters):** *"x4 games make these lack. Think of a player that auto-resolves everything — combat and colony building — because those aspects are well done. That's a milestone. Vice versa as well."* The bar is symmetric: diplomacy must be good enough that a player would happily auto-resolve **combat** to focus on **politics**, exactly as today a player auto-resolves politics to focus on combat. Neither side is the throwaway.

### The core decision (the lever)
**Who do you make a friend, who do you make an enemy, and what does each cost you — abroad AND at home?** Every foreign move (open trade, sign a pact, demand tribute, declare war) has a price paid in two currencies at once: the **foreign** ledger (the other faction's relation score, their allies' reactions) and the **domestic** ledger (your blocs and legitimacy — see INTERNAL). A trade deal that enriches you may enrage your Militarists; a war your Militarists demanded bleeds morale through war-weariness if it drags. **There is no free foreign policy** — that two-front cost IS the depth.

### First contact — the EVENT (the Star Trek / Babylon 5 aspect)
First contact is a **moment, not a line item.** When your sensors first resolve a contact belonging to an unknown faction (the substrate's Step 2 wiring on `SensorScan` → populate `KnownFactions` + fire the event), the game **stops and tells you** — a real event with a decision attached, not a silent dictionary insert:
- **Who blinked first** matters — did you detect them (you hold the information edge, ties to DETECTION/EMCON: a dark first-contact is a different opening than being hailed loud) or did they hail you?
- The **opening stance** is set by *their* archetype + *your* government lean, not hardwired Hostile — a pacifist explorer meeting a fellow trader opens Neutral-curious; a militarist meeting a known raider opens at the edge of war.
- The first decision: **how do you answer** — hail openly (loud, builds relation, reveals you), observe silently (keep the edge, learn first), or warn off (stake a border). This is the cradle of the whole relationship.

First contact is where the **late-game frontier** lives too: a *super-alien* / precursor power or a **crisis faction** (the parked "late-game crisis" aspect) arrives through exactly this path — a first-contact event with a power you can't simply fight, forcing diplomacy as survival. The event system is the same; the stakes scale.

### The relationship as a TRACK (earned cradle-to-grave)
The substrate's `RelationScore` (−100…+100) and `DiplomaticStance` ladder (War ▸ Hostile ▸ Neutral ▸ Friendly ▸ Allied) are the meter. The TEETH are that the score is **earned and lost through actions the player takes**, never a number you set:
- **Earns up:** honoured treaties, delivered trade, joint war against a common enemy, gifts/aid, time at peace.
- **Earns down:** broken treaties (the big one — betrayal craters the score AND signals every *other* faction you're untrustworthy), border incursions, espionage caught, backing their enemy, sanctions.
- **It's a memory, not a mood** — a faction remembers what you did. This makes the score a *reputation* (your standing with the whole neighbourhood), which is what lets a "diplomatic playthrough" exist: you build a web of trust that's an asset as real as a fleet, and one betrayal can burn it.

### Treaties — the levers (each a real, costed decision)
Treaties are the moves. The substrate stores them as flags on `RelationshipState` (`TradeAgreement`, `LogisticsAccess`, `MilitaryAccess`); the teeth give each one a **cost, a benefit, and a domestic consequence** so signing is a decision, not a checkbox:

| Treaty | Foreign effect | Domestic consequence (INTERNAL handoff) |
|---|---|---|
| **Non-aggression** | locks stance ≥ Neutral; breaking it is the betrayal penalty | calms war-demands; Militarists grumble |
| **Trade agreement** | opens inter-faction commerce (Step 6); both profit | pleases Merchants; may anger Labor (foreign competition) |
| **Logistics/transit access** | their ships use your bases / cross your space | Order/security blocs uneasy (foreigners in your space) |
| **Defensive pact** | drags you into their wars (and them into yours) | Militarists pleased; Pacifists alarmed; a real entanglement |
| **Tribute / vassalage** | one pays the other; the strong lever over the weak | pride vs. coffers — the tributary's blocs seethe |

Each treaty is **proposed → considered → accepted/countered/refused** (their archetype + relation score decides), and each one you sign **ripples into your INTERNAL politics** — which is the whole point: foreign policy is a domestic act.

### Casus belli — war needs a REASON (the militarism gate)
You cannot declare war for free. **A war without justification is a legitimacy/morale hit** (your own people ask "why are we dying for this?"); a war with a **casus belli** (a border dispute, a broken treaty, a "Confront [Rival]" demand your own Militarists raised, defence of an ally) is *accepted* by your population. The cost of war is gated by the **MILITARISM dial** (`docs/society/GOVERNMENT-AND-POLITICS-DESIGN.md`):
- **Militarist regime** → casus belli is cheap/easy, war is martial pride (morale **bonus**), conquest is legitimate.
- **Pacifist regime** → casus belli is expensive, war is war-weariness (morale **penalty** that compounds the longer it runs), an unjustified war can topple you.
This is the seam where EXTERNAL war meets INTERNAL legitimacy — and it's why the militarism dial earns its place (it's the coefficient on this exact trade).

### The handoff — INTERNAL ⟷ EXTERNAL (the two engines, one loop)
The layers are not separate games; they feed each other every cycle:
- INTERNAL → EXTERNAL: a **"Confront [Rival]"** demand from your Militarists *is* a casus belli your population already backs; a "Lower Taxes" demand pushes you toward a **trade agreement** for revenue; an "End the War" demand forces you to **sue for peace**.
- EXTERNAL → INTERNAL: a **war outcome** feeds **legitimacy** (win → up, lose → down, per the per-system model); a **broken treaty** by a rival can rally your blocs; **war-weariness** is a standing morale input while a war runs; a lucrative **trade deal** grows the Merchant bloc.
- The **delegate** symmetry holds: just as the Interior Minister auto-runs INTERNAL, the **Foreign Minister** auto-runs EXTERNAL.

### Government RE-SKINS external politics too (consistent with the internal tweak)
Same modulator principle as the INTERNAL layer's re-skin: government type changes the **vocabulary and process** of foreign policy, not just the numbers. A **democracy** ratifies treaties (a legislative act, slow, public, hard to break); a **dictatorship** issues pacts by fiat (fast, personal, as brittle as the ruler); a **theocracy** frames relations as crusade/communion (the faithful vs. the heathen); a **hive** barely does diplomacy at all (it expands or it doesn't). The same `DiplomacyDB` underneath; the string table + process flags (from `GovernmentDB`) re-skin how it reads and how fast/bindingly treaties move.

### The delegate — the Foreign Minister (the auto-resolve path)
A commander (talent-gated, M3 people pool), seated as **Foreign Minister**. The player sets a **doctrine/stance** ("seek allies," "isolationist," "expand by tribute," "keep the peace"), and the minister **auto-handles first-contacts and treaty proposals per that stance, at a competence cost** (a skilled minister lands good deals and reads rivals; a poor one signs bad pacts and misses betrayals). So a war-focused player delegates foreign policy and is merely *notified* of the big moments; a diplomacy-focused player handles every contact personally and never delegates combat instead. **That symmetry is the Every-Layer bar, applied outward.**

**Seated in the leader pipeline (2026-07-07 — the overt twin of the Spymaster).** The FM runs the same six-rung people pipeline as every leader (`docs/society/GOVERNANCE-AND-DELEGATION-DESIGN.md`), and its org mirrors the espionage Director→station-chief→agents shape line-for-line:
- **Empire Foreign Minister** (cabinet) — sets overall doctrine / posture.
  - **Per-faction Foreign Minister** (a station chief *per met rival*) — owns the relationship with *that* rival; proposes/responds.
    - **Envoy / Ambassador** (the field hands) — Envoy for a one-shot negotiation, Ambassador posted for the standing intel window.
- Stance presets (above) are **data-driven and re-skinned by government** (a democracy ratifies; a dictatorship decrees), **biased for NPCs by `DoctrineVector`**, with **contracts** on the posting and the **race/government modulators**. Competence (rung 4) = how well deals land / how well rivals are read.

**The FM MVP (2026-07-07):** seat a per-faction FM + **turn on the reactive "Are we good?" overture loop** (the highest-value "alive galaxy" payoff) + the **NPC counterparty** (the NPC's FM autonomously proposes / responds / declares per its `DoctrineVector` stance — delegation = NPC AI, the mirror) + the one genuinely new piece, the **observation feeder** (fleet-position / territory → the stimuli `ReactiveDiplomacy.Overture` is already waiting for). **Co-requisite, same lesson as espionage: an FM is only worth building if the NPC runs one too** — proposing deals to an inert opponent is solitaire. Follow-ons (each "wire a DARK thing to the FM's decisions," not a new system): the **commitment executor** (exchange catalog → real money/cargo/fleet orders), **betrayal tracking** (promised-vs-delivered), and the **casus-belli morale gate**.

### The EXTERNAL frontier — espionage, wormhole networks, super-aliens, the crisis (placed, not parked-blindly)
These are the *deeper* external aspects the developer named. They are **later**, but they belong on this same spine, not as bolt-ons:
- **Espionage / covert agents** (the B5 "politics with teeth" underside) — **now designed in full: `docs/society/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md`.** It's the **hidden-information ENGINE** that makes the locked full-version diplomacy actually a reading game: the per-rival **Information Ledger** (Inferred→Confirmed→Stale facets — fog-of-war for politics), agents as the M3 intelligence arm (Spymaster delegate + operatives), the broad **covert-action catalog** (gather / steal-tech / sabotage / sow-unrest / turn / disinformation / counter-intel), and the **risk/reward detection bet** (caught = relation-hit → betrayal penalty → casus belli for THEM; agents caught/killed/turned). EXTERNAL-reaches-into-their-INTERNAL via *sow unrest*; the mirror (NPCs spy on you) makes counter-intel a standing decision. Rides the detection-quality fix.
- **Wormhole / jump-gate networks** (the Stargate aspect) — already partly real (jump points + `KnownJumpPoints`); the political layer is **who controls the gate** — a gated chokepoint is a diplomatic asset (grant/deny transit = the logistics-access treaty with a map behind it), and connectivity feeds the per-system legitimacy model (a well-gated province is easier to hold).
- **Super-aliens / late-game crisis** — arrives via the first-contact event with stakes that **force** cooperation (a power you can't out-build), the pressure-release the developer wants so the late game doesn't go stale. Diplomacy-as-survival. **LOCKED 2026-06-30 (options pass, "A"): build A1 → A2 → toward the A3 vision.** A1 = **an awakening super-power** (a faction tier *above* normal rivals, arriving through the first-contact event; you can't out-build it head-on) — this is the capstone, and the **key reason it matters: a crisis is the only thing that makes a *dominant* player NEED allies**, so it's what makes the entire external-politics cluster pay off when it's hardest to. A2 = a **systemic crisis** (plague / AI-revolt / resource-collapse / hazard-escalation, riding existing systems) added next as a **mid-game** pressure that kills mid-game staleness too. A1-then-A2 together **achieve the A3 vision** (escalating tiers across the whole game) without committing to the full tiered build up front.

### Cradle to grave (external politics)
> **first contact** (the EVENT — you learn a faction exists) → a **relationship TRACK** built/burned by your **actions** (treaties honoured, betrayals committed) → **treaties** as levers (trade/access/pact/tribute), each with a **domestic** cost → **casus belli** gates war (justified = accepted, unjustified = legitimacy bleed) → **war/peace outcomes** feed back into INTERNAL legitimacy + morale → a relationship can **die** (betrayal, conquest, your faction or theirs destroyed) — and a burned reputation is a real, lasting loss, the grave rung of the diplomatic game.

### Connections (Prime Directive)
- **Detection/sensors** (built) — first contact rides `SensorScan`; who-sees-whom-first sets the opening edge; EMCON shapes the meeting.
- **INTERNAL politics / government** (designed) — the constant two-way handoff above; government re-skins + modulates external policy; militarism gates casus belli.
- **Combat / fleets** (built) — war is the EXTERNAL lever's violent end; defensive pacts pull fleets into others' wars; IFF (Step 4) makes "who can shoot whom" a diplomacy query.
- **Economy / logistics / commerce** (built/substrate) — trade and access treaties open the inter-faction money + base-sharing paths (Steps 3, 6).
- **People** (M3 talent) — the Foreign Minister and covert agents are people-resources.
- **NPC doctrine** (`DoctrineVector`, `NPCDecisionProcessor` skeleton) — archetypes decide how a faction answers contact, proposes/accepts treaties, and chooses war; this layer gives the dormant processor its *foreign* goals (the trap: faction entities live in `GlobalManager`, which `MasterTimePulse` doesn't iterate — the same wiring problem as the internal layer; solve once for both).

### Build order (design now; build after the INTERNAL layer + substrate)
substrate Steps 1–6 (`DiplomacyDB` + relations + IFF + first-contact wiring + commerce) → first-contact **EVENT** (the moment + opening-stance rules) → treaties as **costed levers** with the INTERNAL handoff → **casus belli** + the militarism gate → war-outcome → legitimacy/morale feedback → the **Foreign Minister** delegate → *(frontier, later)* espionage → gate-control politics → the late-game crisis. **Solve the GlobalManager-not-iterated processor trap once** — it blocks both the internal demand engine and the NPC foreign loop.

### Open (decide when we build)
- Relation-score dynamics (how fast trust builds vs. how hard betrayal craters it) — calibration/feel.
- Whether the player can target an **Allied** faction's ships (Aurora says yes; the substrate Step 4 leans yes — confirm).
- First-contact event cadence + how much info a contact reveals at first sight (ties to the degenerate detection-quality signal — see `GameEngine/Sensors/CLAUDE.md`).
- How "enough provinces rebel" (INTERNAL central collapse) interacts with a rival **exploiting** your unrest via espionage (the EXTERNAL-into-INTERNAL reach).

---

## Making politics FUN + the CONNECTION layer — LOCKED 2026-06-30

This is the lock that turns the substrate + the teeth into a *game*. It answers the developer's two demands: **(1) make "playing politics" actually fun** (4X has always failed here), and **(2) connect everything** — a diplomatic deal must reach down into the physical systems and make them act. The unifying goal the developer set: **the more that can be exchanged between two factions — and the more the world reacts on its own — the deeper and more fun diplomacy gets.**

### Why 4X politics is boring, and the fix
4X diplomacy is a **spreadsheet between empires**: you move a slider, click "propose trade," watch a number. It's dead because (a) it's a number you set, not a thing you *do*; (b) the other side is a black box — you never know *why* they act; (c) there's no *face*; (d) it's optional flavor you can ignore and still win; (e) perfect information means no bluff, no reading, no poker. The games that got pieces right — Crusader Kings (politics as *people* with secrets and schemes), the board game Diplomacy (*negotiation + betrayal under hidden information*), Alpha Centauri (*leaders with faces*) — all point the same way: **people, hidden information, and stakes that hit home.**

### The three fun pillars (LOCKED — full version)
1. **People, not numbers.** Rival factions are led by a **character** (a commander entity with archetype + traits + a readable agenda); your diplomats are characters too (skill + traits that change outcomes and spawn stories). Politics is "Ambassador Chen vs. Chancellor Vex," never "+trade with Faction 3."
2. **Hidden information is the core (FULL version locked).** You do NOT see a rival's true stance or intent — you see their **behavior** (fleet moves, who they treat with, message tone) and must **infer** it, like reading a sonar contact from its noise. **Intelligence is the currency that buys the truth** — a posted ambassador or a spy converts *hidden → known*. This makes diplomacy a *reading* game (poker/CK3 fun), makes **bluffing** real (mass a fleet under EMCON-dark and strike a "friend"; rattle a sabre you won't swing), and gives **espionage a load-bearing job** instead of being a bolt-on. *(Accepted cost: the full version makes detection-quality and the GlobalManager fix HARD prerequisites — see the blast radius.)*
3. **Negotiation is a scene, not a menu.** A treaty is tabled → countered → sweetened/threatened, with the diplomat's skill + the relation + their archetype setting how hard they bargain. **Leverage decides terms** (a fleet on their border, a resource they need, dirt from a spy). The developer's framing, locked as the design metaphor: **diplomacy is a closing fight** — you have positions, apply pressure, read the enemy through the fog, and **commit or bluff**. Standoff-vs-brawl ⟷ pressure-vs-concede. The nudge-back-and-forth IS the gamble.

> **Negotiation depth — LOCKED 2026-06-30 (options pass, "C"):** the target is **C2, the leverage-exchange SCENE** (table offer → counter → adjust with leverage), and **C2 is the CONVERGENCE INTERFACE** — diplomat skill + military/economic leverage + espionage intel all flow *into* this one interaction; it's the payoff screen for the whole external cluster. C2 is also the **foundation for C3** (character-driven intrigue): C3's best bit — *leader agendas you can learn and exploit* — folds in **for free via espionage** (the Information Ledger learns a rival's disposition/secrets; C2 spends that intel as leverage), so we grow toward C3 without a separate build. **C1 (bare costed-flags) is rejected as the experience** — it's only the under-the-hood substrate/MVP fallback, never the thing the player feels. Build: C2 scene on top of the costed-lever substrate; deepen toward C3 as espionage + leader-character data arrive.

Three supports: **scarcity** (few good diplomats + limited attention = you can't court everyone, so *who* you invest in is the strategy), **memory/reputation** (the relationship TRACK — betrayals remembered and avenged, the source of retold stories), and **load-bearing stakes** (a diplomatic path to victory AND to ruin — if you can ignore it and still win, it's flavor; we wire it so you can't).

### Diplomats — the people arm of politics (how they tie in)
Diplomats are the **politics arm of the M3 people-as-a-resource pool** — the same unifying pattern as everywhere else in the game:

| Pillar | The people who run it |
|---|---|
| Combat | Admirals (doctrine) |
| Colonies | Governors |
| Research | Scientists |
| **Politics** | **Diplomats** |

- **Roles** (one person grows through them): **Envoy** (sent for a specific negotiation — skill sets the terms), **Ambassador** (POSTED at a foreign court — the continuous *window into their intentions*, the hidden-info reveal), **Spy/Agent** (the espionage arm — steal hidden info, sabotage).
- **Two tiers** (the Every-Layer symmetry): the **Foreign Minister** is the delegate-autopilot (hand him the portfolio + a stance, ignore politics — the conqueror's path); individual diplomats are the **hands** you task personally (the politics-mainer's path).
- **Grave rung:** a diplomat can fail, provoke an incident, be expelled, be caught spying (→ the betrayal penalty), be assassinated, or be **turned and defect** (a rival spymaster reaching into your house — the mirror). Losing a master diplomat hurts like losing a veteran admiral.

### The COMMITMENT model — a deal emits real orders (the keystone connection)
**This is the connective tissue that makes politics load-bearing.** A treaty is not a flag that sits there — it is a **promise with teeth** that reaches into the physical systems:
- An exchange is either an **instant transfer** (money/tech/cargo *now*) or a **standing commitment** (recurring supply, a posted fleet, a pact that triggers on a condition).
- A standing commitment **emits real orders** into the existing systems each cycle: "send a defense fleet to planet A" emits a **fleet move order** + a standing ROE; "send supplies" emits a **logistics route**; "pay tribute" emits a recurring **money transfer**.
- **Promised vs. delivered is tracked.** There is a gap between the ambassador *agreeing* and the admiral *actually sailing the ships* — and that gap is the **betrayal mechanic**. Each cycle the system checks: did you honor it? Honor → reputation climbs; renege → the betrayal penalty craters your standing with the whole neighbourhood. *(This honor-check is one of the autonomous loops blocked by the GlobalManager trap — see prerequisites.)*

### The EXCHANGE CATALOG — everything two factions can trade, and why (LOCKED: build broad)
The developer's depth principle: **the more that can be exchanged, the deeper diplomacy gets.** Build the catalog *broad*. Every entry names what's exchanged, why each side wants it, instant-vs-standing, and — crucially — **which physical system it routes into** (so the catalog IS the connection map; each row is a wire into a system that already exists or is named in the blast radius).

| Category | Exchange | Why / leverage | Type | Routes into (system) |
|---|---|---|---|---|
| **Economic** | Lump-sum payment / gift | buy goodwill, seal a deal | instant | Ledger (cross-faction transfer) |
| | Subsidy / recurring payment | prop up a client | standing | Ledger |
| | Tribute / vassal payment | the strong bleed the weak | standing | Ledger |
| | Loan / reparations | finance or apologise-with-cash | standing | Ledger |
| | Trade agreement (open commerce + tariff terms) | mutual profit | standing | Logistics + Ledger (commerce) |
| | One-time cargo (minerals/materials/fuel/ordnance/provisions) | cover a shortage | instant | Logistics (cross-faction cargo order) |
| | Standing supply line | sustained shortage (the developer's "request supplies") | standing commitment | Logistics (route) |
| | Resource access rights (mine in my territory) | exploit what they can't reach | standing | Mining/Logistics + access gate |
| | Sell/gift a ship or whole fleet | offload hulls / arm a proxy | instant | Fleets (transfer ownership) |
| **Military** | Station a defense fleet at their world | answer a pirate/crisis threat (the developer's example) | standing commitment | Fleets/Orders (move + ROE) |
| | Hired/mercenary fleet (rent force for money) | force without a pact | standing | Fleets + Ledger |
| | Loan ground troops / an army | help take or hold a planet | standing | Ground combat + Fleets (transport) |
| | Coordinate an attack on a common enemy | concentrate force | event/standing | Combat (joint targeting) |
| | Defensive pact (auto-join if attacked) | deterrence; entanglement | standing trigger | Combat/IFF + Fleets |
| | Military alliance / joint war declaration | win a war together | standing | Combat/IFF |
| | Non-aggression pact | buy a quiet border | standing state | Combat/IFF (stance lock) |
| | Ceasefire / armistice / peace | end a war | state change | Combat/IFF |
| | Military access / transit rights | move through their space | standing access | Movement + access gate |
| | Basing rights | forward-deploy at their base | standing access | Logistics/Fleets + gate |
| | Demilitarized-zone / border agreement | reduce friction | standing state | Combat trigger (no-engage zone) |
| **Information** | Sensor / contact-data sharing (shared fog) | see what they see | standing | Sensors (share `SensorContacts`) |
| | Star charts / jump-point maps | reveal the map | instant | `KnownSystems`/`KnownJumpPoints` |
| | Technology exchange / tech gift or sale | leap a research gap | instant | Research/`FactionTechDB` |
| | Sell a third party's secret (dirt) | intel as a tradable good | instant | Espionage/Events |
| | Joint-espionage pact / no-spy pact | gang up, or stand down | standing state | Espionage |
| **Territorial** | Jump-gate / wormhole transit grant | control the chokepoint (Stargate aspect) | standing access | Movement + gate-control |
| | Logistics-base access | use each other's depots | standing access | Logistics (the gate, already diplomacy-shaped) |
| | Colonization rights (you settle X, I won't) | carve up the frontier | standing agreement | Galaxy/colony claims |
| | Cede / hand over a colony or station | spoils, or a desperate trade | instant | Colony/Station ownership transfer |
| **Political** | Open relations / recognition | acknowledge they exist / their claim | state | DiplomacyDB |
| | Embassy exchange (post ambassadors) | the info window (hidden-info reveal) | standing | People (Ambassador) |
| | Vassalage / protectorate / federation membership | fold a weaker power in | standing state | DiplomacyDB + internal politics |
| | Ultimatum / demand (give me X or war) | coercion from strength | event | DiplomacyDB → casus belli |
| | Apology / reparation for an incident | cool a grudge | instant | DiplomacyDB (relation repair) |
| | Hostage / guarantee exchange | bind a fragile deal | standing | People |
| **People** | Extradite a defector / hand over a prisoner | a person as a bargaining chip | instant | Commanders/People |
| | Loan a specialist (scientist/admiral) | rent talent | standing | People (M3 pool) |
| | Grant asylum to their defector | take their person (an incident) | event | People → relation hit |
| **Coercive** | Sanctions (cut trade) | pressure without war | standing state | Logistics/Ledger (deny) |
| | Blockade threat / saber-rattling | leverage via the fog (maybe a bluff) | event | Fleets + Sensors |
| | Declare casus belli / war | the violent end of the lever | state | Combat/IFF |

*(The list is meant to grow — the lock is "build it broad and data-driven," not "this exact set." New rows are cheap because each is just a transfer or a standing-commitment that emits an order into a system that already exists.)*

### Reactive diplomacy — the world acts on its own (the "Are we good?" engine)
The developer's depth example: *the AI sees a fleet identified as yours near their space and messages a diplomat asking "Are we good?"* **Lock this as a first-class system, and build it as the same emergent engine as internal demands, pointed outward.**

- INTERNAL (built/designed): a sim **PRESSURE** → a **BLOC** voices a **DEMAND** → you enact/refuse.
- EXTERNAL reactive (this): a sim **OBSERVATION** (a fleet position, a war, a weakness, a shortage) → a faction generates an **OVERTURE / QUERY / DEMAND** → you **respond** (reassure / dodge / concede / threaten) → the response nudges the relation + reveals or conceals your intent.

Same generate-from-state machinery — **big reuse, not a new engine.** Examples of observation → overture:

| The AI observes (through ITS fog) | It generates |
|---|---|
| your fleet near their border | **"Are we good?"** — a low-stakes intent probe (the developer's example) |
| you at war with their enemy | an alliance offer |
| you at war with their friend | a warning / demand to stop |
| you weak (lost a battle, internal unrest) | presses the advantage — a demand, vassalization, or war |
| you strong / winning | seeks favor — tribute, a protection deal |
| a pirate/crisis threat on their border | **requests a defense fleet** (→ the commitment that sails your ships) |
| a resource they lack that you hold | a trade proposal |
| you broke a treaty (with anyone) | distrust — guard rises, deals dry up |
| you honored treaties over time | trust — offers deeper deals |
| their own Militarist bloc rose (their internal politics) | more aggressive overtures |

**Two payoffs:** the galaxy feels *alive* (factions act on what they see, not a hidden timer), and **fog cuts both ways** — they react to what *they* can detect of *you*, so EMCON-dark movement can avoid provoking the "Are we good?" probe entirely (sneak the fleet) or you can move loud to *intimidate*. The probe→answer loop is the nudge-gamble in miniature: a cheap, frequent reading contest that keeps the relationship live between the big treaty moments.

### The blast radius — the Prime-Directive map (what the full version touches)
Wide but **funneled through three keystones**. The full hidden-information version lives or dies on them, so they are **hard prerequisites**, built in this order:

> **UPDATE 2026-07-07 — all three keystones are now cleared or dissolved (see the status banner at the top). The prerequisite wall this section erected is DOWN; politics is buildable now.**

1. **The GlobalManager-not-iterated trap** — ✅ **DONE.** `MasterTimePulse` now iterates the `GlobalManager`; `NPCDecisionProcessor` fires monthly. The single fix that unblocked all the autonomous loops is in.
2. **Detection-quality is degenerate** — ✅ **DISSOLVED, not fixed.** `SignalQuality` was **CUT** (`docs/combat/DETECTION-DESIGN.md`, 2026-07-07); the hidden-information gradient moved to the **Information Ledger** (agents + decay), which is where it belonged. This keystone no longer exists — the prerequisite is "the Ledger carries the gradient," not "fix the sensor field."
3. **Hostility-from-diplomacy** — ✅ **substantially DONE.** `CombatEngagement`/`AreHostile` consults `DiplomacyDB` — signed non-aggression/defensive pacts and Friendly/Allied stance make `AtPeace` and suppress the fight. Remaining gap: **fire-control IFF** (can still target own/allied ships) — a small finish, not a foundation.

| System | State | The change |
|---|---|---|
| Fleets / Orders | ✅ order system is faction-agnostic (NPC issues the same orders) | small wire — a commitment→order translator |
| Logistics | ✅ routes exist; gate already faction-aware | medium — cross-faction access + cargo order + payment |
| Combat / IFF | ⚠️ works, but hostility isn't diplomacy-driven; no IFF | **keystone 3** — hostility from `DiplomacyDB` |
| Money / Ledger | ⚠️ single-faction | medium — add cross-faction `Transfer` |
| Sensors / Detection / EMCON | ⚠️ contacts + fog + EMCON work; quality degenerate | **keystone 2** — graduated detection quality |
| Espionage | ❌ none | new build — agents as M3 people, covert actions, detection risk |
| People / Commanders | ✅ commander entities + M3 pool | medium — diplomat roles/skill/traits + rival-leader agenda |
| Government / Internal politics | ✅ built / designed | wire the handoff |
| NPC AI / Doctrine | ⚠️ `NPCDecisionProcessor` dormant | real build — foreign goals + negotiation behavior (rides keystone 1) |
| Time loop (`MasterTimePulse`) | ❌ GlobalManager trap | **keystone 1** — run faction-level processors |
| Events / UI | ❌ stubs | new UI layer (the Visibility Gate — you can't play politics you can't see) |

**Prerequisite chain (the build spine) — UPDATED 2026-07-07:** the three keystones (time-loop trap, detection-quality, hostility-from-diplomacy) are **cleared/dissolved**, and the substrate (DiplomacyDB/relations/first-contact/treaty-effects/war→legitimacy) is **built and wired**. So the remaining spine is the DECIDER + its NPC mirror: **the Foreign Minister MVP** (per-faction FM + the reactive-overture loop + the NPC counterparty + the observation feeder) → the **commitment executor** (exchange catalog → real orders) + **betrayal tracking** + the **casus-belli morale gate** → negotiation-as-a-scene (C2) → the UI (the Visibility Gate). Espionage rides its own doc but shares the FM's people pipeline and the Ledger.

### Locked (developer, 2026-06-30)
- **FULL hidden-information version** — accepting detection-quality + the GlobalManager fix as hard prerequisites.
- **The COMMITMENT model** — treaties emit real orders into fleets/logistics/money; promised-vs-delivered tracked (the betrayal hook). The connective tissue.
- **Build the exchange catalog BROAD and data-driven** — the more that can be exchanged, the deeper; new exchanges are cheap (each is a transfer or a standing-commitment emitting an order).
- **Reactive diplomacy is first-class** — the "Are we good?" engine, built as the internal emergent-demand engine pointed outward (big reuse); fog cuts both ways.
- **Diplomacy-as-a-closing-fight** is the design metaphor for negotiation (pressure / leverage / read / commit / bluff).
- **Diplomats = the politics arm of the M3 people pool** (Envoy / Ambassador / Spy; Foreign Minister = the delegate).

---

## What NOT to Build Yet

- Complex treaty negotiation UI — the data model (Step 1) is sufficient for now; treaties are just setting boolean flags (the *costed-lever* teaty design above is the eventual shape, but v1 ships the flags).
- Espionage / infiltration — **placed** on the external spine above (the EXTERNAL frontier), built **after** the relationship/treaty/casus-belli core lands; not a v1 item.
- Reputation with minor factions — the relationship TRACK above is the model; needs faction archetypes first (Step 5).
- Population happiness from foreign relations — this is now **designed** as the INTERNAL⟷EXTERNAL handoff (trade pleases Merchants, war-weariness bleeds morale); it hooks into the colony/morale system that's now built, so it lands *with* the teeth layer, not "for later."
