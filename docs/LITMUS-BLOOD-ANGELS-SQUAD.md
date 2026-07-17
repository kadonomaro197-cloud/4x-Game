# Litmus Test — Can the game field a 6-man Blood Angels squad? (cradle-to-grave gap analysis)

*An exhaustive, honest audit against the current Pulsar4X codebase. Every CAN is backed by a file:line from the code survey; anything not backed is marked PARTIAL, CANNOT, or UNVERIFIED. This document is the single source of truth — it folds in the adversarial critique: overstated CANs are downgraded, missed gaps are added, and the ranking is recalibrated for the Blood Angels' *signature* unit (Assault / Death Company / Sanguinary Guard), not the bolter-centric Tactical squad.*

---

## 1. The 6-man Blood Angels squad as concrete capabilities

Distilled from the physiology, wargear, identity, and logistics research, here is the checklist of what the game would need to model. This is the yardstick sections 2 and 3 measure against.

> **Framing note (recalibrated):** The iconic Blood Angels 6-man is **not** the Tactical squad (1 sergeant + 4 bolters + 1 heavy). It is the **Assault squad / Death Company / Sanguinary Guard** — all three are **jump-pack + chainsword/pistol** units. That means the two hardest gaps for *this chapter specifically* — jump-pack flight and a dual ranged+melee loadout on one model — sit squarely on the signature unit, not on an edge case. The checklist below is ordered with that in mind.

**A. Physiology & training cost (why a marine is nearly unbuildable)**
1. **Recruit-pool draw** — building a marine draws down a finite human population tied to specific worlds.
2. **Multi-year staged conversion** — a marine is a ~decade-long in-progress project (aspirant trials → 19 implant phases → Scout → Battle-Brother), not a queue-pop.
3. **Gene-seed as a finite, slow-regrowing stock** — each new marine consumes one unit of scarce gene-seed; stock only refills from surviving marines over 5–10 years.
4. **Attrition / washout roll per stage** — most candidates die or wash out; yield ratio is a dial.

**B. Wargear as designable components (cradle-to-grave kit)**
5. **Bolter** — a ranged, *mass-reactive kinetic* weapon (penetrate-then-detonate — kinetic delivery + explosive nature).
6. **Power armour** — sealed, life-supporting, strength-boosting powered plate (lets a marine lug heavy gear and survive vacuum/toxic atmosphere).
7. **Chainsword** — a powered melee weapon.
8. **Dual ranged+melee loadout on ONE model** — bolt pistol + chainsword (Assault) or Angelus boltgun + Glaive Encarmine (Sanguinary Guard, wrist-mounted gun so the hands are free for a two-handed melee weapon). The unit must *shoot on the approach, then hit in melee* — two resolvable profiles, not one blended stat.
9. **Jump pack** — flight/leap mobility (cross terrain, drop into melee, min-safe-distance deep strike). Signature Blood Angels mobility.
10. **Grenades / thrown consumables** — frag + krak, spent per throw, area effect.
11. **Relic tier / master-crafted** wargear (Glaive Encarmine "never breaks," Angelus boltgun) as a quality band above the standard part.

**C. Elite quality & psychology (what makes a marine a marine, not a soldier)**
12. **Elite quality tier** — a per-unit veterancy/caliber multiplier distinct from line troops.
13. **Red Thirst** — a temporary, triggerable combat-aggression state (bonus when charging/charged).
14. **Black Rage** — a permanent, irreversible psychosis flag that removes a model from normal service.
15. **Fearless / uncontrollable behaviour** for Black-Rage models + a **Chaplain handler** override + a **Sanguinary Priest suppression** capability.
16. **Fear / terror aura** — Sanguinary Guard Death Masks impose a morale penalty on nearby enemies (an aura-on-others, distinct from self-morale).

**D. Squad-of-6 granularity**
17. **A discrete model count** — the unit is "6 marines," and casualties drop it to 5, 4, 3… with attack/effectiveness scaling by surviving models.
18. **Per-model role mix** — 1 sergeant + 4 line + 1 special/heavy, each individually losable.

