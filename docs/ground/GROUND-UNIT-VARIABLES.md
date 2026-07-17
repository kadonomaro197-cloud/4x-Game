# Ground-Unit Variables — answering the litmus follow-ups (make ANY unit, not just marines)

The developer asked six questions about whether the ground-combat engine can build *any* unit — a militia mob, a Guardsman, a jump-pack marine, a walking cathedral — instead of just one hard-coded "marine." The short version: the engine is closer than the first-pass litmus report claimed. Four of the six things it marked "can't do" or "half-done" turn out to be a cheap dial, an enhancer, or a small resolver change on parts that already exist. Only three are real builds. Everything below names the mechanic, the file and line where it hooks in, and how much work it actually is.

One rule runs through all of it: every variable lives on the unit's *design* (`GroundUnitDesign`) or on a component attribute (`*Atb`), gets copied onto the live unit when it's raised, and sits at a neutral value that changes nothing until you dial it up. That's what makes it general — a Guardsman and a Blood Angel are the same variables at different settings, with no `if (unit == Marine)` anywhere.

---

## Section 1 — The six questions, answered

### Q1. How does the auto-resolver treat a 6-man squad in combat?

**Today: as ONE combatant with one health bar.** There is no concept of "six models." A squad is either six separate `GroundUnit` objects, or one unit whose Attack and HP were dialed up to stand for six men — but it's one indivisible pool either way. On that narrow point the litmus report is right.

Where the fighting actually happens: `GroundForcesProcessor.ResolveRegionCombat` (`GroundForcesProcessor.cs:261-389`). It's pure arithmetic, no dice — deterministic on purpose, so a fast-forwarded battle comes out identical to one you watch:

- Units are grouped by faction (`:270-275`); each attacker fires at every hostile it can reach.
- **Range gate** (`:302-310`): if `HexDist > Range`, skip. A longer gun shoots without being shot back at.
- **Attack pool** (`:322-327`): `atk = Attack × terrain × locomotion × doctrine`, then `× SalvoScale`; the defender's pool is divided by `coverFort` (`:328`).
- One `WeaponProfile` is built per unit (`GroundCombatant.ToWeaponProfile`, `:340`), then run through the shared `CombatKernel`: dodge (`HitFraction`, `:347`), shield soak (`:351-356`), armour soak (`GroundDamageMatrix.ArmourSoak`, `:363-368`).
- **Simultaneous**: all incoming damage is computed from the pre-salvo state into `incoming[t]`, then applied as `Health -= Min(Health, incoming × DamageTakenMult)` (`:379-386`). A dead unit is removed whole at `:237`.

**How a model-count would plug in — the resolver's shape already fits it, with two one-line hooks.** So the litmus claim that a 6-model squad "can't be represented" is overstated. Add `ModelCount`, `ModelsAlive`, and `PerModelHP` to `GroundUnit` (in `GroundForcesDB.cs`, with `[JsonProperty]` so it survives save/load, added to the copy-ctor at `:185`, and `MaxHealth = ModelCount × PerModelHP` seeded when the unit is raised at `:410-411`). Then:

- **Firepower falls as men die** — at the pool-out site (`:322-327`), multiply `atk` by `(ModelsAlive / (double)ModelCount)`. This rides on top of the existing terrain/doctrine multipliers.
- **Decrement models** — at the pool-in site (`:379-386`), after `Health -=`, recompute `ModelsAlive = ceil(Health / PerModelHP)`.

Health stays the continuous pool; models ride on top as a firepower divisor and a readout. Whole-unit removal at `:237` still fires when the last model dies. **No resolver rewrite** — the pool-out and pool-in sites are already separate steps in the simultaneous-salvo design.

**Two cautions on this one (do not gloss them):**
- **The neutral default is `ModelCount = 1`, not 0.** Zero divides by zero in the multiplier. Neutral is `ModelCount = 1`, `PerModelHP = MaxHealth` — that keeps the multiplier at 1.0 while Health > 0, so a single tank or a Titan (one model, huge HP) is byte-identical to today.
- **Once `ModelCount > 1`, this is a real balance change, not just a display.** Firepower now decays with health. A half-dead squad hits at half strength. That's the correct and intended behavior, but call it what it is — it changes combat outcomes, it isn't a cosmetic counter. Effort still Small.

### Q2. Can you mount multiple weapons on one chassis?

