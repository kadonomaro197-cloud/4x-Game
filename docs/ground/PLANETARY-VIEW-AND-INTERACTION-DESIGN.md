# Planetary View & Interaction — the surface as a board of environments

**As of 2026-08-09.** Status: **DESIGN STUDY (design-only).** No engine code, no CI. This doc + its interactive
prototype (`docs/ground/planetview.html`) answer one question the developer put on the table:

> *"design the way we view and interact with planets in general, and connect it to the auto-resolver and the
> different settings."*

The headline, in one sentence: **a planet is not a dimensionless dot — it is a board of environments, and the square
of ground you choose to fight on IS the setting the auto-resolver reads.** You don't pick a battle's environment from a
menu; the *location picks it for you*, and your only environment decision is **where you commit**.

**Companions (read alongside):**
- `docs/ground/GROUND-SURFACE-MAP-DESIGN.md` — THE surface board this view sits on (region ring → global cylinder →
  mini/city hex). This doc is the **front door and the interaction**; that doc is the **board**.
- `docs/combat/ENVIRONMENT-CONDITIONS-DESIGN.md` — the env-effects catalog + the LIVE/DATA/THEORY grading this view
  reuses verbatim (accepted resolver hooks, engine wiring deferred).
- `docs/combat/resolversim.html` + `docs/combat/RESOLVER-SIM.md` — the working model of the *arena* a battle resolves
  in; the multipliers this view shows are the **real values ported from that sim's surface-environment catalog**.
- `docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md` — the resolver anatomy (§11 the north star; §12 the ground fight).

---

## 1. Why this matters (read this first, in plain English)

Right now a planet in Pulsar4X is closer to a *number* than a *place*. It has a population, some buildings, an owner —
but combat that happens "there" has never really cared *where* on the surface it happened. That's the gap.

Think of it the way a navigator thinks about a chart. The ocean isn't one flat blue field — it's shoals here, deep
water there, a storm rolling through, a coastline that funnels traffic. **Where** you meet the enemy decides half the
fight before a shot is fired. The same is true on the ground: tanks own an open plain and die in a jungle; marines die
on an airless moon without sealed suits; a dust storm collapses your sightlines to knife range.

Pulsar4X already *models* a lot of that surface detail — gravity, temperature, atmosphere, radiation, terrain type.
The problem `docs/combat/ENVIRONMENT-CONDITIONS-DESIGN.md` names is that **almost none of it reaches the part of the
game that decides who wins a fight.** The detail is on the shelf; the combat resolver never opens the jar.

This design closes that. It does two things:

1. **A way to SEE the planet** — a zoomable surface where every square of ground carries a visible environment (its
   terrain, the planet's condition it sits under, and any weather rolling through it).
2. **A way to ACT on it** — the environment of the square you pick becomes, automatically, the environment the
   auto-resolver opens the battle with. **The map is the front door; the resolver is the room.**

The point before the plumbing: **you win by choosing your ground, not by choosing a menu option.**

---

## 2. Four zooms, one surface

The developer's own framing of the surface is **"4 big slices you can zoom into at high accuracy."** Stacked as zoom
levels, the planet reads like a set of nested charts — the strategic plot, the globe, the theatre, the city block —
each the same physical world seen closer. This is not new geometry; it's the ladder that already exists in
`GROUND-SURFACE-MAP-DESIGN.md`, named as an interaction.

| Zoom | Name | What you see & do | Build-state (honest) |
|------|------|-------------------|----------------------|
| **1** | **System** | Planets as points on the orbit map — the strategic board. Where you decide *which world*. | **ENGINE-WIRED** — the system map ships. |
| **2** | **Planet globe** | One continuous hex grid wrapping the whole world (`SurfaceGrid`), banded into the 4 regions. Terrain, ownership, weather at a glance. Where you decide *which theatre*. | **ENGINE-WIRED (G6a)** — `PlanetViewWindow.DrawGlobalHexWindow` renders the sliding cylinder window; the client is fully off the old disks. |
| **3** | **Regional / operational** ← *this study lives here* | The war layer: units move, fight, and capture on real-distance hexes. Each hex carries a **terrain** and inherits the planet's **condition**. Where you decide *which ground*. | **ENGINE-WIRED (W-track)** — units march/fight/capture per-hex; combat resolves on continuous real-metre distances. |
| **4** | **Local / city** | The build layer — each installation sits 1:1 on its own fine tile. Raze it, capture it, defend it. Where you decide *what to build*. | **PARTIAL** — W-track (war zoom) built + CI-gauged; the C-track city builder follows. |

**Why zoom 3 is the one that matters for this task.** Zoom 1 and 2 are strategic — they pick a target. Zoom 4 is
economic — it lays out buildings. **Zoom 3 is where a battle actually happens**, and therefore it's the zoom where the
"environment picks the fight" connection has to live. The prototype (`planetview.html`) is built at this zoom
deliberately: an operational hex band you click to read the fight it produces.

---

## 3. The load-bearing connection — how the ground becomes the fight

This is the heart of the whole design. It's one idea, and everything else is detail hanging off it.

> **A hex's combat environment = its terrain × the planet's condition × the weather rolling through it. That composed
> environment IS the auto-resolver's environment setting.**

Read it as three layers of a stack, like three filters clipped over the same lens:

- **Terrain (per-hex, local).** Open plains / dense jungle / mountains / urban ruins. This is the *ground itself* — it
  changes with every hex. It's the cover you hide in and the speed you can advance at. **Already bends a live ground
  fight today** (the Open/Cover/Rough terrain triangle is wired).
- **Planet condition (global, one per world).** Gravity, temperature, atmosphere, radiation, daylight. This is the
  *world you're on* — it's the same on every hex of that planet. Low gravity speeds everyone up and extends reach;
  a toxic atmosphere bleeds anyone not sealed; vacuum lets rounds fly true but kills the exposed.
- **Weather (transient, geographic).** A dust storm, a lightning storm, an ash storm, a long polar night. This is
  *what's passing over right now* — it sits on some hexes and not others, and it moves. It mostly collapses sightlines,
  turning a long-range duel into a blind, close brawl.

**The composition.** These aren't three separate battles — they're one environment. Each layer contributes multipliers
(and a couple of additive terms), and they **stack** into a single bundle. That bundle is exactly the struct the
resolver already reads as its "environment setting" today. The current sim reads **one** environment from a dropdown;
**composing terrain × condition × weather into that one setting is the new design call** — the rest is already proven.

```
   The hex you pick                Composed environment              The auto-resolver
   ───────────────                 ────────────────────              ─────────────────
   terrain (this hex)     ─┐
   condition (the planet)  ├──►  one multiplier bundle   ──►   opens the fight with it:
   weather (rolling by)   ─┘      (the layers stack)            sight · cover · closing · damage · attrition
```

**Why this is the right shape** (not a menu): it satisfies the project's own laws.
- It's a **decision that stacks** (`REALISM-VS-GAMEPLAY-AUDIT.md`) — the realism (gravity, terrain, weather) earns its
  keep because it's the source of a real choice: *where do I commit my forces?*
- It's **One Verb, Both Seats** (`CLAUDE.md`) — the AI reads the *same composed environment* off the *same hex* the
  player does. There is no "player picks a rich environment, AI gets a crude one." The composition is a property of the
  ground, not of the seat looking at it.

---

## 4. The composition rule (how the layers actually stack)

This mirrors the `multiply()` function proven in `resolversim.html` exactly — no new math, so the design can't drift
from the working model.

An environment is a small bundle of fields. Each layer supplies its own values; the composed total combines them:

| Field | What it controls | Combine rule | Default |
|-------|------------------|--------------|---------|
| `range` | detection / engagement range | **multiply** (×) | 1.0 |
| `closing` | closing speed (how fast lines meet) | **multiply** (×) | 1.0 |
| `dmgK` / `dmgE` / `dmgX` | kinetic / energy / exotic damage | **multiply** (×) | 1.0 |
| `guidedR` | guided-weapon reach | **multiply** (×) | 1.0 |
| `pd` | point-defence effectiveness | **multiply** (×) | 1.0 |
| `eva` | cover → evasion bonus | **add** (+) | 0.0 |
| `dot` | ambient attrition (J/s), armour-resisted | **add** (+) | 0 |

**Multiply for scaling effects, add for stacking effects.** Two layers that each cut sight (say jungle-under-a-dust-
storm) multiply their range factors — the murk compounds. Two layers that each give cover add their evasion — you're
in the trees *and* in the gloom. Two layers that each bleed you (toxic air on a firestorm world) add their attrition —
both are chewing on you at once. This is precisely what the sim does, and the prototype's numeric check confirms every
composed value stays finite and positive across all five test worlds.

**Worked example — Mars, mountains, mid-dust-storm:**
- terrain (mountains) → `eva +0.30`, `closing ×0.40`
- condition (Mars = low-g + toxic) → `closing ×1.30`, `guidedR ×1.25`, `eva +0.10`, `dot +830 J/s`
- weather (dust storm) → `range ×0.60`, `eva +0.12`
- **composed:** `range ×0.60`, `closing ×0.52`, `eva +52%`, `guidedR ×1.25`, `dot 830 J/s`
- **the read:** sight is gone (dust) so it's a close fight; the mountains slow the approach to a crawl even though
  low-g wants to speed it up; everyone's hard to hit; and the toxic air is bleeding both sides the whole time. A
  defender who digs in here makes the attacker pay for every metre — *and the AI reads exactly the same thing.*

---

## 5. The environment catalog (the real numbers)

These are the multipliers the prototype shows, ported verbatim from the surface-environment catalog in
`resolversim.html`. The **grade** is the honesty marker from `ENVIRONMENT-CONDITIONS-DESIGN.md`:

- **🟢 LIVE** — the engine applies this exact effect today (wired).
- **🟡 DATA** — the data exists on the world (gravity, radiation, atmosphere) but combat reads little/none of it yet.
- **🔵 THEORY** — an accepted hook, not yet built anywhere but the sim.

### Terrain (per-hex, local) — 🟢 LIVE (the terrain triangle already bends a ground fight)
| Terrain | Effect | Read |
|---------|--------|------|
| Open plains | `eva −0.05`, `dmgK ×1.10` | Armour's ground — nowhere to hide, kinetic dominates. |
| Dense jungle | `eva +0.20`, `closing ×0.66`, `dmgK ×0.90` | Cover; the advance crawls; infantry's ground. |
| Mountains | `eva +0.30`, `closing ×0.40` | Heaviest cover, slowest close; artillery's ground. |
| Urban / ruins | `eva +0.28`, `closing ×0.60`, `dmgK ×0.85` | The defender's fortress; the attacker bleeds. |

### Planet condition (global, one per world)
| Condition | Grade | Effect | Read |
|-----------|-------|--------|------|
| Temperate | 🟢 LIVE | (baseline) | Benign — breathable, 1 g, survivable. |
| Low gravity | 🔵 THEORY | `closing ×1.30`, `guidedR ×1.25`, `eva +0.10` | Everything moves faster and reaches farther; hard to pin. |
| High gravity | 🔵 THEORY | `closing ×0.70`, `guidedR ×0.75`, `dmgK ×0.90`, `eva −0.05` | Movement drags, guided rounds fall short, nobody dodges. |
| Toxic atmosphere | 🟡 DATA | `dot +830 J/s` (corrosive) | Sealed suits survive; everything else attrites. |
| Airless / vacuum | 🟡 DATA | `dmgK ×1.10`, `guidedR ×1.10`, `dot +830 J/s` (exposure) | Rounds fly true and far; exposure kills the unsealed. |
| Molten / firestorm | 🟡 DATA | `dmgE ×0.80`, `dot +9700 J/s` (heat) | >400 °C — cooks anything not heat-hardened. |
| Cryogenic | 🟡 DATA | `closing ×0.80`, `dot +4200 J/s` (cold) | <−120 °C — movement stiffens; the cold gnaws. |
| Irradiated | 🟡 DATA | `dmgX ×0.90`, `dot +170 J/s` (radiation) | Survivable in hardened kit, attritional in soft. |

### Weather (transient, geographic) — 🔵 THEORY
| Weather | Effect | Read |
|---------|--------|------|
| Dust storm | `range ×0.60`, `eva +0.12` | Sight collapses to knife range; the murk hides everyone. |
| Lightning storm | `range ×0.50`, `guidedR ×0.80`, `dmgK ×0.90` | Sensors blind, guidance fries — a blind close brawl. |
| Ash storm | `range ×0.55`, `eva +0.10` | Volcanic ash chokes sightlines — ambusher's cover. |
| Long night | `range ×0.70`, `eva +0.10` | Detection cut; everyone harder to see. |

**Two hexes that are NOT battles:** **ocean is impassable** to ground forces (out of the graph — a march routes around
it, taking it needs sealift), and **ice is passable but rough**. The prototype honours both (clicking open ocean shows
"impassable," and ice composes as rough terrain), matching the H2b water-passability lock in the surface-map doc.

---

## 6. Two settings meet on the field — environment vs. doctrine

The developer's phrasing was *"connect it to the auto-resolver and the **different settings**."* There are two kinds of
setting, and keeping them straight is what makes the connection clean:

| Setting | Whose call | Set where | What it is |
|---------|-----------|-----------|------------|
| **Environment** | The **ground's** call | The planet view (this map) — fixed by *where you fight* | terrain × condition × weather, composed |
| **Doctrine** | **Your** call | The resolver (Force Management) — your standing orders | stance (close / hold / stand-off / withdraw) + target priority |

**A battle is the region's environment ✕ your doctrine, run through the one shared combat kernel.** The environment is
handed to you by the location — you can only choose it by choosing *where to commit*. The doctrine is yours to set
freely. The planet view fixes the first; the resolver takes the second. They meet on the field.

This is why the prototype's readout ends with a **"Resolve a battle here"** button: in the full client that button
opens the auto-resolver **pre-loaded with the composed environment**, and *then* you set doctrine and watch it resolve.
The environment is not a thing you configure — it's a thing you *inherit from the ground you chose.*

---

## 7. The interaction model — what the player (and the AI) actually DO

The whole loop at the regional zoom, from both seats:

1. **Survey the theatre.** Open zoom 3 on a world you can see (survey-fog gates this — you know the ground where you
   settle or have scouted; `PlanetRegionsDB.Surveyed`). The hex band shows terrain colour per hex, region bands, and
   any weather cell drawn over the hexes it covers.
2. **Read a hex.** Click any hex → the readout composes its terrain × condition × weather into one environment and
   shows the resulting fight: detection range, cover, closing speed, damage-by-nature, attrition, plus a plain-English
   tactical read ("this is a blind, close fight where short punchy weapons win").
3. **Choose your ground.** The decision the whole view exists to serve: *where do I commit?* A defender picks the hex
   whose composed environment favours the fight they want (dig into the mountains-in-a-dust-storm; deny the attacker
   their range). An attacker picks the approach that composes *least* against them.
4. **Commit → resolve.** March a force to the hex (the live per-hex movement), and when it meets an enemy in weapons
   range, the auto-resolver opens with **that hex's composed environment** + your doctrine.
5. **The AI does all of 1–4 with the same primitives.** It reads the same composed environment off the same hexes,
   scores ground the same way, and issues moves through the same order queue. *If it couldn't, the mechanic would be
   too complex by the project's own law* (`CLAUDE.md` "One Verb, Both Seats"). The composition being a property of the
   *ground* — not of a UI panel — is exactly what keeps both seats able to drive it.

**What this view is NOT:** it is not a new order surface. Per the M9 ruling, orders come from Force Management, not
scattered onto the planet view. The planet view's job is to **show the ground and let you read the fight**; committing a
force is the existing movement order, and the environment rides along for free because it's baked into the destination
hex.

---

## 8. Cradle-to-grave & the connection map (the Prime Directive check)

**What feeds INTO this view:**
- `PlanetRegionsDB.SurfaceGrid` (the global cylinder hex grid) — terrain per hex. **ENGINE-WIRED.**
- The planet's condition data — gravity/temp/atmosphere/radiation from `AtmosphereDB` / body data. **DATA-READY** (exists;
  combat reads little of it yet — the honest gap).
