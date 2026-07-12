# People / Commanders Subsystem — Developer Reference

**What it does:** Generates and tracks officers who lead ships, fleets, and (eventually) ground forces. Naval academies on colonies graduate classes of officers on a schedule. Officers have a rank, a type, and experience. Scientists are a special kind of officer with tech-category bonuses that multiply research output.

**Commander combat competence is now a complete loop (2026-07-09, rung-4 wire — slices 3a→3c).** An academy graduate's competence becomes Firepower/Toughness bonuses on their `BonusesDB`; seat that officer as a fleet's flagship and their skill scales the whole fleet in the auto-resolver. So a better-trained admiral wins harder — academy → skill → fight, end to end. (See the rung-4 note below for the three slices.)

**Research academy — the scientist twin of the naval academy (2026-07-12, Exploration X.0-1, engine slice).** `ResearchAcademyDB` (a roster of enrolled `ResearchAcademy` classes, each with a `ClassSize`, `GraduationDate`, `TrainingPeriodInMonths`, and a `SpecialtyCategory` tech-category) + `ResearchAcademyProcessor` (an `IInstanceProcessor`) mirror `NavalAcademyDB`/`NavalAcademyProcessor` exactly — but graduate SCIENTISTS instead of officers. On graduation, `ResearchAcademyProcessor.GraduateScientist(host, trainingMonths, specialty)` creates a scientist entity (`CommanderFactory.CreateScientist` → `Create`), rolls its `ExperienceCap` on the same 0–200 bell curve the naval academy uses (mean shifted by training length — a longer program → a higher-ceiling scientist), and stamps the rolled `CommanderBonuses.RollResearchCompetence(cap, specialty)` onto the graduate's `BonusesDB`. That bonus is the exact shape `ResearchProcessor.RefreshPointModifiers` reads once the scientist is assigned to a lab (`AssignScientistOrder`) — so a better-trained scientist researches faster in their specialty, the research-side mirror of the officer combat-competence loop. Gauge: `ResearchAcademyTests`. **Reachable as of X.0-2** — see below. **Growth loop closed as of X.0-3** — see below.

