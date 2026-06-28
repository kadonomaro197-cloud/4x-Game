# Space Hazards — Subsystem Reference

**What it does:** Puts *terrain* into space. A "space hazard" is a circular region that changes how ships behave while they're inside it. Two flavours ship today, built on one shared component:

- **Gas cloud** — permanent terrain in some (non-home) systems. Inside it your sensors are cut, sub-light movement is dragged, warp crawls, and your hull slowly corrodes. A place to hide, get ambushed, or have to cross.
- **Solar flare** — the home star's *weather*. On a schedule the star erupts a flare that grows near it, blinds sensors in the area and irradiates any ship caught there, then fades and is gone.
- **Star corona** — a permanent danger zone hugging every star. Heat damage that **scales with proximity** following **real radiative flux (∝ 1/dist²)** between the star's surface (`SpaceHazardDB.InnerRadius_m`) and the zone edge — max at the surface, near-zero across the outer corona, climbing steeply only as you close on the star. So a normal planetary orbit (Mercury at 0.39 AU is well outside the ~0.12 AU zone) takes **zero**, the outer corona is a warning band, and only a genuine close dive cooks. Uses `HazardEffect.ScalesWithProximity` + `SpaceHazardProcessor.ProximityIntensity`.

**Why it matters:** before this, empty space was truly empty — planets are tiny dots separated by astronomical gaps and nothing filled them. Hazards give the map geography to fight, hide and route around, which is what makes movement and detection *decisions* instead of straight lines.

---

## Files

| File | Role |
|------|------|
| `HazardEffect.cs` | The typed effect vocabulary — `HazardEffectType` (HeatDamage / RadiationDamage / KineticDamage / SensorJam / MovementDrag / WarpInhibit) + the `HazardEffect` (type + magnitude + wavelength + scales-with-proximity). This is the SPINE: a hazard is a list of these (data); a counter resists by kind (data). **+ the keystone link (2026-06-28):** a derived `[JsonIgnore] Signature` maps each *damage* kind to the coarse shared `Damage.DamageSignature` (HeatDamage→Thermal, RadiationDamage→HardRadiation, KineticDamage→Kinetic; stat kinds → null) via `HazardEffect.SignatureFor`. That's the shared hazard↔weapon flavour word — see `Damage/CLAUDE.md` → "The DamageSignature keystone". |
| `SpaceHazardDB.cs` | The shared component (and `SpaceHazardType` enum, now incl. `Generic` for JSON-authored). A circular region — centred on the entity's `PositionDB`, reaching `Radius_m` — holding a **list of typed `Effects`** (not fixed knobs). `MultiplierFor(kind)` / `BlindsSensors` derive from the list. Plus transient-flare lifecycle fields. |
| `HazardResistanceAtb.cs` | The generic COUNTER: one `IComponentDesignAttribute` that resists an effect kind by a fraction. A new counter is a new component TEMPLATE (data), not new C#. |
| `StarFlareSourceDB.cs` | Marks a star as able to flare and holds its schedule (`NextFlareTime`, mean gap, duration, peak radius). |
| `SpaceHazardTools.cs` | Read-side query. `CombinedAt(system, position)` / `CombinedForEntity(entity)` → a `HazardModifiers` (multipliers multiply, damage sums, blind = any). This is how the sensor/warp code asks "is this point inside a hazard, and what does it do?" |
| `SpaceHazardFactory.cs` | Builds the hazard entities — `CreateGasCloud(...)` and `CreateSolarFlare(...)`. |
| `SpaceHazardProcessor.cs` | `IHotloopProcessor` (5 s, keyed to `SpaceHazardDB`). Grows/fades/expires transient flares, and applies the **per-tick** effects — hull DAMAGE (via the wired `DamageProcessor.OnTakingDamage` path) and Newtonian DRAG — to ships inside. `FlareRadiusAt` (pure, tested) is the grow→peak→fade shape. |
| `SolarFlareProcessor.cs` | `IHotloopProcessor` (1 h, keyed to `StarFlareSourceDB`). When the clock reaches a star's scheduled time it erupts a flare and rolls the next one off the system RNG. |
| `FactionHazardKnowledgeDB.cs` | Per-faction KNOWLEDGE store: the set of `DamageSignature`s the faction has discovered. The thing the research loop gates on (you can't research a counter to a flavour you've never met). Lives on the faction entity, created lazily on first discovery, save/loaded. |
| `HazardDiscovery.cs` | The survey→discover→research connection. `RecordAndAnnounce(ship, hazDb, now)` (called from `SpaceHazardProcessor` for ships inside a hazard) records the hazard's signatures into the owner faction's knowledge; on a NEW discovery it fires an `EnvironmentalHazardDiscovered` event AND `FactionDataStore.Unlock(CounterTechFor(sig))` to open the counter-research (a safe no-op until that tech exists in data). Thread-safe (one lock — discovery is rare and reachable from parallel per-system processors). |

