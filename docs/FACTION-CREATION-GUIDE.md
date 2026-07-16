# Faction Creation Guide — the repeatable SOP

**What this is:** the exact, step-by-step recipe for adding a new playable/NPC faction to the DevTest scenario, with every file, every JSON node, and every landmine spelled out so a future session can repeat it without re-deriving anything. Built from the UEF/UMF/Kithrin buildouts (2026-07-14).

**Read this WHOLE page before authoring a faction.** Most of the pain is not "what to type" — it's the four cross-checks (§6) and the two landmines (§7, §8). Skip those and the faction loads green in `dotnet test` but **crashes the player on New Game**, because the test suite and the game build the start two different ways (see §6).

**As of:** 2026-07-14. Owner-doc for scenario-faction authoring. When you change the faction-load path or a landmine, update this + flip its row in `docs/DOCS-INDEX.md` in the same commit.

---

## 1. The big picture — what a faction is, and the two ways to make one

A **faction** is one empire/side in the game: a name, a personality, some worlds, some ships, and (if it's an NPC) an AI brain. In code it's an `Entity` carrying a `FactionInfoDB` (the "who I am / what I own" datablob). There are two ways one gets created:

| Path | Who uses it | Entry point |
|------|-------------|-------------|
| **Code** | the legacy default start + tests | `FactionFactory.CreateFaction` / `CreateBasicFaction` (C#) |
| **JSON / scenario** ← *this guide* | the **DevTest** start (the "DevTest" button that replaces Quickstart) | `FactionFactory.LoadFromJson` reading a `ScenarioFiles/<faction>.json`, orchestrated by `DevTestStartFactory.CreateDevTest` |

We author factions the **JSON way** — a data file, no C#. That's the repeatable process this guide covers.

**How the DevTest start assembles the game** (`GameEngine/Engine/Factories/DevTestStartFactory.cs:51` `CreateDevTest`), in order:
1. Loads **Sol** via `StarSystemFactory.LoadFromBlueprint` (its id is `"system-sol"` — a colony references it as `"systemId": "system-sol"`). Note: the bare `"sol"` is the STAR's id, not the system's.
2. For **each faction file in the list**: `FactionFactory.LoadFromJson` → sets `FactionOwnerID` → adds Sol to `KnownSystems`. **The FIRST file in the list is the player.**
3. **Second pass** (after all factions exist, so cross-faction names resolve): `ApplyOpeningRelations` (the `"openingRelations"` node) + `ApplyOpeningStrain` (the `"strain"` node).
4. **Third pass:** `GroundStartGarrison.RaiseForFactionColonies` raises each faction's home garrison — **colony worlds only; a station-only faction raises nothing** (see §9).
5. Kicks power generation + one sensor sweep so t=0 is live.

---

## 2. The files you touch (every time)

| File | What you add |
|------|--------------|
| `Pulsar4X/GameData/basemod/ScenarioFiles/<faction>.json` | **NEW** — the faction file itself (§3) |
| `Pulsar4X/GameData/basemod/ScenarioFiles/designs/componentDesigns.json` | any **new** installation/part designs you invented (§4a). Reuse existing ones where you can. |
| `Pulsar4X/GameData/basemod/ScenarioFiles/designs/shipDesigns.json` | the faction's **ship designs** (§4b) |
| `Pulsar4X/Pulsar4X.Client/Interface/Menus/NewGameMenu.cs` (~line 911) | **register the file** in the DevTest launch list `new List<string> { "uef-devtest.json", "umf.json", "kithrin.json", "<faction>.json" }` |
| `Pulsar4X/Pulsar4X.Tests/DevTestScenarioTests.cs` | the same list in the test `NewGame`/`CreateDevTest` calls, **and** a new gauge test (§11) |
| `docs/DOCS-INDEX.md` | flip a row if you touch this guide |

> The DevTest faction list lives in **two** places — the client (`NewGameMenu.cs`) and the tests. Add your file to **both**, or the client shows a faction the tests don't cover (or vice-versa).

---

## 3. The faction JSON — every node, specific

Skeleton (all nodes; delete the ones you don't need — most are optional):

```jsonc
{
  "name": "United Martian Federation",   // REQUIRED. Display name.
  "abbreviation": "UMF",                  // REQUIRED. Short code.
  "isNPC": true,                          // true = AI-controlled (NPCDecisionProcessor acts on it). false = the human player.

  "doctrine": {                           // NPC strategic weights (ignored for the player). Relative, NOT normalized —
    "economic": 0.35,                     //   the LARGEST one drives that cycle's decision. Four keys, all optional (default 0).
    "military": 0.35,
    "tech": 0.10,
    "expansion": 0.20
  },

  "personality": {                        // 12 traits, each 0.0–1.0, 0.5 = neutral. ALL optional (missing → 0.5).
    "aggression": 0.7,  "ambition": 0.8,  "authoritarianism": 0.7, "ruthlessness": 0.6,
    "risk": 0.55,       "honor": 0.4,     "xenophobia": 0.4,       "collectivism": 0.6,
    "zealotry": 0.5,    "guile": 0.5,     "altruism": 0.3,         "curiosity": 0.4
  },
  // the 12 trait names (case-insensitive): Aggression, Ambition, Authoritarianism, Ruthlessness, Risk, Honor,
  // Xenophobia, Collectivism, Zealotry, Guile, Altruism, Curiosity.

  "startingItems": [ /* see §3a — the UNLOCK list */ ],
  "componentDesigns": [ /* see §3b — the buildable-designs list */ ],
  "ordnanceDesigns": [],                  // missile/ordnance design ids (usually empty)
  "shipDesigns": [ /* see §3c — ship design ids from shipDesigns.json */ ],
  "species": [ "species-human" ],         // species blueprint id(s). "species-xenos" for the aliens.

  "colonies": [ /* see §3d — planet colonies */ ],
  "stations":  [ /* see §3e — space stations (optional) */ ],
  "fleets":    [ /* see §3f — starting fleets (optional) */ ],

  "openingRelations": [ /* see §3g — who you start at war with */ ],
  "strain": { /* see §3h — opening war-strain */ },

  "fleetComposition": { /* see §3i — how big a fleet the AI masses (optional) */ },
  "garrison": { /* see §3j — this faction's home-garrison mix (optional) */ }
}
```

### 3a. `startingItems` — the UNLOCK list (the #1 crash source)

This is the flat list of **everything the faction has researched/unlocked**: techs, raw minerals, refined materials, **and component TEMPLATES** (the `UniqueID`s from `TemplateFiles/*.json`, e.g. `"shipyard"`, `"laser-weapon"`, `"space-habitat"`). It is what `ColonyFactory` unlocks into the faction's `CargoGoods` before it builds the start colony.

**THE RULE (memorize):** every template a design uses, and every **material** in that template's `ResourceCost`, MUST be in `startingItems`. If a design needs `electronics` and it's not here, New Game **crashes** in `ComponentDesigner` (`GUID object {id} not found`). See §6.

A comprehensive human faction's list (copy this as a baseline; trim/extend per faction — the aliens swap in `swarm-frame`/`claw-weapon`/`energy-weapon`/`shield-generator`/`disruptor-weapon`/`inertialess-drive` etc.):

```
tech-modern-technology, tech-conventional-engine, tech-geo-surveyor, tech-gravitational-surveyor,
stainless-steel-armor, plastic-armor, aluminium-armor, titanium-armor,
general-storage, warehouse-facility, spaceport, cargo-Shuttlebay, fuel-storage, battery-storage,
passive-sensor, beam-fire-control, cloak-device, pd-director, jammer, warp-stabilizer, drive-reinforcement,
unit-caliber, crew-automation, reactor, battery-bank, rtg, steam-turbine-reactor, conventional-engine,
scntr-engine, alcubierre-warp-drive, refining, component-construction, installation-construction, ship-assembly,
mine, automine, research-lab, research-academy, intelligence-directorate, command-berth, refinery, factory,
shipyard, general-cargo-hold, fuel-cargo-hold, stainless-steel-fuel-tank, infrastructure, space-habitat,
stainless-steel, rp-1, methalox, hydrolox, ntp, plastic, fissile-fuels, electricity, hydrocarbons,
iron, aluminium, copper, titanium, lithium, chromium, fissionables, silicon, graphite, tungsten, nickel,
water, rare-earth-elements, regolith, space-crete, electronics, ree-magnetics,
laser-weapon, railgun-weapon, flak-weapon, sensor-hardening-module, admin-complex, ship-command,
tech-panel-efficiency, tech-panel-bandwidth, tech-panel-density, solarArray, local-construction, launch-complex,
ship-hull, bunker, infantry-unit, armor-unit, artillery-unit, troop-bay, human-frame, ground-rifle,
ground-magazine, ground-locomotion, ground-radar, ground-plating, ablative-plating, reactive-plating,
power-armor, ground-autocannon, vehicle-frame, ground-cannon, walker-frame, energy-weapon, shield-generator,
ward-projector, reflex-booster, swarm-frame, claw-weapon, deflector-array, armour-hardening, ship-magazine,
heat-radiator, pulse-laser, siege-railgun, disruptor-weapon, plasma-repeater, point-defense-mount,
missile-launcher, inertialess-drive, reactionless-drive, food-production
```

### 3b. `componentDesigns` — the buildable-designs list

The `default-design-*` ids (from `designs/componentDesigns.json`) the faction can **instantiate**. A design not in this list can't be placed on a colony or mounted on a ship. **You must list:**
- **every installation `id`** you put on a colony or station (§3d/§3e), AND
- **every ship-component `id`** any of the faction's ships mount (§4b) — hull, sensor, weapons, shields, engines, fuel tanks, batteries, reactors, troop bays, everything.

*(`startingItems` unlocks the TEMPLATE; `componentDesigns` registers the DESIGN built from it. Two different ends — you need both. This is gotcha #10.)*

### 3c. `shipDesigns`

The ship design ids (the `UniqueID`s from `designs/shipDesigns.json`) this faction can build — e.g. `["default-ship-design-gunship", "default-ship-design-trooper"]`. Also referenced by `fleets` (§3f).

### 3d. `colonies` — planet colonies

```jsonc
"colonies": [
  {
    "systemId": "system-sol",
    "location": "Mars",                    // body name as generated (matches GetDefaultName)
    "species": { "name": "Human", "population": 120000000 },
    "installations": [                     // each id must be in componentDesigns (§3b) + its materials in startingItems
      { "id": "default-design-space-habitat", "amount": 500 },   // ← INFRASTRUCTURE. Off-world? Use space-habitat, NOT
      { "id": "default-design-olympus-shipyard", "amount": 2 },  //   default-design-infrastructure. See §7. THE landmine.
      { "id": "default-design-factory", "amount": 6 },
      { "id": "default-design-valles-mine", "amount": 2 }
      // … mines, refineries, research, intel, governance, food, power, warehouses, bunkers …
    ],
    "cargo": [                             // starting stockpile. Every id must be in startingItems. Watch the storage cap (§3d note).
      { "id": "iron", "amount": 500000 },
      { "id": "stainless-steel", "amount": 500000 }
    ]
  }
]
```

> **Cargo storage cap:** cargo is VOLUME-capped by the colony's warehouses. `warehouse-facility` clamps to **10,000 m³ each** (space-habitats add more). ~1,000,000 units each of ~15 materials ≈ 47k m³, which fits 10 warehouses (100k m³) with headroom. Overshoot and the excess **silently clamps to 0** — jamming the economy, no error. Keep per-material amounts moderate (200k–500k) unless you've counted the volume.

### 3e. `stations` — space stations (optional)

Same shape as a colony, but hosted on a body as a station (the Kithrin's Titan outpost). `species` is optional — with a species+population it's a manned station, without it's an automated platform.

```jsonc
"stations": [
  {
    "systemId": "system-sol",
    "location": "Titan",
    "species": { "name": "Xenos", "population": 6000000 },
    "installations": [ /* same rules as colony installations */ ],
    "cargo": [ /* same rules */ ]
  }
]
```
**Station specifics that differ from a colony — see §9** (no infrastructure sizing needed; no auto-garrison; needs a shipyard module to build ships; the AI now sees it via `FactionState` station-awareness).

### 3f. `fleets` — starting fleets (optional)

```jsonc
"fleets": [
  {
    "name": "Mars Home Guard",
    "location": { "systemId": "system-sol", "body": "Mars" },
    "ships": [
      { "designId": "default-ship-design-gunship", "name": "MFS Ares" },
      { "designId": "default-ship-design-gunship", "name": "MFS Olympus" },
      { "designId": "default-ship-design-gunship", "name": "MFS Tharsis" }
    ]
  }
]
```
Each `designId` must be in `shipDesigns` (§3c) and defined in `shipDesigns.json`. **For the AI to mount a strike (conquest), one fleet needs ≥ 3 warships** — `MilitaryComposition.ReadyStrikeFleet` requires `StrikeGroupMinWarships` (3) before it will sail. Fewer = the AI keeps massing.

### 3g. `openingRelations` — who you start at war with

```jsonc
"openingRelations": [
  { "target": "UEF", "atWar": true, "score": -80 }   // target by name OR abbreviation
]
```
Applied in the second pass (after all factions load). `atWar: true` latches war on BOTH sides; `score` seeds the relationship track (−100..+100). Omit the node for a neutral faction. **A conquest-capable NPC needs an `atWar` entry** or `MilitaryTarget.BestEnemyTarget` finds no target and the conquest brain never fires.

### 3h. `strain` — opening war-strain (optional)

```jsonc
"strain": {
  "taxRate": 0.3,                // high war-tax: morale drag + capped income
  "powerDemandPerCapita": 0,     // sustenance squeeze (power) → shortage → morale/starvation
  "foodDemandPerCapita": 0.00001,// sustenance squeeze (food)
  "committedBulk": 20000         // workforce tied up in the war effort (fewer hands to build)
}
```
Sets the INPUTS the economy processors read, so the strain STICKS and compounds over time. Byte-identical if omitted.

### 3i. `fleetComposition` — how big a fleet the AI masses (optional)

```jsonc
"fleetComposition": {
  "name": "Martian Battle Line",   // display/template name (cosmetic)
  "minToDeploy": 4,                // Deployable tier — the smallest fleet the AI will send
  "idealSize": 10,                 // Ideal tier — the size it aims for when solvent
  "perfectSize": 20               // Perfect tier — the size it aims for when rich (or at war)
}
```
The per-faction override of the fleet **aspiration ladder** the AI masses toward (`FactionInfoDB.Fleet*` → `FleetAssembly.TemplateFor`). As a faction's built warships are swept into fleets (`FleetAssembly.AssembleBuiltWarships`), a forming fleet grows to its tier target and **overflow spills into a home-defence RESERVE** — the AI never ships its whole navy (the reserve is the military-commander's job). Treasury balance picks the tier (broke→Deployable, 50k→Ideal, 150k→Perfect); being at war bumps one tier. **Omit → the engine default (`Strike Fleet` 3/8/18) → byte-identical.** A militarist faction fields a heavier line (UMF 4/10/20); a raider a lighter swarm (Kithrin 3/6/12). *(Note the interplay with §3f: a **starting** fleet needs ≥3 warships for `ReadyStrikeFleet` to sail; `fleetComposition` governs what the AI grows toward as it BUILDS more.)*

### 3j. `garrison` — this faction's home-garrison mix (optional)

```jsonc
"garrison": { "Infantry": 4, "Armor": 3, "Artillery": 2 }   // type name (case-insensitive) → count
```
The ground echo of `fleetComposition`: the combined-arms garrison `GroundStartGarrison.RaiseForFactionColonies` raises on **each of this faction's colony worlds** (the third pass, §2 step 4 — colony worlds only; a station-only faction raises nothing). Type names are the `GroundUnitType` enum (Infantry / Armor / Artillery); unknown names / non-positive counts are skipped. **Omit → the engine default light watch (3 Infantry / 2 Armor / 1 Artillery = 6) → byte-identical.** So the militarist UMF garrisons a heavier 4/3/2 legion while the player keeps the default. *(Same reserve principle is meant to extend to battalions later — a garrison shouldn't ship its whole defense off on an invasion.)*

---

## 4. The shared design files

### 4a. `designs/componentDesigns.json` — installation & part designs

Only add a NEW entry if no existing `default-design-*` fits. Structure:

```jsonc
{
  "Type": "ComponentDesign",
  "Payload": {
    "UniqueId": "default-design-olympus-shipyard",   // note: "UniqueId" (lower d) here
    "Name": "Olympus Naval Yards",
    "TemplateId": "shipyard",                        // a UniqueID from TemplateFiles/*.json
    "Properties": [                                  // OPTIONAL — dial overrides. See §8 for the CEILINGS.
      { "Key": "Slip Size", "Value": 25000 },
      { "Key": "Crew Size", "Value": 40000 }
    ]
  }
}
```
- No `Properties` → the template's default dials.
- `Properties` **override** dials — but they **clamp to the template's Min/Max at instantiation** (§8). Check the ceiling first.
- The design's materials come from the **template's** `ResourceCost` — verify those are in the faction's `startingItems`.

### 4b. `designs/shipDesigns.json` — ship designs

```jsonc
{
  "Type": "ShipDesign",
  "Payload": {
    "UniqueID": "default-ship-design-hive-cruiser",  // note: "UniqueID" (upper D) here — different from components!
    "Name": "Kithrin Hive Cruiser",
    "Armor": { "Id": "titanium-armor", "Thickness": 3 },   // Armor.Id must be in startingItems
    "Components": [                                          // every Id must be in the faction's componentDesigns (§3b)
      { "Id": "default-design-ship-hull-heavy", "Amount": 1 },   // a ship NEEDS a hull
      { "Id": "default-design-passive-sensor-s50", "Amount": 1 },
      { "Id": "default-design-disruptor-weapon", "Amount": 3 },
      { "Id": "default-design-deflector-array", "Amount": 2 },
      { "Id": "default-design-beam-fire-control", "Amount": 1 },  // beam weapons want a fire control
      { "Id": "default-design-fuel-tank-1000", "Amount": 2 },
      { "Id": "default-design-alcubierre-500", "Amount": 4 },     // WARP drive — needed to cross a system / reach a target
      { "Id": "default-design-battery-2t", "Amount": 3 },         // stored energy — the warp bubble needs it
      { "Id": "default-design-reactor-2t", "Amount": 2 },         // power
      { "Id": "default-design-NTR1.8", "Amount": 4 }              // sublight engine (thrust → evasion)
    ]
  }
}
```
A **warship** = mounts any weapon (`IsWarship` reads laser/railgun/flak/plasma/disruptor/missile). A **troop transport** = mounts `default-design-troop-bay` (`IsTroopTransport` reads `GroundBayAtb`). A ship that must sail to war needs the **warp chain** (reactor + battery + alcubierre) or `MilitaryReach` reads it as "no range" and it never launches. Model new ships on `default-ship-design-gunship` (warship) / `-trooper` (transport) — they're proven.

---

## 5. Registration — make the game load it

Add `"<faction>.json"` to the DevTest file list in **both**:
- `Pulsar4X.Client/Interface/Menus/NewGameMenu.cs` (~line 911, the DevTest button).
- `Pulsar4X.Tests/DevTestScenarioTests.cs` (the `CreateDevTest(... new List<string>{...})` calls).

**Order matters:** the FIRST file in the list is the player faction.

---

## 6. The cradle-to-grave cross-checks (gotcha #10) — DO THESE OR IT CRASHES PLAYERS

**Why this section exists:** the test suite builds the start colony in C# (`DefaultStartFactory`), but the **game** builds it from the JSON you just wrote. A data mistake ships **green in `dotnet test`** and blows up only on **New Game**. `BaseModIntegrityTests` only checks the base `colony-earth` — it does **NOT** check your scenario faction. So you verify these by hand / script.

The four cross-checks, for **every** id you author:

1. **Installation id defined + registered:** every `installations[].id` on a colony/station is (a) a `UniqueId` in `componentDesigns.json`, and (b) in the faction's `componentDesigns` list.
2. **Ship component registered:** every ship's `Components[].Id` is in the faction's `componentDesigns` list.
3. **Materials unlocked:** every design's **template `ResourceCost`** material is in the faction's `startingItems`. Also: every **template** used is in `startingItems`; every **cargo** id is in `startingItems` (`LoadCargo` hard-indexes — an un-unlocked id crashes); every **Armor.Id** is in `startingItems`.
4. **Fleet ships resolve:** every `fleets[].ships[].designId` is in `shipDesigns` (§3c) and defined in `shipDesigns.json`.

### The validation script (run it before every push)

Save as `/tmp/validate_faction.py`, run from `Pulsar4X/`. Prints `ALL CROSS-CHECKS PASS` or the exact failing id:

```python
import json, glob
templates = {}
for fp in glob.glob('GameData/basemod/TemplateFiles/*.json'):
    try: data = json.load(open(fp))
    except: continue
    if not isinstance(data, list): continue
    for x in data:
        p = x.get('Payload', x)
        if isinstance(p, dict) and p.get('UniqueID'): templates[p['UniqueID']] = p
designs = {p.get('UniqueId'): p for p in (x.get('Payload', x) for x in json.load(open('GameData/basemod/ScenarioFiles/designs/componentDesigns.json')))}
ships   = {p.get('UniqueID'): p for p in (x.get('Payload', x) for x in json.load(open('GameData/basemod/ScenarioFiles/designs/shipDesigns.json')))}
ok = True
for f, hostkey in [('umf.json', 'colonies'), ('kithrin.json', 'stations')]:   # ← add your faction file + its host key
    d = json.load(open('GameData/basemod/ScenarioFiles/' + f))
    si, cd = set(d['startingItems']), set(d['componentDesigns'])
    for host in d.get(hostkey, []):
        for inst in host['installations']:
            i = inst['id']
            if i not in designs: print('FAIL', f, 'install', i, 'undefined'); ok = False
            elif i not in cd:    print('FAIL', f, 'install', i, 'unregistered'); ok = False
            elif (t := templates.get(designs[i]['TemplateId'])):
                for m in t.get('ResourceCost', {}):
                    if m not in si: print('FAIL', f, i, 'material', m, 'missing'); ok = False
        for c in host.get('cargo', []):
            if c['id'] not in si: print('FAIL', f, 'cargo', c['id'], 'not unlocked'); ok = False
    for sid in d.get('shipDesigns', []):
        if sid not in ships: print('FAIL', f, 'ship', sid, 'undefined'); ok = False; continue
        arm = ships[sid].get('Armor', {}).get('Id')
        if arm and arm not in si: print('FAIL', f, 'armor', arm, 'not unlocked'); ok = False
        for c in ships[sid]['Components']:
            if c['Id'] not in cd: print('FAIL', f, 'ship', sid, 'comp', c['Id'], 'unregistered'); ok = False
            elif (t := templates.get(designs.get(c['Id'], {}).get('TemplateId'))):
                for m in t.get('ResourceCost', {}):
                    if m not in si: print('FAIL', f, 'shipcomp', c['Id'], 'material', m, 'missing'); ok = False
    for fl in d.get('fleets', []):
        for sh in fl['ships']:
            if sh['designId'] not in ships: print('FAIL', f, 'fleet ship', sh['designId'], 'undefined'); ok = False
print('ALL CROSS-CHECKS PASS' if ok else 'CHECKS FAILED')
```

---

## 7. LANDMINE #1 — off-world colonies MUST use `space-habitat`, not `infrastructure`

**This one silently kills a whole colony's economy.** Infrastructure is the utility grid that all production scales by: `Efficiency = min(1.0, CapacityProvided / CapacityRequired)`, and a production job with `(int)(rate × efficiency) < 1` **never runs** (`FeasibilityOracle.CanQueue` refuses it, `IndustryTools` skips it).

`default-design-infrastructure` ("Earth-Standard Infrastructure") carries a **`GravityToleranceAtb` 8.8–10.8 m/s²** and **`PressureToleranceAtb` 0.9–1.1 atm**. `InfrastructureProcessor` **skips out-of-tolerance infrastructure entirely** — it provides **ZERO** on a body outside those bounds. Mars is 3.72 m/s² / 0.87 atm → fails both → efficiency **0.0** → **nothing builds, no error.** This was the real "UMF can't build ships" bug.

**The fix:** on any off-Earth body use **`default-design-space-habitat`** (same 1000 capacity, **no** tolerance atb → works anywhere). Only Earth-like worlds may use `default-design-infrastructure`.

**Sizing the infrastructure** (so `provided ≥ required` → efficiency 1.0):
- **Provided** = (space-habitat count) × 1000.
- **Required** = Σ over every **non-infrastructure** installation of `(MassPerUnit / 1000) + CrewReq` (mass in tonnes + crew).
- Per-unit required for common buildings (base dials): factory 30,000 · mine 5,050 · refinery 505 · shipyard 10,080 (Olympus 40,200) · research-lab 120 · research-academy ~4 · admin (Office 1000) 350 · bunker 100 · local-construction 60 · intel-directorate ~11.
- Provision generously (e.g. Mars: ~360k required → 500 space-habitats = 500k provided → 1.0 with margin). The gauge test PRINTS `provided/required/efficiency`, so a red run tells you to add more.

**Stations are exempt** — a station has no `ColonyInfoDB`, so `RecalcCapacity` no-ops and its efficiency is always 1.0 (§9). Its infrastructure modules are cosmetic (we still use space-habitat for consistency).

---

## 8. LANDMINE #2 — dialed `Properties` clamp to the template's Min/Max

When you override a dial via `Properties` (§4a), the value **clamps to the template's `MinFormula`/`MaxFormula` at instantiation**. Set it above the ceiling and it silently clamps DOWN — sometimes below the template's own default. **Check the ceiling before dialing.** Known ceilings at base tech (level 0):

| Template | Dial | Max at base tech | Note |
|----------|------|------------------|------|
| `factory` | Size | `(1+lvl)×1000` = **1000** | default is 5000 (>max) — **do NOT dial up**, it clamps to 1000 (smaller!). Use more factories. |
| `mine` | Area | 100,000,000 (constant) | safe to dial up (Valles = 5,000,000) |
| `refinery` | Size | 10,000 (constant) | dial ≤ 10000 |
| `research-academy` | Class Size | 100 (constant) | dial ≤ 100 (Olympus University = 100) |
| `admin-complex` | Office Space | 10,000 (constant) | dial ≤ 10000 |
| `intelligence-directorate` | Op Capacity / Counter Intel | 10 / 100 | dial ≤ those |
| `command-berth` | Grade | 10 | dial ≤ 10 |
| `shipyard` | Slip Size | `(1+lvl)×5000 + 20000` = **25000** | dial ≤ 25000 (Olympus = 25000). Crew Size max 1,000,000. |

To read a ceiling: open `TemplateFiles/installations.json`, find the template, read the dial property's `MaxFormula` (a `TechData('tech-...')` formula resolves against the faction's researched level; at base that's level 0).

---

## 9. Colonies vs stations — the differences that bite

| | Colony (planet) | Station (on a body) |
|---|---|---|
| Host blob | `ColonyInfoDB` | `StationInfoDB` |
| Infrastructure efficiency | **applies** — must size space-habitats (§7) | **N/A** — always 1.0 (RecalcCapacity no-ops with no `ColonyInfoDB`) |
| Auto home garrison | **yes** (`RaiseForFactionColonies` raises inf/armor/arty) | **no** — a station raises nothing; its army must be built by the AI |
| Crew pool | has a `ColonyManpowerDB` (ship-build crew gate applies) | none → crew gate is "unenforced, always allowed" |
| Base cargo | has `CargoStorageDB` | has one too (`StationFactory` adds it) |
| Seen by the AI economy/conquest brain | yes | **yes, since 2026-07-14** — `FactionState.Snapshot` folds a station **that has an `IndustryAbilityDB`** into its colony list. Before that, a station-only faction snapshotted an empty list and its whole AI no-oped. **A station-only faction MUST include a shipyard/industry module or the AI can't act.** |

---

## 10. Making it a REAL AI player (conquest-capable)

For an NPC to actually build, sail, and invade (not just sit there), it needs ALL of:
1. `"isNPC": true` + a `doctrine` (military/aggressive tilt) + a `personality` (aggression/ambition high).
2. **A build host** — a colony OR a station with an `IndustryAbilityDB` (§9).
3. **A shipyard** on that host (provides the `ship-assembly` line; only a `shipyard` template does — factory/local-construction do NOT).
4. **Efficiency ≥ ~0.01** on that host (§7 — the real gate; off-world → space-habitat).
5. **Ship designs** — at least one warship (weapons) + one troop transport (troop-bay), warp-capable (§4b).
6. **A strike fleet** — ≥ 3 warships in one fleet (`ReadyStrikeFleet` min), warp-charged.
7. **`openingRelations` with `atWar: true`** against a target that owns a colony (so `MilitaryTarget.BestEnemyTarget` finds a prize).
8. Buildable **ground units** (register `default-design-infantry/armor/artillery`) so it can raise an army to invade with.

The AI order emission is gated OFF by default (`NPCDecisionProcessor.EnableOrderEmission = false`) — the brain DECIDES but doesn't ACT until that flag is flipped (a test/PC toggle). Authoring the above makes the faction *ready* to act the moment the gate opens.

---

## 11. The gauge tests to write (`DevTestScenarioTests.cs`)

Never ship a faction without a test — `BaseModIntegrityTests` won't cover it. Follow the existing pattern (load the scenario, assert shape). Minimum gauges (see the existing UMF/Kithrin tests as templates):
- **Loads + shape:** the faction loads as an NPC with its colonies/stations; `IndustryDesigns` non-empty.
- **Can build:** `FeasibilityOracle.CanQueue(ColonyState.Of(host), faction.IndustryDesigns["<a ship>"], faction)` is **true** — the real proof it can build (this is what the infrastructure landmine breaks). PRINT `efficiency` + the ship-assembly rate so a red run names the gate.
- **Ships resolved:** `faction.ShipDesigns.ContainsKey("<ship id>")` (the gotcha-#10 sensor that every component id resolved), and a transport `TryGetComponentsByAttribute<GroundBayAtb>`.
- **Garrison (colonies):** the colony body carries a `GroundForcesDB` with the faction's units.
- **War (if belligerent):** the faction's `DiplomacyDB.GetRelationship(playerId).AtWar` is true.

---

## 12. The step-by-step checklist (do these in order)

1. **Copy a sibling faction file** (`umf.json` for colony-based, `kithrin.json` for station-based) → `ScenarioFiles/<faction>.json`. Rename `name`/`abbreviation`.
2. Set `isNPC`, `doctrine`, `personality`.
3. Write `startingItems` (§3a — start from the §3a baseline, swap in the faction's flavor tech/materials).
4. Author any **new** dialed designs in `componentDesigns.json` (§4a) — **check dial ceilings first** (§8).
5. Author the faction's **ships** in `shipDesigns.json` (§4b).
6. Fill `componentDesigns` (§3b) — every installation id + every ship-component id.
7. Fill `shipDesigns` (§3c), `species`.
8. Author `colonies` (§3d) and/or `stations` (§3e) — **off-world → `space-habitat`, sized** (§7). Add cargo (watch the volume cap).
9. Author `fleets` (§3f — ≥3 warships for a strike fleet), `openingRelations` (§3g), `strain` (§3h).
10. **Register the file** in `NewGameMenu.cs` + the tests (§5).
11. **Run the validation script** (§6) → `ALL CROSS-CHECKS PASS`.
12. **Write the gauge test(s)** (§11).
13. `git commit` + push; **wait for CI green** before building anything on top (one-slice-at-a-time — CI is the only correctness gauge, the SDK can't build here).
14. If CI is red, the gauge test's printed values (`efficiency` / `ship-assembly` / `CanQueue`) name the gate — fix and re-push.

---

## Worked references

- **`umf.json`** — a colony-based, belligerent, conquest-capable human faction (Mars capital + Luna/Venus/Ceres). The template for a planet empire.
- **`kithrin.json`** — a station-based alien faction (Titan outpost). The template for a station power (needs the shipyard module + the `FactionState` station-awareness to act).
- **`docs/DEVTEST-AI-BUILD-LOG.md`** — the running diary; the MARS-AS-CAPITAL and KITHRIN entries (2026-07-14) are the play-by-play of applying this guide, including the two landmines when they first bit.
