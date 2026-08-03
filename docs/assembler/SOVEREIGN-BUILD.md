# The Sovereign, built by the playbook — a Federation explorer-cruiser

> A REPRODUCTION build (Star Trek, Sovereign-class / USS Enterprise-E) — and the interesting part is the
> **cross-franchise translation**: Trek weapons and doctrine map cleanly onto the same generic components,
> proving the catalog isn't Star-Wars-specific.

## Objective & role stack
Starfleet's best balance of exploration and tactical power. Stack: **① range & endure (deep-space explorer) →
② win a fight it can't avoid (torpedoes + phasers behind the fleet's best shields) → ③ see everything first.**
Sacrifice: light hull — it trusts shields and sensors, not armour.

## The Trek → tool mapping (no new components — the discipline)
Rather than invent parallel systems, Trek tech maps onto the generic catalog by **mechanic**:
| Canon | Maps to | Count |
|---|---|---|
| ~16 Type-XII phaser arrays | energy beams: Medium Turbolaser + Laser Cannon | 10 + 14 |
| 10 photon/quantum torpedo tubes | Missile Launcher (guided) | 16 |
| phaser point-defense | Point-Defense Battery | 12 |
| the fleet's strongest **regenerative shields** | Shield Generator | **14** (its primary defense) |
| no ablative armour (canon) | Composite Armour | 8 (light — still trusts shields) |
| 2 warp nacelles (warp ~9.9) | Warp Drive | 2 |
| twin impulse | Ion Drive | 3 |
| ~855 crew, superb medical | Quarters/Life-support/Medical | sized (medical covers the crew) |
| deep-space sensors | Sensor + Long-Range + Fire-Control + ECM | strong |

*A phaser **is** an energy beam; a photon torpedo **is** a guided explosive. Mapping by mechanic (not re-skinning
the catalog with near-duplicates) is exactly what `CONVENTIONS.md` §6 demands — and it works.*

## It closes (all gates green)
Hull **16,149 / 20,000 t** (a full capital cruiser — 81% of the chassis, filled out 2026-08-03) · Firepower **59 MJ/s**
(see the torpedo caveat) · Crew **652** berthed/supported/**medically covered** · Shields **70 MJ — by far the heaviest
of any build here** (14 generators, its whole doctrine) · **Deployment 500 days (~1.4 yr), self-sufficient** (a closed
food loop — a true long-range explorer) · Detection **490 km**, past its own torpedoes (it sees first, as an explorer
should). *Filled in its own idiom — more shields, phasers, torpedoes, sensors and endurance, still deliberately light on
armour — not turned into a brawler.*

> ⚡ **Energy recalibrated to the engine (2026-08-02) — and it surfaced an honest caveat for this ship.** Numbers are
> now real engine units: shields are the engine's deflector pool (the Sovereign's 8 generators = **40 MJ**, the
> heaviest here, off the reactor), warp gates on a battery charge (2 nacelles need 2.5 GJ, so it carries 4 capacitors).
> **The torpedo caveat:** the auto-resolver *stubs* a missile/torpedo at a flat 0.1 MJ/s — so the resolver Firepower
> (43 MJ/s) is **phaser-led, not torpedo-led**, even though the Sovereign is canonically a torpedo boat. Its real
> torpedo punch (GJ-scale kinetic impact) lands in the *live* sim, not the auto-resolve total. That's an engine gap,
> flagged on the launcher — the ship is designed right; the resolver just under-counts guided weapons.

## The honest ledger
**Almost everything is LIVE.** Phasers/PD (firepower + saturation), the strong shields, sensors + fire-control +
ECM, warp, and the self-sufficient crew city all reach the sim. **The one caveat:** torpedoes are LIVE as guided/PD-
answerable weapons, but the auto-resolver stubs their *damage* (see the energy note) — so their firepower lands in
live combat, not the resolver total. **PENDING:** nothing else — the Sovereign carries no strike fighters and no
superweapon, so it has **no aspirational gaps**. It's still among the cleanest builds in the set: a ship whose whole
doctrine (shields + torpedoes + sensors + endurance) is what the engine models well.

**Verdict:** a Federation cruiser built cradle-to-grave from a Star-Wars-flavoured parts bin, entirely by mechanic
— proof the assembler expresses *aspects* across franchises (the North-Star vision), not one universe.

---
*Sources: [Sovereign class — Memory Alpha](https://memory-alpha.fandom.com/wiki/Sovereign_class) · [USS Enterprise (NCC-1701-E)](https://memory-alpha.fandom.com/wiki/USS_Enterprise_(NCC-1701-E)). Specs are production-reference values (not dialogue canon). Built 2026-08-02 via `DESIGNER-DRIVER-PLAYBOOK.md`. No engine/franchise assets touched.*
