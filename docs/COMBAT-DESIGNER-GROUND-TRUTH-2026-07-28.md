# COMBAT & DESIGNER — GROUND TRUTH

**As of 2026-07-28.** The single consolidated record of the 2026-07-28 verification campaign: a **17-pass auto-resolver
audit**, a **7-pass designer audit**, a **cross-audit reconciliation** between them, and a **7-agent parallel survey**
of the designer's whole surface (formula engine · template schema & loader · the four assemblers · client UI · research
gating · design lifecycle & saves · a docs sweep for design intent).

**Why one document.** These began as four and overlapped badly — the same defect appeared under three names, two of them
disagreed, and one carried a claim that would have crashed New Game if acted on. Consolidated here at the developer's
instruction. **The four source documents are deleted; nothing was lost — §16 maps every one.**

> **This file supersedes and replaces:**
> `docs/economy/DESIGNER-AUDIT-2026-07-28.md` · `docs/combat/RESOLVER-AUDIT-2026-07-28.md` ·
> `docs/economy/DESIGNER-RETROFIT-SPEC-2026-07-28.md` · `docs/OPERATION-GROUND-TRUTH-COMPLIANCE.md` *(already merged)*

**Placement note.** Top-level `docs/` normally holds only vision + status dashboards, plus the `DOCS-AUDIT` docs. This
sits with them because it is the same *kind* of artefact — a point-in-time verification sweep — and because it spans
combat, economy and ground, so no single subject subfolder fits.

**Severity key:** 🔴 **BLOCKER** (silently wrong today) · 🟠 **REAL** (a designed thing does not work) ·
🟡 **DEBT** (right but stale/undone) · 🔵 **NOTE** (worth knowing, not broken) · ✅ **verified-good**.

**The two standing rules the campaign ran under, both the developer's:**
> 1. **Every issue recorded, explained clearly and simply.**
> 2. **⛔ DO NOT flag an issue without reading the associated and relevant docs FIRST.**

---

## 0. HOW TO USE THIS DOCUMENT

| If you are… | Read |
|---|---|
| Picking this up cold | §1 the headline · §2 the gauges · §3 the open decisions |
| About to change the designer | §3 · §4 the locks · §7 the gap · §12 the save register · §14 the plan |
| About to change the resolver | §3 · §4 · §9 the resolver root causes · §14 |
| Wondering if something is already known | §8 (designer, 40 findings) · §9 (resolver, 49) · §10 (where they disagreed) |
| Checking whether a claim is trustworthy | §11 corrections — five claims in this campaign were wrong and are named |
| Looking for something that used to be in another file | §16 the deletion map |

---

## 1. THE HEADLINE — the whole campaign in five sentences

1. **The designer's taxonomy was built; the parametric collapse was not.** Measured: **zero dials are shared across any
   door**, so the design's own acceptance bar — *"build a phaser, a lazpistol and a Death-Star beam from the same
   sliders"* — fails literally.
2. **The base mod is a large body of untyped text that neither the compiler nor the tests walk**, which is how six
   `AttributeType` strings naming a non-existent namespace took down four live designer doors, including the entire
   missile-warhead designer, without anyone noticing.
3. **"One resolver" is true of the formulas and false of the fight** — the shared kernel's neutral `Combatant` view has
   **zero production consumers** and there is no shared salvo loop, which is exactly how two incompatible armour models
   coexist while both sides honestly call the same `ArmourSoak`.
4. **One unstated decision — space AGGREGATES, ground INDIVIDUATES — surfaces in four systems** (the battlefield
   container, armour hardening, the shield pool, and the word "engagement"), and canon M19 says they should not differ
   at all.
5. **Both audits independently produced the same sentence from opposite ends:** *the designer models COMPONENTS; the
   resolver models TOTALS* — every defect is a designed detail flattened into a number before the fight starts, and
   unable to influence it again.

---

## 2. THE GAUGES — numbers that cannot rot

Track these. Each is one script away, and none depends on a status column anyone must remember to flip.

| Gauge | Today | Target | Why it is the right gauge |
|---|---|---|---|
| **Base-mod component templates** | **96** (67 on 2026-07-08 → 89 on 07-13 → 96) | **~37** | `CATEGORIES.md` §2 locks *"~37 doors replace 67 templates."* The pile has been **growing**. Every new hand-authored template is motion away from the locked design. |
| **Shared dials per door** | **0** | the whole point | Weapons ▸ Energy holds 4 templates whose dial sets are **disjoint**; Weapons ▸ Ballistic, 7 templates, also disjoint. Until this is non-zero the "family you design inside" does not exist. |
| **Templates that construct without throwing** | **92 / 96** | 96 | Four throw on designer-open. No test covers this (§7 R-2). |
| **`WeaponProfile` fields fed from the design** | beam 7/10 · **ground 6/10** · railgun 4/10 · **missile 0/10** | 10/10 | §9 root cause A, quantified in §8 Pass 4. |
| **`CombatKernel.Combatant` production consumers** | **0** | both domains | The merge reached the arithmetic and stopped before the structure (§9 RC-3). |
| **Doctrine dials that reach the fight** | **9 / 14** | 14 | Five are inert; four of those five are the 2026-07-24 behaviour rulings (§8 Pass 7 + §10 C-1). |
| **Templates with any tech gate** | **18 / 96** — and **0 of 22** ground templates | — | Ground combat has no research at all (§8 D3-1). |

---

## 3. OPEN DECISIONS — the developer's calls, nothing else proceeds past them

Ordinary work can proceed on everything in §14 Phase A and B today. These cannot be inferred from the code.