- Per-hex hazards (ruling M11 — hazards become per-hex from terrain + geography). **DESIGN** (M11 not built).
- Weather — **DESIGN** (no transient surface-weather system exists yet; the prototype models it as a moving cell).

**What this view feeds INTO:**
- The auto-resolver's environment setting — the composed bundle. Today the resolver reads **one** env; composing three
  layers into it is **the design proposal**. The multipliers themselves are **SIM-PROVEN** in `resolversim.html`.

**What it shares STATE with:**
- `PlanetViewWindow` (the live globe client) — same `SurfaceGrid`, same regions, same units. This view is a *zoom* of
  that window, not a parallel map.
- The ground movement/combat system (`GroundForcesProcessor`) — same hexes units already march and fight on.

**What it TRIGGERS:**
- A battle resolution, pre-loaded with the composed environment. (Trigger is the existing movement-into-range, not a
  new order.)

**The cradle-to-grave chain for the connection** (mineral → … → decision → loss):
- The **terrain** rung is fully live: it's on the map, it's researched into no component (it's just ground), and it
  already shapes a fight.
- The **condition** rung is data-ready: the world *has* the gravity/atmosphere numbers; the missing rung is combat
  *reading* them. A unit's answer to a hostile condition is a **component** — a sealed-systems fit that zeroes the
  toxic/vacuum DoT (already a designed part in the assembler), heat-hardening for firestorm worlds, etc. That's the
  cradle-to-grave hook: you *research and build* the ability to survive an environment, and losing that component
  re-exposes the unit. **The grave rung wires environment survival to the damage system.**
- The **weather** rung is design-only end to end (no transient weather system yet).

---

## 9. Build-state honesty — what's real, what's proposed, the one honest gap

Straight about the layers, the way this project insists on. Three states:

- **🟢 ENGINE-WIRED — the board.** The region ring + the global cylinder hex grid + per-hex terrain are shipped; the
  client is fully on the globe (G6a). The terrain triangle (Open/Cover/Rough) already bends a ground fight. *You can
  fly the zoom-2/zoom-3 view in the real game today.*
- **🟡 SIM-PROVEN — the environment math.** Every multiplier this view shows runs live in `resolversim.html` and
  reshapes a battle there — proven on the bench, **not yet wired into the C# resolver.**
- **🔵 DESIGN — the composition.** The resolver reads **one** environment today. Composing terrain × condition ×
  weather into that one setting — the heart of this view — is the new design call. And two of the three layers'
  *sources* are design-only: **per-hex hazards (M11) and transient weather** don't exist in the engine yet.

**The one honest gap, named plainly:** the board is real and the environment math is proven, but **the wire between them
does not exist yet.** Today combat that happens on a hex does not read that hex's composed environment. This design is
the specification for building that wire — it is not a claim that the wire is built. The prototype is a *model* of the
finished behaviour, not a screenshot of it.

---

## 10. The prototype — `docs/ground/planetview.html`

A single self-contained HTML study of zoom 3. It is a **design study, not shipped UI** — it demonstrates the
interaction and the connection so the developer can react to the *feel* before any engine work is authorized.

What it does:
- **Five worlds** (Earth / Mars / Kiln volcanic / Hoth ice-moon / Styx dead-world) — each with a real condition strip
  (gravity, temp, atmosphere, radiation, daylight) and its own terrain palette + weather kind.
- **A clickable operational hex band** (16 columns × 6 rows, banded into the 4 regions), terrain-coloured, ocean and
  ice handled, a weather cell you can toggle and move across the hexes.
- **An engagement readout** — click a hex and it composes terrain × condition × weather into one environment, shows
  each layer with its LIVE/DATA/THEORY grade, the composed multiplier bundle, and a plain-English tactical read, then
  offers **"Resolve a battle here"** (which, in the full client, opens the resolver pre-loaded with that environment).
- **Honest grading throughout** — every effect carries its wiring status, and the "what's real vs. designed" section
  spells out the board (built), the math (sim-proven), and the composition (design).

**Verification (headless, no CI):** the embedded script compiles clean, the full render path runs to completion under a
DOM stub with zero throws, and the environment-composition math produces finite, positive values across every
planet × terrain combination (checked via `/opt/node22/bin/node`, the same harness family as the resolver sim).

---

## 11. Open questions for the developer

These gate turning this study into engine work; none block the design itself.

1. **Does composition read at battle-open, or re-read each tick?** The clean model opens the fight with the composed
   env once. If weather can roll in *during* a long fight, the env would need re-reading per tick (cheap, but a
   decision). Recommend **open-once** for v1; weather-during-battle is a later refinement.
2. **How coarse is "one condition per planet"?** The prototype treats condition as global (true to the data today).
   But a world could have banded conditions (equatorial firestorm, polar cryo). Recommend **global per world for v1**,
   per-hex condition as an M11 follow-on.
3. **Weather: authored or emergent?** No transient weather system exists. Is weather a scripted cell (simple, the
   prototype's model) or generated from geography + climate (richer, more work)? Recommend **authored cells for v1**.
4. **Sealed/hardened components as the condition answer** — the assembler already has sealed-systems parts. Confirm
   these zero the matching DoT (toxic/vacuum → sealed; firestorm → heat-hardened) so the cradle-to-grave loop closes.
5. **Which zoom owns "commit a force"?** Recommend the existing Force-Management order (M9), with the planet view as
   read-only front door — *not* a new order surface on the map.

---

## 12. One-paragraph summary (for a cold read)

A planet is a **board of environments**. You see it at four zooms — system, globe, regional, city — and the regional
zoom is where battles happen. Every hex carries a composed environment: its **terrain** (the ground), the planet's
**condition** (the world), and any **weather** rolling through. That composition **is** the setting the auto-resolver
opens the fight with — so you choose a battle's environment by choosing **where you commit**, and the AI reads the exact
same thing off the exact same hex. The board is **built**, the environment math is **sim-proven**, and the **wire
between them** — composing three layers into the resolver's one environment setting — is the design this doc specifies.
The prototype (`planetview.html`) is the working model of that finished behaviour.
