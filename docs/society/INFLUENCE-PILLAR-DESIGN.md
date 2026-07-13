# Influence — the Fourth Conquest Pillar (design)

**Status:** concepts + path forward (design conversation 2026-07-07). No engine code yet. Reframes the parked "religion 5th civic dial" (`docs/society/GOVERNMENT-AND-POLITICS-DESIGN.md`) into its correctly-scoped form, and unifies with the covert half already designed in `docs/society/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md`.

---

## What it does, in one line

Adds a **fourth way to wage war — conquest by belief, culture, or ideology, with no weapons designed.** You flip a rival's worlds by converting their population until the world **secedes** and joins you. **Religion is flavor #1** of this pillar, not a system of its own.

## Why it matters

- The game has **three conquest vectors** — Military (hot war), Espionage (cold war), Diplomacy (overt pressure) — but **no INFLUENCE vector**: no way to win hearts and minds. This is the missing fourth.
- It turns **legitimacy** — today a mostly-inert internal number — into a **contested battlefield** (attacked from outside, defended by governance). A system earns its weight by becoming a front.
- It's a **complete victory path** (the "every layer a complete game" bar): win by conversion, never build a warship — the player who auto-resolves combat and conquers by faith.
- It **stages named-franchise aspects**: BSG (the religious war *is* the war), Stargate (the Ori crusade, false-god empires, Ascension), Dune (the messianic religion). An aspect earning a system, per the north star.

---

## The universal pillar shape — influence as the fourth column

Every conflict pillar is the **same skeleton** with a different medium. Influence drops in beside the three already designed:

