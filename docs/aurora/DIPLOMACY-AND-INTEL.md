# Aurora 4X — Diplomacy & Intelligence (Design Reference)

Source: aurora-manual `15-diplomacy/` (v2.7.1). Constants approximate — verify before hard-coding (see `INDEX.md`).

> **In plain terms:** When you meet another alien empire, you start as strangers who can't even talk. First you crack their **language** (a 0–100% bar); the more you understand each other, the more you can do — from "we come in peace" at 20% up to alliance talks at 80%+. A running **points score** sets your relationship (Hostile → Unfriendly → Neutral → Friendly → Allied). Treaties and trade slowly raise it; shooting at them craters it. **Intelligence** is separate: you park ships with "ELINT" listening gear near their worlds and slowly learn what they've got — population, factories, ship designs — without them minding, because passive listening isn't considered spying. **Pulsar now has a built diplomacy substrate and a built espionage chain of its own** (see §6) — this doc is the *Aurora* reference for those systems, not a claim they're unbuilt. The live Pulsar-side designs are `docs/DIPLOMACY-DESIGN.md` (external politics) and `docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md` (intel).

---

## 1. Relationship tiers (a points score)

| Points | Status | Effect |
|-------:|--------|--------|
| < −100 | Hostile | attacks on sight |
| −100…−25 | Unfriendly | high tension, few options |
| −25…+25 | Neutral | default at first contact |
| +25…+75 | Friendly | trade & most options open |
| > +75 | Allied | full cooperation, shared intel |

**Any** weapons damage drops them straight to Hostile if you were above the line.

---

## 2. Talking first (the language barrier)

Communication runs 0→100%; you must build it before real diplomacy:
| Comm % | Unlocks |
|-------:|---------|
| 20–30% | basic messages (peace, warnings) |
| 40–50% | treaty/trade proposals |
| 60–70% | complex negotiation, demands |
| 80%+ | alliance proposals |

First contact needs: both sides set to "Attempting Communication," present in the same system, mutually detected.

---

## 3. Earning (and losing) points

A **xenophobia** trait scales everything — xenophobic races give fewer points and forgive less.

**Per year (positive):**
- Active diplomacy (with module): `((Bonus×4)+1) × 100 × (1 − Xeno/100)`
- Trade agreement: `100 × (1 − Xeno/100)`
- Research treaty: `200 × (1 − Xeno/100)`
- Friendly status (passive): `100 × (1 − Xeno/100)`; Allied: `200 ×`

**Negative:**
- Combat damage: −0.1 to −1.0 per point of damage (shield-only −0.1; internal −1.0; ground combat −0.01/ton)
- Attacking diplomatic ships: −300; unprovoked attack: −100
- Territory intrusion: `SQRT(Tonnage + PopEM×10) × ThreatLevel`

A **Diplomacy Module** (30 HS, 300 BP, 50 crew) in-system is needed for full positive gains (halved without one).

---

## 4. Trade

Needs ≥50% comms, Friendly+, and a Trade Agreement. Generates ~100 diplo-points/yr (× xenophobia factor) plus wealth per delivery (sales tax 0.5, shipping tax 0.25–0.5/jump).

---

## 5. Intelligence (passive only in C#)

C# removed VB6's spy teams. Two passive sources feed a **racial intelligence points** pool:

**ELINT modules** (listening gear on a ship): 1 pt/day each in range, × commander Intelligence bonus, × (100−Xeno)/100, −80% if language untranslated. Decays ~25%/yr when you stop watching.
| Strength | Size | BP | Crew |
|---------:|-----:|---:|-----:|
| 5 | 10 HS | 100 | 15 |
| 14 | 10 HS | 280 | 15 |

(cost = 20 × strength; needs matching EM-sensor tech.)

**What points reveal (about a population):**
| Points | Revealed |
|-------:|----------|
| 100 | population size + total installation count |
| 200 | factories, mines, spaceports |
| 300 | refineries, maintenance |
| 500 | research & training facilities |

Also: 1 pt/day per detected enemy active sensor (full specs at 100 pts). **Prisoner interrogation:** `(Crew/10) + (Rank³)` points, reduced by friendlier relations (Hostile = full, Allied = −90%).

Key: **ELINT has no diplomatic penalty** — passive listening isn't espionage. No counter-intel system. Only **naval** officers get Intelligence bonuses (not ground commanders).

---

## 6. Pulsar status & mapping

**Both a diplomacy substrate and an espionage chain are now built and wired in `GameEngine/Factions/`.** This section maps the Aurora ideas above to the Pulsar code that exists today. The live Pulsar-side design docs are `docs/DIPLOMACY-DESIGN.md` (external politics) and `docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md` (intel). Note the three states below — some pieces are wired all the way to a live trigger, others are built but the player-facing trigger isn't hooked up yet, and the NPC-autonomous behavior ships flag-gated OFF by default. **None of it is runtime-verified through the client** (CI can't run the client), so treat "wired" as "code exists and is connected," not "confirmed working in play."

| Aurora idea | Pulsar code | State |
|-------------|-------------|-------|
| Relationship points / tiers | `DiplomacyDB` + `RelationshipState` (attached to every faction by `FactionFactory`, lines ~383/444); `Diplomacy.DeclareWar/MakePeace/BreakTreaty` | Built. First contact is live — `FirstContact.OnDetection` is called from `SensorScan.cs:120` on a real detection, records a mutual Neutral relationship, fires a first-contact event. |
| Communication %, treaties, trade | `Treaties.Propose/WouldAccept`; `CasusBelli` (militarism gate); `ReactiveDiplomacy` (relationship drift); `TradeIncome`/`TradeIncomeProcessor` | Built but the player trigger isn't wired — `Treaties.cs:30` notes "Nothing calls Propose yet." NPC-autonomous proposals are gated OFF (`NPCDecisionProcessor.EnableDiplomaticProposals = false`). No literal comm-% language barrier; the gate is relationship score. |
| ELINT / racial intelligence | Full espionage chain: `IntelDirectorateDB`/`IntelDirectorateAtb` (registered in JSON, cradle-to-grave component), `IntelDirectorateProcessor` (recruits agents), `Espionage.TaskAgent` + `EspionageProcessor` (resolve ops via `CovertActionCatalog`/`CovertRisk`/`InformationLedger`, with grave-rung agent loss). Client `IntelligenceWindow` reads it. | Built. This is active covert action, not Aurora's passive-only ELINT. NPC-autonomous mirror/ledger are gated OFF (`EnableEspionageMirror`, `EnableIntelLedger` = false). |

**Scoping note (updated for the North-Star pivot):** the old "diplomacy and intel are out of scope / low priority" stamp is retired. Per `docs/NORTH-STAR-VISION.md`, diplomacy-as-politics and intelligence are funded systems, and the substrate above is the result. The Aurora spec in §1–§5 remains the *design reference* for deepening them; the Pulsar-side owners are the two design docs named above.
