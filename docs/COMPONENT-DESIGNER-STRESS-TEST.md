# Component Designer — Franchise Stress Test & Hole-Plugging

**As of:** 2026-07-08 · companion to `COMPONENT-DESIGNER-CATEGORIES.md` (the locked 11-category design). Full unit-by-unit build of 12 iconic units across Star Trek / Star Wars / Stargate, and the best way to plug every hole they exposed.

**Headline:** ~80% of every franchise falls straight out of the doors+dials. Of the 12 holes, **11 plug with a dial/mode on an existing door or by reusing an engine system Pulsar already has** (jump points, capture, doctrine-switch, shield-regen, the crew-supply gate, units-as-entities). Only a few need a genuinely new mechanic — and the deepest one (H4) is a *boundary decision*, not a new parallel system. The categorization holds.

---

## Part 1 — The 12 units, built door by door

Notation: `Category ▸ Door (dial notes)`. ✅ clean · ⚠Hn strains hole n.

### Star Trek

**1. Galaxy-class *Enterprise* — the explorer flagship**
- Chassis ▸ Hull *(large, high budget, many hardpoints)*
- Propulsion ▸ Warp *(cruise dial high)* + Reaction *(impulse, sublight maneuver)*
- Power ▸ Generation *(warp core = very-high-output source)* + Storage
- Weapons ▸ Energy *(phaser arrays — wide-arc, continuous-beam)* + Guided *(photon torpedoes)*
- Defense ▸ Shields *(large regen pool)* + Armor *(light)*
- Sensors ▸ Detection *(long range)* + Survey *(deep — a science ship)* + Fire Control
- Civic ▸ Habitation *(families aboard — big)* + Development *(labs = Research)*
- Logistical ▸ Storage + Transfer *(shuttlebay, cargo)* · Command ▸ *(flagship span)* · Enhancers ▸ Systems *(computer core)*
- **Strains:** ⚠H5 saucer separation (one hull → two ships); ⚠H1 transporter. Holodeck = a Habitation luxury dial (fine).
- **Insight:** a civilian+science+warship on one hull — the categories fuse it effortlessly. Only transporter + saucer-sep miss.