| The skeleton | **Military** (hot war) | **Espionage** (cold war) | **Diplomacy** (overt) | **Influence** (belief war) |
|---|---|---|---|---|
| **Medium** | ships / troops | agents | relationships / treaties | **belief / culture (their people's hearts)** |
| **Delegate** | Admiral / General | Spymaster | Foreign Minister | **Influence Minister / Prophet** |
| **The hand** | (fleet / formation) | agent / operative | Envoy / Ambassador | **missionaries / cultural envoys** |
| **Target** | territory / hulls | secrets | a rival's stance | **a population's allegiance** |
| **Projection** | weapon range / movement | agent insertion | treaty proposal | **missionary delivery / a culture field** |
| **Counter / defense** | point-defense / armor | counter-intel | diplomatic savvy | **state religion / censorship / cultural insulation** |
| **The "kill"** | destroy / occupy | steal / sabotage / turn | ally / vassalize | **convert → the world secedes and joins you** |

Same leader pipeline, same delegate model, same always-on mirror. **Influence is a peer conquest vector at the same altitude as war** — not a sub-feature of government.

---

## The "war without a weapon" — the kill reuses what's built

Influence's kill is **conquest through the legitimacy/rebellion system that already exists.** A world's allegiance is a tug-of-war: the owner's **governance** (governor competence, morale, met demands) props legitimacy **up**; your influence campaign pushes it **down from the outside**. Past the collapse threshold the world **secedes** — and if you've been converting it, it secedes *to your faith/culture* (joins you, or goes independent as a co-religionist). A planet taken without a shot, purely by flipping its people.

Mechanically this is small: `LegitimacyInputs` already carries hook-slots (e.g. `GovernorCompetence`) that a source populates. Influence adds **one more input — external belief-pressure** — exactly parallel. **The rebellion model IS the kill; influence is just a new attacker on the same battlefield.**

## It unifies with espionage — same target, different medium

**Espionage's `sow-unrest` action is already covert influence.** It reaches into a rival's per-system legitimacy to incite rebellion — a *deniable spike*. The influence pillar is the **overt, sustained, ideological version of the same attack** — a *campaign*, not a spike. Same target (their population's allegiance → secession), different medium (open belief-spread vs. covert agitation). So influence **completes an axis espionage already started**, at the loud end, feeding the *same* legitimacy input.

## The conquest spectrum (four routes to one prize)

- **Hot war** — take a world by force (Military).
- **Cold war** — spike its unrest covertly (Espionage `sow-unrest`).
- **Belief war** — convert its people until it defects (Influence).
- **Politics** — get its owner to *cede* it (Diplomacy).

Four routes to the same prize — control of worlds and factions — weapons optional.

---

## Flavors — religion is #1; the machinery is shared

The pillar is one system; the **flavor** is data:

- **Religion / faith** (#1) — temples, prophets, holy worlds, crusade; the BSG/Stargate/Dune vein.
- **Culture** (#2) — the Sins-style soft power: art, media, a way of life others adopt.
- **Political ideology** (#3) — spreading your *government ethos* (liberate the oppressed, export the revolution).

The **government dial sets capacity and style per flavor**: a **theocracy** is strong at *religious* influence, a **democracy** at *cultural*, a **hive/machine** is immune and incapable. That's exactly why religion is a flavor, not the pillar — swap the flavor, same machinery.

---

## How it expresses at each altitude (all riding pillars we've mapped)

- **Empire — the Secular⟷Spiritual dial** (the un-parked 5th civic): sets your influence *capacity + style*, re-skins vocabulary. Rides the government-as-modulator framework (built dials).
- **Population — the Faithful/ideological bloc + demands** (build a temple, declare a world holy, launch a crusade; refuse the faithful → **schism**). Rides the blocs + demand engine + Interior Minister + the per-system rebellion model. *This is the DOMESTIC side of the same pillar — your own people's belief, which the enemy attacks back.*
- **Planet — holy / contested worlds**: a **new site type in the exploration field-site catalog** (`docs/explore/EXPLORATION-CONTENT-DESIGN.md`). A holy world generates faith/legitimacy, draws pilgrims (population/economy), and is a **flashpoint** (a rival holding it = casus belli; you can't bombard it without enraging the Faithful). Rides the planetary-value scarcity we designed.
- **Inter-faction — faith as a diplomacy axis**: shared faith = alliance basis; different faith = friction; **holy war = a casus belli**. Rides `DiplomacyDB` + casus belli.
- **Deep / late — psionics / a metaphysical realm** (religion-flavor's deep end): precognition (foresight that stacks with the detection asymmetry), psionic leaders, risk/reward bargains, up to a **metaphysical late-game crisis**. Rides the exploration anomaly event-chains + tech + the late-game-crisis frontier.

---

## The delivery fork (the one genuinely-new choice)

How does influence *reach* a target world?
- **(A) Missionary-as-hand** — a missionary/prophet is delivered to the target like a spy (reuses the physical-delivery loop from `docs/society/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md`), operates in place raising belief-pressure, and is **interceptable/expellable** (counter-influence). *Recommended first — it's CONNECT (rides the delivery loop + the leader pipeline) and makes influence a decision/target like espionage.*
- **(B) Culture field** — the Sins-style area emanation: influence radiates from your worlds and bleeds into adjacent rival space passively. *This is the genuinely-new build (Pulsar has no influence-spread field). Park it as "Influence 2.0."*

---

## Earns its weight + the mirror

- **The decision:** a whole victory path (conquer by conversion) *and* a standing defense (counter-influence — is a rival flipping my frontier worlds?). Where do you point scarce missionaries; which of your worlds do you shore up?
- **The mirror (co-requisite, same lesson as espionage/diplomacy):** influence is only real if the **NPC runs it back at you** (delegation = NPC AI). An Influence Minister converting an inert opponent is solitaire; the NPC must convert *your* frontier, making counter-influence a standing decision. Legible both ways (Visibility Gate: you see a world's allegiance slipping).

## Cradle to grave

> a **flavor** is set by your government dial → an **Influence Minister** is seated (leader pipeline) → **missionaries** (hands, academy-trained, scarce) are delivered to a target world → they raise **belief-pressure** on that world's legitimacy (competence = rate; countered by the owner's governance + counter-influence) → past the threshold the world **secedes/converts** (conquest, no weapon) → **grave:** missionaries can be **expelled, martyred, or turned** (rung 6); a converted world can be **re-converted back** by the former owner; your own **holy world lost** is a legitimacy + morale blow; a **schism** can split your own faith.

## Connections (Prime Directive)

- **Legitimacy / rebellion** (built) — the battlefield and the kill; add the external belief-pressure input (parallel to `GovernorCompetence`).
- **Espionage** (`sow-unrest`) — the covert twin; both feed the same legitimacy input; influence is the overt/sustained end.
- **Leader pipeline** (`docs/society/GOVERNANCE-AND-DELEGATION-DESIGN.md`) — Influence Minister + missionaries are leaders; same six rungs, stances, contracts, race/gov modulators.
- **Exploration field-sites** (`docs/explore/EXPLORATION-CONTENT-DESIGN.md`) — holy/contested worlds are a site type; planetary value.
- **Diplomacy** — shared faith = alliance; conversion/holy-war = casus belli.
- **Government / internal politics** — the dial (capacity/style) + the domestic Faithful bloc + schism.
- **Delivery substrate** — missionary delivery reuses the spy-delivery loop; culture-field (2.0) is net-new.
- **People-loss rung (rung 6)** — expelled/martyred/turned missionaries.

## Locked vs. open

**Locked (2026-07-07):**
- **Influence is the fourth conquest pillar** — a peer to Military/Espionage/Diplomacy, same skeleton, same leader pipeline, same mirror.
- **The kill = conversion → secession**, via the built legitimacy/rebellion system + one new "external belief-pressure" input. War without a weapon.
- **Espionage `sow-unrest` = the covert twin** — same target, different medium; influence completes the axis at the loud end.
- **Religion is flavor #1** (culture, political ideology are #2/#3 on the identical machinery); the government dial sets capacity + style per flavor. This un-parks the religion 5th-civic dial into its correct scope.
- **Delivery = missionary-as-hand first** (rides the spy-delivery loop); the **culture-field is 2.0** (the one genuinely-new build).
- **The NPC mirror is a co-requisite** — counter-influence is a standing decision.

**Open (decide when we build):**
- Belief-pressure math (attack rate vs the owner's governance defense) — calibration so a campaign can *nudge* a world toward secession over time but not flip it overnight.
- Whether conversion flips a world *to you* vs *to independence* vs *to a co-religionist third party* — and re-conversion dynamics.
- The Faithful-bloc + schism specifics (shared with the internal-politics demand engine).
- Psionics / metaphysical frontier depth (its own later system).
- Culture-field (2.0) spread model, if/when built.

**Build order (after the leader pipeline + the legitimacy system, both of which exist/are-designed):**
1. **The external belief-pressure input** on `LegitimacyInputs` + conversion→secession (the kill) — the smallest slice that makes influence real; reuses the rebellion model.
2. **The Influence Minister + missionaries** (rides the leader pipeline) + a **religion flavor** + missionary delivery (rides the spy-delivery loop).
3. **The NPC mirror + counter-influence** (co-requisite) + the allegiance-slipping readout (Visibility Gate).
4. **Holy worlds** (a field-site catalog entry) + the **diplomacy axis** (shared faith / holy war).
5. **Frontier:** the Faithful-bloc/schism depth, the psionic/metaphysical end, and the culture-field (2.0).