**E. Upkeep & scarcity (the developer's explicit ask)**
19. **Hard population cap** — a marine is one item off a hand-counted list; a death is a permanent decrement, not a re-queue.
20. **Standing upkeep as it exists** — food/rations, credits/materials drawn continuously whether or not it fights.
21. **Armour maintenance / condition state** — power armour has a *condition* that decays with use, needs a *capped specialist pool* (Techmarines) to restore, and neglect carries a combat penalty. A distinct cost stream from generic upkeep.
22. **Ammunition as a consumable stock** — bolt rounds spent by firing, needing a resupply chain, can run dry.
23. **Grave rung with consequence** — losing a marine costs something (gene-seed recovery roll, population write-back, casualty event), not a silent delete.

---

## 2. Cradle-to-grave, button-by-button, in the CURRENT game

The chain a player would actually walk, each step tagged **CAN / PARTIAL / CANNOT / UNVERIFIED**.

### (a) Research the tech that unlocks the kit — **PARTIAL**
The unlock *mechanism* is fully built and proven: `Tech.Unlocks` (`TechBlueprint.cs:13`, `Tech.cs:28`) moves ids from `LockedCargoGoods → CargoGoods` on `IncrementTechLevel`, and `ResearchProcessor.DoResearch` syncs newly-unlocked designs into `IndustryDesigns` (`ResearchProcessor.cs:133,138–144`) — exactly how ship armour is gated today. **But no ground unit is actually tech-gated:** the base-mod ground templates have `ResearchCost:0` and their ids sit in `earth.json` `StartingItems` (start-unlocked), and grep finds **no tech referencing any ground-unit/scout/magazine/power-armor id** and no "ground-combat" tech category. So "research power-armour tech" is *authorable in JSON today* (put the id in a tech's `Unlocks`, out of `StartingItems`) but is **not a live capability** — unwritten data, not a code gap.

### (b) Mine minerals — **CAN**
`MineResourcesProcessor` (daily) reads `MineralsDB`, scales by accessibility + infrastructure efficiency, moves units into the colony `CargoStorageDB` (`MineResourcesProcessor.cs:64,71`). Deposits deplete permanently. Fully cradle-to-grave and gauged.

### (c) Refine materials — **CAN**
`ProcessedMaterial : IConstructableDesign` queues on an industry line and refines raw minerals into cargo via `ConstructStuff → OnConstructionComplete` (`IndustryTools.cs:230`). iron (raw) → stainless-steel/aluminium (refined).

### (d) Design each component in the Component Designer
The designer plumbing all works — `ComponentDesigner.CreateDesign` (`ComponentDesigner.cs:123`) builds any template from NCalc dial formulas; every ground `*Atb` is a real bound attribute. Per component:

- **Bolter — PARTIAL.** `GroundWeaponAtb` (`GroundWeaponAtb.cs:30`) with dials Mass/CarryMass, Attack, Range, Mode; base-mod `ground-rifle` under Weapons ▸ Ballistic (`ComponentDoors.cs:50`). You *can* author a ranged kinetic gun. You *cannot* author its "mass-reactive" character: `Mode` hard-couples Ballistic→Kinetic (`GroundCombatant.cs:56-60`), there is **no dial to make a kinetic round detonate as explosive**, and **no per-part Penetration or PerShotEnergy dial** (those live only on prebuilt whole-unit `GroundUnitAtb`).
- **Power armour — PARTIAL.** The "power armour" item is the `power-armor` **augment** (`installations.json:2398`, `GroundAugmentAtb`, StrengthBonus +300) — a good franchise fit for "lets a frame lug heavy gear," authorable under Enhancers ▸ Bio-augmentation (`ComponentDoors.cs:104-105`). Per-nature armour soak (VsKinetic/VsEnergy/VsExplosive/VsExotic) exists on `GroundArmorAtb.cs:34-40`. **But sealed environment / vacuum / life-support is NOT authorable** — it lives in a design-level `EnvResistance` dict folded in at raise time (`GroundForcesDB.cs:433`, reading `design.EnvironmentalResistance`), with no component template dial. **And armour has no *condition/durability/maintenance* state at all** (see GAP 7).
- **Chainsword — CAN (in isolation only — see (e) and GAP 5).** `GroundWeaponMode.Melee` (`GroundWeaponAtb.cs:12`, range 0); base-mod `claw-weapon` under Weapons ▸ Melee (`ComponentDoors.cs:57`), maps to undodgeable Tracking-1 (`GroundCombatant.cs:56,74`). A chainsword = `claw-weapon` with a higher dialed Attack. **Caveat: you can author one melee weapon, but you cannot make a unit resolve it *alongside* a ranged weapon as two separate profiles** — the assembler blends all weapons to a single Attack + single DamageType (GAP 5). The chainsword *part* is buildable; the *chainsword-marine loadout* is not.
- **Jump pack — CANNOT.** `GroundLocomotionAtb` models only SpeedFactor, RoughHandling, Amphibious (`:27-32`). There is **no flight/jump/leap/vertical/ignore-terrain dimension**; the chassis enum is Foot/Tracked/Walker/Hover (`GroundChassisAtb.cs:11-17`). A fast all-terrain drive is buildable; a jump pack is unrepresentable.
- **General augment (bionics) — CAN.** `GroundAugmentAtb` (`:26`) dials Mass, StrengthBonus, EvasionBonus, ToughnessBonus, Shield, ShieldRegenFraction — fully player-authorable.
- **Plasma/melta (energy) — CAN, melta-penetration PARTIAL.** `energy-weapon` Mode=Energy under Weapons ▸ Energy (`ComponentDoors.cs:48`); no per-part armour-penetration dial (same gap as bolter).
- **Grenades / thrown consumables — CANNOT.** `GroundWeaponAtb` has no thrown/area/consumable-per-throw concept. Frag/krak grenades are unrepresentable (minor, but a named part).

### (e) Assemble the squad in the Entity Assembler — **PARTIAL (assembles a UNIT, not a 6-model squad; blends all weapons to one profile)**
The flow fully EXISTS: pick "Ground Unit" in `ShipDesignWindow` (`_assemblyKindNames`, line 46; `GroundKindIndex=1`), mount a `GroundChassisAtb` frame (locks the kind), add parts, watch emergent stats — `GroundUnitAssembly.Compute` (`GroundUnitAssembly.cs:71`) sums Attack/Defense/HP/Range/Evasion/Shield against a carry budget with per-item and power/ammo gates, rendered as a live table with red "OVER" (`ShipDesignWindow.cs:417,452-467`). Save → `RegisterAssembledDesign` (`GroundUnitAssembly.cs:254`) registers a buildable `GroundUnitDesign` on the industry rails.

**Two things this hides:**
- **What you assemble is ONE abstract combatant with a single continuous HP pool.** `GroundUnit` (`GroundForcesDB.cs:28-160`) has `MaxHealth`/`Health` as `double`s and **no ModelCount/squad-size/strength-in-men field anywhere.** "Count" in the assembler means parts-per-unit (e.g. 3 armour plates), not models-per-squad (`GroundUnitAssembly.cs:119`). You can build a "Space Marine unit," but not a 6-model squad that loses one marine at a time (GAP 1).
- **The assembler flattens every mounted weapon into ONE summed `Attack`, ONE `DamageType` (heaviest weapon's nature wins), and the longest `Range`.** A marine with **bolt pistol + chainsword** — the defining assault loadout — is not two resolvable profiles (a ranged attack and a separate melee attack); it collapses to one blended number with a single damage nature. The dual ranged+melee loadout **CANNOT** be expressed (GAP 5).

### (f) Build it through industry — **PARTIAL**
Materials + industry-points: **CAN.** Both build routes carry real `ResourceCosts` consumed by `IndustryTools.ConsumeResources` (`IndustryTools.cs:189,237`; `GroundUnitDesign.cs:34,114`); base-mod `infantry-unit` spends iron/steel/aluminium (`installations.json`). Fully gauged (`GroundUnitBaseModTests`).
Population / manpower / training draw: **CANNOT.** The crew gate is ship-only — `if (designInfo is ShipDesign shipToCrew && shipToCrew.CrewReq > 0)` (`IndustryTools.cs:152`); a ground unit is a `ComponentDesign`/`GroundUnitDesign`, never a `ShipDesign`, so it draws **zero** population/workforce/talent. `GroundUnitDesign` has no `CrewReq` field at all. The template's `CrewReq:100` is a red herring — it feeds `InfrastructureDB` demand, not the population pool. No recruitment, no years-of-training, no washout. Money is also never deducted (`CreditCost` is display-only, `ShipDesign.cs:198`).

### (g) Field it on a planet — **CAN (one-shot snapshot, no evolving state)**
On build completion, `GroundUnitDesign.OnConstructionComplete` (`:111`) → `GroundForces.RaiseUnit` (`GroundForcesDB.cs:394-447`) places a `GroundUnit` on the colony's planet, snapshotting Attack/Defense/Health/Ammo/Range and folding `EnvResistance` from `design.EnvironmentalResistance` at raise (`:433`). CAN stands. **Flag:** the snapshot is frozen — nothing on the fielded unit can change post-raise except health/ammo/position. There is no condition, morale, or veterancy state to evolve.

### (h) Pay its upkeep / consume resources as it exists — **CANNOT**
**A standing ground unit is 100% free after it is built.** `GroundForcesProcessor.ProcessBody` (the one hourly surface tick, `GroundForcesProcessor.cs:71-246`) has **no billing, no food/credit/ammo draw step.** `GroundUnit` has no `CostPerDay`/`Upkeep`/provisions field. Ammo is inert plumbing: `GroundAmmo.Consume` (`GroundAmmo.cs:31`) exists but its **only callers are tests** (6 hits, all in `GroundAmmoTests.cs`) — `ResolveRegionCombat` never drains it. Armour has no maintenance state to bill against either. A garrison sitting idle, or fighting, costs the empire nothing. This is the developer's explicit ask and the game **cannot** currently reflect it.

### (i) Move it across the planet — **engine CAN (CI-tested) / client UNVERIFIED — and it is a formation of 6 INDEPENDENT units, not a squad**
The engine wiring is real and CI-tested: select units → `CreateFormation` + `AssignUnit` per unit (`PlanetViewWindow.cs:991,993`) — six units into one formation, first assigned becomes leader. March via `OrderFormationMove` (`:1163`) or `OrderFormationMoveToGlobalHex`, marching at the slowest member's pace so they arrive together (`GroundForcesDB.cs:648,672`). Queue waypoint plans ("move → move → hold → dig in") via `QueueFormationOrder` (`PlanetViewWindow.cs:1074-1086`), run one-at-a-time by `ProcessFormationOrders` (`GroundForcesProcessor.cs:485-529`), with Clear-plan and Disband buttons.

**Two honesty caveats the flat "CAN" would hide:**
- **There is no squad object.** A `GroundFormation` groups six *separate* `GroundUnit` objects — the same monolith GAP 1 describes. "Move the squad" really means "march six independent tokens together." It is not the 6-model squad the litmus test asks for.
- **The client half is runtime-UNVERIFIED.** Per L11, CI compiles the client but cannot run it. The engine calls earn CAN; the button-by-button *player experience* (does the formation UI actually work on screen) is unproven until the developer's local Windows build exercises it.

### (j) Lose it — the grave rung — **PARTIAL**
A unit is silently deleted with zero accounting: `forces.Units.RemoveAll(u => u.Health <= 0)` (`GroundForcesProcessor.cs:237`) — no event published, no transaction charge, no population/gene-seed returned or lost, no casualty ledger. Leader loss = free reassign (`MaintainFormations`, `:564-579`). The rebuild end exists AI-side (`GroundReinforcement.NeedsReinforcement` re-queues below-target garrisons). But losing a unit costs the empire nothing observable — no gene-seed recovery roll, no manpower write-back, no morale hit, no log event.

---

## 3. Where we fall short — ranked GAP LEDGER

Most load-bearing first, recalibrated for the Blood Angels' signature (jump-pack, dual-loadout) unit. Effort S/M/L is rough.

### GAP 1 — No 6-model squad granularity *(developer's ask #ii)* — **CANNOT — effort L**
`GroundUnit` is one monolithic HP bar (`GroundForcesDB.cs:28-160`) — no `ModelCount`/`ModelsAlive`/`MaxModels`, so no "5 of 6 alive," no per-model attrition, no attack-scales-with-survivors (health is a single `double`). **Nearest hooks:** the `GroundFormation` primitive (`GroundForcesDB.cs:294`) gives model-by-model loss if you field 6 separate `GroundUnit`s — but that treats a squad as a fleet-of-one-man-ships (heavy: each marine its own UnitId, backing entity, combat line). A true squad needs a new `ModelCount` field on `GroundUnit` + casualty logic in `ResolveRegionCombat` that decrements models and scales Attack/carry + an assembler concept of "this design fields N models" (today `count` is parts-per-unit, `GroundUnitAssembly.cs:119`). This is the core missing primitive; no partial scaffold exists.

### GAP 2 — No standing upkeep, no consumption-as-it-exists *(developer's ask #i)* — **CANNOT — effort M**
Nothing bills or feeds a standing unit; ammo is never drained. **Nearest hooks:** copy `StationUpkeepProcessor.BillUpkeep` (`StationUpkeepProcessor.cs:22-63`) — an `IHotloopProcessor` that computes a cost and calls `factionInfo.Money.AddExpense(date, TransactionCategory.StationUpkeep, …)`. Because L9 forbids a second hotloop on `GroundForcesDB`, fold the billing *inside* `ProcessBody` gated to monthly via a last-billed date. Add a `CostPerDay`/`UpkeepCredits` field on `GroundUnitDesign` snapshotted onto `GroundUnit`, plus a `TransactionCategory.GroundForceUpkeep`. For consumption specifically: wire the existing `GroundAmmo.Consume` (`GroundAmmo.cs:31`) into `ResolveRegionCombat`, and add a garrison food/credit draw from the host colony stockpile each tick (no such draw exists).

### GAP 3 — No population/manpower/training draw + no scarcity cap — **CANNOT — effort M–L**
An army costs steel and factory-time but no *people*, no time-to-train, no washout, no cap — the opposite of the lore's load-bearing scarcity. **Nearest hooks:** extend the ship crew gate at `IndustryTools.cs:152` (and the `OnConstructionComplete` commit) so a `GroundUnitDesign` build also runs `ManpowerTools.ResolveBuild`/`CommitCrew` (`ManpowerTools.cs:31,42`) — but `GroundUnitDesign` needs a `CrewReq`/manpower field first (it has none). Gene-seed as a finite stock, staged multi-year conversion, and the ~1,000 hard cap are all from-scratch — no engine primitive tracks a "long-lived in-progress unit project" or a per-faction unit ceiling. This is what makes six marines *feel* irreplaceable rather than re-queueable.

### GAP 4 — Jump pack (flight/leap mobility) — **CANNOT — effort M**
`GroundLocomotionAtb` has only SpeedFactor/RoughHandling/Amphibious (`:27-32`); the chassis enum is Foot/Tracked/Walker/Hover. No vertical/air/ignore-terrain axis. **For Blood Angels this is signature doctrine, not an edge case** — Assault, Death Company, and Sanguinary Guard all ride jump packs. **Nearest hook:** add a flight/leap dial to `GroundLocomotionAtb` and a movement branch in `ProcessBody` steps 1a-1c (`GroundForcesProcessor.cs:89-135`) that lets it skip terrain cost. New mobility dimension, no existing scaffold.

### GAP 5 — No dual ranged+melee weapon profile per unit — **CANNOT — effort M** *(report missed this)*
`GroundUnitAssembly.Compute` flattens every mounted weapon into ONE summed `Attack`, ONE `DamageType` (heaviest wins), and the longest `Range` (`GroundUnit` carries a single `Attack` field). A marine with bolt pistol + chainsword — or Angelus boltgun + Glaive Encarmine — cannot "shoot on the approach, then hit in melee" as two profiles; it resolves as one blended number with a single damage nature. This is the whole point of a Blood Angels assault marine and it is unbuildable. **Nearest hook:** per-weapon `WeaponProfile` resolution in the combat kernel (`ResolveRegionCombat`) instead of a summed stat — the unit resolves each mounted weapon on its own delivery/nature/range.

### GAP 6 — No per-unit psychology (Red Thirst / Black Rage / fear aura) — **CANNOT — effort M**
Grep of `GroundCombat/` for morale|psychology|fury|rage|fatigue|suppress returns nothing relevant. A `GroundUnit` has no morale/fury/cohesion field; combat is deterministic strength-math — never routs, never rages, never breaks. `ColonyMoraleDB` is *population* morale, not per-unit. From-scratch: a field on `GroundUnit` + a modulation in `ResolveRegionCombat` + a per-tick recover/decay step. No hook for the temporary/triggerable Red Thirst, the irreversible Black-Rage flag, the Chaplain-handler override, the Sanguinary-Priest suppression, or the Death-Mask **fear aura** (a distinct aura-on-*others* morale penalty with no hook).

### GAP 7 — No armour maintenance / condition-decay state — **CANNOT — effort M** *(report undersold this)*
Distinct from GAP 2's generic upkeep bill. There is **no durability/condition/maintenance field** on `GroundUnit` or the armour snapshot; no decay with use, no neglect penalty, no capped specialist (Techmarine) throughput. This is the mechanic that ties marines to the Armoury/Techmarine scarcity the lore is built on. **Nearest hook:** a condition field on the armour snapshot + a decay step in `ProcessBody` + a maintenance-throughput gate drawing on a specialist-capacity pool.

### GAP 8 — No elite-quality / veterancy dial for ground — **PARTIAL — effort S–M**
The `UnitCaliberAtb` axis (FirepowerMult/ToughnessMult, health-scaled, baked at build — `UnitCaliberAtb.cs:31,35,39`) **exists only for ships.** There is no `GroundUnitCaliberAtb` and no veterancy/experience field on any ground type. `GroundFormation` stance (`AttackMult`/`DamageTakenMult`, `GroundForcesDB.cs:317-319`) is switchable doctrine, not baked caliber. **Nearest hook:** mirror `UnitCaliberAtb` as a mountable ground augment snapshotted onto `GroundUnit` at raise — a well-scaffolded, low-risk port.

### GAP 9 — Bolter's mass-reactive nature + weapon penetration/per-shot-energy on the part — **PARTIAL — effort S–M**
`GroundWeaponAtb.Mode` hardwires Ballistic→Kinetic (`GroundCombatant.cs:56-60`); no explosive-nature toggle for a kinetic round, no per-part Penetration/PerShotEnergy dial (those live on prebuilt `GroundUnitAtb` only). **Nearest hook:** decouple nature from delivery on `GroundWeaponAtb` + add the two dials; the unified space weapons that mount on `GroundUnit` (railgun/plasma/disruptor) are a partial workaround today.

### GAP 10 — Sealed environment / life-support not authorable — **PARTIAL — effort S–M**
Vacuum/hazard survival is a design-level `EnvResistance` dict folded in at raise (`GroundForcesDB.cs:433`, from `design.EnvironmentalResistance`), with no component template dial. **Nearest hook:** surface it as a dial on a new environment-hardening augment (GroundCombat CLAUDE.md flags this as the E4→v2 promotion).

### GAP 11 — Grave rung has no consequence — **PARTIAL — effort S**
`RemoveAll(Health<=0)` (`GroundForcesProcessor.cs:237`) deletes silently. **Nearest hook:** the ship path fires a `CrewLosses` event on destruction — mirror it with a casualty event + a manpower/gene-seed write-back to the colony (depends on GAP 3 existing first).

### GAP 12 — Grenades / thrown consumables — **CANNOT — effort S–M (minor)**
`GroundWeaponAtb` has no thrown/area/consumable-per-throw concept. Frag/krak grenades — standard squad kit — cannot be authored. **Nearest hook:** a thrown/area weapon mode on `GroundWeaponAtb` that draws a per-throw stock (rides on the ammo-consumption wiring from GAP 2).

### GAP 13 — No ground-combat research tree authored — **PARTIAL — effort S (data only)**
Substrate proven (ship armour), but no tech references any ground id. Pure JSON authoring (`techs.json` `Unlocks` + remove from `StartingItems`), no code change.

---

## 4. Bottom line

**Honest verdict: the game does NOT fake an "it works" — but it can carry a Space Marine squad only *partway* down the cradle-to-grave chain, and what it fields is a monolithic ground unit with a marine's paint job, not a Blood Angels squad.**

The *front half is genuinely strong* and code-confirmed: you can mine minerals, refine materials, author most of the kit in a real component designer (chainsword part fully; bolter, power-armour-augment, energy weapon, bionics partially), assemble it into a buildable ground unit in the Entity Assembler, build it while consuming real materials, field it on a planet, and march a six-unit formation across the map with waypoint orders (engine CI-tested; client runtime unverified).

But the chain breaks on exactly the things that make a *Blood Angels* squad a Blood Angels squad. The unit is one monolithic HP bar with **no 6-model granularity**; it draws **no population and no training time** to build; it pays **no upkeep and consumes nothing as it exists**; its armour has **no condition/maintenance state**; one model **cannot carry a ranged *and* a melee weapon as two profiles** (the whole point of an assault marine); it **cannot jump-pack**; and it dies as a **silent delete** with no gene-seed/manpower consequence. The two features the developer named explicitly — "costs upkeep + consumes resources as it exists" and "a 6-man squad" — are precisely the two the game **cannot** do today.

**The signature-unit reality:** the iconic Blood Angels 6-man is an Assault / Death Company / Sanguinary Guard squad — jump-pack + chainsword/pistol. So the two hardest gaps (jump-pack flight, dual ranged+melee loadout) sit on the *defining* unit, not an edge case. A bolter-centric Tactical framing would quietly undersell how central the CANNOTs are to this chapter.

### Definitive ranked top gaps blocking "make a Blood Angels squad"

1. **6-model squad granularity — CANNOT (L).** No `ModelCount`/`ModelsAlive`; health is one `double`. Add the field + per-model casualty/attack-scaling in `ResolveRegionCombat`. Core missing primitive, no scaffold. *(developer's ask #ii)*
2. **Standing upkeep + consumption-as-it-exists — CANNOT (M).** `ProcessBody` has no billing/feeding step; `GroundAmmo.Consume` has only test callers. Copy `StationUpkeepProcessor.BillUpkeep` into `ProcessBody`; add `CostPerDay` + `TransactionCategory.GroundForceUpkeep`; wire `Consume` into the resolver. *(developer's ask #i)*
3. **Population / gene-seed scarcity + training time — CANNOT (M–L).** Crew gate is `is ShipDesign` (`IndustryTools.cs:152`); ground draws zero people. Extend the gate + give `GroundUnitDesign` a `CrewReq`; gene-seed stock, staged conversion, ~1,000 cap are from-scratch. Makes six marines irreplaceable, not re-queueable.
4. **Jump-pack flight — CANNOT (M).** `GroundLocomotionAtb` has no vertical/leap/ignore-terrain axis. Signature Blood Angels doctrine — bumped up from the report's #4 framing. Add a flight dial + terrain-skip branch in `ProcessBody`.
5. **Dual ranged+melee weapon profile per unit — CANNOT (M).** *(Report missed this.)* Assembler blends all weapons to one Attack + one DamageType; pistol-and-chainsword can't resolve as two attacks. Add per-weapon `WeaponProfile` resolution in the combat kernel.
6. **Per-unit psychology (Red Thirst / Black Rage / fear aura) — CANNOT (M).** No morale/rage field on `GroundUnit`. From-scratch field + resolver modulation; no hook for Black-Rage flag, Chaplain override, Priest suppression, or Death-Mask aura.
7. **Armour maintenance / condition-decay — CANNOT (M).** *(Report undersold this.)* No durability/condition state, no neglect penalty, no Techmarine capacity cap. Distinct from #2. Add a condition field + decay step + maintenance-throughput gate.

Everything below the line — elite-quality dial (PARTIAL S–M), mass-reactive bolter nature + per-part penetration (PARTIAL S–M), sealed/life-support armour (PARTIAL S–M), consequential grave rung (PARTIAL S), grenades (CANNOT S–M, minor), research gating (PARTIAL S, data only) — has a real existing hook to build from. The substrate is genuinely strong and the missing pieces are specific and nameable, not a rebuild. But do not tell the developer he can "make a Blood Angels squad" today: he can make **one monolithic ground unit with a marine's paint job**, field it, and march it — and nothing more of what makes it Blood Angels.
