# The Sovereign, built by the playbook — a Federation explorer-cruiser

> A REPRODUCTION build (Star Trek, Sovereign-class / USS Enterprise-E) — and the interesting part is the
> **cross-franchise translation**: Trek weapons and doctrine map cleanly onto the same generic components,
> proving the catalog isn't Star-Wars-specific.

## Objective & role stack
Starfleet's best balance of exploration and tactical power. Stack: **① range & endure (deep-space explorer) →
② win a fight it can't avoid (torpedoes + phasers behind the fleet's best shields) → ③ see everything first.**
Sacrifice: light hull — it trusts shields and sensors, not armour.

## The Trek → tool mapping (no new components — the discipline)
Rather than invent parallel systems, Trek tech maps onto the generic catalog by **mechanic** (counts re-verified against
the 2026-08-04 research pass — see the correction note below):
| Canon | Maps to | Count |
|---|---|---|
| **Type-XII** phaser arrays — 12 as-built → **16 after the *Nemesis* refit** (a generation above the Galaxy's Type-X) | energy beams (Phaser Array + Phaser Bank in the sim) | 16 + 14 |
| **10 torpedo tubes** — 1 forward **quantum** turret + 9 **photon** (canon refit) | Guided missiles, **split photon + quantum** in the sim | 12 photon + 4 quantum |
| phaser point-defense | Point-Defense Battery | 12 |
| the fleet's strongest **regenerative shields** | Shield Generator | its primary defense → **120 MJ** |
| armour-light — trusts shields, not plating (ablative is a tech-manual claim, not strict on-screen canon) | Composite Armour | light |
| **2 fixed nacelles**, subspace-safe warp field (no pivoting pylons like *Voyager*) | Warp Drive | 2 |
| twin impulse — capable, but fights with **firepower + positioning, not agility** | Ion Drive → modest evasion (0.12) for a 685 m hull | 3 |
| ~855 crew, superb medical | Quarters/Life-support/Medical | sized (medical covers the crew) |
| deep-space sensors | Sensor + Long-Range + Fire-Control + ECM | strong |

*A phaser **is** an energy beam; a torpedo **is** a guided explosive. Mapping by mechanic (not re-skinning
the catalog with near-duplicates) is exactly what `CONVENTIONS.md` §6 demands — and it works.*

> **⭐ Research correction (2026-08-04) — deeper cross-check across Memory Alpha / Beta / DITL / Ex Astris.**
> Four things sharpened, all in propulsion + weapons: **(1) Phasers are Type-XII, not Type-X** — Type-X is the
> *Galaxy*-class (Enterprise-D); the Sovereign is the generation up (~60% more beam power/segment, tech-manual).
> **(2) The torpedoes split** into a rapid-fire forward **quantum** turret + photon tubes — the sim now models both,
> the quantum as a heavier warhead (higher penetration + per-shot alpha). **(3) The "first quantum torpedo" myth:**
> the USS *Defiant* fired them first (2371); the Enterprise-E was **second** (2373) — the Sovereign was the first class
> *designed around* the quantum torpedo, not the first to fire it. **(4) Agility is overstated in fan specs** — the
> "handles like a ship ¼ its size" line is fan-spec, and the same sources admit it's *less* nimble than the smaller
> Intrepid/Prometheus and fights with firepower, not dodging — so **evasion stays modest (0.12)**, correct for a 685 m
> capital. Warp: only **Warp 8** was ever filmed; the "9.9x" maxima are reference-book figures that disagree
> (9.7 / 9.985 / 9.995), and the game has no warp-factor dial anyway. *Sources: [Memory Alpha](https://memory-alpha.fandom.com/wiki/Sovereign_class) · [Quantum torpedo](https://memory-alpha.fandom.com/wiki/Quantum_torpedo) · [DITL](https://www.ditl.org/article-page.php?ArticleID=5) · [Ex Astris Scientia](https://www.ex-astris-scientia.org/articles/sovereign.htm).*

## It closes (all gates green)
Hull **19,685 / 20,000 t** (**min/maxed 2026-08-03 — mass-bound, 315 t from the wall**) · Firepower **84 MJ/s**
(see the torpedo caveat) · Crew **800** berthed/supported/**medically covered** · Shields **120 MJ — by far the heaviest
of any build here** (24 generators, its whole doctrine — the min/max poured the residual into shields + phaser banks) · **Deployment 500 days (~1.4 yr), self-sufficient** (a closed
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
