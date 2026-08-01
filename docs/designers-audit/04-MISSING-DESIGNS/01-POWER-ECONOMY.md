# Missing design 1 — THE POWER ECONOMY

> **What it is, in one breath:** right now, almost nothing on a ship *costs power to run*. Only the warp
> drive declares a standing power demand, and only energy weapons drain the battery when they fire. Shields
> recharge for free. Sensors cost nothing to keep listening. And a **colony can't generate power at all** —
> there's no building that produces it. This design adds the one missing piece that ties the whole power
> system together: **a generic "runs on power" dial every active component gets**, a colony power producer,
> and shields/sensors that actually draw on the reactor. It answers cracks **C12** (no generic power-draw),
> **C15** (shields free), and build **E-build-11** (no colony power).
>
> **Why this is the most-connected gap:** power touches weapons (can I fire?), propulsion (can I warp?),
> defense (can I hold shields?), sensors (can I stay lit up?), and the whole colony morale loop (a
> power-short colony loses morale). Fixing it lights up more of the other ten designers than any other single
> change.

---

## 1. The input surface (verified in source)

The power blob already exists and is rich — `Energy/EnergyGenAbilityDB.cs`:

| Field | Meaning | Who writes / reads it |
|-------|---------|-----------------------|
| `MaxOutputFromReactor` / `MaxOutputFromSolar` (:22,24) | the two supply sources (kW) | written by `EnergyGenerationAtb` (reactor) / `EnergySolarGenerationAtb` (solar) at install |
| `TotalOutputMax` (:17-20, computed) | total supply = reactor + solar | **read by `SustenanceProcessor.cs:60`** (colony power-shortage morale term) |
| `Demand` (:29, `AddDemand` :45-50) | the aggregated **standing** draw (kW) | written only by **warp** (`WarpMoveProcessor.cs:246 AddDemand(BubbleSustainCost)`); netted in `EnergyGenProcessor.cs:59-64` (`Output = capacity − Demand`) |
| `Output` (:44) | net available power (kW) | the emergent result of supply − demand |
| `Load` (:38) | Demand ÷ TotalOutputMax (0–1) | read by `EmconActivityProcessor` for reactor-heat signature (gated off) |
| `EnergyStored` / `EnergyStoreMax` (:56,61, kJ) | the **battery** (per energy type) | **burst** consumers subtract directly: energy weapons (`GenericFiringWeaponsProcessor.cs:69-70`), warp create-cost gate (`WarpMoveCommand.cs:265`) |
| `LocalFuel` / `IsFuelStarved` (:63,72) | the reactor's fuel (dormant gate) | `EnergyGenProcessor` when `EnableFuelExhaustion` on |

**The two existing draw mechanisms — this is the whole design in embryo:**
1. **Standing demand** — warp calls `AddDemand(BubbleSustainCost)`, which accumulates into `Demand`, and the
   processor nets `Output = TotalOutputMax − Demand` every tick. *A continuous cost.*
2. **Burst draw** — a weapon subtracts a lump straight out of `EnergyStored` when it fires, and the reactor
   recharges the battery between shots. *A per-use cost.*

**Everything else that should cost power uses neither.** Shields regen free (`CombatKernel.ResolveShield`
reads no power). Active sensors draw nothing. Life support draws nothing. And **no colony template grants
`EnergyGenAbilityDB`** — so a colony's `TotalOutputMax` is 0 and the power-shortage morale wire can never
come alive.

---

## 2. What already exists (extend, don't rebuild)

- The **Power designer** (`powerderived.html`) already produces `EnergyGenerationAtb` / `EnergySolarGenerationAtb`
  (reactor/RTG/turbine/solar) with the Generate/Collect door — the **supply** side is done.
- The **balance** is done: `EnergyGenProcessor` nets `Demand` against supply and drains/charges `EnergyStored`.
- The **two draw shapes** exist (standing via `AddDemand`, burst via `EnergyStored` subtract).

**So this is ~70% built.** The missing 30% is: (a) a *generic* way for any component to declare a draw
(today only warp/weapons are hard-wired), (b) making a colony able to carry the supply blob, (c) pointing
shields/sensors at the draw.

---

## 3. The door and the dials (derived, not invented)

Per the North Star: the **forced choice** is a door; the **free number** is a dial.

### The door — DRAW SHAPE (derived from the two existing mechanisms)
Every active component that runs on power answers **one forced question**: *is my draw STANDING or BURST?*
- **Standing** (continuous): nets against `Output` every tick — life support, active sensor, held shield,
  warp sustain. Reproduces the warp `AddDemand` path.
- **Burst** (per-use): drains a lump from `EnergyStored` on each use, recharged between uses — a weapon shot,
  a sensor ping, a jump. Reproduces the weapon path.

This is a *real* door because the two shapes read **different** sim variables (`Demand` vs `EnergyStored`) and
behave differently under a brownout (a standing load is shed continuously; a burst load waits for the battery
to recharge). It is not two names for one thing.