**Mounting several weapons already works. The gap is the RESOLVER, not the assembler.** This is the key correction to the litmus report.

- **Mounting N weapons is legal today.** `GroundUnitAssembly.Compute` walks every part (`parts` is `IEnumerable<(design,count)>`, loop at `:107`); any part carrying a `GroundWeaponAtb` gets added. The only limit is the carry budget: each item must fit `itemMass ≤ MaxItemFraction × capacity` (`:161-162`) and the total must fit `used ≤ capacity` (`:182-183`), where capacity = the chassis `BaseStrength` + the sum of augment `StrengthBonus` (`:83,89-91`). A bolter *and* a chainsword on one frame is already allowed, exactly as the developer said — as long as the chassis carries the weight.
- **But the assembler FLATTENS them** (`:119-121`): `r.Attack += w.Attack × c` sums all of them, `r.Range = max(Range)` keeps only the longest, and `r.DamageType` becomes the single *heaviest* weapon's mode — the lighter weapon's nature and delivery are thrown away. The `GroundUnit` snapshot then holds only single scalars (`GroundForcesDB.cs:49,66,86,93,100`), and the resolver synthesizes exactly **one** `WeaponProfile` per unit (`:340`). So a bolter+chainsword unit fights as one blended gun.

The litmus was right about the *symptom* ("blends to one Attack + one DamageType") but wrong if it implied the assembler blocks multi-weapon mounting, or that ships can resolve per-weapon and ground fundamentally can't.

**The per-weapon machinery already exists and is wired** — this is downstream plumbing, not a new system:
- `GroundCombatant.ToWeaponProfile` (`GroundCombatant.cs:66-85`) already builds a full `Combat.WeaponProfile` (nature × delivery × velocity × tracking × saturation × range × penetration × energy).
- `CombatKernel.HitFraction` / `ShieldSoakFraction` / `BurstShotCount` / `ArmourSoakBurst` already resolve one profile at a time — the same path ships use, and the ground resolver already calls into it.
- `NatureDeliveryFor(GroundWeaponMode)` (`GroundCombatant.cs:54-61`) already maps each mode to a kernel nature/delivery.

**Cheapest general path** (the same shape `ShipCombatValueDB.Weapons` uses — hold a `List<WeaponProfile>`, resolve each):
1. In `GroundUnitAssembly.Compute`, besides summing Attack, also emit a `List<GroundWeaponSnapshot>` — one entry per mounted `GroundWeaponAtb × count` — and snapshot it onto `GroundUnit` (a new `[JsonProperty]` list, deep-copied in the copy-ctor). Keep the summed Attack/DamageType as a fallback so a monolithic or garrison unit stays byte-identical.
2. Add `GroundCombatant.ToWeaponProfiles(unit)` → `List<WeaponProfile>` next to the singular method — one profile per snapshot entry, or a one-element list from the flattened scalars when there's no list.
3. In `ResolveRegionCombat` (`:298-372`), loop over the unit's profiles instead of one: each has its own Range (so the bolter's range gate at `:307` differs from the chainsword's), its own nature (shield soak), its own penetration and shot count, all accumulating into `incoming[t]`.

This handles any N-weapon unit for free — no new balance system, just plumb the list the assembler already builds and loop the resolve it already runs once.

### Q3. Can a jump pack be an enhancer?

**Overstated — reclassify from CANNOT to SMALL ADD.** Most of a jump pack is authorable as data today; only true flight / ignore-terrain needs one new dial and a few guarded branches.

- **Already pure data today:** `GroundLocomotionAtb` (`GroundLocomotionAtb.cs:27-32`) dials `SpeedFactor`, `RoughHandling`, and `Amphibious`; `GroundAugmentAtb` (`GroundAugmentAtb.cs:30`) raises the carry budget and can grant evasion. "Faster + amphibious + handles all terrain + carries more" falls straight out of the store (`GroundMobility.cs:39-67,71-85`) with no engine work.
- **The one missing piece — true flight / leap / ignore-terrain-cost.** Terrain cost and the ocean wall are hardcoded in two central pure functions:
  - `HexPathfinder.cs:42` `IsImpassable` (Ocean), `:47-63` `HexMoveMult` (open ×1 / cover ×1.5 / rough ×2.5), used in `FindPath` (`:84,103-104`) and `FindGlobalPath` (`:141,164-165`).
  - `GroundMobility.TerrainMult` (`:91-98`) and `StepSecondsFor` (`:103-106`) — the single march-timing site.
