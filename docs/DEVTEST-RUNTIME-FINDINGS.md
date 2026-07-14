# DevTest Runtime Findings — the playtest bug ledger

**Purpose.** DevTest (the everything-enabled conquest sandbox that replaced Quickstart) is our hardest *runtime* test — and runtime is exactly where CI is blind (it compiles the client, can't run it). This is the running ledger of every bug the DevTest playtest surfaces, so we can see **how widespread each one's root cause is** — is it a one-off, or an instance of a *class* that lurks elsewhere? Each row records the symptom, the root cause + its class, the fix, and a **blast-radius** assessment.

**As of:** 2026-07-14 · branch `claude/devtest-faction-design-xpfnhe`

**How this is used.** When a DevTest boot throws or misbehaves, the fix isn't done until we've asked "what *class* is this, and where else does that class live?" A localized fix that ignores the class just defers the next instance. The two big classes below each have a standing sweep item.

---

## Findings

| # | Symptom (what the player saw) | Root cause + CLASS | Fix | Blast radius |
|---|-------------------------------|--------------------|-----|--------------|
| R1 | New Game crashed: `KeyNotFoundException: 'sol'` before the world loaded | **ID-resolution / two-ends drift.** `DevTestStartFactory` looked up the Sol *system* blueprint by `"sol"`, but that's the *star's* id — the system's is `"system-sol"`. Same id feeds the runtime `StarSystem.ID`, so every scenario `systemId` had to match too. | Use `"system-sol"` in the loader + all three scenario files. | Contained (one loader + 3 data files). Guards against the general "an id referenced in one place must resolve in the other" class — same family as gotcha #10 (JSON id drift). |
| R2 | Faction name printed blank (`viewing faction id=277 ()`) | **Path-specific field left unset.** `FactionFactory.CreateFaction` (the `LoadFromJson` path) never sets `Abbreviation`, and the UI keys readouts off it — unlike `CreateBasicFaction`. | `LoadFromJson` reads an `"abbreviation"` field; scenarios set UEF/UMF/KTH. | Contained. Watch for other fields the two faction-creation paths set inconsistently. |
| R3 | **Clock wouldn't advance on Play (the time-stall).** | **⭐ CLASS A — unguarded hard dictionary index in a tick processor.** `MineResourcesProcessor.MineResources` hard-indexed the planet's deposits `planetMinerals[key]` with a key from the colony's mining-RATE table. The rate table keeps a mineral with rate 0 when the planet has no deposit of it (`CalculateActualMiningRates` guards `planetMinerals` with `ContainsKey`; the tick didn't). A mine whose mineable set is broader than the body's deposits — **a DevTest booting on a random seed whose Earth lacks one of the default mine's minerals** — threw *every tick*, on the parallel sim thread, unobserved → the clock froze. | Skip a mineral with no deposit (mirror the `ContainsKey` guard the storage line right below already had). Regression: `MiningRobustnessTests`. | **See Class A below.** The specific mining path is now fully guarded; the *class* is the engine-side twin of client gotchas #10/#11/#14 and warrants a processor-wide sweep. |
| R4 | Luna & Venus showed their surface, though only Earth should be surveyed | **⭐ CLASS B — a faction-agnostic flag used as if per-faction.** `Region.Surveyed` is a single shared bool per region. The **UMF colonizes Luna + Venus**, and creating those colonies calls `RevealAll()`, flipping the shared flag for *everyone*, including the player. | `PlanetViewWindow` now gates the globe on the **viewing faction's** per-faction `GeoSurveyStatus` (`GeoSurveyableDB.IsSurveyComplete(myFaction)`), which the colony loader stamps per-owner. | **See Class B below.** The player view is fixed; the underlying shared flag is a known v1 limitation. |

*(Not bugs — tuning from the same playtest, recorded for context: the player's starting unlocks were trimmed 123→73 so weapons/warp/ground/shields are researched, not free; the homeworld sensor stays.)*

---

## ⭐ Class A — unguarded hard dictionary index in a tick processor

**The pattern.** A processor iterates one collection and hard-indexes *another* dictionary by the same key, assuming the two are always in sync. When some state path leaves them out of sync (a random seed, an unusual colony build, a foreign-owned entity), the index throws — and because hotloop processors run on the **parallel sim thread**, the throw is *unobserved* and **freezes the clock** instead of surfacing. This is the engine-side twin of the client's crash family (root `CLAUDE.md` gotchas #10/#11/#14 — "never hard-index a runtime dictionary from a UI path"); the same rule applies to processors, where the cost is a silent sim freeze rather than a client crash.

**Sweep (2026-07-14, first pass).**
- Mining path — **fixed + contained.** Every `planetMinerals[key]` / `_minerals[key]` in `MineResourcesProcessor` is now behind the `ContainsKey` guard; `MiningHelper.CalculateActualMiningRates:59` was already correct (the model to mirror). No other mining-rate hard-indexes exist.
- **Not yet swept:** the other daily/hotloop processors (colony economy, population, research, energy, logistics, industry-build). A processor that indexes a faction/cargo/material dict by a key it got from iterating a *different* collection is the risk shape.

**Standing item.** Run a processor-wide sweep for the shape `for (x in A) { … B[x.Key] … }` where `B` can diverge from `A`, and convert each to `TryGetValue`/`ContainsKey`-guarded. Highest value because the failure mode is a silent full-sim freeze. *(Candidate for a focused workflow: fan out over the hotloop processors, flag every cross-collection hard-index, verify each can/can't diverge.)*

---

## ⭐ Class B — a faction-agnostic flag used as if per-faction

**The pattern.** State that should be **per-faction** (who has surveyed / who can see / who owns) is stored as a single shared value, so one faction's action leaks to all. `Region.Surveyed` is the instance: it's one bool per region, flipped by **four** callers — `ColonyFactory.RevealAll` (×2), `GeoSurveyProcessor.RevealAll`, `GroundSensors.RevealRegion` — each of which reveals the body **to everyone**. The DevTest exposes it because rivals hold worlds the player hasn't scanned (UMF on Luna/Venus).

**Sweep (2026-07-14).**
- Player planet-view — **fixed** by gating on per-faction `GeoSurveyableDB.GeoSurveyStatus` instead of `Region.Surveyed`.
- **Underlying flag still shared.** Every other consumer of `Region.Surveyed` (ground-combat map reveal, per-hex deposit visibility, region-detail labels) still treats "surveyed" as global. Harmless while the player's top-level view is correctly gated, but any future *per-faction* ground/survey feature will re-trip it.

**Standing item.** If per-faction ground visibility becomes a real requirement, promote `Region.Surveyed` (bool) → a per-faction set (mirror `GeoSurveyableDB.GeoSurveyStatus`'s `Dictionary<factionId, …>` shape) and update the four callers + all readers. Flagged in `PlanetRegionsDB` as the v1 "reveal is world-level and faction-agnostic" limitation.

---

## The meta-lesson (why this ledger exists)

Both R3 and R4 were **not** what they first looked like — R3 looked like the AI (we'd just turned it on), R4 looked like a fix that didn't take. In both cases the real cause was a general class the DevTest happened to trip because it stresses paths a normal game doesn't (random seed → odd mineral sets; rival-held inner-system worlds → cross-faction leaks). The value of DevTest is exactly this: it's an integration stress test that surfaces *classes* of latent bug. Logging them here — with the blast-radius question answered, not just the one instance patched — is how we keep the fixes from being whack-a-mole.

**The flight recorder earned its first keep here:** R3's `[FATAL]` line in `game_logs/` named the exact crashing method in seconds. Observability first, then the fix.
