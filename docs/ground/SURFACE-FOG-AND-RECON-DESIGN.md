# Surface Fog of War & Reconnaissance — Design (LOCKED 2026-07-17)

> **Status:** design LOCKED with the developer. Build path chosen: **A — ground fog first** (the foundation both the
> two-tier survey and an honest "easiest landing" score stand on). Build in the gauged slices below, one CI-green slice
> at a time. Companion: `docs/ground/GROUND-SURFACE-MAP-DESIGN.md` (the surface map this rides on), the two-tier survey
> half also belongs to `docs/explore/`.

---

## The one-line idea

**Reconnaissance is a single mechanic that lifts a single fog.** A scout walking a planet's hexes reveals, per faction,
*what's on the ground* (deposit assays, hidden sites — the **survey**) **and** *who's on the ground* (enemy garrison —
the **landing intel**). Build the per-faction ground-fog layer once, and both the survey and the "easiest place to land"
score light up together.

---

## Why this is the foundation (the finding that reordered the build)

A surface-data catalog (3-agent survey, 2026-07-17) found: **there is no per-faction fog of war on the ground at all.**
- `Region.Surveyed` is one **world-level** bool, faction-agnostic (`PlanetRegionsDB.cs:118-124`) — there is no
  "which regions has *this* faction scouted."
- `GroundForcesDB.Units` is one **flat public list** holding BOTH sides' units with every field visible — the enemy's
  whole garrison is readable by anyone.
- Hex deposits (`GroundHex.DepositMineralId/DepositAmount`) are stored **un-masked** (plain int/long) — unlike the
  body-wide `MineralsDB.Amount`, which IS per-faction `Masked`. So the assay leaks.

So today an AI or player "sees" an enemy's entire garrison and every deposit on a planet it has never set foot on. Both
of the systems we want to build depend on closing this:
- The **two-tier survey** (below) is *about* a per-faction reveal — it can't exist without one.
- An honest **"easiest landing" score** must read *what the attacker can see*, not ground truth — else the AI is
  omniscient.

Hence **path A: build the per-faction ground-fog layer first.**

---

## The LOCKED survey model — two tiers, orbit vs. boots