- **Minimal fix:** add a `Flight` / `IgnoresTerrain` / `VerticalFactor` dial to `GroundLocomotionAtb` (it matches the existing parametric dials there; `GroundMobility` already reads locomotion off the store), then a guarded branch so a flyer (a) treats `HexMoveMult` as 1.0 and `IsImpassable` as false, and (b) skips the timing penalty. Because the pathfinder and the timing already funnel through those two pure functions, this is **a guarded branch on a new dial, not a new movement system** — Small, general, and any chassis can mount the component (the `CONVENTIONS §6` "abilities are components" path).

### Q4. Is there a defense component/dials that works AS power armour?

**Yes — and the litmus report conflated two different components here.** `GroundArmorAtb` is *already* a complete, authorable protective armour. Power armour = ceramite plate = a high-Defense, nature-tuned `GroundArmorAtb`, today. Nothing is missing for the protection itself.

- **The armour (protection) is complete and wired cradle-to-grave:** `GroundArmorAtb.cs:18-40` carries `Mass`, `HP`, `Defense` (flat soak per hit), and four nature-soak dials — `VsKinetic` / `VsEnergy` / `VsExplosive` / `VsExotic` — all authorable from JSON via the 7-arg constructor (`:55-64`; the 3-arg ctor at `:46-51` keeps plain plate byte-identical); `ResistFor(nature)` at `:68-75`. The flow: the assembler folds it in (`GroundUnitAssembly.cs:123-134` → `:168-174` → snapshot `:212-215`), the unit picks it up at raise (`GroundForcesDB.cs:410-411`, `:427-430`; `ArmourResistFor` at `:171-177`), and the resolver applies both the flat Defense and the live nature soak (`GroundForcesProcessor.cs:363-368`, `natureFactor = ArmourResistFor(profile.Nature)`).
- **What the report mis-read:** the *armour* is `GroundArmorAtb` (HP + Defense + nature soak). The **"power-armor augment"** it flagged as PARTIAL is a *different* part — `GroundAugmentAtb` (`GroundAugmentAtb.cs:26-42`), a *strength/carry* boost (`StrengthBonus`, `:30`) plus evasion/toughness/shield, whose job is raising the carry budget (`GroundUnitAssembly.cs:88-91`) so the frame can *bear* the heavy plate — not to be the plate. A marine mounts both. The report read the augment's status and pinned it on the armour. In this doc they are split.
- **The one genuinely missing bit — SEALED / vacuum / life-support.** Environmental resistance is folded onto a *design* at raise (`GroundForcesDB.cs:432-435`, `ResistanceTo` at `:162-166`, `GroundUnitDesign.cs:90`) and applied by the environmental-attrition consumer — but there is **no component-template dial for it**; `EnvironmentalResistance` is set nowhere in the assembler, only in tests (`GroundForcesTests.cs:604,609`). Exposing it is a **Small add** using the byte-identical pattern already proven twice on the `ArmourVs*` dials: add a `SealedFraction` (or a `Dictionary<HazardEffectType,double>`) dial to `GroundArmorAtb`/`GroundAugmentAtb`, fold it in `GroundUnitAssembly.Compute` alongside the `ArmourVs*`/`ShieldRegen` weighted-average code (`:123-147,168-177`), and copy it to the design in `ToGroundUnitDesign` (`:212-221`) — the snapshot and application ends are already built (`GroundForcesDB.cs:432-435` → `GroundForcesProcessor.cs:153`). A new dial on an existing part, not a new system.

### Q5. Can training be a general door/dial?

**Yes — one quality multiplier, baked in at build time, drawn from scarce people, slow to produce, lost when the unit dies. The engine already does exactly this for ships (`UnitCaliberAtb`, "Veteran Cadre"). The job is to generalize that one proven mechanism into a single Training dial that reads across ships AND ground.** The ship half is genuine connect-work; the ground half is a real (if pattern-following) build. Be honest about that split — see below.