### The dial — POWER DRAW (kW for standing, kJ-per-use for burst)
A single number: how much power this component needs. **Intrinsic test: PASS** — you can set "this radar
draws 400 kW" knowing only the radar, nothing about the ship it's bolted to. So it is a legitimate
**component dial**, exactly like the existing `WeaponSupply.PowerDraw_W` but generalized to every component.

### What stays EMERGENT (shown, never set)
- **Net available power** (`Output = TotalOutputMax − ΣDemand`) — needs the whole entity. A readout.
- **Load %** — emergent. A readout (and the signature driver, design 4).
- **Endurance** (how long the battery lasts under a burst pattern) — emergent from draw × store.

**The whole model in one sentence:** *a component declares a draw (dial) with a shape (door); the assembler
sums the draws; the reactor's supply minus the standing draws is the net (emergent); a brownout sheds
standing loads and starves burst loads until the battery recharges.*

---

## 4. Colony power (E-build-11) — the same supply blob, mounted on a colony

The colony half is small: **a power-generation component that grants a colony an `EnergyGenAbilityDB`.**
`SustenanceProcessor.cs:60` already reads `TotalOutputMax` off the colony; it just always finds 0 because no
colony building carries the blob. The `solarArray` template is already `PlanetInstallation`-mountable — the
fix is to confirm it (or a new "power plant" building) installs `EnergyGenAbilityDB` with a nonzero
`MaxOutputFromSolar`/`MaxOutputFromReactor`. **This is the hard prerequisite for the power half of A-flip-2**
(the colony power-shortage morale wire cannot bite until a colony can produce power).

The Power designer already makes the component; the only question is **mount** — this is the same "generalize
the host off ships" move the off-world-infrastructure doc calls for. A reactor on a colony is the same
`EnergyGenerationAtb`, just installed on a `PlanetInstallation` instead of a `ShipComponent`.

---

## 5. Shields draw standing power (C15)

With the generic standing-draw in place, **shield regen becomes a standing draw**: a held shield declares a
kW draw that nets against `Output`; under a brownout the shield can't hold. Today `CombatKernel.ResolveShield`
regenerates purely from `regen × dt` with no power read — this design makes the regen *conditional on
available power* (scale regen by `min(1, Output / shieldDraw)`), so a browned-out reactor drops your shields.
That's the missing Power→Defense wire, and it's the decision the design creates: **do I over-build the
reactor so I can hold shields *and* fire, or do I gamble that I won't need both at once?**

---

## 6. The price (no free capability)

The draw dial itself is a *demand*, so it "costs" nothing on the drawing component — **the cost is on the
supply side**: to meet a bigger total draw you install a bigger reactor, which costs more **mass** (Chassis
budget), more **crew**, and more **fuel** (`LocalFuel` burns faster at higher `Load`). That is the honest
tradeoff and it needs no new pricing mechanic — it rides the reactor's existing mass/crew/fuel cost. The one
new *engine* cost is the netting itself, which already exists for warp.

---

## 7. Worked reproduction (prove it rebuilds what exists)

- **Warp sustain** = a *standing* draw of `BubbleSustainCost` kW → nets against `Output`. ✅ reproduces
  `WarpMoveProcessor.cs:246` exactly.
- **Energy weapon shot** = a *burst* draw of `costKJ` from `EnergyStored`, recharged between shots. ✅
  reproduces `GenericFiringWeaponsProcessor.cs:69-70`.
- **Warp departure** = a *burst* gate: `EnergyStored ≥ BubbleCreationCost`. ✅ reproduces `WarpMoveCommand.cs:265`.

All three fall out of the two-shape model as special cases — which is the North Star proof that the
generalization is right.

---

## 8. Cradle to grave

**fissile mineral** (mined) → **fuel** (refined, `fissile-fuels`) → **reactor component** (designed in the
Power designer, gated by power tech) → **installed** on ship or colony → **the power-budget decision** (what
can I run at once — fire, warp, and shields, or pick two?) → **reactor destroyed** → `TotalOutputMax` drops →
standing loads shed, shields fall, weapons go quiet: a component-level loss that *matters* and sends you back
to re-build. Every rung exists today except the middle "runs-on-power decision," which is exactly what this
design adds.

---

## 9. Gauge & blast radius

- **Gauge:** (a) a ship whose summed standing draw exceeds `TotalOutputMax` shows `Output ≤ 0` and sheds
  load; (b) a colony with the power building has `TotalOutputMax > 0` and its power-shortage morale term
  reads 0; (c) a brownout drops shield regen (a shielded ship under-powered loses its shield over time).
- **Blast radius:** the generic draw is *additive* — with no component declaring a draw, `Demand` is
  unchanged and behaviour is byte-identical (same guard the warp path uses). Turning on shield-draws and
  colony-power is where balance shifts; gate each behind its own step and re-baseline. Ships must not suddenly
  brown out on turn 1 — so ship reactors must be sized to the *standing* draws before shields are wired to
  power (sequence: generic draw → colony power → shield draw, per Phase 3 waves).

---

*Design 1 of 5. This is the keystone — most of the other four touch it. Next: settling a world.*