---

## The complete loop — survey → discover → research → rated armour (cradle to grave, 2026-06-28)

The hazard discovery/resistance/research loop the developer asked for is wired end to end:

1. **Survive/encounter** — a ship inside a hazard takes damage stamped with the hazard's `DamageSignature` (the keystone flavour: corona heat → Thermal). Armour rated against that flavour (`DamageResistBlueprint.SignatureResistance`) takes less — and a ship is hit **as the material it's actually clad in** (the `ComponentPlacement` IDCode wiring, `Damage/CLAUDE.md`).
2. **Discover** — `HazardDiscovery.RecordAndAnnounce` records the flavour into the faction's `FactionHazardKnowledgeDB`, fires an `EnvironmentalHazardDiscovered` notification, and `Unlock`s the counter-tech.
3. **Research** — the counter-tech (`tech-thermal-shielding`, in the new **Stellar Science** category, `techCategories.json`/`techs.json`) was LOCKED; discovery opens it. Completing it `Unlock`s the rated armour + its build material.
4. **Build & clad** — the rated armour (`nickel-steel-armor`, made of `nickel-steel` — built from iron/nickel/chromium) is now buildable; its material's `DamageResistBlueprint` (`damageResistance.json` IDCode 50) carries thermal `SignatureResistance`, so a ship clad in it survives the corona. Lose the ship → re-build; the knowledge persists (save/loaded), the armour is re-buildable.

**Gauge:** `Pulsar4X.Tests/HazardResearchLoopTests.cs` runs the whole chain on a real faction through the New-Game data path: locked → discover → open → research → armour unlocked → its material resists thermal.

**ALL SIX flavours are now wired (source → discover → tech → rated armour), every one gauged in `HazardResearchLoopTests`:**

| Flavour | Source (real, in-game) | Damage path | Counter tech → armour (IDCode) |
|---|---|---|---|
| **Thermal** | star corona | wavelength (IR) | `tech-thermal-shielding` → nickel-steel (50) |
| **HardRadiation** | solar flare (UV) | wavelength (UV) | `tech-radiation-shielding` → tungsten-plating (75) |
| **Kinetic** | debris field (~25% of systems) | wavelength (0) | `tech-ablative-plating` → ablative-composite (125) |
| **Corrosive** | gas cloud (reclassified from the mislabelled HeatDamage) | **flat site** | `tech-corrosion-plating` → corrosion-resistant-alloy (175) |
| **EMStorm** | ion storm (`CreateIonStorm`, ~15% of systems) | **flat site** | `tech-em-hardening` → em-shielding-mesh (185) |
| **Gravimetric** | gravimetric anomaly (`CreateGravimetricAnomaly`, ~8%, proximity-scaled) | **flat site** | `tech-structural-reinforcement` → reinforced-trusswork (195) |

**The flat non-wavelength damage SITE (2026-06-28)** — the three non-wavelength flavours (Corrosive/EMStorm/Gravimetric) can't run through the per-pixel wavelength sim, so `DamageProcessor.ApplyNonWavelengthDamage` applies the hit **flat** and spread evenly across the ship's components, **reduced by the ship's armour-material `SignatureResistance[sig]`** — so "the armour material IS the counter" holds for all six. Routed by `DamageProcessor.OnTakingDamage` via `DamageSignatures.UsesWavelengthArmorPath(sig)` (false → flat site). **Default-identical** for every existing caller: a beam/missile/kinetic-hazard fragment carries a wavelength-path signature (Kinetic is the struct default), so the branch never fires for them. The three new `HazardEffectType` kinds (`CorrosiveDamage`/`EMDamage`/`GravimetricDamage`) are **appended** to the enum (gotcha #10). Gauge: `DamageSignatureResistanceTests.NonWavelengthHit_FlatSite_RatedArmourTakesLess`.

**v1 simplifications (documented, flagged — not bugs):** the flat site spreads damage evenly (a field effect) and does health attrition only (no full destruction bookkeeping). Per-flavour targeting is a later refinement: EM should hit **electronics/sensors** (not hull), Gravimetric should scale by **hull size** (tidal), Corrosive should eat the **surface** over time. The decision/loop is fully wired; the damage *fidelity* of the trio is the next polish pass.