**Where it lives:** a single shared augment `TrainingAtb : IComponentDesignAttribute` with one "Training Level" dial (0 = militia … 1 = elite), `MountType: ShipComponent, ShipCargo, GroundUnit` — one door in the existing Component Designer, fittable on any unit. Author it like the `unit-caliber` template in `electronics.json`: a `GuiSelectionMaxMin` slider → `AtbConstrArgs` → the C# attribute (the dial→formula→attribute path is `ComponentDesigner.cs:43-49,71-75`; `ComponentDesignProperty.cs:101-106,85-98`). It carries yield multipliers mirroring `UnitCaliberAtb.cs:35,39` (`FirepowerMult` / `ToughnessMult` / `EvasionMult`) plus a `TrainingLevel` the cost formulas reference. **Do NOT rename `UnitCaliberAtb`** — `TypeNameHandling.Objects` embeds the class name in save files (landmine L3), so renaming breaks every save; add the new attribute alongside it. (A chassis/hull dial would be cleaner for ground but weaker for ships, since not every base-mod ship mounts `ShipHullAtb` — see the fallback at `ShipDesign.cs:229-239` — so leading with one augment for both avoids that gap.)

**The asymmetry that decides the wiring** — ship build-time ignores component build-points, ground sums them:
- **Ship:** `ShipDesign.Recalculate` sets `IndustryPointCosts = MassPerUnit × 0.1` (`ShipDesign.cs:221`) — it never sums `BuildPointCost`. Training isn't mass, so making it cost ship build-time needs a **new term** at `:221`.
- **Ground:** `GroundUnitAssembly.Compute` sums each part's `IndustryPointCosts` (`:226,231`) — a training part's `BuildPointCost` flows into ground build-time **for free**.

**What it costs (each mapped to a real hook):**

| Cost | Hook | Ship | Ground | Effort |
|---|---|---|---|---|
| Build TIME | build-points from the dial | NEW term at `ShipDesign.cs:221` | free (`GroundUnitAssembly.cs:226/231`) | S / — |
| Scarce skilled people | `ColonyManpowerDB` **talent** pool (pop × 0.005, `ColonyManpowerDB.cs:27,48`) | reuse the caliber path: `TrainingAtb.CrewReq` → `ShipDesign.TalentReq` (`:85,191-192`), gated at `IndustryTools.cs:158-159`, committed at `ShipDesign.cs:135-136`, released in `ShipFactory.DestroyShip` | NEW — no ground manpower gate exists: add `TalentReq` to `GroundUnitDesign`, gate/commit in `GroundUnitAtb.OnComponentInstallation` via `ManpowerTools.HasTalentToBuild`/`CommitTalent` (`ManpowerTools.cs:68,76`), release at casualty removal | S / M |
| Credits + materials | `CreditCost`/`ResourceCost` formulas scale with the dial in JSON | data-only, no engine change | same | S |

The **talent draw is load-bearing.** Talent is only 0.5% of population and is the *same pool* science teams, governors, and ship cadres pull from — so an elite unit ties up officers you can't use elsewhere. That shared scarcity is what stops elite-spam without inventing any new "training points" resource.

**What it yields:** a veterancy multiplier **scaled by health back toward 1.0**, so a shot-up unit reverts to green-crew baseline (that's the grave rung — exactly `UnitCaliberAtb`'s pattern). Ship side: read it in `ShipCombatValueDB.Calculate` next to `UnitCaliberFirepowerMult`/`ToughnessMult` (multiply Firepower/Toughness/Evasion). Ground side: mirror the `ArmourVs*` snapshot pipeline (`GroundUnitDesign.cs:46-49`) — sum into `GroundUnitAssembly.Compute` → a `TrainingMult` on `GroundUnitDesign` → snapshot in `RaiseUnit` → apply in `ResolveRegionCombat` (`:340-363`) on Attack/Defense/HitPoints/Evasion. Note the yield is read through *two* separate code paths (`ShipCombatValueDB.Calculate` for ships, `ResolveRegionCombat` for ground) — one dial, two apply-sites. That's the thinnest part of the "same dial for both" claim, and it's disclosed, not hidden.

**Be honest about "mostly connect."** The *ship* veterancy is genuine connect-work — the caliber read already exists. The *ground* veterancy is a real build, even though it reuses proven patterns: it's three fresh Medium pieces — the ground yield read (M), the ground talent gate/commit/release (M), and base-mod templates + example units (M) — on top of the ship build-time term (S). Frame it as "ship veterancy = connect; ground veterancy = a real M-build that reuses known patterns." Don't let "mostly connect" set the time expectation for the ground side.

