# Litmus → 100% — the consolidated fix list for the Marine AND the Venator (2026-08-01)

> **What this is:** everything that has to be fixed or added to take **both** franchise builds — the Blood
> Angels Assault Marine (`LITMUS-BLOOD-ANGELS-BUILD.md`, ~70%) and the Venator-class Star Destroyer
> (`LITMUS-VENATOR-BUILD.md`, ~65%) — to **100%**, deduplicated into one list, with recommendations. Several
> gaps are **shared** — one fix lifts both. Every item maps to the interconnection audit's plan
> (`docs/designers-audit/03-CORRECTION-PLAN.md`) or the ground gap ledger
> (`docs/LITMUS-BLOOD-ANGELS-SQUAD.md`) — this is not net-new work, it's the *franchise-accuracy view* of the
> same backlog.
>
> **The pattern both builds share:** each one is strong exactly where its combat system is finished (ground
> for the marine, ship for the Venator) and stalls at its **signature** — the marine can't be a *six-model
> squad*, the Venator can't *launch its fighters*. Getting to 100% is: a few cheap shared fixes, then each
> signature, then the depth layers.

---

## 1. THE CONSOLIDATED LIST (deduplicated, every facet of both)

**Effort:** S = data/one-line · S–M = small build · M = real build · L = new core primitive.
**For:** 🔴 marine · 🔷 venator · ⭐ **both** (shared — fix once).

| # | Fix / addition | For | Effort | What it unlocks | Hooks / audit ref |
|---|----------------|-----|--------|-----------------|-------------------|
| **SHARED — do these first, each lifts both** |
| S1 | **Elite-quality / caliber** — (a) fix the firepower-caliber enhancer so it bites in *live* combat (it only feeds the test-only resolver today) **and** (b) add a **ground** caliber dial (`GroundUnitCaliberAtb`, mirror of `UnitCaliberAtb`) | ⭐ | S + S–M | Elite marines (a veteran cadre that actually hits harder) **and** elite Republic gunnery on the Venator | audit **D-gate-2** (a) + squad ledger **GAP 8** (b) |
| S2 | **Penetration + nature-decoupling on the weapon** — a per-part `Penetration` dial + decouple damage *nature* from *delivery* (a kinetic round that detonates) | ⭐ | S–M | The bolter's **mass-reactive** round + armour-piercing rounds for the marine; **AP turbolasers / proton torpedoes** for the Venator | audit **D-gate-3** (ship) + squad ledger **GAP 9** (ground) |
| **SIGNATURE — the single highest-value fix per subject** |
| V1 | **Carrier launch loop** — a launch order that scrambles a wing + the strike-craft-as-a-sub-fleet resolver (the bay component already exists) | 🔷 | M | The Venator's *whole identity* — 400 fighters that actually launch and fight | **`CARRIER-DESIGN.md`** (design-locked, unbuilt) + `Docking/` (`DockTools` built, **no game caller**) |
| M1 | **6-model squad granularity** — a `ModelCount`/`ModelsAlive` field on `GroundUnit` + per-model casualty & attack-scaling in the ground resolver + an assembler "this design fields N models" concept | 🔴 | **L** | "5 of 6 marines alive" — the squad, not one monolithic HP bar. *The developer's explicit ask #ii* | squad ledger **GAP 1** — core missing primitive, no scaffold |
| **DEPTH — the living-army / power-economy / flavour layers** |
| M2 | **Standing upkeep + consumption** — bill a fielded unit (food/credits) and **drain ammo** as it fights (`GroundAmmo.Consume` exists but only tests call it) | 🔴 | M | A marine costs the empire something *as it exists*. *Developer's ask #i* | squad ledger **GAP 2**; copy `StationUpkeepProcessor.BillUpkeep` into `ProcessBody` |
| M3 | **Population / gene-seed / training draw + scarcity cap** — a `CrewReq` on `GroundUnitDesign`, a gene-seed stock, staged multi-year conversion, a hard unit ceiling | 🔴 | M–L | Six marines that are **irreplaceable, not re-queueable** — the lore's load-bearing scarcity | squad ledger **GAP 3**; extend the crew gate (`IndustryTools.cs:152`) to ground |
| V2 | **Ship energy-weapon reactor gate** — mirror the ground gate onto ships (guns need a reactor) | 🔷 | M | A Venator can't mount turbolasers with no power plant (today it's "legal") | audit **D-gate-1** (behind a default-off flag; fix start ships first) |
| V3 | **Shield power cost** — deflectors draw standing power; a brownout drops shields | 🔷 | M | The real power **trade-off** between guns, shields, and engines | audit **C15 / Design 1** (needs the generic power-draw first) |
| M4 | **Jump pack** — a flight/leap/ignore-terrain axis on `GroundLocomotionAtb` + a terrain-skip branch in `ProcessBody` | 🔴 | M | Signature Blood Angels doctrine — drop into melee, cross terrain | squad ledger **GAP 4** — new mobility dimension |
| M5 | **Dual ranged+melee as two profiles** — per-weapon `WeaponProfile` resolution in the ground kernel (today it sums to one `Attack` + one nature) | 🔴 | M | Bolt-pistol-*and*-chainsword as two real attacks (ships already do this; ground blends) | squad ledger **GAP 5** — *note: the range-band half already works post real-distance* |
| M6 | **Per-unit psychology** — Red Thirst (triggerable aggression), Black Rage (irreversible flag), Death-Mask fear aura, Chaplain/Priest handlers | 🔴 | M | What makes a marine a marine, not a soldier | squad ledger **GAP 6** — from scratch, no hook |
| M7 | **Armour condition / maintenance** — a durability state that decays with use + a capped Techmarine throughput + a neglect penalty | 🔴 | M | Ties power armour to the Armoury scarcity the lore runs on | squad ledger **GAP 7** — distinct from M2's generic bill |
| M10 | **Consequential grave rung** — a casualty event + gene-seed/manpower write-back on death (today it's a silent delete) | 🔴 | S | Losing a marine *costs* something | squad ledger **GAP 11** (depends on M3) |
| **CLEANUP / DATA / OPTIONAL** |
| M12 | **Ground-combat research tree** — author techs that gate the marine's kit (pure JSON) | 🔴 | S | "Research power-armour tech" becomes real, not start-unlocked | squad ledger **GAP 13** — data only |
| M11 | **Grenades / thrown consumables** — a thrown/area weapon mode drawing a per-throw stock | 🔴 | S–M | Frag/krak grenades (rides M2's ammo wiring) | squad ledger **GAP 12** (minor) |
| V6 | **Scale / hull-size axis** — bigger hull tiers or a length model so a 1,137 m Venator ≠ a frigate by mass alone | 🔷 | M | True capital-ship *scale*, not just a mass budget | *abstraction call — may be acceptable as-is* |
| V7 | **Named-turret resolution** — per-turret instead of a bucketed fire-mix | 🔷 | M | Eight named DBY-827s vs an aggregate | *depth call — bucketing is fine for auto-resolve* |

**Already resolved since the last ledger:** the marine's **sealed environment** (GAP 10) is now real — the
`sealed-systems` augment exists and does exactly this. One fewer gap than the old audit lists.

---

## 2. Recommended sequence (best return first)

### Tier 1 — the shared cheap wins (do these first: 2 fixes, both builds jump)
**S1 (elite/caliber)** and **S2 (penetration + nature)**. Each is S/S–M, and each improves *both* the marine
and the Venator at once — elite units and armour-piercing/mass-reactive weapons are franchise-critical for both.
S1's ship half (the firepower-caliber live fix) I already traced end-to-end in the audit as safe (no
double-count). **Highest ROI in the whole list.**

### Tier 2 — the two signatures (the single most-defining build per subject)
- **Venator → V1, the carrier launch loop.** This is the highest-leverage Venator fix *and* mostly
  **connect-not-build**: the hangar bay is a real component, the docking machinery is built, and the
  strike-craft-as-sub-fleet resolver is already *designed* (`CARRIER-DESIGN.md`). Wire the launch order + the
  sub-fleet hook and the Venator goes from "gunship with a garage" to an actual Star Destroyer. **Do this
  before the marine's big one** — it's a fraction of the effort for a whole identity.
- **Marine → M1, the 6-model squad.** The developer's #1 ask and the fix that unlocks the most downstream
  (per-model attrition, casualty consequence, squad flavour) — but it's the one **L** on the list (a new core
  primitive with no scaffold). Budget it as its own slice.