**Research academy — the buildable component (2026-07-12, Exploration X.0-2, reach slice).** `ResearchAcademyAtb` (mirrors `NavalAcademyAtb`, plus a `SpecialtyCategory` string) makes the academy a real, buildable installation: on install it seeds a `ResearchAcademyDB` and schedules `ResearchAcademyProcessor`. The base-mod component is `research-academy` (`GameData/basemod/TemplateFiles/installations.json`) with three designer dials — **Class Size**, **Class Length** (the school-quality knob), and **Research Specialty** (a `GuiTechCategorySelection` whose string threads through `AtbConstrArgs` into the atb, the same pattern the research-lab uses for its bonus category). The design `default-design-research-academy` is in `componentDesigns.json`, and Earth's start colony (`ScenarioFiles/systems/sol/earth.json`) lists both the `research-academy` template (StartingItems) and the design (ComponentDesigns) — so it's **buildable from turn one, like the research lab** (deliberately NOT tech-gated; the sibling installations aren't either). **Registration note:** the atb's `AttributeType` in JSON is the REAL FQN `Pulsar4X.People.ResearchAcademyAtb`, NOT the legacy `Pulsar4X.Atb.` prefix the (unreachable) naval academy uses — that prefix wouldn't resolve through `Type.GetType`, and the naval academy only gets away with it because it's never actually instantiated (it's in no colony's build list). Gauge: `ResearchAcademyBaseModTests` instantiates the design through the real `ComponentDesigner` (template → NCalc → atb) and asserts the atb binds with its dials — so a wrong FQN or an arg-count/order mismatch fails in CI, not in a player's New Game (the client is CI-blind). `BaseModIntegrityTests` covers the material/unlock end. **Save/load note:** because the design is stored on the colony, `ResearchAcademyAtb` MUST carry a parameterless ctor + `[JsonProperty]` dials (the `GroundWeaponAtb` idiom) — without the ctor `Game.Load` throws "Unable to find a constructor to use" on any save containing it (caught by `SaveLoadWithJobTests`, fixed in `28bf2ec`). The unreachable naval academy dodges this only by never being instantiated.

**Research academy — experience over time (2026-07-12, Exploration X.0-3, growth slice — the loop closes).** A scientist now IMPROVES as they work, the "ceiling now + bonus on top" model: the graduation competence (rolled from `ExperienceCap`, the school-set floor) is untouched, and a SEPARATE, growing bonus is earned by actually working a lab. `ResearchProcessor.GrowScientistExperience(scientist, techCategory, gain)` (pure/testable) raises an assigned scientist's `CommanderDB.Experience` toward its cap and replaces the experience bonus (`CommanderBonuses.RollResearchExperienceBonus`, Name `"Research Experience"`, distinct from the graduation `"Research Aptitude"` so growth never disturbs the floor) in the category they research — capped at `ExperienceCap`, so the school still bounds ultimate potential. `ResearchProcessor.DoResearch` calls it monthly (`entity.StarSysDateTime.Day == 1`, `ExperienceGainPerMonth` = 2) for an assigned scientist ENTITY, then re-folds `PointsPerDay`. **Byte-identical for the default start:** the legacy scientist path leaves `ResearcherDB.ScientistId == -1`, so no assigned entity-scientist exists and nothing grows (the smoke/loop tests are the tripwire; no existing test assigns an entity-scientist AND advances the clock). Gauge: `ResearchExperienceTests` (growth up to the ceiling, floor left intact, folds through the real reader).

---

## Admin delegation seats (the Command "play at your own altitude" layer)

`AdminSpaceAtb` (a component attribute) grants an entity **admin seats** — one per installed admin component (an `admin-complex` on a colony, a `ship-command` bridge on a hull); `AdminLevel` (Ship→Empire) sets the seat's scope. `AdminSpaceDB.CommanderSeats` holds the seats as `AdminSpaceAbilityState` (each carries a `ComponentName` identity + the seated `CommanderDB` / `CommanderID`, -1 = empty). `AssignAdministratorOrder` seats a commander into a post by matching `ComponentName`, and sets `CommanderDB.AssignedTo`. `AdminSpaceProcessor.CalcEntityAdminSpace` (an `IInstanceProcessor`) rebuilds the seat list from the installed components.

**Durable-seat fix (2026-07-09, foundation slice 1 — dossier ⚙10).** `CalcEntityAdminSpace` used to allocate a *fresh* seat list every pass, so the next processor tick silently un-seated every administrator — nothing downstream could hold an assignment (its own comment flagged the doubt). It now reconciles: the pure, unit-tested `AdminSpaceProcessor.ReconcileSeats(previous, current)` carries each existing seat (and the commander in it) across a recalc, matched by `ComponentName` — the same key `AssignAdministratorOrder` uses — adds a fresh empty seat for a new component, and drops a seat whose component was removed while clearing its occupant's `AssignedTo` (no dangling assignment). Gauge: `AdminSpaceSeatReconcileTests`.

**Decapitation grave rung (2026-07-09, foundation slice 2 — dossier ⚙10).** Losing the command structure now empties the post it held. (a) **Component loss:** `AdminSpaceAtb.OnComponentUninstallation` used to *throw* `NotImplementedException` (destroying a command component crashed) — it now drops that component's seat and frees its occupant via `AdminSpaceProcessor.DropSeatForComponent`. Keyed by name because the uninstall hook fires *before* the component leaves `ComponentInstancesDB` (`Entity.RemoveComponent:170` then `:173`). (b) **Officer death:** `CommanderFactory.DestroyCommander` vacates a killed commander's seat via `AdminSpaceProcessor.VacateSeat` (the `CrewLosses` event still fires for the log). Both helpers are pure/unit-tested in `AdminSpaceSeatReconcileTests`.

**Rung-4 competence wire — read half (2026-07-09, foundation slice 3a — dossiers ⚙6/⚙10).** The "a person's skill modifies an outcome" mechanism, the commander-side mirror of the scientist research fold. `BonusCategory` gained combat categories (`Firepower`, `Toughness`, `People/BonusesDB.cs`), and `CommanderBonuses.CombatMultiplier(bonusesDB, category)` reads a commander's `BonusesDB` into a combat multiplier (product of `(1 + Value)` over matching-category bonuses; 1.0 when none). Pure/unit-tested (`CommanderCombatBonusTests`).

**Rung-4 competence wire — resolver half (2026-07-09, foundation slice 3b).** The read helper is now folded into the fleet auto-resolver. `CombatEngagement.GetCombatShips` computes `FleetCommanderMult(fleet, category)` — `FleetDB.FlagShipID` → the flagship's `ShipInfoDB.CommanderID` → that commander's `BonusesDB` → `CommanderBonuses.CombatMultiplier` — and multiplies it into every ship's firepower/toughness mult, on top of doctrine. So a fleet's **flagship commander's competence scales the whole fleet**. Defensive (1.0 when there's no flagship / commander / bonus) → **byte-identical to pre-commander combat** until a commander carries a bonus (every existing combat fixture is the tripwire). Gauge: `CommanderCombatWireTests`.