**2. Borg Cube — the hole magnet**
- Chassis ▸ Structure/Mega *(cube — regular, no "front")*
- Command ▸ **distributed/hive** ⚠H10 *(no bridge)*
- Power ▸ Generation *(massive)* · Propulsion ▸ Warp *(transwarp)* + Exotic *(transwarp conduits ≈ gate network)* ⚠H8
- Weapons ▸ Exotic *(cutting beam)* + **assimilation** ⚠H9 *(convert, don't destroy)*
- Defense ▸ **adaptive** Shields ⚠H2b *(immune to a weapon after one hit)* + **self-regenerating** structure ⚠H2a
- Industrial ▸ Fabrication ⚠H3 *(builds more Borg internally)* · Logistical ▸ Storage *(drones)*
- **Strains:** five at once — H10, H8, H9, H2a, H2b, H3.
- **Insight:** if the designer can express a Cube, it's essentially complete. It's the concentrated worst case alongside the Replicator.

**3. USS *Defiant* — the glass cannon**
- Chassis ▸ Hull *(small, tight budget)*
- Weapons ▸ Guided *(quantum torpedoes)* + Energy *(pulse phaser cannons)* — **deliberately over-armed**
- Power ▸ Generation *(oversized core — costs budget+mass)* · Propulsion ▸ Warp + Reaction
- Defense ▸ Armor *(ablative — regenerating* ⚠H2a*)* + Shields
- Sensors ▸ **EW cloak** ✅ *(the Romulan device — validates the new EW door)*
- **Strains:** only ⚠H2a (regenerating ablative armor). The "too much gun for the hull" is **not a hole** — the **supply/budget gate is supposed to bite, and it does** ✅ (the Defiant only works by also mounting an oversized core, which eats budget/mass — exactly the tradeoff the gate enforces).
- **Insight:** the Defiant *validates the supply gate* — the canonical frame-straining glass cannon, modeled natively.

**4. Security officer + hand phaser + tricorder — the small end**
- Chassis ▸ Personnel
- Weapons ▸ Energy *(hand phaser — tiny output, **stun/kill mode dial** ✅)*
- Sensors ▸ Detection + Survey *(tricorder — a survey sensor on a soldier ✅ universal mounting)*
- Defense ▸ Armor *(uniform, minimal)* · Logistical ▸ Storage *(kit)*
- **Strains:** none. **Insight:** validates the Personnel end, stun/kill as a **dial**, and universal mounting *downward* (a survey rig on a person).

### Star Wars

**5. Imperial Star Destroyer — the capital**
- Chassis ▸ Mega/Hull *(capital)*
- Weapons ▸ Energy *(turbolaser batteries, wide-arc)* + Ballistic *(ion cannons — "disable" nature)* + Exotic *(tractor beam ⚠ — a ranged grab)*
- Propulsion ▸ Warp *(hyperdrive)* + Reaction · Power ▸ Generation *(huge)*
- Defense ▸ Shields *(deflector)* + Armor · Command ▸ *(fleet flagship — big span)*
- Logistical ▸ Storage *(**TIE hangar** = carrier* ⚠H6*)* + Transfer *(launch)* · Sensors ▸ Detection + FC · Civic ▸ Habitation *(crew of thousands)*
- **Strains:** ⚠H6 carrier (houses/launches fighter chassis); the tractor beam = an **effect on an external unit** (cousin of H1/H9).
- **Insight:** a mostly-clean capital; its exotic bits point at nested-chassis (H6) and the "act on another unit" effect family (tractor/capture/assimilate share a shape).

**6. X-wing — the fighter**
- Chassis ▸ Hull *(fighter — tiny budget)*
- Weapons ▸ Energy *(4 laser cannons — linked-fire dial)* + Guided *(proton torpedoes)*
- Propulsion ▸ Reaction + **Warp (hyperdrive on a fighter)** ✅
- Power ▸ Generation + Storage · Defense ▸ Shields *(small)* + Armor *(titanium)*
- Enhancers ▸ Systems *(**astromech droid** — AI + in-flight repair ≈H2a)* · Sensors ▸ Detection + FC
- **Strains:** ⚠H5a S-foils *(config toggle: cruise vs attack)*; astromech = *crew-that's-a-component* (a droid slot — mostly fits Systems).
- **Insight:** validates **universal mounting** (a hyperdrive on a fighter just works) and flags **config-states** (cheap, a doctrine-switch-style dial). Droid=component vs pilot=being shows the boundary is workable.

**7. AT-AT — the walker**
- Chassis ▸ Vehicle *(large)*
- Propulsion ▸ Traction *(**walker legs** — rough-handling high, slow, **ground-only medium** ✅)*
- Weapons ▸ Energy *(heavy head lasers)* · Defense ▸ Armor *(very heavy — blaster-immune)*
- Logistical ▸ Storage *(troop bay = carrier* ⚠H6*)* · Command ▸ *(command walker)*
- **Strains:** ⚠H6 carrier. The famous "wrap the legs" weak point is **already modeled** — kill the Traction component → immobilized (component-level damage).
- **Insight:** validates Vehicle + Traction + heavy Armor + a medium constraint; its weak point falls out of the existing damage model. Clean.

**8. Jedi + lightsaber — the boundary probe**
- Weapons ▸ Melee *(an **energy** melee — Melee door, **Energy nature**: nature is cross-cutting ✅)*; wielder = Chassis ▸ Personnel
- **Strains:** the two deepest. ⚠H7 the blade **deflects blaster bolts** (a weapon that is *also* a defense); ⚠H4 **the Force** (telekinesis/precognition/mind-trick — NOT gear; it comes from the Jedi).
- **Insight:** the best **boundary probe.** The saber = gear (Melee/Energy + a "deflect" defense-effect, H7). The Force = the **being** — a **People trait** that *emits the same effects a component would* (precognition→evasion buff, telekinesis→a push effect, mind-trick→a subvert effect). This is the exact shape of the H4 plug.

### Stargate

**9. The Stargate (+ DHD) — the network/teleport probe**
- Chassis ▸ Structure *(a fixed ring)*
- Transport ▸ **network node** ⚠H8 *(dials another gate by address, opens a wormhole)* + **matter transport through it** ⚠H1
- DHD = a Command/control interface *(the dialer)*
- **Strains:** the purest H8 + H1. A gate isn't a drive (it doesn't move itself) — it's an **addressable node** that sends *other* things to another node.
- **Insight:** **reuse Pulsar's existing JumpPoints subsystem** — a Stargate is a *buildable, addressable jump-point node.* Collapses H8 into a system already in the tree.