Confirmed with the developer ("deposits visible-but-un-assayed from orbit, special sites fully hidden until scouted —
this is it"):

| Tier | Who does it | What it reveals |
|------|-------------|-----------------|
| **Space survey** (orbit) | a survey SHIP in orbit (the existing `GeoSurveyProcessor`) | the planet's **parameters** (gravity/atmosphere/temperature/size) **and** each **mineral deposit's LOCATION + TYPE** (there's space-crete at hex 5,99) — but the deposit reads **UN-ASSAYED**: you do NOT learn the **amount (50) or rate (0.1)**. **Special sites** (research / exploration / ruins) are **NOT shown at all.** |
| **Ground survey** (boots) | a **scout unit** walking the surface hexes | the **assay** — a deposit's real **amount + rate** (is it worth mining) — revealed hex-by-hex as the scout covers ground; **and** it uncovers the **hidden special sites** that never showed from orbit. |

Prospector's logic: you spot the outcrop from the air, you assay it on foot.

**This reuses machinery we already have:** deposit amounts already support **`Masked` per-faction** values, and the space
survey already grants **partial** faction access. Space survey = *partial* (location + type, amount masked) → ground
scout = *full* (unmask the assay). The one real change is that hex deposits must become per-faction masked like the
body-wide pool (today they leak).

---

## The recon capability is a COMPONENT (not a "scout unit" type)

Per the LOCKED PRINCIPLE in `GameEngine/GroundCombat/CLAUDE.md`: a scout is **a ground chassis carrying a recon
component**, designed in the Component Designer and assembled in the Entity Assembler — never a hardcoded
`GroundUnitType.Scout`. The recon ability **lives in Sensors** (it IS a sensor — a ground-side one): either a new
`GroundSurveyAtb`/`ReconAtb` or extra dials on an existing sensor attribute. Deciding which is a slice-3 detail; either
way the assembler mounts it.

**Cradle-to-grave for the scout:** mineral → material → the recon component (research-gated) → assembled onto a ground
chassis in the Entity Assembler → built at a colony → deployed → walks hexes revealing ground → **shot off / the unit
dies → you lose the recon reach** (the grave rung — reconnaissance is a losable capability). Every rung is free because
it's a component (`CONVENTIONS.md` §6).

---

## The "easiest landing" score (the second consumer of the fog)

From the same catalog — the AI scores a landing region off the **fog-limited** picture (what it has scouted), strongest
signal first:
1. **Ownership** (`Region.OwnerFactionID`): unowned (−1) = unopposed; enemy = a fight — the primary filter.
2. **Enemy garrison strength** in the region (aggregate `GroundForcesDB.Units` by region+faction, sum Attack/Health) —
   the dominant term; **weakest-defended = easiest**. Read through the fog (only scouted regions).
3. **Fortification** (`GroundFortification.DefenseMult`) — soft (1.0) beats bunkered.
4. **Terrain** (`GroundTerrain.Classify`/`CoverDefenseMult`) — Open (0.9) is the easy beachhead; Rough (1.5) a fortress;
   **Ocean = disqualified** (troops strand).
5. **Hazard** (`PlanetEnvironmentsDB.ForRegion`) — a damage hazard bleeds the beachhead; prefer none.
6. **Proximity** — hops to the target (shortens the campaign) + adjacency to a friendly region (reinforce/stage).
7. **A resource hex nearby** — so the forward base pays for itself.

**Small helpers to build** (flagged not-readable today): a per-region **garrison-strength** read, a **defensibility
roll-up** (this IS the score), and a **distance-to-target** walk (only 1-hop adjacency is free today).

## Beachhead / FOB — the anchor (design essence, locked)

The FOB is a **role, not a new component**: the AI lands at the "easiest" region, plants an existing installation there
(a bunker / radar station), and that region becomes the **marked landing + staging anchor** it routes further troop
transports to before fanning out. "Beachhead" and "deployment zone" collapse into **this one anchored region**. Almost
no new buildables — the new part is the AI logic (pick the easy region, plant the anchor, drop reinforcements there).

---

## Build plan (path A — one CI-green slice at a time)

1. **Ground-fog data model + reveal API** (additive, byte-identical): a per-faction ground-reveal record on the planet
   body — `Reveal(faction, region/hex)` / `IsRevealed(faction, region/hex)` — and per-faction masked hex deposits.
   Nothing consumes it yet. Gauge: reveal→isRevealed round-trip + a save/load of the mask.
   **✅ BUILT (2026-07-17):** slice **1** — `PlanetRegionsDB.PerFactionRevealed` + `RevealRegionFor`/`IsRegionRevealedFor`/
   `RevealAllRegionsFor` (per-faction REGION reveal). Slice **1b** — `GroundHex.DepositAssay` (`Masked<long>`, faction
   bit-mask) + `RevealDepositLocation` (Partial)/`RevealDepositAssay` (Full)/`AssayFor` (per-faction masked deposit
   ASSAY; closes the leak). Both additive/byte-identical, nothing consumes them yet. Gauge: `GroundFogTests`.
2. **Wire the space survey to the fog:** on `GeoSurveyProcessor` completion, reveal each region's **geography + deposit
   location/type** to the surveying faction, but leave the **assay (amount/rate) masked**. (Split the current "reveal
   everything" into partial.) Gauge: after a space survey, the faction sees the deposit's type but the amount reads
   masked.
   **✅ BUILT (2026-07-17):** `GeoSurveys/SurveyReveal.RevealWorldTo(regionsDB, factionId, factionMask)` — a pure,
   testable helper: `RevealAllRegionsFor(factionId)` (geography, per-faction) + `RevealDepositLocation(factionMask)` on
   every deposit hex (Partial = located, assay masked). `GeoSurveyProcessor` calls it at survey completion, right after
   `EnsureGridForBody`, KEEPING the world-level `RevealAll()` alongside (additive/byte-identical for the old consumers
   until the client migrates to the per-faction read — **slice 2b**, client, CI-blind). Gauge: `SurveyRevealTests`
   (surveyor sees geography + Partial deposits; non-surveyor stays fully fogged; world-level `Surveyed` untouched).
3. **The recon component + reveal-on-move:** a ground-side recon `*Atb` (Sensors) the assembler mounts; as a unit
   carrying it enters a hex, `Reveal(faction, hex)` fires + **unmasks that hex's deposit assay**. Gauge: a scout walking
   a hex flips it from masked→assayed for that faction only.
   **✅ BUILT (2026-07-17) — and it was mostly a CONNECT, not a build (Prime-Directive win):** the recon component +
   reveal-on-move ALREADY EXISTED — `GroundSensorAtb` + the base-mod `ground-radar` component (designed/buildable/losable),
   revealed every tick by `GroundSensors.RevealFromUnits` (hooked in `GroundForcesProcessor`; grave rung built-in). Slice 3
   WIRED it into the per-faction fog: alongside the kept world-level reveal it now `RevealRegionFor(faction, region)` and
   unmasks the deposit ASSAY (`RevealDepositAssay(mask)` → Full/exact) of deposit hexes in the region a scout STANDS in
   (boots-on-the-deposit). Gauge: `GroundSensorsTests.RadarScout_RevealsPerFaction_AndUnmasksDepositAssay`. Refinement
   (flagged): unmask is region-granular where the scout stands, not strictly per-walked-hex; per-hex-walked is a follow-up.
4. **Hidden special sites:** sites exist on hexes but are invisible until a scout reveals them (the exploration payoff).
   Gauge: a site is unlisted for an un-scouted faction, listed after a scout reaches it.
5. **Fog-limited enemy garrison read:** the landing-intel helper returns only what the faction has scouted (an
   un-scouted region reads "unknown", not "empty"). Gauge: faction A can't see faction B's garrison in an un-scouted
   region.
6. **Consumers:** the **Kithrin expand loop** (survey→build-surveyor→found rungs on `ExpandResolver`) and the
   **easiest-landing score + FOB** both read the fog-limited picture. Gauge: the AI dispatches a survey then colonizes;
   the landing score ranks a scouted weak region above an un-scouted one.

Each slice is gated/additive so CI stays byte-identical until the consumer slice flips it on (the same discipline the
detection/EMCON stack used).

---

## Connections (Prime Directive)

- **Feeds IN:** `PlanetRegionsDB` (regions/hexes), `GroundHex` (deposits/sites), `GroundForcesDB` (units),
  `GeoSurveyProcessor` (space survey), `GroundForcesProcessor` (unit movement — where reveal-on-move hooks), the
  Component Designer + Entity Assembler (the recon component), `MineralsDB`'s `Masked`/partial-access pattern (the
  model to mirror per-hex).
- **Feeds OUT:** the client surface map (`PlanetViewWindow` — draws only revealed hexes/deposits/enemies — the visual
  fog), the Kithrin `ExpandResolver`, the landing-site AI + FOB, the mining stage (an assayed deposit is what you
  decide to mine).
- **Shares STATE:** the per-faction reveal record is the single source of truth both survey and landing read; the
  existing per-body `GeoSurveyableDB.GeoSurveyStatus` (body-level, per-faction) is the sibling pattern to mirror at the
  region/hex granularity.
- **Grave rung:** lose the scout (recon component destroyed) → lose the reveal reach; a captured/bombed region can turn
  fog back on for the loser.