### Tier 3 — the depth layers (group them by theme so they compound)
- **Marine "living army" (M2 + M3 + M10):** upkeep/consumption + gene-seed/training scarcity + a consequential
  death. Together these are the developer's *explicit* two asks ("costs upkeep + consumes as it exists" and
  "hand-counted scarcity") — build them as one economic layer so six marines finally feel irreplaceable.
- **Venator "power economy" (V2 + V3):** the ship reactor gate + shield power cost. Together they create the
  guns-vs-shields-vs-engines power trade-off — and V3 rides Design 1 (the generic power-draw) from the audit,
  so sequence it after that.
- **Marine "soul & mobility" (M4 + M5 + M6 + M7):** jump pack, dual-profile, psychology, armour condition —
  the Blood-Angel-specific flavour. Lower priority than the living-army layer, higher than cleanup.

### Tier 4 — cleanup / optional
Ground research tree (M12) and grenades (M11) are cheap data/small builds; the Venator's scale (V6) and
named-turret (V7) items are **judgment calls** — bucketed fire and a mass-budget "size" are defensible
abstractions, so only spend on them if the developer wants literal scale fidelity.

---

## 3. The one-screen answer

- **Cheapest thing that helps both:** S1 (elite/caliber) + S2 (penetration/nature). Two small fixes, four
  franchise features.
- **Biggest single Venator win, smallest effort:** V1 (carrier launch) — already designed, half-built.
- **Biggest single marine win, biggest effort:** M1 (6-model squad) — the core primitive, the #1 ask.
- **The developer's two explicit asks live in M1 (squad) + M2/M3 (upkeep + scarcity)** — everything else is
  franchise polish on top.
- **Nothing here is a rebuild.** Every item has a named hook or an existing design; the marine's combat and
  the Venator's combat are both *finished*, so this is all edge-work.

---

*Consolidated from `docs/showcase/LITMUS-BLOOD-ANGELS-BUILD.md`, `docs/showcase/LITMUS-VENATOR-BUILD.md`,
`docs/LITMUS-BLOOD-ANGELS-SQUAD.md`, and `docs/designers-audit/03-CORRECTION-PLAN.md`, 2026-08-01.*