**Rung-4 competence GENERATOR (2026-07-09, foundation slice 3c) — the loop closes.** Academy graduates used to get an empty `BonusesDB`, so the wire had nothing to fold. Now `NavalAcademyProcessor` calls `CommanderBonuses.RollCombatCompetence(commanderDB.ExperienceCap)` on each graduate and writes the resulting Firepower + Toughness bonuses onto their `BonusesDB`. The magnitude scales with the graduate's `ExperienceCap` (0–200, mean ~100, shifted by training length): cap 200 → the full `CommanderBonuses.MaxCombatCompetenceBonus` (0.15 = +15%), 100 → half, ≤0 → none — modest by design (a tiebreaker on doctrine + composition, not a replacement). Gauge: `CommanderCombatBonusTests` (the generator + the generate→read round-trip). **Still open:** an aggressive-vs-defensive split (Firepower ≠ Toughness) is a later flavor pass; `ConsoleSpace` span-of-control (still computed then discarded); and the academy *tier* dial (a bigger competence ceiling for elite academies) from the essence extensions.

---

## Files

| File | Role |
|------|------|
| `CommanderDB.cs` | DataBlob for an officer. Has Name, Rank (int), Type (`CommanderTypes` enum), Experience, ExperienceCap, CommissionedOn, RankedOn. **No bonus fields.** |
| `CommanderFactory.cs` | Creates commander entities. `CreateAcademyGraduate()` generates a fresh officer; `Create()` attaches it to the game world. |
| `NavalAcademyDB.cs` | DataBlob on a colony running a naval academy. Tracks graduating class sizes and timing. |
| `NavalAcademyAtb.cs` | Component attribute on the Naval Academy installation. `ClassSize` (how many graduates per cycle), `ClassLength` (months to graduation) → drives the `NavalAcademyProcessor`. |
| `NavalAcademyProcessor.cs` | `IInstanceProcessor`. Fires when a class is due to graduate. Calls `CommanderFactory.CreateAcademyGraduate()` for each graduate, attaches them to the faction, then schedules the next class. |
| `SpeciesDB.cs` | DataBlob on a species entity. Holds the species definition including `ColonyCost()` — the habitability calculation for a planet given this species' tolerances. |
| `SpeciesDBExtensions.cs` | Extension methods on `SpeciesDB`, including the colony cost formula. |
| `SpeciesFactory.cs` | Creates species entities. |
| `TeamsDB.cs` | Tracks which teams (scientists, commanders) exist in a faction. |
| `TeamsHousedDB.cs` | DataBlob on a colony. Dict of `TeamTypes` → List of team objects. `TeamsByType[TeamTypes.Science]` is how `ResearchProcessor` finds scientists. |