**One second-order hole to close in this same slice — captured units.** The design commits talent at `OnComponentInstallation` and releases it at casualty removal (`:237`). But ground units change owner via `TryCapturePlanet` (`GroundForcesProcessor.cs:597`). On capture the unit doesn't die, so the *original* builder's committed talent is never released, and the *capturing* faction never committed any. (Upkeep, Q6, handles capture correctly because it bills the current `FactionOwnerID` live every month — but the talent commit/release model does not.) This is a genuine design decision to make, not a footnote: either release-on-capture, or accept a permanent talent leak on captured units as a deliberate rule. Decide it inside the training slice.

**Why it generalizes:** militia → conscript → regular → veteran → elite are **not five unit types — they're five positions on one slider** (0.05 / 0.3 / 0.6 / 0.9 / 1.0), the same way a Guardsman and a walking cathedral are both `GroundChassisAtb` at different values. Ship it as a few template presets. Keep the **hard talent wall** (`HasTalentToBuild` has no conscript bypass) — a militarist government can conscript *bulk* crew (`CrewShortagePolicy.BuildUnderstaffed`) but must NOT be able to conscript *veterans*; that gap is the realism. Optional deeper layer later: a `TrainingCapacityAtb` "Military Academy" installation that *modulates* the training build-time term the way infrastructure efficiency scales production (`IndustryTools.cs:115`) — cleaner than a hard concurrent cap (Large). The same `TrainingLevel` scalar can later seed a starting commander's skill — one variable read by combat, manpower, and people.

### Q6. Can there be accurate upkeep + consumption?

**Yes — copy the station's monthly billing into the ground processor, put upkeep and ration/ammo dials on every unit, and wire the can't-afford behavior. Three cost axes, NOT equal work — build them in order.**

The pattern to copy is clean and small: `StationUpkeepProcessor.cs:40-62` `BillUpkeep` — compute the cost, resolve the faction via `game.Factions.TryGetValue`, `Money.AddExpense(date, category, desc, cost)`, and it already guards an unowned host (`factionId < 0`) and a captured owner (`TryGetValue`); it runs monthly (every 30 days). `ColonyEconomyProcessor.cs:42-80` is the income twin, and `ResearchProcessor.cs:107-118` is the check-then-pay idiom (`if funds < cost return;`).

**AXIS 1 — standing credit upkeep (SMALL, the core of the ask, a fully closed loop):**
- Dial `UpkeepCredits` on `GroundUnitDesign.cs:~83`, mirrored on `GroundUnitAtb.cs:~43` (the additive-ctor-arg pattern at `:52`), snapshotted onto `GroundUnit` (a field near `Attack` in `GroundForcesDB.cs:49`, copied in the copy-ctor `:187`, set in `RaiseUnit` `:409`).
- A save-safe clock `[JsonProperty] LastUpkeepBilled` on `GroundForcesDB` — ⚠️ **it MUST be added to the copy-ctor at `:373-381`** or it resets on every save/load.
- Fold the billing into `ProcessBody` (`GroundForcesProcessor.cs:~75`) behind the last-billed date — **no second processor** (landmine L9 forbids a second hotloop processor on `GroundForcesDB`). A `BillGroundUpkeep` step groups units by `FactionOwnerID` (copy the `byFaction` grouping at `:270-275` — a contested body holds two sides), sums each faction's Σ`UpkeepCredits`, resolves via `TryGetValue`, and calls `AddExpense(TransactionCategory.GroundForceUpkeep, …)`; skip `FactionOwnerID < 0`; all inside the existing try/catch at `:59` (landmine L4).
- Append `GroundForceUpkeep` to `TransactionCategory` (`Ledger.cs:20`, **at the end** so the existing integer values stay save-stable). **About 40 lines, one enum, one dial. This is the small copy-the-station-pattern add** — and the loop closes cleanly: `AddExpense` debits Money, `ColonyEconomyProcessor` tax credits it, and a shortfall drives a real consequence (desertion attrition below), with no infinite-negative-money spiral.