---

## Player agency — the DECISION the hazard creates (read this before tuning damage)

A hazard that just deals unavoidable damage is "pretty, not a decision" — the anti-pattern `docs/REALISM-VS-GAMEPLAY-AUDIT.md` warns against. The hazard earns its keep only as the source of a stacking decision. Three layers give that:

1. **See it → avoid it (navigation).** Every hazard draws as a ring on the map, and the corona's damage **scales with proximity** — the outer edge is a low-damage *warning band* you can pull out of, not an instant-death wall. The decision: take the shortcut through/near it vs. route around (time/fuel vs. risk).
2. **Prepare → survive it (design + research).** Hazard damage carries a **wavelength** (`DamageWavelength_nm` — corona/cloud = infrared/heat, flare = UV/radiation), so it runs through the **same armour wavelength-absorption model as a beam weapon** (`DamageTools.DealDamageEnergyBeamSim`). That means **the armour material you clad a ship in IS its hazard defence** — heat-reflective armour survives the corona where an unshielded hull cooks. This is deliberately NOT a bespoke "shield" system: per `CONVENTIONS.md` §6 (abilities are components) the armour the player already designs/researches/builds/loses is the lever, so it inherits the whole cradle-to-grave chain for free. More armour (thickness) + the right material (band) both help, and both stack with the weapon triangle.
3. **A reason to go there (risk/reward).** Avoidance is only a *decision* if there's a payoff for taking the risk — a shortcut, a hiding spot (the gas cloud's sensor cut), a resource near the star, a barrier enemies can't cross without shielding. Without a payoff the hazard is just a "don't go here" wall. **This layer is design-only so far** (the corona/cloud don't yet gate a reward) — flagged as the next slice.