**10. Goa'uld Ha'tak — the clean alien capital**
- Chassis ▸ Hull *(pyramid)* · Weapons ▸ Energy *(staff cannons + belly plasma)* · Defense ▸ Shields
- Propulsion ▸ Warp *(hyperdrive)* + Reaction · Power ▸ Generation *(naquadah — high source)*
- Logistical ▸ Storage *(Jaffa + death gliders = carrier* ⚠H6*)* + **ring transporter** ⚠H1 · Command ▸ *(System Lord flagship)*
- **Strains:** only H6 + H1. **Insight:** a clean alien capital — proves the categories aren't Trek/Wars-specific. Rings = H1 recurrence.

**11. Replicator — the boss fight**
- Chassis ▸ Personnel/sub-personnel *(**modular blocks** — the chassis IS the swarm)*
- Industrial ▸ **Fabrication = replicate-self** ⚠H3 *(consumes matter → copies of its own design)*
- Defense ▸ **adaptive** ⚠H2b · Weapons ▸ Ballistic/Melee *(block projectiles)*
- **reconfigurable** ⚠H5 *(blocks rearrange)* · Command ▸ distributed/hive ⚠H10
- **Strains:** the worst case — H3 + H5 + H2b + H10 at once.
- **Insight:** the stress-test's boss. Its four dials (self-fabricate, config-states, adaptive shield, hive command) are exactly the plugs below — if they exist, a Replicator is designable, which means the model is franchise-complete.

**12. Jaffa warrior — the Enhancers validator**
- Chassis ▸ Personnel
- Weapons ▸ **staff weapon** ⚠H7 *(ONE item, TWO doors: a **plasma bolt** (Energy, ranged) + a **blunt club** (Melee))*
- Defense ▸ Armor *(serpent armor + helmet)*
- Enhancers ▸ **Bio-augmentation** *(the **symbiote** grants health/healing/longevity — validates Enhancers ✅)*
- Sensors ▸ *(helmet optics)* · Logistical ▸ Storage
- **Strains:** only ⚠H7 (dual-mode weapon). **Insight:** validates Enhancers ▸ Bio-augmentation (the symbiote is *exactly* "a bio-mod that makes this soldier different") and flags H7 (multi-role components).

---

## Part 2 — The unifying insight: a shared EFFECT bus