**AXIS 2 — ammo drain on firing (code is SMALL, the design decisions are MEDIUM; and it's an OPEN loop until Axis 3):** the pool and the drain helpers are **built and tested but only called from tests today** — `GroundAmmo.Consume`/`Refill` (`GroundAmmo.cs:31-49`) and the pool `MaxAmmo_kg`/`CurrentAmmo_kg` (`GroundForcesDB.cs:58-62`, snapshotted `:414-415`); the file header even admits the drain should "ride the resolver." Wire it at the firing site (`GroundForcesProcessor.cs:342`): before an ammo-using attacker fires, `if (IsDry) continue;` else `Consume(kg)`. `CarriesAmmo` returns false for energy/melee units (`MaxAmmo_kg == 0`), so they're byte-identical and never go dry. Two honest flags:
- The *code* is ~6 lines (Small). The *Medium* is design: the kg-per-shot model (scale off `PerShotEnergy`/`Attack`) and the mixed-weapon rule. For v1 a dry unit goes fully silent; a per-weapon "keeps its energy/melee weapon when the magazine's empty" refinement waits on the Q2 multi-profile work.
- **On its own, Axis 2 is a half-closed loop.** Ammo drains for real, but `ResupplyUnit` refills it from nowhere physical — so ammo effectively "costs nothing to replace." That's a fine intermediate slice, but it isn't a closed loop until the resupply-from-`CargoStorageDB` bridge (the front edge of Axis 3) exists. Don't let ammo ship as "done."

**AXIS 3 — physical rations/materials from a colony stockpile (LARGE, a real economy build):** dials `RationsPerMonth` + `MaintenancePerMonth` on `GroundUnitDesign`; in the same monthly gate, find the faction's colony on this body (iterate `ColonyInfoDB` where `PlanetEntity.Id == body.Id`, the pattern at `GroundForcesProcessor.cs:597`) and check-then-consume its `CargoStorageDB` (copy `ConstructionCargo.cs:89` / `IndustryTools.ConsumeResources` `:237-266`). Two honesty flags from the investigation:
- **There is no "food" cargo good today.** Food is a production *rate* (`FoodProductionAtbDB` → `SustenanceProcessor`), not a stored material. Two ways to close it: add a `food` ProcessedMaterial (a six-point registration, gotcha #10), OR — the better connection — couple garrison demand into `ColonySustenanceDB` so an army becomes population-equivalent food demand competing with civilians, reusing the built starvation loop wholesale. The coupling is *recommended but unproven*; it's a design choice at the center of this axis, not a settled fact.
- **An invasion force has no friendly colony on the body**, so it has nowhere to draw and it starves. That's the *correct* accurate outcome, and it's the natural trigger for degradation.

**"Accurate" means the dials scale off mass × quality, not hand-typed numbers:** `UpkeepCredits ≈ k_pay × CrewReq + k_cap × CreditCost` (an elite marine is high on both → high upkeep; militia are cheap), `RationsPerMonth ≈ k_food × CrewReq` (a big cheap militia eats *more* total than a small elite squad — correct), ammo-per-shot ∝ `PerShotEnergy`, maintenance ∝ `Defense × ArmourVs*`. The same stats that make a unit strong make it expensive to keep — the weight firewall the realism audit demands.

**Can't-afford behavior (second-order), cheapest first:** a credit shortfall → don't bill, apply a desertion/readiness attrition (reuse the environmental-attrition step at `:143-158`, or a `Readiness` counter that at 0 removes the unit at `:237`); a ration shortfall → starvation attrition (free if Axis 3 couples to `SustenanceProcessor`); ammo dry → silence (already graceful — the unit persists, its weapons just go quiet). Desert-vs-degrade should be a flagged tuning constant.

**Economy closure — the blunt truth:** the **credit** loop (Axis 1) closes this week and is fully real. The **physical** loop (Axis 3) does *not* close until the `ColonySustenanceDB` coupling (or a new `food` good) is built and its unproven design choice is settled. So: the money side of upkeep is real and closable now; the food/supply side is a genuine Large build with an open design decision at its center. Don't collapse the two into "upkeep is one small add."

**How it generalizes:** every dial is a `GroundUnitDesign` field snapshotted at `RaiseUnit`, so any unit — garrison, player-designed, invader, dev monolith — carries them; 0 is byte-identical; billing groups by faction so it's correct on a contested body. No `if (UnitType == Marine)` anywhere. It's the same `AddExpense`-to-Ledger shape stations and colonies already use — three hosts, one pattern.

---

## Section 2 — The generalizable variable set (the real payoff)

Every row is a door/dial on an existing `*Atb` or a resolver read. Added once, it serves ALL future units, not just marines. "Byte-identical" means: at the neutral value, an existing unit behaves exactly as it does today.

| Capability | Status | file:line hook | Effort | Generalizes to |
|---|---|---|---|---|
| **Multi-weapon-profile resolve** (ranged bolter + melee chainsword as two attacks) | SMALL ADD (machinery present, plurality missing) | snapshot list in `GroundUnitAssembly.Compute` (`:107-122`); new `ToWeaponProfiles` beside `GroundCombatant.cs:66-85`; loop the resolve `GroundForcesProcessor.cs:298-372` (was one profile at `:340`) | S–M | any N-weapon unit — same `List<WeaponProfile>` shape ships use |
| **Mounting N weapons on one chassis** | EXISTS (carry-bounded) | `GroundUnitAssembly.cs:107` loop; gates `:161-162,182-183` | — | any chassis within its carry budget |
| **Nature-tuned protective armour** (power armour = ceramite) | EXISTS (complete, cradle-to-grave) | `GroundArmorAtb.cs:18-40,55-64`; applied `GroundForcesProcessor.cs:363-368` | — | any unit — high-Defense, 4-way nature-soak plate |
| **Sealed / life-support dial** | SMALL ADD (only missing armour bit) | new `SealedFraction` on `GroundArmorAtb`/`GroundAugmentAtb`; fold `GroundUnitAssembly.cs:123-147`; consumer wired `GroundForcesDB.cs:432-435`→`GroundForcesProcessor.cs:153` | S | any unit needing vacuum/hazard survival |
| **Flight / jump-pack mobility enhancer** | SMALL ADD (speed/carry/amphibious EXIST; flight missing) | speed/amphibious `GroundLocomotionAtb.cs:27-32`; new `Flight` dial + guarded branch in `HexPathfinder.cs:42,47-63` and `GroundMobility.cs:91-106` | S | any chassis mounting the locomotion/jump component |
| **Ground quality / caliber (evasion-toughness-firepower)** | EXISTS for ships; ground-read pending | ship `UnitCaliberAtb.cs:35,39` read in `ShipCombatValueDB.Calculate` | — / M | ships now; ground via the Training dial |
| **Training → veterancy (time + talent → quality)** | ship half = CONNECT; ground half = real M-build (yield read M + talent gate M + templates M) | `TrainingAtb` like `unit-caliber`(`electronics.json`); ship time term `ShipDesign.cs:221`; talent `ColonyManpowerDB.cs:27,48` + `ManpowerTools.cs:68,76`; ground apply `GroundForcesProcessor.cs:340-363`; close capture-talent hole at `:597` | S ship / M ground | ships AND ground; militia→elite is one slider |
| **Model-count / squad casualties** | SMALL ADD (resolver shape amenable; balance-affecting) | `ModelCount`/`ModelsAlive`/`PerModelHP` on `GroundForcesDB.cs`; attack ∝ survivors `:322-327`; decrement `:379-386`; **neutral = 1, not 0** | S | any multi-body unit (squad, platoon) |
| **Standing upkeep → Ledger** | SMALL ADD (station pattern to copy) | copy `StationUpkeepProcessor.cs:40-62` into `ProcessBody` `GroundForcesProcessor.cs:~75`; dial on `GroundUnitDesign.cs:~83`; enum `Ledger.cs:20`; `LastUpkeepBilled` in copy-ctor `:373-381` | S | any owned unit, any faction, contested bodies |
| **Ammo consumption on firing** | code S / design M; OPEN loop w/o resupply bridge | `GroundAmmo.Consume` `GroundAmmo.cs:31-49` at firing site `GroundForcesProcessor.cs:342` | M | any ammo-fed weapon (energy/melee auto byte-identical) |
| **Physical rations/maintenance from stockpile** | BIGGER BUILD (consumption economy; food-as-good unproven) | dials on `GroundUnitDesign`; draw from colony `CargoStorageDB` per `:597`; couple to `ColonySustenanceDB` OR new `food` good | L | any unit with a supply source; starvation for unsupplied |

---

## Section 3 — Corrected cheapest-first verdict

### Overstated in the litmus report (a cheap dial / enhancer / resolver-read away)

- **"6-model squad can't be represented"** — two one-line hooks (`GroundForcesProcessor.cs:322-327` and `:379-386`), no rewrite. Caveat: neutral is `ModelCount = 1` (not 0), and once above 1 it *does* change combat balance (firepower decays with health) — not a cosmetic counter.
- **"The gap is the assembler" / "ground can't resolve per-weapon"** — the gap is the resolver plus the singular snapshot; the assembler already mounts N weapons (`GroundUnitAssembly.cs:107`) and the per-weapon kernel machinery is fully present and wired (`GroundCombatant.ToWeaponProfile`, `CombatKernel.*`).
- **"Jump pack CANNOT"** — speed / carry / all-terrain-handling are authorable data today; only flight / ignore-terrain is missing, and that's one dial plus guarded branches in two already-single-funnel pure functions.
- **"Power armour PARTIAL"** — the *armour* (`GroundArmorAtb`) is complete and wired cradle-to-grave; the report read the *strength-augment's* status and pinned it on the armour. Only the sealed/life-support dial is genuinely missing, and that's a small add.

### Genuinely load-bearing builds (not a dial)

- **Ammo-drain design (M).** The wiring is ~6 lines (Small), but the kg-per-shot model and the mixed-weapon silence rule are real decisions — and the loop stays open (ammo refills from nowhere) until the Axis-3 resupply bridge exists.
- **Ground training-talent path (M) — plus a capture hole.** No ground manpower gate exists today; the gate/commit/release is new work, and it must also handle units that change owner via capture (`GroundForcesProcessor.cs:597`), which the naive commit-at-build/release-at-death model does not.
- **Physical rations/consumption economy (L).** Needs a supply-source resolver, a food-as-good decision (a new material or coupling to `ColonySustenanceDB` — recommended but unproven), and a starvation feedback loop. This is the one axis that's a real system build, not a paste.

### Honest cheapest-first ordering — monolith-with-a-paint-job → real squad

1. **Standing credit upkeep (S)** — copy `StationUpkeepProcessor.BillUpkeep` into `ProcessBody` behind a last-billed date; one enum, one dial, `LastUpkeepBilled` in the copy-ctor. Immediate "an army costs money," fully closed loop.
2. **Sealed / life-support dial (S)** — completes power armour; the consumer is already wired.
3. **Flight dial + guarded branch (S)** — jump pack becomes real; two single-funnel pure functions.
4. **Model-count / squad casualties (S)** — two one-liners; neutral `ModelCount = 1`, not 0; understand it shifts balance once above 1.
5. **Multi-weapon-profile resolve (S–M)** — snapshot the list, add `ToWeaponProfiles`, loop the resolve; unlocks the mixed-weapon ammo nuance.
6. **Training: ship build-time term (S) → ground yield read (M) → ground talent gate (M) → base-mod templates (M)** — reuse the `UnitCaliberAtb` read; close the capture-talent hole in this same slice.
7. **Ammo drain (M = S code + M design)** — depends on #5 for the mixed-weapon rule; an open loop until #8's resupply bridge.
8. **Consumption economy / rations + starvation (L)** — the one real system build: supply-source resolver + food-as-good decision (`ColonySustenanceDB` coupling recommended, unproven) + starvation feedback.

### The blunt sentence

The developer's instinct was right. Four of the six capabilities the litmus report marked CANNOT/PARTIAL are cheap dials or resolver-reads on existing `*Atb`s — six of the eight items above are Small or Small–Medium. Only three are genuine builds: ammo-drain *design* (M), the ground training-talent path (M, with an unflagged capture hole to close), and the physical rations/consumption economy (L).

### The payoff the developer named

None of these are marine-specific. Every dial lives on `GroundUnitDesign` / `GroundLocomotionAtb` / `GroundArmorAtb` / the shared `TrainingAtb`, snapshotted at `RaiseUnit`, byte-identical at zero — so a Guardsman, a walking cathedral, a militia mob, and a Blood Angel are all the *same variables at different settings* (a single tank = `ModelCount 1`; a Titan = one model / huge HP; a laser unit = `CarriesAmmo false`; billing groups by faction with no unit-type check anywhere). Building the Blood Angel correctly builds the toolkit for every unit that follows.

### Where the investigations flagged uncertainty rather than asserting

- The ammo kg-per-shot model and the mixed-weapon "keeps fighting with energy/melee when dry" rule are v1-deferrable decisions, not solved (Axis 2).
- The food-as-good vs. `ColonySustenanceDB`-coupling is an open design choice, with the sustenance-coupling recommended but not proven (Axis 3).
- The training-talent commit/release model needs a release-on-capture decision (Q5, C3).
- Whether any of this works *live* is unverifiable here — CI can't run the client (landmine L11), so all of it lands on the local-runtime checklist.
