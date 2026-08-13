# MSN-04 Sazabi — the walker that flies, translated

**As of 2026-08-06.** Char Aznable's Neo Zeon flagship mobile suit (*Mobile Suit Gundam: Char's Counterattack*, UC 0093),
deep-researched from canon and built through the Component Designer + Entity Assembler as a **ground WALKER with flight**
— the mobile suit's defining dual nature — with the cross-domain half honestly flagged. A clickable preset in the
Entity Assembler (`⬢ MSN-04 Sazabi`).

## Canon specifications (sourced — see bottom)

| Spec | Value |
|---|---|
| Model | MSN-04 Sazabi (Neo Zeon / Anaheim Electronics) |
| Head height / overall | 23.0 m / **25.6 m** |
| Weight | 30.5 t empty / **71.2 t** full |
| Generator output | **3,960 kW** |
| **Total thrust** | **133,000 kg** (2×14,000 + 2×13,300 + 8×9,800) — its signature |
| Armor | Gundarium alloy; **psycho-frame**-encased cockpit |
| Sensor range | ~20,400 m |

**Armament (canon):**
- Waist-mounted **mega particle gun** — 8.8 MW
- **Beam shot rifle** — 10.2 MW (concentrated beam *or* spread "shot")
- **Beam tomahawk** — melee axe, stows in/deploys from the shield (throwable)
- **2× beam sabers** — one in each forearm
- **Shield** — 3 built-in small missiles
- **6× funnels** — 10.6 MW each, **psycommu remote all-range** weapons on the backpack
- **Psycommu system + psycho-frame** — Newtype brainwave interface; the suit reacts as an extension of the pilot

---

## The translation problem — "a walker with flight capabilities"

A mobile suit is the single cleanest thing to expose a real engine limit: **it is a humanoid WALKER that also FLIES.**
Pulsar4X models a unit as a **ground walker** (marches; the ground resolver) **OR** a **flying ship** (space; evasion from
thrust/mass) — **never both.** So the Sazabi forces a choice, and every honest translation loses one half:

| Build it as… | Wins | Loses |
|---|---|---|
| **Ground walker** (chosen) | the mech identity · melee (sabers/tomahawk) · gundarium armour · walks/holds ground · beam guns fire | **flight** (can't take off into space combat) · its true agility is capped at the ground evasion ceiling (0.42) |
| Fighter-ship | flight · space combat · agility → evasion ~0.85 (thrust÷mass, like the ARC-170) | the **walker** identity · melee · ground-hold |

**Per your ask, this build is the WALKER** — with the flight, the funnels, and the psycho-frame **expressed and flagged**
so the intent is on record even where the engine can't read it yet. The three PENDING flags below **are** the
walker-with-flight gap, named:
1. **Flight across domains** — a ground unit taking flight into space/air is unmodeled (the `Flight Thruster` says so).
2. **Funnels — remote / all-range** — a weapon that detaches and strikes from every angle has no reader (aggregate combat).
3. **Psycho-frame / Newtype** — pilot-skill → combat amplification, and thought-control of funnels, have no model.

*The ideal future representation is a **dual-domain unit** — a fighter that deploys as a walker (or vice-versa) — which
is exactly the carry-class/bay seam the assembler already half-expresses. That's the engine work a mobile suit points at.*

---

## The build — capability → component → count → door

| Capability | Component | Count | Door | State |
|---|---|:--:|---|:--:|
| 25 m humanoid war-frame (gundarium) | **Mobile Suit Frame** `gm_mecha` (walker, agile) | 1 | Chassis | ✅ LIVE (walker) |
| Generator (feeds beams + funnels) | **MS Fusion Reactor** `gm_msreactor` (8 MW) | 1 | Power | ✅ LIVE |
| Beam shot rifle + waist mega particle gun | **Beam Rifle / Mega Particle Gun** `gw_beam` | 2 | Weapons | ✅ LIVE (firepower) |
| 6 psycommu funnels (all-range) | **Funnel** `gw_funnel` | 6 | Weapons | 🟠 firepower LIVE · **all-range PENDING** |
| 2 forearm beam sabers (+ shield tomahawk) | **Rending Claws / melee** `gw_claw` | 2 | Weapons | ✅ LIVE (melee, high pen) |
| Gundarium armour | **Heavy Carapace Plating** `ga_heavyplate` | 3 | Defense | ✅ LIVE |
| 133,000 kg thrust — flight + AMBAC agility | **Flight Thruster / Vernier Pack** `gp_flight` | 1 | Propulsion | 🟠 evasion LIVE · **FLIGHT PENDING** |
| Psycho-frame / psycommu (Newtype) | **Psycho-Frame / Psycommu** `ge_psychoframe` | 1 | Enhancers | 🟠 caliber ×1.3 LIVE · **Newtype PENDING** |
| Sensors (psycommu-linked) | **Ground Radar** `gs_radar` | 1 | Sensors | ✅ LIVE (detection) |

*Shield + 3 missiles: omitted (a minor point-defense capability; the shield's tomahawk is folded into the sabers).*

## Min-max — an ace's glass cannon, not a tank

The Sazabi is Char's personal suit: **agility + alpha strike**, not bulk. So it is min-maxed the way the Prometheus was
— light for its host to stay fast, armament-dense:

- **Mass 3,268 / 6,000 budget (54%)** — deliberately *not* filled. On the ground host, evasion is `0.22 + 0.20` when a
  drive is mounted → **0.42, the ground ceiling** (its true flight-agility of ~0.85 is the walker-host cost, flagged).
  Keeping it light also keeps upkeep and detection profile down.
- **Firepower 4.45 MJ/s** — 2 beam guns + 6 funnels + 2 sabers, all ×**1.3** from the psycho-frame's caliber sharpening.
  Alpha-dense for a single unit (a mobile suit out-guns a tank, under-guns a warship — correct).
- **Power closes:** draw 3.3 MW (beams + funnels) ≤ **8 MW** reactor. Gate green.
- **Gundarium ×3** for survivability (toughness 344) without sacrificing the light-frame agility.
- Residual is left **open on purpose** — spending it on armour would make it a slow tank and betray the design; the ace
  suit's value is the dodge and the funnel alpha.

## Computed readout (the assembler's `compute()`)

| Total | Value |
|---|---|
| Mass / budget | 3,268 / 6,000 (54%) — no over |
| **Firepower** | **4.45 MJ/s** |
| Toughness | 344 |
| **Evasion** | **0.42** (ground ceiling; a fighter-host Sazabi would reach ~0.85) |
| Detection | 200 km |
| Power | 3.3 / 8 MW — **OK** |
| Crew | 3 |
| **Build** | **11,392 build-points · 197k credits · 3,650 t materials · RP6** |
| Upkeep | 327 cr/month |

**Cost-in-context:** at ~197k credits it prices like a Clone squad or a hair under an Escort — a single elite mobile
suit costs about what nine clone troopers do, which reads right for an ace's hand-built flagship suit.

## LIVE / PENDING ledger

- ✅ **LIVE:** the walker frame · the mega-particle/beam-rifle firepower · the beam sabers (melee, penetration live on
  ground) · gundarium toughness · the reactor + power gate · detection · the psycho-frame's caliber sharpening ·
  build + upkeep cost.
- 🟠 **PENDING (the walker-with-flight gap, flagged not faked):** **flight across to space** · **funnels' remote /
  all-range attack** · **Newtype amplification + funnel thought-control** · and the sub-modeled **flight-agility**
  (0.42 on ground vs ~0.85 as a fighter).

---

*Sources:* [The Gundam Wiki — MSN-04 Sazabi](https://gundam.fandom.com/wiki/MSN-04_Sazabi) ·
[Gunpla Wiki — RG MSN-04 Sazabi](https://gunpla.fandom.com/wiki/RG_MSN-04_Sazabi) ·
[Baidu Baike — SAZABI](https://baike.baidu.com/en/item/SAZABI/1451221) · manufacturer spec sheets (Bandai MG/RG Ver.Ka).
*Design/reference only — no franchise assets touched; the build uses the generic mobile-suit parts added to the
assembler catalog.*
