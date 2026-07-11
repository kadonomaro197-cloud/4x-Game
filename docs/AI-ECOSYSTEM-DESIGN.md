# AI Ecosystem — emergent inter-faction politics (NO galaxy-AI)

> **Status: v0.2 DISCUSSION DRAFT (2026-07-10).** Core questions RESOLVED (§2): fogged perception with **ignorance-as-a-driver** (intel-gathering is a trait-scored objective — Borg aggressive-scan vs Federation trade/research-agreement); **alliances EARNED organically** (shared-adversity/boot-camp bonding, depth decides if they survive the enemy's death); **balance-of-power emergent with NO rubber-band** (react-or-die → intelligence IS survival); **scored stance-selection**; **reputation as a fog-gated SOCIAL property** (needs a 3rd party; covert betrayal is free unless caught — Honor×Guile). Plus the interlocks (§3). The **Ecosystem** rung — what happens when several fully-designed factions (Organism level: fractal needs-ladder + traits + mood + transition engine) share a galaxy and must react to *each other*. The last design rung that adds genuinely new machinery; **Galaxy + Supercluster mostly compose it.** Cross-links: `AI-OBJECTIVE-ENGINE-DESIGN.md` (the actors), `AI-COMMAND-AND-COMMUNICATION-DESIGN.md` (traits/mood/the scored-choice engine), `DIPLOMACY-DESIGN.md` (the relationship substrate + treaties + casus belli, already built).

---

## 0. The load-bearing principle — there is NO galaxy-AI; the Ecosystem is the Organism engine pointed OUTWARD

No central mind stage-manages galactic politics. **Every inter-faction behavior EMERGES from individual factions each running their own needs-ladder + transition engine, with the OTHER FACTIONS as inputs** (a rival is just another situation/gauge — "a rising rival" is already one of the bounded situation-types). The Ecosystem layer adds only two things:
1. **What a faction PERCEIVES about the others** (the reading game).
2. **The inter-faction TOOLS** (ally / betray / threaten / appease / vassalize — the diplomacy actions, mostly already in the substrate).

The engine we already built produces the politics.

### The acceptance test — trace every emergent behavior back to individual engines
If a desired behavior needs a special *"galaxy rule,"* we've failed the no-galaxy-AI principle. The four we want, traced:

- **Balance of power** → each faction's *Survive-instinct* reads a would-be hegemon as a survival threat → individually balances (ally against the strongest / build up). *Emergent.*
- **Coalition vs. a rising threat** → several factions independently read the same riser as a threat → each seeks allies against it → they find each other (aligned objectives). *Emergent.*
- **The alliance CRACKS when the common enemy dies** → the alliance was each faction's *"ally against X to survive"*; X gone → the threat-gauge drops → the alliance objective no longer scores → each **re-plans** (the transition engine's re-score) and old ambitions resurface. **The dissolution is just the re-plan firing when the situation changes** — no "alliance expiry rule." *Emergent.*
- **Betrayal** → a faction reads *"my ally is now weak/distracted; taking them serves my goal more than the alliance"* → re-plans to attack, **gated by Honor** (high-Honor won't; low-Honor will). *Emergent, trait-gated.*

Every one traces back. The Ecosystem is the **inputs + tools** that let the built engine produce politics — not a second engine.

---

## 1. The ONE thing we must get right — the STRUCTURAL-threat read

Balance-of-power emerges **only if a faction fears RISING power, not just active attacks.** The threat-gauge must register *"a neighbor becoming powerful enough to eventually crush me"* as a survival threat **now** — otherwise you get **runaway snowballing** (one faction wins because nobody opposed its rise until too late — the classic 4X-AI failure). This is the load-bearing requirement of the whole rung: the survival-read must include **structural / rising threat**, not just "is attacking me this turn." Get this right and the balance-of-power dance emerges; get it wrong and the galaxy snowballs.

---

## 2. Design questions — RESOLVED (developer's calls, 2026-07-10)

1. **Perception → FOGGED, and IGNORANCE is itself a driver.** A faction reads *estimates* of a rival's strength and *infers* intentions from observed behavior; espionage sharpens it; misjudgment is a feature. **The upgrade:** an information deficit is a **GAUGE** — "I know nothing about faction X and that's dangerous" — that spawns an **objective** (close the gap), satisfied by a **trait-scored plan** like any other transition:
   - **Borg** → aggressive probes/scans into enemy space, *tip-off be damned* (Aggression + Collectivism).
   - **Federation** → open a trade route / research agreement to *earn* access peacefully (low-Xeno, Honor).
   - **Guile faction** → infiltrate quietly (covert intel).
   So fog is **interactive** — the AI *acts* on not-knowing; intelligence-gathering is a first-class driven objective, not background bookkeeping. "The AI must see the problem and be given options to solve it."
2. **Alliances → EARNED organically, not declared.** An alliance is the **top of a warming trajectory**, never a seed pact. A **shared enemy is the spark** (soft alignment); the **bond is built by shared adversity + cooperative deeds** (fighting the common foe together, sending aid) — *boot-camp / shared-trauma bonding*. A formal pact is the **capstone**, not the start. **Emergent gradient:** the *depth* of the bond decides whether it survives the enemy's death — a shallow marriage-of-convenience cracks the moment the threat's gone (the re-plan fires); a deep brotherhood-of-arms forged over a long war may endure. (The `DiplomacyDB` relationship score already models the warming.)
3. **Balance of power → EMERGENT, and NO rubber-band.** *"If you can't react to a rising power quick enough, tough — space is unforgiving."* No artificial balancing, no safety net; a missed threat-read is a legitimate loss, and snowballing is a valid outcome. **This is what makes Q1 matter:** with no net, **intelligence IS survival** — the faction that invested in knowing its neighbors sees the hegemon rising in time to act; the blind one gets snowballed before it noticed. **Consequence:** the **structural-threat read (§1) must be genuinely good** — a competent faction MUST be able to see a riser — because there's nothing catching it if it can't.
4. **Target / side selection → the scored engine.** Each inter-faction stance (ally X / attack Y / appease Z / stay neutral) scored by **THREAT × OPPORTUNITY × AFFINITY × HISTORY**, weighted by traits + current tier (Survive-tier picks defensively; Ambition-tier picks a predator's targets). Confirmed.
5. **Reputation → real, but SOCIAL and FOG-GATED (needs a 3rd party).** Reputation only works if someone else is there to *learn* of the deed — with only two factions there's no reputation, just a private grudge, so **reputation is an emergent property of a 3+ faction galaxy** (developer's hard requirement). And it's **fog-gated:** a **covert** betrayal (a secret op) only costs you *if you're caught*; an **overt** one (breaking a public treaty, an open backstab) is known to all who can observe. So **Honor × Guile interact** — a Guile faction betrays in the shadows and keeps its name; an open brute eats the hit; and getting *caught* in a covert betrayal is catastrophic (reputation hit *plus* you're exposed as a schemer). Reputation propagates through the same channels as all info (observation + the wronged party telling your rivals). This is where the **Honor trait gets its ecosystem-scale teeth.**

## 3. The interlocks (why the rung "clicks")

- **Q1 × Q3 — intelligence is survival.** No rubber-band (Q3) is what *rewards* intel-gathering (Q1): good eyes = early warning on a riser = you live; ignorance = a surprise death. Q3 supplies the incentive that makes Q1 worth doing.
- **Q2 × Q5 — cooperation builds both bond AND reputation.** The shared-adversity deeds that deepen an alliance (Q2) are the same observed reliability that builds a good reputation (Q5). Fighting beside someone is *how you earn trust*, twice over.
- **Q5 × Guile/Honor — the shadow game.** Covert betrayal preserves reputation unless exposed → the espionage detection game (getting caught) becomes the hinge of the whole trust economy. High-Guile-low-Honor = the schemer nobody can pin; low-Guile-low-Honor = the pariah everyone shuns.
- **Q1 × Q5 — reputation rides the fog.** You only lose standing with factions that *learn* of your betrayal — reputation propagates through the same perception channels as everything else, so a well-hidden deed (or an isolated victim who can't spread word) costs little.

---

*The bet holds: perception (with ignorance as a driver) + earned alliances + a good structural-threat read + scored stance-selection + fog-gated social reputation, and coalitions / balance-of-power / betrayal / the-alliance-that-cracks all EMERGE from the faction engine we already built — no central galaxy-AI. Galaxy (the arc/crisis) and Supercluster (franchise-staging) then compose this layer rather than adding new mechanics.*