Before the per-hole plugs, the pattern under half of them: **the same EFFECT keeps showing up from different SOURCES.** A transporter, a Force push, a tractor beam, an assimilation beam, a self-repair field, a lightsaber parry — these are *effects* (teleport, push/grab, capture/subvert, regen, deflect) emitted by different things (a component, a pilot's innate trait, a weapon firing mode, a chassis config).

**The plug that makes the others coherent: define a shared vocabulary of EFFECTS, and let any SOURCE emit any effect.**

```
  EFFECTS (the bus):  damage · capture/subvert · teleport · grab/push ·
                      regen/repair · resist-buff · evasion-buff · jam · reveal
  SOURCES that emit:  a COMPONENT (gear — the designer)
                      a WEAPON FIRING MODE (stun vs kill, kill vs capture)
                      a CHASSIS CONFIG (S-foils open → +fire arc)
                      a PEOPLE TRAIT (the being — the Force, psionics, veterancy)
```

With this, five holes stop being special cases: **H1** (teleport effect), **H4** (traits emit effects), **H7** (a weapon emitting a defense effect), **H9** (a weapon emitting capture), and the tractor beam (grab effect) are all "a source emitting an effect from the bus." You build the bus once; you never build a bespoke "transporter system" or "Force system."

And it fixes the **boundary** cleanly:
> **Gear = the designer. The being = the People system. Effects = the shared bus both emit into.**
That is the answer to H4: the designer never makes a Jedi; the **People system** carries the Force-sensitivity trait, which emits *the same effects* a component could. No parallel "powers" system, no fake component.

---

## Part 3 — Plugging every hole (best fix, by cost)

### Tier A — a dial/mode on an existing door (cheapest; no new system)

| Hole | Plug |
|------|------|
| **H1 teleport** | **Logistical ▸ Transfer "delivery mode" dial**: physical (docking/conveyor, short) vs **teleport** (instant, ranged, mass/cycle-limited, heavy power draw). Transporter, rings, beaming = the same dial at different range/mass. Emits the **teleport effect** on the bus. |
| **H2a self-repair** | **Enhancers ▸ Systems "regen" dial** — regenerate a pool (hull/armor/shield) per tick. Reuses the shield-pool regen mechanic; "what it repairs" is a dial. |
| **H3a mobile fabrication** | **Universal mounting** — let Industrial doors mount on Hull/Vehicle/Personnel, not just Structure. The engine already discovers industry by ability-blob, not host (`DESIGNER-AUDIT/06`); it's a mount-flag change. A factory ship / construction rig falls out. |
| **H3b self-replication** | **Industrial ▸ Fabrication "output = own design" mode** — consume matter, build a copy of self. Grey-goo is bounded by tech/scale caps + matter. Reuses Fabrication. |
| **H5a config-states** | **Chassis config-states** — a design carries 2–3 named configs (stat/active-component profiles), switched in-play on a cooldown. **Reuse the fleet-doctrine / ground-stance switch mechanic** verbatim, one level down. S-foils, combat/travel mode, dig-in. |
| **H7 hybrid components** | **Multi-role components** — a single design carries abilities from >1 door (already how templates work: a reactor carries EnergyGen + SensorSignature). Staff = Energy + Melee; lightsaber = Melee + a deflect effect. The designer just needs to *permit* adding a second role. |
| **H9 conversion** | **Weapons ▸ Exotic effect = capture/subvert** — "damage" resolves to a capture roll (flip owner) instead of HP. **Reuse the existing capture primitive** (region-flip / boarding-capture) at unit scale. Emits the **capture effect** on the bus. |
| **H10a crewless** | **Enhancers ▸ Systems "automation" dial** — reduces the crew requirement; at max, crew→0 (a drone). Reuses the crew term of the supply gate. |
| **H10b hive** | **Command "structure" dial** — hierarchical vs **distributed/hive** (one node's span covers many units, no per-unit command). Lose the node → the hive is disrupted (the counter). A Command dial. |
| **H11 scale extremes** | **Tune the Chassis scale dial + tech/scale caps** to span Personnel(sub-kg) → Mega(planet); verify emergent-stat formulas stay sane across ~10 magnitudes. No new mechanic. |
| **H12 exotic power** | **Power ▸ Generation "source" dial** — high-end, no-fuel sources behind tech (naquadah, zero-point). Already the source-dial concept. |

### Tier B — reuse an engine system Pulsar already has

| Hole | Plug |
|------|------|
| **H8 gate network** | **Reuse the JumpPoints subsystem.** A Stargate = a *buildable, addressable jump-point node* (Chassis ▸ Structure + a "gate" ability that registers it in a named network). Dial a destination node → traverse. The DHD = the addressing UI. Subspace relays = the sensor/comms variant (extend Detection/EW across nodes). |
| **H6 carrier / nesting** | **Reuse units-as-entities.** Logistical ▸ Storage gains a "bay" mode that holds *unit-entities* (fighters/troops/gliders); Transfer launches/recovers them. A carrier = a unit whose bay contains child units. Verify the entity model supports a unit holding child units (it should). |
| **H5b separable chassis** | **The carrier mechanic taken to the limit** — saucer separation = a design with a *detachable primary module* that is itself a mini-chassis; "separate" = the module becomes an independent unit (launch), the mothership continues degraded. Solve H6 → saucer-sep falls out + a "detach" order. |

### Tier C — a genuinely new mechanic (the real, bounded work)

| Hole | Plug |
|------|------|
| **H4 innate powers** | **The People-trait → effect-bus layer.** A commander/pilot/species carries innate **traits** that emit effects onto their crewed unit (the Force → evasion buff + push + subvert; psionics; veterancy). This is the biggest, but it's a **boundary/architecture decision**, not a parallel system — traits emit into the *same* effect bus components use. Pulsar already has commanders giving unit bonuses; this generalizes that into the shared bus. |
| **H2b adaptation** | **A new resolver rule: resistance-climbs-with-exposure.** A Shields "adaptive" dial: after taking damage of a given *nature*, resistance to that nature rises (capped, decays). Counter is already in the weapon model — **modulate weapon nature** (rotate frequencies) to stay ahead. Small, self-contained resolver addition; hooks the existing weapon-nature system. |

### The cost picture
- **12 of ~16 sub-plugs are Tier A** — a dial or mode on a door the parametric model already has.
- **3 reuse an existing subsystem** (jump points, capture, units-as-entities/doctrine-switch).
- **Only 2 are new mechanics**, and both are bounded: the **effect-bus + People traits** (H4 — the one architectural decision worth making early, because it also plugs H1/H7/H9 and defines the designer's edge) and **adaptive resistance** (H2b — a small resolver rule).

**Conclusion:** the holes do **not** threaten the 11-category model — they largely *confirm* it. The parametric approach was built to absorb "types" as dials, and that's exactly what most plugs are. The one high-value early decision is the **shared effect bus with the gear/being boundary** (build once, plug five holes, keep "the Force" honest). Everything else is a dial, a mount-flag, or reuse of a system already in the tree.
