# Ground-Unit Designer — Design Record

**Status:** design-locked (core model) + build-in-progress. **As of 2026-07-05.**
**Owner decisions captured from the 2026-07-05 design conversation.**

> Read this before touching any ground-unit *design/assembly* code, and before building any part of the
> ground-unit design UI. It is the single source of truth for how a player creates a ground unit. The
> per-slice detail lives in `GameEngine/GroundCombat/CLAUDE.md`; the surrounding war layer is
> `docs/GROUND-COMBAT-MAP-DESIGN.md`.

---

## 1. The objective (the north star for this system) — **EXACT essence recreation of ANY sci-fi unit**

**The success criterion (the developer's call, 2026-07-05): you can recreate the ESSENCE of *any and every*
sci-fi ground unit you can think of.** Names need not match; the essence must — *exactly*. If there is a unit we
can imagine but cannot build, **the parts bin has a hole, and the designer is not done.** This is not "assemble
from parts" as a nice-to-have; total expressive coverage of the sci-fi design space **is the goal of the system.**

The way we get there: build *any* unit the way you build a ship — by **assembling components** — with "within
reason" enforced by **physics (mass vs. strength)**, never by a category whitelist. A Guardsman, a Space Marine, a
Zergling, a Siege Tank, an AT-AT and a Jedi are all things a player *designs*, not things we hard-code.

The load-bearing principle: **ground units are as LOOSE as ships.** A ship here has no hull class — `ShipDesign`
is just *a name + a list of components + armor*, stats = the **sum of the parts** (`ShipDesign.Recalculate`).
Ground units follow the identical pattern, plus **one rule ships lack** (a carry-capacity gate — §4).

### 1a. The completeness gate — the ESSENCE AXES (how we prove "any unit")

To guarantee *any* unit — not just the ones we happened to list — the parts bin must let the player set each of
these orthogonal **essence axes** independently. A unit is fully specified by where it sits on every axis; if the
bin can't move an axis, a whole class of units is unbuildable. **The designer is "done" only when every axis is
independently expressible.** This table is the standing coverage gate — update it as parts land.

| Essence axis | Spanned by | Coverage (2026-07-05) |
|---|---|---|
| **Scale** — lone soldier ↔ km Titan | Frame Size × build-Count | ✅ |
| **Firepower** — how much hurt | Weapon Attack | ✅ |
| **Range** — melee ↔ orbital artillery | Weapon Range | ✅ |
| **Delivery** — alpha vs sustained vs saturation/AoE, direct vs indirect | Weapon knobs | ⚠️ gap (rate/alpha/AoE) |
| **Damage type** — kinetic/energy/plasma/psychic/bio/EMP | Weapon Mode → resistances | ⚠️ partial |
| **Survivability** — soak / dodge / shield(+regen) / self-heal / numbers | Armor + Augment | ⚠️ gap (regen, self-heal) |
| **Mobility** — speed + foot/tread/walk/**hover/fly/teleport/burrow/jump** | Frame Locomotion | ⚠️ gap (air, teleport, speed) |
| **Role** — line/assault/support/heal/build/scout/EW/AA/transport | Weapon + Utility | ⚠️ gap (support, actions, AA, EW) |
| **Nature** — bio/mech/energy/synthetic/undead/psychic | Frame flavor → healing, morale, env-resist | ⚠️ partial |
| **Economy** — expendable swarm ↔ elite few | part cost × Count | ✅ |
| **Special mechanics** — self-destruct / teleport-deploy / summon / morph / cloak / terror-aura | Utility + flags | ⚠️ gap |
| **Command** — faceless swarm ↔ named hero | Formation + Count | ✅ |

**Read:** ~5 axes fully spanned, ~7 partial/gap. The holes concentrate in **weapon delivery, survivability modes,
mobility modes, roles/utilities, and special mechanics** — the priority work to reach "any unit," matching the
§6a gaps. Closing an axis unlocks a whole class of units. The §6 catalog is how we *find* axis holes; this table
is how we *track* them.

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

## 6. Reference build catalog (the coverage test — **GROWING**)

This is a **living catalog**, not a one-time proof. Its job: keep adding archetypes from across sci-fi until we're
confident the parts bin covers the whole design space — and **the builds that DON'T fit are the point**, because
each one that needs something we don't have reveals a missing part-type, knob, or a scope decision (§6a). Add a
row whenever a new archetype comes up; move the ⚠️ gaps into §6a and resolve or defer them **before locking G-D3**.

Every ✅ build is `Frame + Weapon + Armor + Augment (+Utility/Supply)` with different numbers and a name —
nothing hand-coded.

| # | Unit (source) | Composition (the parts) | What it stresses | Verdict |
|---|---------------|-------------------------|------------------|---------|
| 1 | **Space Marine** (40K) | Human frame · Power Armor (+str) · Bolter (heavy ballistic) · Ceramite plate | augment unlocks a weapon a bare frame can't lift | ✅ covered |
| 2 | **Imperial Knight** (40K) | Huge walker · Battle Cannon (artillery) · Ion Shield (augment) · Adamantium | top end — huge frame swallows huge guns | ✅ |
| 3 | **Zergling** (SC) | Tiny organic frame · Claws (melee) · thin carapace · Adrenal (+evasion) | bottom end; low str is fine (light weapon); tiny carry-size → hordes per dropship | ✅ |
| 4 | **Siege Tank** (SC) | Tracked frame · Siege Cannon (artillery) · composite · **Siege-Mode (utility posture)** | a Utility can carry a stance/posture | ✅ |
| 5 | **AT-AT** (SW) | Huge walker · many mid Lasers (energy) · massive plate | many-mid-guns vs. one-big-gun; same walker template as the Knight | ✅ |
| 6 | **Jedi** (SW) | Human frame · Lightsaber (melee) · **no armor** · Force (augment: +evasion, +shield/deflect) | survive by dodging, not soaking | ✅ |
| 7 | **Ork Boy / Ogryn** (40K) | Big organic frame (high HP, mid str) · Choppa (melee) · thick hide | cheap-tough-melee horde on a *bigger* frame | ✅ |
| 8 | **Artillery Battery** | Static/wheeled frame · Long Gun (artillery, huge range) · minimal armor | extreme range, (near-)immobile — pure indirect fire | ✅ |
| 9 | **Vindicare / Sniper** (40K) | Human frame · Long Rifle (single-shot, huge range, low rate) · light armor · Camo (augment) | one big accurate shot ≠ a machine-gun at the same DPS | ⚠️ **weapon knobs** |
| 10 | **Protoss Zealot** (SC) | Bipedal frame · Psi Blades (melee) · **Plasma Shield that regenerates** | a shield that *recharges* out of combat | ⚠️ **shield regen** |
| 11 | **Terminator** (40K) | Human frame · Tactical Dreadnought Armor (augment) · Assault Cannon · **Teleport** | a unit that **deploys** by teleport/drop-pod (bypasses the dropship) | ⚠️ **deployment methods** |
| 12 | **Combat Engineer / Sapper** | Human frame · sidearm · **Demo Charge + Fortify Kit** | a unit whose job is an **action** (build / repair / demolish), not damage | ⚠️ **action utilities** |
| 13 | **Field Medic / Apothecary** | Human frame · sidearm · **Medical Suite (heals adjacent friendlies)** | a **support** unit that buffs/heals *other* units | ⚠️ **friendly auras** |
| 14 | **Gunship / Valkyrie** | **Hover frame (air)** · door guns · light armor · troop bay | the **air layer**, and a unit that itself **carries** other units | ⚠️ **air + unit-as-transport** |
| 15 | **Kamikaze Drone / Bomber** | Tiny cheap frame · **Warhead (one huge hit, consumes the unit)** | a **single-use / self-destruct** unit | ⚠️ **consumable units** |
| 16 | **Hover Cavalry / Bike** | Fast Hover frame · light gun/melee · — | **speed** as the defining trait — is unit speed a first-class stat? | ⚠️ **per-unit speed** |

### 6a. Coverage gaps surfaced by the catalog (resolve or defer before locking G-D3)

The ⚠️ builds above point at things the current 5-part model doesn't yet express. Each is a design question:

- **Weapon needs more than flat `Attack`** — rate-of-fire / alpha-strike / accuracy, so a sniper (one big shot)
  differs from a machine-gun at the same average DPS. *(Likely: add knobs to `GroundWeaponAtb`, mirroring the ship
  weapon-flavor system.)*
- **Shield regen** — an augment shield that recharges out of combat, not just a flat soak. *(Add a regen knob.)*
- **Deployment methods** — teleport / drop-pod / orbital insertion that bypass the dropship. *(Ties to Transport
  T1; probably a Utility or a frame trait.)*
- **Action utilities** — units that DO things (build fortifications, repair, demolish, capture faster). Needs a
  Utility "effect" vocabulary, not just passive stats.
- **Friendly auras (support)** — heal / buff *nearby friendly* units. A whole support dimension the resolver
  doesn't model yet.
- **Air layer + unit-as-transport** — can ground units engage flyers? can a *unit* carry other units (a gunship,
  an APC)? Big scope decision.
- **Consumable / single-use units** — self-destruct, one-shot. Needs a "consumes self on use" flag.
- **Per-unit speed** — today movement uses region/hex crossing-time; is there a per-unit speed stat that a fast
  frame/augment raises? (Confirm vs. the H2 hex-movement model.)

**None of these block the G-D3 assembler** (it computes stats + enforces the carry gate from the parts that exist).
They're the backlog that grows the parts bin toward "full scope" — pick them off as dedicated slices, each with
its numbers flagged.

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

---

## 11. The SPACE (ship) designer — apply the lessons back (flagged, 2026-07-05)

The developer flagged that **"we'll probably need to fix the space version as well."** The ground designer forced
us to name things the ship designer never made explicit, and once the ground UX is settled we should audit the
ship designer against the same principles. Not yet scoped — recorded so it isn't lost. Candidate items to decide:

- **Part-category clarity.** The ground bin has clean functional types (Frame/Weapon/Armor/Augment/Utility/Supply).
  `ShipDesignWindow` today just lists *all* `ComponentDesigns` grouped by a free-text `ComponentType` string (it
  doesn't even filter to `ShipComponent` mount — ground parts would show up in it until we filter). Worth a clean
  category model + mount filter.
- **The "does it fit / what does it cost" readout.** Ground gets an explicit carry-capacity gate + a live cost
  sum. Ships have the cost sum but no equivalent "budget bar" — should they, and should the two designers present
  it the same way?
- **Generalize-by-function audit.** Ship weapons are already template+knobs (good); check the rest of the ship
  parts follow the same "one general type, not per-flavor" rule.
- **Convergence question (the big one).** Should ship / ordnance / ground designers become **one assembly UX**
  (pick a chassis/hull, drop parts, see budget + emergent stats + cost), or stay three windows? This is the §10
  "how all the component designers work" question, one level up.

**Do not touch the ship designer as part of the ground track** — this is a separate, later effort that starts with
its own design pass. It's here as a pointer, not a to-do for the current slices.