**The spine is BUILT (the generic effect ↔ counter pairing).** Hazards are now a typed `Effects` list (data), and the non-damage effects (SensorJam / MovementDrag / WarpInhibit) are countered by the generic `HazardResistanceAtb` component:
- `SpaceHazardTools.ResistanceFraction(ship, kind)` sums every installed `HazardResistanceAtb` for that kind, **scaled by component health** — a destroyed module gives nothing (the grave rung, for free; read live, no ReCalc). `ApplyResistance(mult, resist)` shrinks the hazard's stat-cut.
- The consults apply it: `SensorScan` (a hardened ship sees through a jam/blind), `WarpMoveProcessor` (a stabiliser shrinks warp inhibition), `SpaceHazardProcessor` (drag reduced by resistance).
- **A new hazard is data** (`SystemBlueprint.Hazards` JSON — typed effects + a polar placement; see Alpha Centauri's `Centauri Gas Cloud`). **A new counter is data** (the six-point component chain — `electronics.json` template → `componentDesigns.json` → `earth.json` StartingItems + ComponentDesigns; the `sensor-hardening-module` is the worked example, reusing the ONE generic Atb, so zero new C#).

**Flagged follow-ups:** more counter components as pure data (a Drive Reinforcement vs MovementDrag, a Warp Stabiliser vs WarpInhibit — each a JSON template); **research-gate** them (move the template from `StartingItems` to a tech's `Unlocks` — one JSON move; v1 ships them unlocked); a **per-ship "survivability in this hazard" readout** (the INFORMATION-DELTA principle); and the **risk/reward payoff** for entering a hazard (so avoidance is a real decision, not always-correct).

---

## How the effects are wired (Prime Directive — the connections)

Two kinds of effect, applied two different ways:

- **Per-tick effects (damage, drag)** are *pushed* by `SpaceHazardProcessor`: each tick it finds ships inside each hazard and applies them. Damage rides the existing `DamageProcessor` path (same as a missile hit). Drag scales `NewtonMoveDB.CurrentVector_ms` — so it only bites a ship in **free-flight** (coasting/thrusting through the medium), not one in a stable Kepler orbit.
- **Query-time effects (sensor cut, warp slow)** are *pulled* by the systems they affect, via `SpaceHazardTools`:
  - **Sensors** — `SensorScan` reads the observer's hazard mods and gates contact registration: blinded (flare) → no contacts; sensor-cut (gas cloud) → contacts beyond the observer's *reduced* reach (using the `SensorTools.DetectionRange_m` reverse-solve) are dropped. **Default-identical when no hazard is present**, so existing detection/fog tests are untouched.
  - **Warp** — `WarpMoveProcessor.StartNonNewtTranslation` scales warp speed by the hazard at the ship's departure point (clamped to a crawl, never 0, so the transit-time division can't blow up).

**Placement** lives in `Galaxy/StarSystemFactory.cs`: `LoadFromBlueprint` gives the home star a `StarFlareSourceDB` (first flare within a month); `CreateSystem` gives every generated star one and drops a gas cloud in ~40% of generated systems.

**Rendering** is client-side (CI-blind): `SystemMapRendering.UpdateHazardRegions()` draws each region as a coloured `SimpleCircle` (gas cloud green, flare orange), rebuilt each frame so a growing flare re-reads its size.

---

## Gotchas

1. **Why `StarFlareSourceDB` is separate from `StarInfoDB`.** Only ONE hotloop processor may key a given DataBlob type (`ProcessorManager` is `Dictionary<Type, IHotloopProcessor>`), and `StarInfoDB`'s slot is already taken by the combat battle-trigger. So flare scheduling hangs off its own marker blob on the star.

2. **Drag only affects `NewtonMoveDB` ships.** A ship in a stable orbit (`OrbitDB`, no `NewtonMoveDB`) is *not* slowed — it isn't "ploughing through" the medium in the thrust sense. This is deliberate; don't "fix" it by touching the Kepler/orbit integrator (it's known-fragile — see `Movement/`).

3. **Warp slow is keyed off the DEPARTURE point only (v1).** A ship that starts outside a cloud and warps *through* it isn't slowed; a true block (`WarpSpeedMultiplier` 0) is clamped to a crawl rather than refusing the order. Both are flagged follow-ups.

4. **Damage calibration is borrowed.** `DamagePerSecond` feeds `DamageFragment.Energy` on the same scale as beam/missile damage (~1 point / 100 J, 1000 = 100% health). Gas cloud 50 J/s ≈ slow attrition; flare 500 J/s ≈ noticeable. Tune in the factory if combat damage gets recalibrated.

5. **Corona damage is inverse-square, NOT linear (2026-06-28 — "make the damage make sense").** Proximity-scaled damage uses `SpaceHazardProcessor.ProximityIntensity(dist, outerR, innerR)`: with an `InnerRadius_m` (the star's surface) it models radiative flux ∝ 1/dist², normalised to 1 at the surface and 0 at the zone edge — so the outer ~90% of the corona is nearly harmless and damage concentrates within a few stellar radii (the physically-honest "only a close dive cooks"). Magnitude (800 J/s at the surface) is the *peak*; the curve scales it down hard with distance. A hazard with **no** `InnerRadius_m` (e.g. a gas cloud, which uses flat damage anyway) falls back to the original linear band, so this is back-compat. Gauge: `HazardDamageCalibrationTests` (Mercury = 0; concentrated near the star; linear fallback). **The sentiment generalises:** any new proximity hazard around a body should set its `InnerRadius_m` so its damage falls off physically rather than as a flat disc.

6. **Hazard hits carry a `DamageSignature` (the keystone).** `SpaceHazardProcessor` stamps each damage `DamageFragment` with the effect's flavour (`HazardEffect.Signature`, e.g. corona heat → Thermal), so armour rated against that flavour (`DamageResistBlueprint.SignatureResistance`) resists it — the same label a matching weapon carries. See `Damage/CLAUDE.md` → "The DamageSignature keystone".

---

## Tests

`Pulsar4X.Tests/SpaceHazardTests.cs` (engine-only → CI-green):
- `GasCloud_AffectsInsideOnly` — the region query bites inside the radius and nowhere else.
- `FlareRadius_GrowsToPeakThenFades` — the flare shape.
- `HomeStar_HasFlareWeather` — placement wiring (Sol's star gets a `StarFlareSourceDB`).
- `Flare_BlindsThenExpires` — a flare blinds sensors at the star and is removed once it expires.

`Pulsar4X.Tests/SpatialEnvironmentsDioramaTests.cs` (engine-only → CI-green): **the whole-system verification** — parks a probe ship at each of six greedily-far-apart bodies in Sol, drops a DIFFERENT hazard on each (one per flavour, each parented to its body so it tracks the probe), drives the `SpaceHazardProcessor`, and asserts the faction DISCOVERS all six signatures — printing an environment→detected readout. The developer's "fill the system with test ships, let them sit, ensure the game catches it all" as a permanent CI gauge: if any spatial environment silently breaks, this goes red and names it. (Drives the processor directly, like `Flare_BlindsThenExpires` — the harness doesn't reliably auto-fire hotloops.)