| # | Decision | Why it is not inferable | Blocks |
|---|---|---|---|
| **Q-A** | **Does a fight AGGREGATE or INDIVIDUATE?** In space the fleet is the unit of account (one shield pool, fleet-averaged armour hardening, the whole star system as one battlefield); on the ground everything is per-unit. | Making them agree means changing one side. *"Does one shielded battleship cover the whole fleet?"* is a gameplay decision, not a refactor. Four systems already differ; a fifth will follow whichever way it is left. | §14 Phase C/D, the resolver merge |
| **Q-B** | **Which armour model wins** — flat per-source soak (ground, and what the shared kernel already implements) or armour-as-hit-points (space)? | `Penetration` and per-shot alpha are **undefined** against a pool — they are not un-wired on ships, there is nothing to wire them to. There is no third option that keeps both. | penetration on ships, the merge |
| **Q-C** | **Damage bucketing vs real targeting.** The July ruling locks *"compute the math for ONE unit + its doctrine, then distribute across N"* with health-weighted spreading; the 2026-07-28 ruling locks *"pick a target, fire, excess over that target's remaining health is WASTED."* | **Both are the developer's own rulings.** They collide on exactly one point: health-weighted spreading *is* the smear that makes alpha meaningless. Proposed reconciliation in §9 P1-4 — **recorded, not adopted.** | `TargetPriority`, the alpha dials |
| **Q-D** | **The station build model.** `OFF-WORLD-INFRASTRUCTURE-DESIGN.md:129` locks *"deploy a bare platform, then build its modules ON it — **NOT** design-and-assemble-complete-at-a-shipyard (that was the **rejected alternative**)."* `StationDesign` (2026-07-15) **is** the rejected alternative, and **both front doors now ship.** | Someone built the thing the doc says was ruled out and the doc was never updated. | station assembler work |
| **Q-E** | **`Chassis ▸ Prebuilt Units`** — `CATEGORIES.md` §2 locks that Chassis *"kills the whole-unit shortcuts."* The 2026-07-23 dial audit and the live client both treat "Prebuilt Units" as a door. | The newer artefact drifted *away* from a lock rather than superseding it. | §14 D1 |
| **Q-21** | **What a planet capture transfers** — deliberately left open since the July ruling set. | *(carried; unchanged)* | ground capture |
| **Three smaller ones** | **`Amphibious`** — wire it into the pathfinder or delete the dial (it doubles the part's mass and does nothing). **The three undefined minerals** — recommend repointing to defined materials rather than minting three new ones (new minerals ripple into system-gen and every colony stockpile). **`PDC`** — a mount flag with zero templates and no designer: build or delete. | | §14 Phase B |

---

## 4. THE LOCKED DECISIONS — constraints on all of this (do not re-litigate)

Thirty-six were recovered from the design docs. The load-bearing ones, each with its source:

| # | Locked | Source |
|---|---|---|
| **L1** | **ONE designer for EVERY buildable thing.** *"DO NOT build, propose, or document a separate per-domain designer window."* | `UNIVERSAL-ASSEMBLY-DESIGN.md` §0 · 2026-07-14 |
| **L2** | Two tools: **Component Designer** (makes the PIECES) + **Entity Assembler** (`ShipDesignWindow`, retitled — *"It is NOT a ship designer; ships are just one entity kind it assembles"*) | ibid. |
| **L3** | **Chassis + components; every stat EMERGES from the sum**, gated by physics (structure vs mass), at any scale | ibid. §1 |
| **L4** | *"`GroundUnitAssembly` is the **shared assembler core** (not a ground-only thing)"* — ⚠ **the code never honoured this**; three parallel assemblers were written *after* the lock | ibid. §0 |
| **L5** | Components are universal **by TYPE, not by SETTING** — *"whether it ends up on infantry, a tank, a ship, or an installation is decided by the MOUNT + the chassis's carry capacity, never by a category label"* | ibid. §2a · 2026-07-05 |
| **L6** | Scale/upgrade techs are **per-TYPE and setting-agnostic** — *"Beam Focusing Range," not "Ground Weapon Yield"* | ibid. |
| **L7** | **Name the 1–2 AXES first; the role is a COMPUTED READOUT of the specs**, never a picked label | ibid. §2b · 2026-07-06 |
| **L8** | **11 categories / 37 doors — DESIGN-LOCKED** | `CATEGORIES.md` header · 2026-07-09 |
| **L9** | **Roles are dials, not doors.** *"Name the dial before you reach for a new door."* | `CATEGORIES.md` §2 |
| **L10** | Chassis **KILLS** the whole-unit shortcuts (`infantry-unit`/`armor-unit`/`artillery-unit`) | `CATEGORIES.md` §2 |
| **L11** | **The parallel ground attribute systems die by DELETION, not merger** — `GroundWeaponAtb`, `GroundSensorAtb`, `GroundLocomotionAtb`, `GroundAugmentAtb`, `GroundArmorAtb`, `GroundMagazineAtb` | `CATEGORIES.md` §3 |
| **L12** | **The designer makes GEAR, not the BEING.** *"a Jedi's telekinesis is a pilot trait, not a component. This boundary is load-bearing."* | `CATEGORIES.md` §3 + Appendix |
| **L13** | **Anti-dominance:** *"every option must BUY its advantage… if an option has no catch, it's a bug in the design."* | `DIALS.md` §1.1 |
| **L14** | **No authored requirement graph** — mass/volume is the one hard wall, *"a physical container, not a rule."* | `DIALS.md` §0b |
| **L15** | Under-supply **THROTTLES** (soft, visible); it never hard-fails | ibid. |
| **L17** | ***"Never ship a dead knob."*** | `DIALS.md` §0d |
| **L18** | **Express every dial in the resolver's existing joule scale — do not invent a number scale** | `DIALS.md` §0e |
| **L19** | **Combat is only ONE consumer of a dial, and rarely the main one.** *"A door graded only by its combat use is a door half-designed."* | `DIALS.md` §0f |
| **L20** | **THREE acceptance criteria: Reachable · Mirrored · Observable.** *"A door that passes only #1 is a display piece."* | `DIALS.md` §0g · 2026-07-09 |
| **L22** | **COUNT RESOLVERS, NOT DOORS** — a new door with no new resolver is DATA | `DIALS.md` §0i |
| **L23** | **Exotic is the extensibility slot** — a new sci-fi weapon is an effect on the bus, *"never a new door."* | `DIALS.md` §1.5 |
| **L25** | ⭐ **THE NAMED KEYSTONE: port the ground carry gate to ships + stations.** *"Do that, and 'the numbers force the build' — the founding promise of the entire designer — becomes real across ALL scales instead of just the ground."* | `DIALS.md` §11 |
| **L26** | Hull frame mass is **REAL mass** (developer's call 2026-07-10: *"we're making a real game"*) | `DIALS.md` ⚙11 |
| **L28** | **Abilities are components — do NOT invent parallel systems.** Modeling a capability as a component is what buys research-gating, construction, save/load and the design UI *for free*. | `CONVENTIONS.md` §6 |
| **L29** | **Transparency:** benefits AND costs visible as stats; prefer parametric dials to preset menus. *"A knob with no visible consequence is a trap."* | `CONVENTIONS.md` §16 · 2026-07-08 |
| **L30** | Cradle-to-grave: **mineral → material → production → component → research → installed → decision → loss.** *"A missing rung is a design gap to fill (or a deliberate, written deferral), never a thing to skip."* | root `CLAUDE.md` |
| **L31** | ONE universal tool for any faction shape — *"the same one a human faction and a Zerg-like faction use; the only specificity lives in the DIALS."* | `DIAL-AUDIT-2026-07-23.md` |
| **L32** | **One Verb, Both Seats** — *"If the AI cannot use a mechanic with the SAME primitive the player uses, the mechanic is too complex. Full stop."* | root `CLAUDE.md` · 2026-07-28 |
| **L33** | **Weapons is the pilot category** — prove the loop there, then replicate | `CATEGORIES.md` §6.3 |
| **L35** | Every new gameplay number is a **flagged JSON default**, never silently hardcoded | `UNIVERSAL-ASSEMBLY-DESIGN.md` §4 |
| **L36** | **Do NOT big-bang refactor.** Converge incrementally; **UX last.** | ibid. |

⚠ **Two slices proposed earlier in this campaign are WITHDRAWN by these locks.** The plan to *add*
`Penetration`/`PerShotEnergy` dials to `GroundWeaponAtb` so the three prebuilt units could retire is condemned at
**both ends** by L10 + L11. The real target is also locked, including its method:

> *"one universal weapon design (TYPE + specs) that **BOTH resolvers read**; the mount decides the setting. This is the
> architectural piece — it needs a **design doc + phased plan BEFORE code**, not a blind refactor. **Sequenced with the
> developer.**"* — `UNIVERSAL-ASSEMBLY-DESIGN.md` §2a

---

## 5. THE DESIGNER — AS BUILT

### 5.1 The three objects (never conflate them)

| Thing | What it is | Where it lives |
|---|---|---|
| **Template** (`ComponentTemplateBlueprint`) | The JSON recipe: NCalc formulas + dials. Read-only mod data. | `GameData/basemod/TemplateFiles/*.json` → `FactionDataStore.ComponentTemplates` |
| **DESIGN** (`ComponentDesign`) | *Your* blueprint — a template with the dials set and the numbers already computed. | `FactionInfoDB.InternalComponentDesigns` (canonical, per faction) |
| **INSTANCE** (`ComponentInstance`) | The physical part bolted to one ship/colony. Holds health, enabled, load %. | `ComponentInstancesDB.AllComponents` on the parent entity |

Because Newtonsoft runs with `PreserveReferencesHandling.Objects`, a design in `InternalComponentDesigns`,
`IndustryDesigns`, `CargoGoods` and every `ComponentInstance.Design` is **one shared object**, not copies.

### 5.2 The formula engine and the attribute binder

**Evaluation order** (`ComponentDesigner.cs:24-78`): identity → Description (eagerly, only if it starts with `'`) →
seven designer-level `ChainedExpression`s **constructed but not evaluated** → mount/industry/cargo → resource-cost
expressions (**unknown id throws**, `:66`) → every `ComponentDesignProperty` in template order → `EvalAll()` (`:77`).

- **Construction ≠ parsing.** NCalc compiles lazily, so **a syntax error surfaces at first `Evaluate()`, not at load.**
  `ChainedExpression.HasErrors()`/`Error()` (`:189-197`) — the validator, already written — has **zero callers.**
- **`EvalAll()` evaluates only the eight designer-level formulas.** Property formulas stay `Result == null` until read.
- 🔵 **Order bug:** `SetMass()` computes `Density = MassPerUnit / VolumePerUnit` while `VolumePerUnit` is still 0 →
  a transient `Infinity`/`NaN` for one statement, corrected by `SetVolume()` on the next line.
- **`SetAttributes()`** (`:81-115`): `EvalAll()` → per property gate on `AttributeType != null && IsEnabled` →
  `SetValue()` force-recalc → read `AtbConstrArgs` → `Activator.CreateInstance` → `AttributesByType[type] = attrbute`.

**The custom NCalc surface — 10 parameters, 9 functions.** Live usage census: only **`Mass`** (659 sites) and
**`GuidDict`** (5) are exercised by data; `PropertyValue` 675 sites, `TechData` 58, `AtbConstrArgs` 120.

**Binding is EXACT-ARITY.** `Activator.CreateInstance(Type, object[])` is called without
`BindingFlags.OptionalParamBinding`, so `RuntimeType.CreateInstanceImpl` **culls any constructor whose parameter count
exceeds the supplied argument count before default-value logic is reached — a C# optional parameter does not count.**
Widening conversions still apply (int→double works, double→int does not), which is why every ground `*Atb` takes
`double`. On mismatch: `MissingMethodException` → caught at `:97` → rebuilt into a named `Exception` → **rethrown**.
⚠ The diagnostic handler itself dereferences `constructorArgs` unguarded (`:101-104`), so a null arg replaces the useful
message with a bare NRE.

### 5.3 The template schema and the mod loader

**96 payloads · 95 distinct ids · 517 properties.** Every field on `ComponentTemplatePropertyBlueprint` is read (no dead
field). On `ComponentTemplateBlueprint`, `Formulas` is **hard-indexed to exactly 8 keys** (`Description, Mass, Volume,
CrewReq, HTK, ResearchCost, BuildPointCost, CreditCost`) — **a 9th key is inert and a missing key is a
`KeyNotFoundException` at designer construction.** `Formulas`/`ResourceCost`/`Properties` are dereferenced **with no
null guard** → NRE.

**Load path:** `modInfo.json` → `ModLoader.LoadModManifest` reads `DataFiles` **in list order** → typed blueprints →
`ApplyModGeneric` → `ModDataStore` → `FactionDataStore` puts **everything** in `Locked*` → `Unlock(id)` promotes one.

**Where an entry can be dropped, and whether it is recorded:**

| Point | Recorded? |
|---|---|
| null/empty `UniqueID` → skip | **YES** → `SkippedEntries` (asserted by `BaseModIntegrityTests`) |
| **duplicate `UniqueID` → silent per-field merge** | **NO** — no record, no throw |
| `"Operation": "Remove"` | NO (intentional) |
| a file absent from `DataFiles` | NO — never loaded, nothing logged |
| template loaded but never unlocked | NO — invisible until a design references it |
| missing `Type`/`Payload`, unknown `Type` string | **THROWS** — kills the whole load |

**Schema census — the strategic finding.** The schema already contains the parametric mechanism and almost nothing uses
it:

| Feature | What it is | Templates using |
|---|---|---|
| **`GuiIsEnabledFormula`** | **conditional dials — one template behaving as several. Exactly what "37 parametric doors" needs.** | **2** — *and both are in the broken-namespace set* |
| `GuiSelectionMinMaxRange` + `PairedPropertyName` + `MaxRangeFormula` | a paired tolerance band | **1** (`infrastructure`) |
| `GuiTechSelectionList` · `GuiTextSelectionFormula` · `GuiDisplayBool` | implemented both sides | **0** |
| `EnumTypeName` | typed option lists | 5 |
| `TechData()` in a Min/MaxFormula | research widens the envelope | 13 |
| `DataDict` | 40 blocks authored | **only 9 reachable** — 31 are inert allocations |
| `CollectionOperation` / `Operation: Remove` | loader supports both | **0** |

> **The retrofit needs far LESS new schema than the docs imply — it needs the EXISTING schema exercised, and the two
> templates that already exercise it fixed.**

**Dead schema:** `ModInstruction.Payload` · `ModManifest.PlayerFactionStartingItems`/`AIFactionStartingItems` ·
`Blueprint.FullIdentifier` (debug-only for templates) · the `EnumDict` NCalc function · three `GuiHint` members · the
`PDC` mount flag (0 templates).

**`ComponentType` cannot be the category key** — 19 free-string values with near-synonyms (`Energy Generator` /
`Energy Generation`). `ComponentDoors` (the client's hand-map) exists to paper over exactly that, and is a **4th
registration point with no test** — 4 templates already fall through to "Other".

**MountType flag coverage (96):** ShipCargo 56 · ShipComponent 45 · PlanetInstallation 45 · GroundUnit 32 · Fighter 15 ·
Station 7 · Missile 3 · **PDC 0**.

### 5.4 The four assemblers

**Fidelity rank across 15 design surfaces: SHIP → GROUND → STATION → BUILDING.**

- **SHIP — 11/15 + 100% of attributes.** The only host with a real engine budget gate (`OverMassBudget`, behind the
  static `EnforceMassBudget`), a physical damage layout from part volume + aspect, and committed crew.
- **GROUND — most design *stats*, fewest *attributes*.** ~20 flattened fields, sourced from **6 of ~50** attribute
  types. **`CrewReq` has ZERO hits in all of `GameEngine/GroundCombat/`** — a ground army costs no manpower where a
  ship does. Its backing entity is **inert**: `GroundUnitEntity.cs:57` uses the low-level add that (its own header
  says) does **not** fire install hooks or `ReCalcAbilities`, so a radar/cargo bay/shield on a ground unit grants **no
  ability blob**. `Valid` is **advisory** — the design registers anyway.
- **STATION — computes `BuildMass`/`CrewRequired`/`Valid` and DISCARDS them** (no field exists to hold them).
  `StationFactory.cs:56` gives a deployed station a **bare `new MassVolumeDB()`**, so station mass never reflects its
  design, and `InitialPopulation` defaults 0 with the client never passing one ⇒ **every player-designed station
  deploys unmanned.**
- **BUILDING — the assembled thing has NO post-build identity.** `OnConstructionComplete` installs foundation + modules
  **onto the colony**, so a "factory building" designed as a foundation plus four modules becomes, one tick later,
  **five unrelated colony components.** The chassis+parts model inverts at completion. *(Fix anchor:
  `GroundFootprintAtb`, which the foundation template already carries — the ground war map needs a located,
  capturable, bombardable building, not five loose parts.)*

**Mount-flag enforcement: 0 of 4 at assembly.** `ComponentMountType` has 19 hits in `GameEngine`, **none** a legality
check in ship/ground/building/station. The one real engine check is `OrdnanceDesign.cs:97` (missiles, a fifth host).
Enforcement is a **client-UI courtesy** (`ShipDesignWindow.cs:1157`). *(Reconciled with the older "~1.5 of 4" claim:
**design-time 1.5 of 4, install-time 0 of 4.**)*

**`IChassisAtb` is the generalisation seam and NO ENGINE CODE READS IT.** All four chassis attributes implement it;
only the client consumes it. Each assembler re-tests its own concrete type.

**Duplication:** `StationAssembly.Compute` and `BuildingAssembly.Compute` are **line-for-line the same algorithm**
(Structure↔Footprint renamed) · `AddCosts` copy-pasted **3×** · the 12-line batch-job lifecycle block re-implemented
**4×** · four `*AssemblyResult` classes, four budget computations, four registration functions.

### 5.5 The client UI

**Distinct design UIs:** Component Designer (`ComponentDesignWindow` + `ComponentDesignDisplay`) · **Entity Assembler**
(`ShipDesignWindow` — assembles **four** entity kinds, branching on the mounted chassis's `IChassisAtb.BudgetKind`) ·
Component Library (read-only, F5) · Blueprints browser (F4, debug) · Mod File Editor (main menu only) ·
**`OrdnanceDesignWindow` — UNREACHABLE DEAD UI**: no toolbar entry, no hotkey, no menu name. ⚠ `Pulsar4X.Client/CLAUDE.md`
lists it as *"Functional | Missile design"* — **that row is wrong.**

**The journey:** toolbar button 1 → the taxonomy is built **ONCE at `GetInstance()`** → click a Category, click a Door →
`SetDoor` → `new ComponentDesigner(...)` → a "Type" dropdown appears when the door holds >1 template → dials render per
`GuiHint`, each writing straight through to the engine (**no "apply" — every drag re-evaluates the whole formula
graph**) → name + Save → `CreateDesign`.

- **The design snapshot is FROZEN at startup.** A component you save this session **never appears** in the middle list;
  a template unlocked by research **never appears** in the tree, for the life of the process.
- **Switching the "Type" dropdown discards every dial the player has set.**
- **"Edit" is a misnomer** — Save always calls `CreateDesign`, minting a **new Guid and a new Tech**. Reopening and
  saving *clones*; it never updates. Ten tunings = ten designs and ten unremovable techs.
- **Reopening restores `ListSelection` for the fuel dial ONLY** — enum, tech, ordnance and formula combos show the
  *template default* while the stored value is correct. `_techSelectedIndex` is a **static** field shared by every tech
  dial on every template.
- The name buffer is 128 bytes but the size arg is **32**, so names truncate at 31 chars; an empty name silently does
  nothing.
- The component designer emits **zero `SessionLog` lines**; the ground/station/building saves do.

### 5.6 Research gating

**The mechanism works end to end, live, with no caching, and is CI-proven twice.** Worked example:
`techs.json` defines `tech-beam-range` (`DataFormula "10000 * Pow(2,[Level])"`) → it starts in `LockedTechs` →
`earth.json` lists `tech-modern-technology` in `StartingItems` → `ColonyFactory` calls `Unlock` + `IncrementTechLevel`
→ its `Unlocks[1]` list (35 entries) promotes `tech-beam-range` to `Techs` at **Level 0** → `weapons.json` gives the
laser's Range `"MaxFormula": "TechData('tech-beam-range')"` → NCalc resolves it live off the faction's current level →
`SetMax()` runs every frame → research moves the level → the ceiling doubles.

- **`TechData` = a magnitude · `TechLevel` = a count of unlocked options.** Both **degrade an unknown tech to 0**,
  deliberately and documented (it was crashing the designer) — so a typo'd tech id is indistinguishable from an
  unresearched one.
- **The 56 techs:** 15 raise a design ceiling · 19 move a stat · 12 are pure unlock gates · **10 are PURE SINKS**
  (researchable, priced, wired to nothing — including all four warhead techs and three jump-point techs).
- **Three categories are empty:** ground-combat, defensive-systems, sensors. And the three sensor techs are filed under
  *Missiles & Kinetic*, so a lab with the "Sensors" specialty gets its bonus **on nothing**.
- **There is no prerequisite field.** Dependency is forward-only via `Unlocks`, enforced by *visibility*
  (`IsResearchable` requires the id be in the unlocked `Techs`). Shape: one cost-0 root opens 35 techs; only two chains
  go deeper than one hop. `IncrementTechLevel` has **no `MaxLevel` guard**.
- **`FactionTechDB` is vestigial** — threaded through every `ChainedExpression` ctor and never dereferenced.

### 5.7 Lifecycle and saves

**What is serialized: BOTH the computed values and the dial settings — but only the values are load-bearing.**
`ComponentDesign` is a plain class, so Newtonsoft's opt-*out* rules write every public member, including the fully-built
`AttributesByType` objects. **Nothing re-runs the formulas on load** — no `[OnDeserialized]`, no designer
reconstruction.

> **Consequence:** changing an NCalc formula in a template **does not change existing saved designs.** An old save's
> laser keeps the mass, cost and damage it was designed with. A rebalance — or a *bug fix* — does not heal old saves.
> The one place the formula *does* re-run is **reopening a design in the designer**, which then saves a
> differently-costed twin under a new id.

**`TemplatePropertyValues`** is the only record of intent and it is lossy: it deliberately skips non-player-settable
properties, records a `Type` it **never reads**, boxes nearly everything as `double`, and its replay switch **silently
drops** anything it does not recognise (notably a `long`, which is what Newtonsoft produces reading an integral JSON
number into an `object`).

---

## 6. THE DESIGNER — THE INTENT

**Purpose, in the authors' words:**
> *"A weapon (or engine, or sensor…) is not a **type you pick off a shelf** — it's a **family you design inside.** Click
> Weapons, get five doors; behind each door is a wall of sliders. 'Laser,' 'phaser,' 'railgun,' 'disruptor' aren't
> authored parts — they're **slider settings** that fall out of the family. Fewer categories, each one bottomless."*
> — `CATEGORIES.md` §1

**The decision it creates is a tradeoff under a physical budget, never a menu pick:**
> *"The player is never told 'you must add X.' They dial the **outcome they want**; every dial emits **physical
> quantities**… and physics does the forcing. There is **no authored requirement graph**… An authored graph *tells* you
> the rules. The physical budget *lets you find out.* **We build the second.**"* — `DIALS.md` §0b

**The governing rule:** *"a dial is only real if it moves a number the simulation reads. No cosmetic knobs."*
**The anti-dominance rule:** *"every option must BUY its advantage… if an option has no catch, it's a bug in the design."*

**The acceptance bar:**
> *"a franchise fan should be able to sit down at **Weapons ▸ Energy** and build a phaser, a lazpistol, and a Death-Star
> beam **from the same sliders** — and have each mount on any chassis that can supply it."* — `CATEGORIES.md` §6

**Universality means the SUPPLY GATE decides, not a whitelist:** *"Everything a player builds is the SAME kind of
object: a chassis that provides a structural budget, plus components that consume the budget… gated by physics, at any
scale, within reason"* — and *"'within reason' is enforced by physics + tech + scale caps, **never by a category
whitelist**."*

**Cradle-to-grave is an acceptance test, not a nice-to-have** — and §0g raises it to **three** criteria:
**Reachable · Mirrored · Observable.** *"A door that passes only #1 is a display piece."*

**What the prior audit says universality actually requires — TWO locks:**
> *"**Lock #1 — the mount flag** (shallow)… **Lock #2 — the processor reader** (deep, the real wall). Even if you fix
> every flag, a mounted ability only *does something* if some **processor reads that attribute off that host**."*
> …*"the universality is real in the basement and **lost on the main floor**."* — `DESIGNER-AUDIT/00`

**The proof-of-pattern to copy:** *"`EnergyGenerationAtb` (reactors) is the one already-universal ability. It is read by
a **space** processor *and* a **ground** system off the same attribute. **Every other capability should look like
this.**"*

---

## 7. THE DESIGNER — THE GAP

### 7.1 The one-sentence gap

> **The taxonomy was built. The collapse was not.**

`ComponentDoors.cs:12-15` says so itself: *"This is the WINDOW's view of the design; the parametric 'laser/railgun are
just dial settings' collapse is a **later engine change**."*

**The measurement:** Weapons ▸ Energy holds four templates — `laser-weapon` (21 dials), `pulse-laser` (4),
`plasma-repeater` (4), `energy-weapon` (4). **Set intersection: empty.** Weapons ▸ Ballistic, seven templates: also
empty. You pick a Type and get that template's private slider wall.

**And the inverse fault:** `ground-rifle`, `ground-cannon` and `ground-autocannon` have **identical** dial sets
(CarryMass · Attack · Mode · Range_m) — three "types" that already *are* one parametric family, uncollapsed. **That is
the cheapest possible first proof of the pattern.**

**Door state:** 11/11 categories present; **36 doors declared, 34 live** — Propulsion ▸ Fluid and Chassis ▸ Mega never
render (no template classifies into them), and Chassis ▸ **Prebuilt Units** exists although `CATEGORIES.md` says to kill
it (**Q-E**). 91 of 95 templates mapped; the 4 unmapped fall through to "Other" and nothing vanishes.

### 7.2 The eight as-built findings that most change what we do

| # | Sev | Finding |
|---|---|---|
| **R-1** | 🔴 | **ADDING A CONSTRUCTOR OVERLOAD BREAKS SAVE-LOAD ON ~12 `*Atb` CLASSES.** Newtonsoft auto-uses a parameterized constructor **only when there is exactly one public ctor**. Two public ctors + no parameterless ⇒ `JsonSerializationException: Unable to find a constructor to use`. **At risk:** `AdminSpaceAtb`, `CargoStorageAtb`, `CargoTransferAtb`, `EnergyStoreAtb`, `EnergyGenerationAtb`, `EnergySolarGenerationAtb`, `GeoSurveyAtb`, `GravSurveyAtb`, `LocalConstructionAtb`, `NewtonionThrustAtb`, `ReactionlessThrustAtb`, `OrdnancePayloadAtb`+subclasses. ⚠ **This makes the planned "add a Range dial via a new overload" slice UNSAFE as written.** Mitigation is one line per class in the same change: `[JsonConstructor] private XAtb() {}` — the pattern `IndustryAtb.cs:20-21` already uses. |
| **R-2** | 🔴 | **FIXING THE SIX NAMESPACE STRINGS UNMASKS TWO WORSE BUGS.** (a) **`ComponentDesign.AttributesByType` is NEVER CLEARED** (written only at `ComponentDesigner.cs:95`). `missile-payload` has three mutually-exclusive payload attributes gated by `GuiIsEnabledFormula`; switching payload type **adds** the new one and **leaves** the old ⇒ **a design carrying two warheads.** (b) **`NavalAcademyAtb` has three public ctors and no parameterless** ⇒ R-1's load break, live. Both are masked *only* because those templates throw first. **The first slice must fix all three together or it ships worse than it removes.** |
| **R-3** | 🔴 | **A DEBUG-BUILD CLIENT BRICK, 16 WINDOWS WIDE.** `Window.cs:22` `_beginCount` is **static**, incremented in `ValidateBeginCall`, decremented only in `End()`, **with no per-frame reset**. Sixteen windows — incl. `ComponentDesignWindow.cs:120`, `ComponentsWindow.cs:203`, `ColonyManagementWindow.cs:108`, `ToolBarWindow.cs:277` — put `Window.End()` **inside** the `if (Window.Begin(...))` block. In a Debug build (`dotnet run`'s default): **collapse the Component Designer once, or let it throw once, and every subsequent `Window.Begin` throws forever — blanking all 52 wrapper windows for the rest of the session.** Release compiles the counter out. ⚠ `Pulsar4X.Client/CLAUDE.md`'s claim that the wrapper *"calls `End` unconditionally"* is **false** (`Window.cs:57-69`). |
| **R-4** | 🔴 | **THE PER-DESIGN RESEARCH PATH ENDS IN A CRASH.** `ResearchProcessor.cs:133` registers a completed design as `IndustryDesigns[tech.UniqueID]` — keyed `"tech-<guid>"` — while **every consumer keys by the design's own `UniqueID`**. A player who designs a component, researches it, and clicks "+ New Job" hits a **`KeyNotFoundException`**. Invisible today because everything base-mod is `StartResearched`. **Blocks charging research for any part — i.e. blocks the ground research tree.** One-line fix: key by `tech.Design.UniqueID`. |
| **R-5** | 🟠 | **THE LOADER HAS NO IDENTITY CONTRACT.** A duplicate `UniqueID` is a silent **per-field merge** (last-non-null wins, written onto the first object); a value type always boxes non-null, so **`MountType` is overwritten even when the second entry omits it** — a partial override silently zeroes it to `None`. `SkippedEntries` records **exactly one** failure mode (a null key), so **`BaseModIntegrityTests` passes with a live collision.** **Live cost:** `installations.json` (#8) loads before `storage.json` (#9), storage wins, and **Earth's `default-design-spaceport` builds the *Space Port*, which has no `CargoStorageAtb`** — the planetary spaceport complex's storage is unreachable data. |
| **R-6** | 🟠 | **TWO DESIGNERS EXIST IN FACT — One Verb, Both Seats violated inside the designer.** Three constraints are **client-only**: the paired-range gap (`SetMaxRange`'s sole caller is `ComponentDesignDisplay.cs:629`), step granularity (`SetStep` uncalled by the engine outside the enum branch), and the fuel-type safety filter (`:950-955` vs the unguarded `ExhaustVelocityLookup`). ⇒ **the JSON/AI path is unbounded, unstepped, unfiltered, and can crash on a fuel the UI would have hidden.** |
| **R-7** | 🟠 | **FIVE LATENT EVALUATOR BUGS, HELD BACK ONLY BY WHAT THE DATA HAPPENS NOT TO USE.** `CreditCost` returns `ResearchCostValue` (`ChainedExpression.cs:374`) · `Volume_km3` returns **m³** (`:334`) · `MineralCosts` is a byte-identical duplicate of `ResourceCosts` (`:363`) · `SetPropertyValue` **always throws after its side effect** · **no cycle guard** (`:179-187`) ⇒ a formula cycle is an **uncatchable `StackOverflowException`**. ⚠ **Corrects this campaign's own "the formula layer is SOUND"** — true of the authored DATA, **false of the ENGINE**. A retrofit that starts authoring new formulas walks straight into these. |
| **R-8** | 🟠 | **`_isDependant = false` IS SET ON THE WRONG OBJECT** (`ChainedExpression.cs:529` sets `this`, not the new `argExpression`), so the one-shot temp expressions **do** register themselves — the exact failure the code comment forbids. `DependantExpressions` is append-only, never cleared, and `SetAttributes()` runs **every frame of a slider drag** ⇒ unbounded growth per session plus a re-entrant add into the list being iterated = `InvalidOperationException: Collection was modified` mid-designer. **The "chained" half of `ChainedExpression` does not work.** |

### 7.3 The silent-failure sites, ranked by what a player loses

1. **An unresolved build-cost material is dropped, no log** (`ComponentDesigner.cs:61-63`). Live on three ids.
2. **A stale attribute survives after its dial is switched off** (R-2a) — worst on `missile-payload`.
3. **A disabled dial's whole attribute is skipped without trace** (`:86`) — 23 live gates.
4. **A second property binding the same `*Atb` type silently overwrites the first** (`:95` uses `dict[type] =`, not `Add`).
5. **Clamp-to-bounds is silent AND it burns the formula** (`ComponentDesignProperty.cs:242-246`) — after the first write
   that dial no longer tracks tech. *(This is landmine L7, confirmed and extended.)*
6. **A tech dropdown silently drops options** for unresearched/unknown techs, shifting `ListSelection` underneath.
7. **`TechData`/`TechLevel` degrade an unknown tech to 0** — deliberate and correct, but a typo is invisible.
8. **`_isDependant`** (R-8) — silent growth, then a non-silent exception.
9. **`StepValue` unenforced outside the client** · 10. **the paired-range gap unenforced outside the client.**
11. **Culture-unsafe formatting** — `ComponentDesignProperty.cs:69` does `.ToString()` with **no culture** on a tech
    value; on a comma-decimal locale the option expression becomes garbage. Windows-only, client-only, CI-invisible.
12. **An unparseable description degrades to raw text** — ✅ **deliberate, correct, well-commented.** Listed so it is
    not mistaken for a bug.

### 7.4 Numbers the designer shows that decide nothing

- **`laser-weapon`'s entire thermal/optics readout** — 8 of its 10 display rows are intermediates feeding only each
  other and `Mass`. `ThermalOutput_W` is read **only** by the per-weapon firing sim; the auto-resolver reads
  `CombatHeat_kJps`, which the laser's 7-arg constructor defaults to **0**. **The most elaborate numbers in the whole
  designer do not touch the battle the game actually resolves.**
- **`Amphibious`** — doubles the part's mass, read by zero gameplay lines.
- **`Size`** on all four ground frames — passed in, cloned, read nowhere; free, because frame masses are hardcoded.
- **Combo indices on reopen** — the widget lies while the stored number is right.

---

## 8. THE DESIGNER AUDIT — 40 findings across 7 passes

**Tally: 9 🔴 · 15 🟠 · 8 🟡 · 5 🔵 · 3 ✅.** Two of the four checks below are the framing that made the audit useful:
the 2026-07-08 `docs/DESIGNER-AUDIT/` asks *is the designer UNIVERSAL* (can a part mount on many hosts); this campaign
asked *is it **FAITHFUL*** (does a dial you turn get recorded correctly and arrive at the thing that reads it). **Both
matter and they are independent** — a part can be perfectly universal and carry a wrong number.

### Pass 1 — re-verify the 2026-07-08 audit at HEAD

| # | Sev | Finding |
|---|---|---|
| **D1-1** | 🟠 | **Duplicate template ids — and the loader MERGES per-field.** Only two are real same-Type collisions: `ComponentTemplate spaceport` (installations.json + storage.json) and a benign `Gas hydrogen-sulphide` twice in one file. See **R-5** for the mechanism and the live cost. |
| **D1-2** | 🟠 | ⚠ **CORRECTED — see §11 #2.** The three ground-stance ids are **not** a loader collision (different `Type`s → separate dictionaries). What is true: two rival catalogs describe the same three stances and the *code* picks one. Downgraded 🔴→🟠. |
| **D1-3** | 🟡 | **The prior audit's per-template analysis is proportionally stale.** It counted 67 templates; there are now 96 (+43%). Its file-by-file enumeration is partial by construction. |
| **D1-4** | 🔵 | **The prior diagnosis is still the right frame** — two locks (mount flag, processor reader), nine duplicated ability pairs, and three parts of the tree that already do it right (industry, research/unlock, `EnergyGenerationAtb`). ⚠ **`PDC` is still at zero templates; `Fighter` is NOT dangling — 15 templates carry it** (corrected in Pass 2). |

### Pass 2 — does an authored dial BIND?

**What passed:** 675/675 `PropertyValue` references resolve · 58/58 `TechData` · 70/70 class names exist · 119/120 ctor
arities bind. **And the ten attribute types authored at two different arities are DELIBERATE backward-compat overloads**
(e.g. `GroundArmorAtb.cs:45`: *"the 3-arg ctor keeps every EXISTING base-mod plating template byte-identical"*), not
starved templates.

| # | Sev | Finding |
|---|---|---|
| **D2-1** | 🔴 | **SIX `AttributeType` STRINGS NAME A NAMESPACE THAT DOES NOT EXIST (`Pulsar4X.Atb`) — FOUR LIVE DESIGNER DOORS THROW ON OPEN.** `ComponentDesignProperty.cs:103-105` throws **unconditionally in the constructor**, so no "is this dial enabled" check can dodge it. Dead: `logistics-office` (→`Pulsar4X.Logistics`), `naval-academy` (→`Pulsar4X.People`), `missile-electronics-suite` and `missile-payload` (all →`Pulsar4X.Weapons`). All four are wired as doors in `ComponentDoors.cs:59,74,130,137`, and `ComponentDesignDisplay.SetTemplate` has **no try/catch**. **Which door throws depends on name-sort order:** "Logistics Office" sorts first, so *that* door throws on the door click; the other three throw when the player picks them in the "Type" dropdown. ⚠ **The docs already caught ONE** (`naval-academy`, named in two places) — **nobody spotted it was a class of six, or that it kills the missile designer.** |
| **D2-2** | 🟠 | **An orphan starting design points at one of them** — `componentDesigns.json:390-394` defines `default-design-logistics-office`. No scenario references it, so it is latent; the day one does, New Game throws during faction setup. |
| **D2-3** | 🟠 | **The shaped-charge warhead supplies 5 args to a 6-param ctor** — the only arity mismatch in the base mod. ✅ **Cheaper than it looks: `Liner Thickness` already EXISTS as a dial** (default 3, already feeding `LinerVolume`); the formula simply never passed it. **One line.** |
| **D2-4** | 🟠 | **Three build-cost materials do not exist and are SILENTLY DROPPED** (`ComponentDesigner.cs:61-63`). ⚠ **Worse than first recorded: `gallicite` is `missile-electronics-suite`'s ONLY cost line, and `duranium`+`mercassium` are `ordnance-cargo-hold`'s only two — both parts currently cost NOTHING to build.** 347 of 350 cost references are good. |
| **D2-5** | 🟡 | **`solarArray` is the worst-authored template.** The only one of 96 whose mount flags are a **raw number** (`"MountType": 1` = ShipComponent only), so **a solar array cannot be installed on a colony** despite being in Earth's `StartingItems`. It also carries the mod's only duplicate property name (`Area` + `Area ` with a trailing space). ⚠ An *exact* duplicate would throw `ArgumentException` at `ComponentDesigner.cs:74` — the trailing space is all that keeps it harmless. |
| **D2-6** | 🔴 | **THE MISSING GAUGE — no test constructs a `ComponentDesigner` for all 96 templates.** ~20 lines catches every binding failure above. ⚠ **This was already specified and DEFERRED by the 2026-07-23 audit** (*"a test that instantiates a `ComponentDesigner` from EVERY unlocked base-mod template and asserts no throw… Deferred"*). It is an **owed** gauge, not a new idea. |
| **D2-7** | 🔵 | **Dials built, read by the resolver, and turned by no part in the game:** flak `Recoil` (always 0 — the 6-arg ctor is unreachable from data) · beam `CombatHeat_kJps` (only `pulse-laser` dials it) · shield `ShieldRegenFraction` (3 of 4 augments at the 0.34 default) · armour nature-tuning on `ground-plating` (intentional — "a plain plate"). **Authoring work, not bugs.** |
| **D2-8** | 🔵 | Cosmetic: `genericWpndbargs`'s description lists **five** parameter names for a **four**-value formula, with an unterminated quote and a `WpnTypes type` parameter no constructor has. A stale human note in a field nothing parses. |

### Pass 3 — reach, delivery, and research

| # | Sev | Finding |
|---|---|---|
| **D3-1** | 🔴 | **THE ENTIRE GROUND STACK HAS NO RESEARCH GATE.** Only **18 of 96** templates carry any tech gate; **42 are BOTH `ResearchCost: "0"` AND tech-ungated**, and *every* ground part is among them — all four frames, all five weapons, plating, radar, locomotion, magazine, all three augments, the constructor, all three prebuilt units. **On turn one, with zero research, `ground-rifle` dials to Attack 5000 / Range 100 km.** Contrast `laser-weapon`, whose Range ceiling **is** `TechData('tech-beam-range')`. **The research rung of cradle-to-grave is absent for all of ground combat.** |
| **D3-2** | 🟠 | **`Amphibious` is a dial the player PAYS FOR that does NOTHING.** A live slider on `ground-locomotion`; turning it on **doubles the part's mass** (`Mass: 100 * SpeedFactor * (1+RoughHandling) * (1+Amphibious)`); read by **zero lines of code**. `HexPathfinder.IsImpassable` returns true for Ocean unconditionally, and its own comment says amphibious gating is *"a cradle-to-grave follow-on."* **The cleanest example of a costed decision with no effect.** |
| **D3-3** | 🟡 | **`GroundChassisAtb.Size` does nothing and is FREE** — a live slider on all four frames, stored, cloned, and **zero `.Size` reads in GameEngine**. It doesn't even cost, because frame masses are hardcoded *(the prior audit's **T2a**, confirmed)*. |
| **D3-4** | ✅ | **THE DESIGNER → ASSEMBLER HOP IS SOUND — and this MOVES root cause A.** **41 of 43** ground dials across 13 attributes have a reader. So the loss is **not** designer→assembler; it is one hop further, at **assembler → `WeaponProfile`**. A materially different place to fix, and the expensive half is already correct. |
| **D3-5** | 🔵 | **Door coverage is good — 91 of 95 mapped**, and `Classify` has an explicit "Other" fallback so the four strays still appear. Zero doors point at a non-existent template. |
| **D3-6** | 🟡 | **Research where it exists is mostly a price tag, not a ceiling.** `flak-weapon` costs `[Mass]` to research but **all five of its dial ceilings are hardcoded constants** — no amount of research widens a flak design. |

### Pass 4 — the assembler → resolver hop

**The ledger — ✅ from the design · ⬛ an engine constant · ⬜ always zero:**

| Weapon | DPS | Velocity | Tracking | Saturation | **Range** | Nature | Delivery | Penetration | PerShotEnergy | Heat | **from design** |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Ship — Beam | ✅ | ✅ | ✅ | ✅ | ✅ `MaxRange` | ⬛ | ⬛ | ⬜ | ⬜ | ✅ | **7/10** |
| Ship — Railgun | ✅ | ✅ | ✅ | ✅ | ⬛ 500 km | ⬛ | ⬛ | ⬜ | ⬜ | ⬜ | **4/10** |
| Ship — Flak | ✅ | ✅ | ✅ | ✅ | ⬛ 50 km | ⬛ | ⬛ | ⬜ | ⬜ | ⬜ | **4/10** |
| Ship — Plasma | ✅ | ✅ | ✅ | ✅ | ⬛ *borrows railgun's* | ⬛ | ⬛ | ⬜ | ⬜ | ⬜ | **4/10** |
| Ship — Disruptor | ✅ | ⬛ light-speed | ⬛ 1.0 | ✅ | ⬛ 400 km | ⬛ | ⬛ | ⬜ | ⬜ | ⬜ | **2/10** |
| **Ship — Missile** | ⬛ 100 kJ/s | ⬛ 5 km/s | ⬛ 0.9 | ⬛ 1.0 | ⬛ 1000 km | ⬛ | ⬛ | ⬜ | ⬜ | ⬜ | **0/10** |
| **GROUND — any unit** | ✅ `Attack` | ⬛ per-mode | ⬛ per-mode | ⬛ per-mode | ✅ hexes × pitch | ✅ from `Mode` | ✅ from `Mode` | ✅ | ✅ | ⬜ | **6/10** |

| # | Sev | Finding |
|---|---|---|
| **D4-1** | 🔴 | **YOU CANNOT DESIGN A WEAPON'S RANGE — except on a beam.** `ShipCombatValueDB.cs:52,62,68,73` hardcode flak 50 km, disruptor 400 km, railgun 500 km, missile 1000 km; plasma **borrows the railgun's**. None of those templates even offers a Range dial — `railgun-weapon`'s four dials are Muzzle Velocity, Kinetic Energy Per Shot, Rounds Per Second, Tracking. ⚠ **Why this is a headline:** the developer's X9 ruling commences every battle at *"the range of the highest-range weapon on the unit of the group"* — so **the opening range of every fight is a fixed ladder (missile > railgun > disruptor > flak) that no design decision can reorder.** The standoff-vs-brawl anchor of the closing model is a choice of weapon *class*, not of design. |
| **D4-2** | 🔴 | **A MISSILE CARRIES ZERO DESIGNED NUMBERS INTO COMBAT — 0 of 10.** All five values are stubs. **Every missile in the game fights identically**, whatever warhead, engine or seeker you fit. **Compounds with D2-1: you cannot design a missile, and it would not matter if you could.** |
| **D4-3** | 🟠 | **`Penetration` and `PerShotEnergy` are literal 0 for EVERY ship weapon** and come from the design on the ground. Under canon **M19** that asymmetry is a violation, not phasing. *(§7/§9 explain **why**: they are undefined against a pooled-toughness model.)* |
| **D4-4** | 🟠 | **A ground weapon's velocity, tracking and saturation are NOT designable** — three constants picked by the `Mode` dropdown (Melee 1/1.0/1 · Artillery 300/0.0/100 000 · else 1000/0.0/1). No rate of fire, no muzzle velocity, no accuracy. **Deliberate and commented** (it is how ground got onto the shared kernel) — and it is the ground half of the "same depth as space" gap. |
| **D4-5** | 🟠 | **GROUND CARRIES MORE DESIGN FIDELITY INTO THE FIGHT THAN MOST SHIP WEAPONS — the opposite of the assumption.** Ground 6/10 including the two fields **no ship weapon carries at all**; railgun/flak/plasma 4/10; missile 0. Only the beam beats ground. **Root cause A is a SPACE problem at least as much as a ground one.** |
| **D4-6** | 🟡 | **`HeatPerSecond` is fed by exactly one weapon type** — and with D2-7, by **one template** in the base mod. The burst-vs-sustained decision is not in the game yet. |
| **D4-7** | 🟡 | **A doc had gone stale under the code** — `FLEET-COMBAT-CLOSING-DESIGN.md` §ROOT A still said railgun/flak/missile were rangeless. **De-staled 2026-07-28** with what actually changed. |

### Pass 5 — the armour reconcile and the value that never updates

| # | Sev | Finding |
|---|---|---|
| **D5-1** | 🔴 | **"ARMOUR" MEANS TWO DIFFERENT MECHANICS INSIDE THE ONE RESOLVER.** **Ground:** a flat per-source soak — `ArmourSoak` subtracts `(armour − penetration) × 1.5 × natureFactor` off **each incoming source**, and `ArmourSoakBurst` splits a salvo so each chunk is soaked separately. **Ship:** armour thickness folds **straight into Toughness** as joules (`ShipCombatValueDB.cs:518-520`), and hardening is a **fleet-averaged percentage** off the salvo. **Plain English: on the ground armour stops a fixed amount of every hit; in space armour is extra hit points.** ⚠ **That is why `Penetration`/`PerShotEnergy` are 0 on ships — not lazy, UNDEFINED against a pool.** `CombatEngagement.cs:1366` says so. **Under M19 this is the deepest violation found.** |
| **D5-2** | 🟠 | **A SHIP'S ARMOUR HARDENING PROTECTS THE WHOLE FLEET, NOT THE SHIP THAT PAID FOR IT.** `FleetArmourSoakFraction` takes a toughness-weighted average across the fleet and multiplies the **whole incoming salvo** by it — so one hardened cruiser protects the unarmoured freighters. On the ground the same dial is strictly per-unit. **Same designed part, opposite scope** — and it inverts a real decision. |
| **D5-3** | 🟠 | **EVERY "GRAVE RUNG" CLAIM SCALED BY `comp.HealthPercent` IS INERT.** `ShipCombatValueDB.Calculate` has **exactly one production call site — `ShipFactory.cs:144`, at construction** — and **nothing invalidates the blob** (zero `RemoveDataBlob<ShipCombatValueDB>`). So every health multiply is frozen at build-time 1.0. **That silently falsifies the cradle-to-grave grave rung written into at least six attribute doc-comments** (`RadiatorAtb`, `ShipMagazineAtb`, `PointDefenseAtb`, `UnitCaliberAtb`, `ArmourHardeningAtb`, and `ShieldAtb`'s *"a damaged/shot-off generator projects a weaker/no shield"*). |
| **D5-4** | 🟠 | **A REFIT IS STALE TOO.** `Entity.AddComponent` installs on a built ship and **never** recomputes. *(`ShipRefitBegan`/`Completed` exist as event types with **zero publishers** — a refit pipeline was scaffolded and never built, which is why nobody hit this.)* |
| **D5-5** | 🟠 | **THE AI READS THE FROZEN NUMBER.** `FactionRollup.MilitaryStrength` sums `Firepower + Toughness` and feeds **`NPCDecisionProcessor`** (objective selection), **`ThreatAssessment`** (who to fear) and **`AIDecisionRecorder`** (the log its behaviour is audited from). **A faction that has lost half its navy still rates itself — and is still rated by rivals — at full strength.** |
| **D5-6** | 🔵 | ✅ **Credit: the ship armour NATURE model is real and IS authored.** `ArmourHardeningAtb` → `ArmourSoakVs*` → `FleetArmourSoakFraction`; the template is in Earth's `StartingItems` and a base-mod ship mounts one. **The gap is the SHAPE and SCOPE of the model, not the presence of the matchup.** |

### Pass 6 — how much of "one resolver" is one thing

**`CombatKernel` members by PRODUCTION callers:** `ArmourSoak` 6 · `ArmourSoakBurst` 4 · `BurstShotCount` 3 ·
`HitFraction` 2 · `ShieldSoakFraction` 2 · `LandedFraction`/`SoakFractionOf`/`ResolveShield` 1 each ·
**`Combatant` — 4, ALL INSIDE `GroundCombatant.cs` ITSELF** · **`ResolveSalvo` — DOES NOT EXIST.**

| # | Sev | Finding |
|---|---|---|
| **D6-1** | 🔴 | **THE "ONE SALVO KERNEL" SHARES THE ARITHMETIC, NOT THE STRUCTURE.** `CombatKernel.Combatant` — the neutral view the class doc calls load-bearing (*"so neither the hex board nor the ship `Entity` leaks into the math"*) — **has zero production consumers**; the only caller of `ToCombatant` is one test. The ship side never builds one. There is **no `ResolveSalvo`**, so each domain still runs its own loop over its own types. **Sharing a subtraction does not force agreement on what you subtract from — which is exactly how D5-1 is possible.** |
| **D6-2** | 🟠 | **THE DEAD BRIDGE WOULD REGRESS A WORKING FEATURE IF WIRED UP AS-IS.** `ToCombatant` hardcodes `ShieldRegen = 0`, while the live ground resolver **already** regenerates per unit from a **designed** dial (`GroundForcesProcessor.cs:395`), fed part→design→unit and gauged by a named test. **The obvious next merge step silently deletes a dial that works today.** Flag it *on the bridge*. |
| **D6-3** | 🟠 | **SHIELDS ARE ONE FLEET-WIDE POOL IN SPACE AND PER-UNIT ON THE GROUND.** `ApplyShield` drains `FleetCombatStateDB.ShieldPool_J`, seeded from every ship's summed capacity — so one battleship's generator absorbs damage aimed at unshielded freighters. **Third system splitting the same way** (with the battlefield container and armour hardening). ⇒ **Q-A**. |
| **D6-4** | ✅ | **THE GROUND SHIELD CHAIN IS THE BEST-BUILT THING FOUND — copy it.** `GroundAugmentAtb.ShieldRegenFraction` (a real JSON dial, 6-arg ctor) → a **Shield-weighted** assembly average (not a naive mean) → design → raised unit → resolver regen — **with a test that asserts the DECISION** (fast small ward vs slow big generator), not the plumbing. **Every rung present, gauged, traceable in one grep. This is what "done" looks like.** |

### Pass 7 — is doctrine really "the only thing that changes how forces act"?

**All 14 blueprint fields ARE authored across the 25-entry catalog. Nine reach the fight; five do not.**

| # | Sev | Finding |
|---|---|---|
| **D7-1** | 🔴 | **FIVE OF FOURTEEN DOCTRINE DIALS NEVER REACH THE FIGHT — and FOUR are the 2026-07-24 behaviour rulings.** **`TargetPriority`** (#18): a string parser exists; **no selector function exists anywhere**, and both resolvers still spread fire by current health — precisely what the enum's own doc-comment says it was written to end. **`BreakAwaySeconds`** and **`Pursues`** (#15): accessors with **zero callers** — retreat is still instant, disengage still free. **`RetreatCasualtyThreshold`** (#15): `EffectiveRetreatCasualtyThreshold` has **zero callers, not even a test**; the resolver reads a `const 0.5` swung by **personality**. **`SpeedMult`**: read by no movement code. ⇒ **doctrine changes firepower, toughness, whether you shoot first and when you break off — not who you shoot, whether you chase, how long breaking away takes, or how fast you move. It is still mostly a stat multiplier.** |
| **D7-2** | 🔴 | **A GREEN CI TEST CERTIFIES THESE FEATURES AS DELIVERED.** `UnifiedDoctrineTests.cs:226` is described as *"The catalog actually **delivers** the behaviours the rulings asked for: something pursues…"* and asserts `all.Any(CombatDoctrine.Pursues)`, `ParseTargetPriority(...) == FinishWounded`, `EffectiveBreakAwaySeconds(...) > 0`, and *"Break and Roll chases the rout"*. **Every one checks that a JSON file contains a value. Nothing chases anything.** ⚠ **The Visibility Gate INVERTED — a gauge reading normal while the system is inert.** Worse than no test: it converts *"not built"* into *"verified built"* on the only correctness gauge the project has. **Any fix must re-point these four assertions at behaviour in the same slice.** |
| **D7-3** | 🟠 | **"ENGAGEMENT" MEANS TWO DIFFERENT THINGS.** Space `EngagementPosture` (WeaponsFree/WeaponsHold/ReturnFire — *will you shoot first*) vs ground `GroundEngagementStance` (HoldGround/CloseToEngage/StandOff — *will you advance or kite*). **Ground never reads the doctrine's posture at all.** The "unified catalog" carries a field half the game ignores. **Fourth instance of the space/ground split** ⇒ **Q-A**. |
| **D7-4** | 🟡 | **`SpeedMult` is inert AND the UI states it as fact** — printed on the active-doctrine line and the selection preview (`FleetWindow.cs:724,767`). An inert dial the player cannot see is a wasted opportunity; **one the UI reports as an active effect is misinformation.** |
| **D7-5** | ✅ | **NINE OF FOURTEEN DO LAND** *(corrected from ten — see §11 #1)*: `FirepowerMult`/`ToughnessMult` on **both** domains, `DamageTakenMult` on ground, `IsRetreat`, `EngagementPosture` (space), `CooldownSeconds` at **all three** switch sites, and `Domain`/`Family`/`DisplayName`. **The catalog is real work; the gap is specifically the behaviour half.** |

---

## 9. THE RESOLVER AUDIT — 49 findings, FIVE root causes

**Method:** 17 passes, each preceded by reading the relevant design docs. **Rate check:** passes 1–3 found 15 defects ·
4–5 found 9 · 6–7 found 10 · 8–10 found 5 (two were self-corrections) · 11–12 found **1**, with Pass 12 finding **zero**
· 13–17 found 5 — but **one was a 🔴 found the moment the sweep crossed from engine into client**, which no earlier pass
had done.

### The five root causes

| # | Root cause | In one line | Key findings |
|---|---|---|---|
| **A** | **The design→resolver FEED is lossy** | *The designer's numbers don't arrive.* | ground weapon 6/10 *(re-measured — see §10 C-3)* · alpha zeroed in **both** domains · **a missile 0/10** · a fire-control dial gated by a flag nothing switches on · combat values **frozen at build** · a gutted hull still reads as an armed warship |
| **B** | **The battlefield is a CONTAINER, not an ENGAGEMENT** | *Everything aggregates at the fleet/region level, which is the wrong level.* | a **star system** / a **region** IS the battlefield · `ResolveRegionCombat` is **O(units²)** · wings have **no position** to manoeuvre with · fleet-wide range decisions · **one long-range gun hijacks the fleet** · an **all-beam fleet closes to point-blank** · **one shield pool per fleet** |
| **C** | **Battle state is EPHEMERAL** | *The battle's own accumulated state does not survive the battle.* | **P6-5** below |
| **D** | **Doctrine cannot carry behaviour** | *The runtime object has nowhere to put its own dials.* | `FleetDoctrineDB` has **no fields** for `TargetPriority`/`RetreatCasualtyThreshold`/`BreakAwaySeconds`/`Pursues` — the fix is a **save-schema change**, not a copy bug · the same concept has two names across domains, one pair **reciprocal** · ground has no `SpeedMult` field at all |
| **E** | **Two worlds** | *The live sim and the auto-resolver are different games.* | the auto-resolver **never spawns ordnance** · the same missile does 100 kJ/s or up to 5 GJ depending which path resolves the fight · orbital bombardment exists only in the path that does not run the battle · space combat has **no environment at all** while ground applies hazard attrition mid-fight |

### The findings that most change what we do

| # | Sev | Finding |
|---|---|---|
| **P6-5** | 🔴🔴 | **EVERY SCRAP OF BATTLE ATTRITION IS ERASED ON DISENGAGE.** `CombatEngagement.cs:848` **removes the whole `FleetCombatStateDB`**, and its pools **lazily re-seed to FULL at first contact** (`:692`). On disengaging a fleet is handed: **full ammunition · full shields · zero heat · a full manoeuvre reserve · and its accumulated partial damage deleted.** Four consequences: **(a) FREE REARM** — magazines and the whole ammo-logistics chain are meaningless in space. **(b) FREE SHIELD + HEAT RESET** — instant, not regenerated; radiators stop mattering between fights. **(c) THE KITING CLOCK DEFEATS ITSELF** — `ManeuverBudget` exists expressly to make "kite forever" impossible, and disengaging refills it. **(d) A FLEET THAT DISENGAGES BEFORE EACH KILL THRESHOLD NEVER LOSES A SHIP.** **Not a missing feature — an exploit that also silently deletes four designed systems.** |
| **P7-2** | 🔴🔴 | **TWO MISSILE MODELS DISAGREE BY UP TO FOUR ORDERS OF MAGNITUDE.** The live sim states its own scale: *"orbital closing speed with a 100 kg dry mass carries **50 MJ–5 GJ**, which destroys many components in one hit."* The auto-resolver gives the same launcher **100 kJ/s**. **They have never been calibrated against each other.** |
| **P15-1** | 🔴 | **THE BATTLE REPORT IS A FOG-OF-WAR LEAK.** `BattleLog` is a **static class with ONE global event list** and `BattleReportWindow` contains **zero** occurrences of "faction" — no filter of any kind. **The player sees every battle between every faction anywhere in the galaxy**, including fights they never detected. **The fix is cheap: `BattleEvent` already carries a `FactionId` that nothing reads.** |
| **P3-1** | 🔴 | **`EnableFireControlRange` IS A DESIGNER DIAL THAT NOTHING SWITCHES ON.** It gates a real researchable, buildable, installable fire-control component from contributing to combat values. Default false, **and no client code sets it true.** The whole cradle-to-grave chain exists and the last switch is off. |
| **P5-1/2/3** | 🔴 | **THE COMPUTED RANGE PREFERENCE CAN CONTRADICT THE DOCTRINE.** `FleetManeuver` = **min** evasion; `FleetDesiredRange` = the **longest finite** range; **neither takes a doctrine argument.** A fleet ordered to brawl that carries **one** long-range gun stands off at that gun's range. A fleet armed **only** with beams computes a desired range of **0** and closes to contact. *(⚠ that last one is unreachable with base-mod data — see §10 C-4.)* |
| **P6-2** | 🔴 | **THE RETREAT DECISION IS A HARDCODED CONSTANT MODULATED BY PERSONALITY, NOT BY DOCTRINE** — while all 25 catalog entries author their own threshold that has nowhere to live. |
| **P4-5/4-6** | 🔴 | **SPACE COMBAT HAS NO ENVIRONMENT AT ALL, AND THE ASYMMETRY IS MIRRORED.** Ground applies hazard attrition during a fight; `CombatEngagement`/`CombatKernel` contain **zero** hazard references. Meanwhile ground **generates** SensorJam/MovementDrag and applies **neither**, while space applies both to movement/sensors. **Hazards affect space movement but not space combat; ground combat but not ground movement.** |
| **P13-1** | 🟠 | **BOMBARDMENT IS A SIDE EFFECT, NEVER A DECISION** — `ApplyGroundBombardment` has exactly one caller, inside the live damage path. **A fleet that wins orbit in an auto-resolved battle has no path to the surface whatsoever.** |
| **P8-3** | 🔴 | **EMBARKED TROOPS DIE SILENTLY.** The resolver has zero references to troop bays or transports. Losing a whole invasion army produces one line: *"Transport Alpha destroyed."* **The loss is correct; the silence is not.** |
| **P4-4** | 🟠 | **AN ADMIRAL IMPROVES A FLEET; A BATTALION'S LEADER DOES NOTHING.** `GroundCombat/` contains **zero** references to `OfficerCharacter`/`CommanderDB`/`BonusesDB`. Academies and officers feed ships and stop at the shoreline. |
| **P6-1** | 🟠 | **THE TWO AMMO MODELS MEASURE DIFFERENT THINGS** — space burns ammo proportional to energy fired × time; ground burns a flat 1.0 kg per salvo. **On the ground a rifleman and a ten-cannon tank consume identical ammunition**, and magazine size measures *ticks you can fight*, not shots. |
| **P10-2** | 🟠 | **49 COMBAT FIXTURES EXIST AND NOT ONE OF THIS AUDIT'S FINDINGS HAS A TEST.** *"The suite proves the code does what it does; it does not prove the code does what the DESIGN says."* **Every fix needs its gauge written from the design statement, not from current behaviour — otherwise the tests lock in the bugs.** |
| **P15-2** | 🟠 | **The 250-event battle-report cap trims from the FRONT**, discarding exactly the opening of a fight — and the cap is **global, not per-battle**, so one battle's history evicts another's. |
| **P4-3** | 🟡 | **Determinism relies on dictionary iteration order** — five iterations with damage applied in-loop. Stable in practice, unguaranteed in principle. |
| **P16-1** | 🔵 | **49 `FLAGGED` balance constants** — the flagging discipline working as intended. **Sizing note:** most fixes here will invalidate a slice of them. *(⚠ see §10 C-6 — "flagged" ≠ "in the right layer".)* |
| **P4-1/2, P9-1, P12-1..4, P17-1** | ✅ | **Verified-good, do not re-check:** no RNG in any resolver · the combat halt cannot deadlock · **fog INSIDE a battle is built and good** (*"FIRST-STRIKE: {A} detects {B}, which is BLIND — it takes fire it can't return"*) · all four save/load checks clean · research reaches combat correctly *(⚠ with the caveat at §10 C-5)*. |

---

## 10. CROSS-AUDIT RECONCILIATION — where the two audits disagreed

Two audits, days apart, deliberately different lenses: the resolver audit read **code and design docs**; the designer
audit read **base-mod data and the code consuming it**. The disagreements were worth more than the agreements.

| # | Sev | Reconciliation |
|---|---|---|
| **C-1** | 🔴 | **THE RESOLVER AUDIT WAS RIGHT AND THE DESIGNER AUDIT WRONG — `RetreatCasualtyThreshold` IS INERT.** `EffectiveRetreatCasualtyThreshold` has **zero callers, not even a test**, and the ten references counted in `CombatEngagement.cs` are all to a same-named **`const 0.5`** and its personality modulation. **A name collision was counted as a wire.** ⇒ the doctrine ledger is **9 of 14, five inert**, matching resolver P2-1 exactly. |
| **C-2** | 🔴 | **⭐ NEW — NEITHER AUDIT COULD FIND THIS ALONE. THE PARTS DESIGNER CANNOT EXPRESS PENETRATION OR ALPHA AT ALL, AND THE PLAN IS TO DELETE THE ONLY PATH THAT CAN.** X5 said the assembler never writes them; the designer ledger said both arrive. **Both true, of DIFFERENT PATHS.** `GroundWeaponAtb` has exactly five fields — Mass, Attack, Range, Range_m, Mode — **none of them penetration or per-shot energy**; only `infantry-unit`/`armor-unit`/`artillery-unit` carry them via `GroundUnitAtb`. ⇒ **a unit you design from parts is strictly weaker IN KIND than a prebuilt one.** ⚠ **And those three prebuilts are marked for removal (L10). Retiring them first deletes armour penetration from the ground game.** |
| **C-3** | 🟠 | **BOTH AUDITS' FEED RANKINGS WERE WRONG.** X5/P7-1 ranked *ground 2/10 · railgun "all designed" · missile 0/5*. Actual: **ground 6/10** (prebuilt) or **4/10** (parts-built) · **railgun 4/10** · **missile 0/10**. **The mis-ranking is load-bearing** — it is what put *"the ground feed is the lossy one"* into the plan, when a prebuilt ground unit carries more designed fidelity than any ship weapon except the beam. |
| **C-4** | 🟡 | **P5-3 IS REAL IN CODE BUT UNREACHABLE WITH BASE-MOD DATA.** It needs a beam with `Range_m == 0`; **both beam templates carry `"MinFormula": "1000"`**, so neither can be authored at the sentinel. A latent trap, not a live misbehaviour — data knowledge the code-only lens could not supply. |
| **C-5** | 🔵 | **A ✅ IN ONE AUDIT NEARLY SUPPRESSED A 🟠 IN THE OTHER.** P17-1 records *"a built ship keeping its build-time numbers is **correct behaviour**"* — true for **research** (a new gun does not retrofit flying ships). D5-3 records the same frozen value as a defect — true for **damage and refit**. **Write it down as: freeze on RESEARCH · refresh on DAMAGE and REFIT.** |
| **C-6** | 🔵 | **"PROPERLY FLAGGED" IS NOT "IN THE RIGHT PLACE."** P16-1 counts 49 `FLAGGED` constants and calls the discipline healthy — correct. D4-1 calls the weapon-range constants a blocker — also correct, and `RailgunRange_m` is literally among the 49. **`FLAGGED` says a number is provisional; it does not say the number should not be a constant at all.** |
| **C-7** | 🔵 | **THE COVERAGE MAP.** The resolver lens **never read the base-mod JSON**, so it could not have found the six dead namespace strings or the four dead doors. The designer lens **never ran a battle forward**, so it would never have found the disengage refill, the dictionary-order determinism risk, or the battle-report fog leak. **Neither swept the engine↔client seam — the one surface that yielded a 🔴 on first contact.** |
| **C-8** | ✅ | **THE STRONGEST EVIDENCE EITHER AUDIT CONTAINS: two independent methods converged on one sentence.** Reading code: *"the designer models COMPONENTS; the resolver models TOTALS."* Reading data: the same sentence with the data half filled in. **Neither borrowed the other's framing.** |

---

## 11. CORRECTIONS MADE DURING THIS CAMPAIGN

Recorded because a wrong claim in a doc is more dangerous than no claim, and because the pattern is instructive: **five
of the six were caused by trusting a name, a count, or a prior doc instead of the source.**

1. **`RetreatCasualtyThreshold` wrongly cleared as wired** (§10 C-1). Cause: counted references to a same-named
   **constant** as evidence the doctrine field was read. **Fixed:** D7-1/D7-5 corrected; the ledger is 9 of 14.
2. **D1-2's mechanism wrong** — claimed three ground-stance ids collide in the loader and *"load order decides a
   formation's behaviour."* They declare **different `Type`s** and land in **separate dictionaries**. **Fixed:**
   downgraded 🔴→🟠; the slice still stands, its justification changed.
3. **"The formula layer is SOUND"** — true of the authored **DATA** (675/675, 58/58, 119/120), **false of the ENGINE**
   (§7 R-7: five latent evaluator bugs). **Measured the wrong half.**
4. **`Fighter` called a dangling mount flag** — it is carried by **15 templates**. Only `PDC` is at zero. Cause: a bad
   count. **Fixed in Pass 2** along with a proper recount of all mount flags.
5. **🔴 A DOC LANDMINE RETRACTED.** `DOCS-AUDIT-2026-07-27.md` carried a self-correction claiming that adding
   `upkeep = 0` to `GroundUnitAtb` is safe *"because the constructor already uses optional trailing parameters."*
   **False — and it would crash New Game.** `Activator.CreateInstance` is called **without `OptionalParamBinding`**, so
   a ctor with more parameters than supplied args is culled before default-value logic. One ctor, 7 params; three
   templates supply exactly 7; an 8th parameter ⇒ no match ⇒ New Game dies on the ground stack. **The original claim was
   right and its self-correction was wrong. Retracted in place** with the correct pattern (a new overload, as
   `GroundArmorAtb` does).
6. **Two proposed slices withdrawn** — adding dials to `GroundWeaponAtb` to protect the prebuilt units. **Both ends are
   condemned by L10 + L11.**

**Also corrected in other docs during the campaign:** `FLEET-COMBAT-CLOSING-DESIGN.md` §ROOT A (said railgun/flak/missile
were rangeless a month after the code and its test moved on) · `Pulsar4X.Client/CLAUDE.md` is wrong twice (the
`Window.Begin/End` safety claim, and listing `OrdnanceDesignWindow` as *"Functional"* when it is unreachable) ·
`DESIGNER-AUDIT/04-BASEMOD-TEMPLATES.md` is stale (claims 89 payloads/88 ids and **omits the `Station` flag entirely**;
actual 96/95 with Station on 7 templates) — **do not size the retrofit off that doc.**

---

## 12. THE SAVE-COMPATIBILITY RISK REGISTER — consult before every slice

| Change | Safe? | Why / mitigation |
|---|---|---|
| Add a public field to `ComponentDesign` or a **plain** `*Atb` | ✅ | Opt-out serialization picks it up. **Give it a sane initializer — that initializer IS the migration.** |
| Add a field to a **`BaseDataBlob`-derived** `*Atb` | ⚠ | `BaseDataBlob` is `MemberSerialization.OptIn` — **needs `[JsonProperty]`** or it is silently not saved. |
| **Add a field but not update `Clone()`** | ⚠ **#1 retrofit trap** | 19 of 20 DataBlob `*Atb` `Clone()`s enumerate ctor args (`=> new XAtb(a,b,c)`) — a new field is silently dropped. Prefer the copy-ctor form (`GroundLocomotionAtb.cs:45`). |
| **Add a ctor OVERLOAD** | ⛔ | **R-1.** Add `[JsonConstructor] private XAtb() {}` in the same change. |
| **Add a ctor PARAMETER** | ⛔ | **Exact-arity binding** — every base-mod template binding that atb must change in lockstep. `BaseModIntegrityTests` is the sensor. |
| **Rename / move an `*Atb` class** (namespace counts) | ⛔ | Breaks **twice**: the `$type` on the value **and** the assembly-qualified `Type` **key**. Needs a converter. |
| Add a template Property with a new `GuiHint` | ⚠ | If the hint is not in `CreateDesign`'s switch it falls to `default:` and records `ValueString`. Works by accident — check both switches. |
| Change an NCalc formula | ✅ | **Existing saved designs are frozen.** Note a *reopened* design DOES recompute → a differently-costed twin. |
| Remove a field | ✅ | Old key ignored on load. |

✅ **Landmine L12 does not currently bite** — all 20 `BaseDataBlob`-derived `*Atb` classes have a `Clone()`.

**Three pre-existing save landmines found:**
1. **`OrdnanceDesign` cannot survive `Game.Load`** — implements `ISerializable`, writes only 3 doubles, and has **no
   deserialization constructor**. CI misses it (the harness loads no `ordnanceDesigns`); **a menu-started game does.**
2. **`NavalAcademyAtb`** — three public ctors, no parameterless (R-2b). Masked only by the namespace bug.
3. **`OrdnanceShapedPayload`'s stats are private fields** → they round-trip as **zero**.

⚠ **`ComponentInstancesDB` has a partial copy-ctor** omitting `AllDesigns`, `DesignsAndComponentCount` and
`ComponentsByAttribute` — a clone would have empty attribute lookups. Latent (no gameplay path clones it today); fix
before anything starts cloning entities between managers.

---

## 13. VERIFIED-GOOD — do not rebuild these

- ✅ **The ground shield chain** (D6-4) — the only end-to-end example of a dial that is designed, assembled, delivered,
  resolved **and gauged on the decision it creates.** **Hold every new chain against it.**
- ✅ **The designer → assembler hop** — 41 of 43 ground dials have a reader.
- ✅ **The authored data is consistent** — 675/675 `PropertyValue`, 58/58 `TechData`, 119/120 arities, all 8 required
  `Formulas` keys in all 96 templates, no duplicate property names, all enum properties supply Min/Max/Step.
- ✅ **Research → designer ceiling works, live, with no caching**, CI-proven twice. One JSON line is the mechanism.
- ✅ **The ship assembler** — 11/15 surfaces + 100% of attributes, a real engine budget gate, a physical damage layout.
- ✅ **Ten of fourteen doctrine dials land**, on both domains, cooldown enforced at all three switch sites.
- ✅ **The ship armour NATURE matchup is real and authored.**
- ✅ **The 11-category taxonomy** — correct, cheap, navigable. The fault is the collapse behind it.
- ✅ **No RNG in any resolver · the combat halt cannot deadlock · fog inside a battle is built and good · all four
  save/load determinism checks clean.**
- ✅ **`laser-weapon`** — the one weapon whose research gate raises its design ceiling. **The shape to copy.**
- ✅ **Deliberate and correct, not bugs:** the description raw-text fallback (an apostrophe once corrupted the ImGui
  stack) · `TechData` degrading an unknown tech to 0 (it stopped a designer crash) · the backward-compat ctor overloads.

---

## 14. THE PLAN

**Phase A — make it safe to touch. Nothing else proceeds until these land.**

| # | Slice | Why | Gauge |
|---|---|---|---|
| **A1** ⭐ | **The designer smoke test over all 96 templates + the six namespace strings + the `AttributesByType` clear + `NavalAcademyAtb`'s `[JsonConstructor]` + the shaped-charge 6th arg** | D2-6 · D2-1 · **R-2 forces the extra three** | Is itself the gauge. **Must ship green** — that is why the fixes ride along. |
| **A2** | **`ResearchProcessor.cs:133`** — key by `tech.Design.UniqueID` | R-4 — **unblocks all research work** | a design → research → queue round trip |
| **A3** | **`Window.End()` outside the `if`, 16 windows** | R-3 — a client-wide brick, the highest-value non-designer fix found | local runtime: collapse the designer, then open any window |
| **A4** | **Make the loader's silent drops LOUD** — duplicate ids and unresolved cost ids into `SkippedEntries`; wire `ChainedExpression.HasErrors()` (**already written, zero callers**) into a validate-all-at-load pass | R-5 · R-7 | `BaseModIntegrityTests` fails on a collision |

**Phase B — cheap correctness wins, each independent, each with a gauge**
B1 the five evaluator bugs (R-7) · B2 `_isDependant` (R-8) · B3 the spaceport collision + three undefined materials +
`solarArray`'s numeric mount flag · B4 hoist the paired-range gap, step granularity and fuel filter into the engine
(R-6) · B5 decide `Amphibious` / `Size` / `SpeedMult` — **wire or remove, but stop charging for `Amphibious`** ·
B6 re-point `UnifiedDoctrineTests`' four assertions at behaviour (D7-2) · B7 the battle-report faction filter
(P15-1 — the field already exists).

**Phase C — the locked keystone**
**C1 — port the ground carry/mass gate to ships and stations (L25).** *"Do that, and 'the numbers force the build' —
the founding promise of the entire designer — becomes real across ALL scales."*
**C2 — give the station and building assemblers somewhere to PUT their computed mass/crew/validity, and give a finished
building a post-build identity** (it currently dissolves into loose colony components).
**C3 — recompute the combat value** (D5-3/4/5) — one change, three consumers, one gauge. Carry C-5's sentence:
**freeze on research · refresh on damage and refit.**

**Phase D — the parametric collapse (the actual retrofit) — needs a design doc FIRST**
**D1 — one universal weapon design both resolvers read**, ground parallels deleted (L11), prebuilts retired (L10).
**Explicitly locked as "needs a design doc + phased plan BEFORE code, sequenced with the developer."** ⚠ **Sequencing:
`GroundWeaponAtb` must gain penetration/alpha (or the universal door must) BEFORE the prebuilts retire (C-2).**
**Start with `ground-rifle`/`ground-cannon`/`ground-autocannon` — they already have identical dial sets**, the cheapest
possible proof of the pattern.
**D2 — ground research gating** *(blocked on A2)*. Zero C# needed: add `tech-ground-small-arms` with
`DataFormula "5000 * Pow(2,[Level])"` (**Level 0 MUST equal 5000** or the start is not byte-identical and L7's silent
clamp zeroes the heavy rifle's `Attack: 200`), add its id to `tech-modern-technology`'s `Unlocks["1"]` (**one line covers
all four start lists**), swap the `MaxFormula`, add a test mirroring `WeaponScaleGateTests`.
**D3 — weapon range as a design dial** *(blocked on R-1's `[JsonConstructor]` mitigation)*.
**D4 — the structural resolver merge** *(blocked on **Q-A** + **Q-B**)*: a real `ResolveSalvo`, both sides presenting
`Combatant`, one armour model. **Carry D6-2 as a hard note so the merge does not delete the working ground shield-regen
dial.**
**D5 — missiles** *(blocked on A1 + D4)* — opening the warhead designer while the launcher contributes five stubs ships
a designer whose dials demonstrably do nothing.

**Then: the same treatment for the auto-resolver**, and **the engine↔client seam** (C-7) — the one surface neither audit
swept, which yielded a 🔴 on first contact.

---

## 15. PRIME-DIRECTIVE CONNECTIONS

- **Designer** → every buildable: ship, ground unit, station, building, missile. Reads templates + techs; writes
  designs consumed by industry, the assemblers, and both resolvers.
- **Resolver** → reads `ShipCombatValueDB` / `GroundUnit` snapshots (both **frozen at build**), `FleetDoctrineDB`,
  `FleetCombatStateDB` (**destroyed on disengage**), sensors (the detection gate), commanders, personality.
- **Research** → widens design ceilings via `TechData` in a `MaxFormula`; gates template and design availability
  through `Unlock`. **Ground reads nothing from it.**
- **Industry** → `IndustryDesigns` keyed by design id; the research completion path keys it wrongly (**R-4**).
- **AI** → `FactionRollup.MilitaryStrength` (frozen), `ThreatAssessment`, `NPCDecisionProcessor`,
  `AIDecisionRecorder` — and **must be able to drive every designer/doctrine primitive the player can (L32)**.
- **Save/load** → `TypeNameHandling.Objects` + `PreserveReferencesHandling.Objects`; §12 is the register.
- **Client** → the Component Designer, the Entity Assembler, the doors map (a **4th registration point with no test**),
  and the `Window.Begin/End` contract (**R-3**).

---

## 16. THE DELETION MAP — where everything went

| Deleted file | What it held | Now in |
|---|---|---|
| `docs/economy/DESIGNER-AUDIT-2026-07-28.md` | 7 passes, 40 findings, the consolidation, the cross-audit reconciliation | **§8** (all 40 findings, verbatim severity) · **§10** (C-1…C-8) · **§2, §3, §14** (gauges, decisions, plan) |
| `docs/combat/RESOLVER-AUDIT-2026-07-28.md` | 17 passes, 49 findings, 5 root causes, the rate check, the correction table | **§9** (root causes + the findings that change what we do + the rate check) · **§10** (its corrections) |
| `docs/economy/DESIGNER-RETROFIT-SPEC-2026-07-28.md` | the 7-agent survey: as-built, intent, gap, save register, phased plan | **§5** (as-built) · **§6** (intent) · **§7** (gap, R-1…R-8) · **§12** (save register) · **§14** (plan) |
| `docs/OPERATION-GROUND-TRUTH-COMPLIANCE.md` *(deleted earlier)* | the campaign's process record | `docs/DOCS-AUDIT-2026-07-27.md` §15 |

**Nothing was dropped.** Individual pass-by-pass narration and per-agent transcripts were compressed into the findings
tables — every finding id (D1-1…D7-5, P1-1…P17-1, C-1…C-8, R-1…R-8) is preserved with its severity and its file:line
evidence.