---

## The CommanderTypes Enum

`CommanderTypes` (in `Engine/DataStructures/Enums.cs`) is Navy / Ground / Scientist / Civilian / **Intelligence**. **Intelligence (Espionage E2, 2026-07-12)** is a covert operative (spy) — recruited by an Intelligence Directorate (`Factions/IntelDirectorateProcessor`, the naval-academy pattern) via `CommanderFactory.CreateAgent`. Its skill is a `BonusCategory.Espionage` "Tradecraft" bonus (`CommanderBonuses.RollEspionageCompetence`, scaled by ExperienceCap; `MaxEspionageCompetenceBonus` 0.6) read back through `CommanderBonuses.EspionageSkill01` (0..1) into the covert-op detection roll (`Factions/CovertRisk`). So an operative is the people/skill twin of a scientist, pointed at the spy game. Gauge: `IntelOperativeTests`.

---

## What Actually Works vs What Doesn't

| Feature | Status | Notes |
|---------|--------|-------|
| Naval academy generates officers | ✅ works | `NavalAcademyProcessor` fires on schedule |
| Officers have rank + experience | ✅ stored | But experience doesn't do anything yet |
| Scientist bonuses multiply research | ✅ works | See `Tech/CLAUDE.md` |
| Commander bonuses on combat/navigation | ❌ missing | `CommanderDB` has no bonus fields — the data model doesn't support it |
| Ground force commanders | ❌ missing | No `CommanderTypes.Ground`, no ground skill fields |
| Commander death on ship destruction | Partial — `ShipFactory.DestroyShip()` kills the commander | See `Ships/CLAUDE.md` |
| Flagship commander bonus | ❌ missing | `FleetDB.FlagShipID` exists but no bonus applied |

---

## SpeciesDB — the Colony Cost Formula Lives Here

`SpeciesDB` holds the species' environmental tolerances. The `ColonyCost(planetEntity)` method (in `SpeciesDBExtensions.cs`) computes the CC value that `PopulationProcessor` uses. **This is the formula we need to verify and possibly replace in Phase 2c.**

Key: `ColonyCost()` should return the worst-single-factor value per the Aurora spec in `docs/aurora/COLONY-ENVIRONMENT-AND-POPULATION.md §1`. Verify it does this correctly before building the infrastructure cap formula on top of it.

---

## Pulsar Status vs Aurora

| Aurora concept | Pulsar | Status |
|----------------|--------|--------|
| Officers generated by academies | `NavalAcademyProcessor` | ✅ functional |
| Scientist research bonuses | `Scientist.Bonuses` | ✅ functional |
| Commander combat/navigation bonuses | Nothing — `CommanderDB` has no bonus fields | ❌ not implemented |
| Ground force commanders | Nothing | ❌ not implemented |
| ~300 starting officers, 10/yr | NavalAcademy controls output rate | ✅ configurable via component |
| Officer experience affects performance | Experience is stored, never read | ❌ data without behavior |

---

## Phase 4 Additions Needed

1. Add `CommanderTypes.Ground` to the enum.
2. Add skill bonus fields to `CommanderDB` (or create a `GroundCommanderDB` extending it) — e.g., `Dictionary<string, float> SkillBonuses` like `Scientist.Bonuses`.
3. A `FormationDB` will reference a commander entity ID as its commander.
4. `GroundCombatProcessor` reads the commander's bonuses when calculating formation combat effectiveness (attack/defense multiplier).
5. Ground academies: a new installation type (`GroundForceTrainingCentre`?) graduates ground commanders — same `NavalAcademyProcessor` pattern, different `CommanderType`.
