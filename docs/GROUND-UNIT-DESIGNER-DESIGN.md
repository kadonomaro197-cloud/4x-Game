# Ground-Unit Designer — Design Record

**Status:** design-locked (core model) + build-in-progress. **As of 2026-07-05.**
**Owner decisions captured from the 2026-07-05 design conversation.**

> Read this before touching any ground-unit *design/assembly* code, and before building any part of the
> ground-unit design UI. It is the single source of truth for how a player creates a ground unit. The
> per-slice detail lives in `GameEngine/GroundCombat/CLAUDE.md`; the surrounding war layer is
> `docs/GROUND-COMBAT-MAP-DESIGN.md`.

---

## 1. The objective (the north star for this system)

**Give the player the tools to build *any* ground unit they can imagine — within reason — the same way they
build a ship: by assembling components.** A Guardsman, a Space Marine, a Zergling, a Siege Tank, an AT-AT and a
Jedi should all be things a player can *design*, not things we hard-code. "Within reason" is enforced by
**physics (mass vs. strength)**, never by a category whitelist.

The load-bearing principle (the developer's call): **ground units are as LOOSE as ships.** A ship in this engine
has no hull class — `ShipDesign` is just *a name + a list of components + armor*, and its stats are the **sum of
the parts** (`ShipDesign.Recalculate`). Ground units follow the identical pattern, with **one extra rule ships
don't have** (a carry-capacity gate — see §4).

---

## 2. The core model — a unit is an assembly

A **ground-unit design** = a **frame (chassis)** + a **list of mounted parts** (weapons, armor, augments,
utility, supply). Everything about the unit *emerges from the sum of its parts*, exactly like a ship:

- **Attack** = Σ weapon attack
- **Reach** = the longest-range weapon
- **HP** = frame base HP + Σ armor HP
- **Defense / evasion / shield / toughness** = frame + Σ armor + Σ augment
- **Cost** (credits + minerals + build points) = Σ component cost — *the designer shows this, for free, from the
  same sum the ship designer already does*
- **Transport carry-size** = the unit's assembled mass (this feeds the dropship — see §8)

**No unit "classes."** "Human vs. Titan" is not a type — it's a *frame with different numbers*. (See §3 Chassis.)

---

## 3. The parts bin — five general part types (generalize by FUNCTION, not flavor)

The whole "multitude of applications" is covered by a **handful of general, parametric part types** — *not* a new
class per weapon. There is no `BolterAtb`/`AutocannonAtb`/`LightsabreAtb`; there is **one `GroundWeaponAtb`** with
knobs, and a bolter vs. a beam saber vs. a ship-scale laser is *the same part with different numbers and a name*.

| Part type | Attribute (`GameEngine/GroundCombat/`) | Knobs | Role |
|-----------|----------------------------------------|-------|------|
| **Frame / Chassis** | `GroundChassisAtb` | BaseStrength, BaseHP, Size, Locomotion (Foot/Tracked/Walker/Hover), CarryClass (Personnel/Vehicle) | The body. Its **strength IS the carry-capacity budget**. Continuous numbers → human, mech, Titan are all this. |
| **Weapon** | `GroundWeaponAtb` | Mass, Attack, Range (hexes), Mode (Melee/Ballistic/Energy/Artillery) | Firepower. Its **Mass is what the frame must bear**. |
| **Armor** | `GroundArmorAtb` | Mass, HP, Defense | Survive-by-soaking. |
| **Augment** | `GroundAugmentAtb` | Mass, **StrengthBonus**, EvasionBonus, ToughnessBonus, Shield | Survive-by-dodging + **the strength-unlock**. Power armor, the Force, adrenal glands, thrusters, energy shields are all this. |
| **Supply** *(planned)* | `GroundSupplyAtb` | Mass, Endurance/Ammo | Provisions & ammo — trades combat gear for staying power. |
| **Utility** *(planned)* | `GroundUtilityAtb` | Mass, effect | Sensors, targeting, and **postures** (e.g. Siege-Mode deploy = trade mobility for range → hooks the stance system). |

Every part is a real component (`CONVENTIONS.md` §6): designed, researched, built, installed, saved — the
ship-component machine for free. Parts carry the new `ComponentMountType.GroundUnit` flag so they stay out of the
ship/colony build lists, and the ground designer filters to them.

---

## 4. The gate — the one rule ships don't have

A frame can only carry so much. Two limits, both **computed** (not a per-class constant):

1. **Total capacity:** Σ mounted-part mass ≤ **carry-capacity** = frame `BaseStrength` + Σ augment `StrengthBonus`.
2. **Per-item limit:** any single part's mass ≤ **max-liftable** (a fraction of the carry-capacity) — the
   *"a bare human can't shoulder a 1000-lb autocannon"* rule.

**Augments raise strength**, which is *why* the fiction works: a bare Human Frame (strength 100) can't lift a
heavy autocannon (mass 120); bolt on **Power Armor (+300 strength)** and it can. That single fact — captured in
the base-mod Power Armor part — is the whole "Space Marine" story, and it's pure physics, so the player discovers
it by trying, not by reading a rules table. A Titan mounting a laspistol is trivially under budget: silly but
allowed. **"Anything within reason" = anything the mass-vs-strength numbers permit.**

---

## 5. Build quantity — one design, one controllable "bunch"

The bloat trap ("100 single infantry is foolish") is solved by separating *design* from *quantity*:

- A **design** describes **one element** (one zergling, one Marine, one mini-Gundam).
- When you click build, a **quantity** says how many are in the batch.
- The result is **one `GroundUnit` carrying a Count** — commanded as a single object, its firepower/HP scale with
  the count, and combat attrites the count (you watch *"1000 → 640 zerglings left"*). **One object, not 1000.**
- `GroundFormation`s group multiple bunches (a formation of [1000 zerglings] + [50 hydralisks]).

So *"an army of mini-Gundams"* = design **one** Mini-Gundam (referencing ~4 parts you designed once) → build N →
one bunch. Zero per-unit and zero per-component-per-unit work. **Three levels of reuse:** part *templates* →
part *designs* (instantiated once) → *unit designs* (assembled once) → built N.

---

## 6. Proof the model holds — 6 units, 3 franchises, one parts bin

Every unit below is `Frame + Weapon + Armor + Augment` with different numbers and a name — **nothing hand-coded**:

| Unit | Frame | Weapon | Armor | Augment | What it proves |
|------|-------|--------|-------|---------|----------------|
| **Space Marine** (40K) | Human | Bolter (heavy) | Ceramite | **Power Armor (+str)** | augment unlocks a weapon a bare frame can't lift |
| **Imperial Knight** (40K) | Huge walker | Battle Cannon | Adamantium | Ion Shield | top end — huge frame swallows huge guns |
| **Zergling** (SC) | Tiny organic | Claws (melee) | thin carapace | Adrenal (+evasion) | low strength is fine (light weapon); tiny carry-size → hordes per dropship |
| **Siege Tank** (SC) | Tracked | Siege Cannon | composite | **Siege-Mode (utility posture)** | a Utility can carry a stance |
| **AT-AT** (SW) | Huge walker | many mid lasers | massive plate | — | same walker template as the Knight, wildly different look |
| **Jedi** (SW) | Human | Lightsaber (melee) | **none** | **Force (+evasion, +shield/deflect)** | survive with no armor — dodging, not soaking |

---

## 7. Reconciliation with the earlier fixed-stat units (A1/A2)

Slices A1/A2 shipped 3 base-mod units as **single fixed-stat `GroundUnitAtb` components** (infantry/armor/
artillery). Those are **not wasted** — under the assembler they become the **default pre-built loadouts** (e.g.
Guardsman = Human Frame + Rifle; Marine = Human Frame + Power Armor + Autocannon), so a New Game still ships with
ready units. The build→raise hook (a built unit-design raises a `GroundUnit` on the colony's planet) is unchanged;
only the *source of the stats* moves from a fixed number to the assembled sum.

---

## 8. Connections (Prime Directive)

- **Transport (T1):** a unit's **carry-size for a dropship = its assembled mass** — the designer feeds the
  invasion system directly (replaces the interim per-type carry-size hack in `GroundTransport`). The frame's
  `CarryClass` (Personnel/Vehicle) picks which bay hauls it.
- **Stance/ROE:** a **Utility posture** (Siege-Mode) is the same lever as `GroundFormationDoctrine` — utilities
  aren't just passive stats.
- **Dodge model:** augment **EvasionBonus** rides the same evasion currency the *ship* dodge resolver uses.
- **Industry/research:** parts are components → they build from minerals and gate behind tech, cradle-to-grave.
- **Combat resolver:** the assembled Attack/Defense/HP/Range are what `GroundForcesProcessor.ResolveRegionCombat`
  already consumes — no change to the resolver, just where the numbers come from.

---

## 9. Build roadmap (one slice per push, CI-gated)

| Slice | What | Status |
|-------|------|--------|
| **G-D1** | The 4 general part attributes (`GroundChassisAtb`/`GroundWeaponAtb`/`GroundArmorAtb`/`GroundAugmentAtb`) + construction gauge | ✅ built |
| **G-D2** | Base-mod parts (Human Frame, Service Rifle, Composite Plating, Power Armor) + `ComponentMountType.GroundUnit` + JSON→atb gauge | ✅ built |
| **G-D3** | **The assembler:** a unit design = frame + parts; stats + cost summed; **the capacity + max-item gate enforced**; + vehicle frame / autocannon / cannon parts; + the 3 default loadouts as real assemblies | ⏳ next |
| **G-D4** | **The client design window** — assemble a unit like a ship (pick frame, add parts, see the gate + emergent stats + cost); build with a quantity | ⏳ |
| G-D5+ | Supply/ammo parts; the build-quantity → `GroundUnit.Count` "bunch"; Utility postures; more parts | ⏳ |

---

## 10. STILL TO DESIGN (open — do not assume these are decided)

The developer flagged (2026-07-05) that **how the component designers actually work is not yet designed.** Open
questions to settle *before* G-D4:

- **Where does part design live?** Do ground Weapon/Armor/Augment/Frame get their own designer windows, or share
  the existing `ComponentDesignWindow` (filtered to `GroundUnit` mount)? How does the player set the knobs?
- **The assembly UI.** How does the player pick a frame and drop parts onto it? How is the **carry gate shown**
  (a mass bar that fills; a red "over budget" state; a greyed-out too-heavy part)? Mirror `ShipDesignWindow`?
- **Max-item fraction.** What fraction of carry-capacity is the per-item limit (a hard number to pick + flag)?
- **Hard caps.** Is there any ceiling augments *can't* exceed, or is it purely additive? (Leaning additive.)
- **Quantity UX.** Where does the build-quantity slider live, and how does the "bunch"/Count read out in the map
  and combat?
- **The broader "all the component designers" question** — whether the ship / ordnance / ground designers should
  converge on one assembly UX, or stay separate. Not yet scoped.

**Rule:** every new gameplay number introduced by any slice above (part stats, gate fractions, costs) is put in
JSON as a sensible default and **flagged for the developer in the slice report** — never silently hardcoded.
