# AI Ecosystem — emergent inter-faction politics (NO galaxy-AI)

> **Status: v0.1 DISCUSSION OPENING (2026-07-10).** The **Ecosystem** rung — what happens when several fully-designed factions (Organism level: fractal needs-ladder + traits + mood + transition engine) share a galaxy and must react to *each other*. The last design rung that adds genuinely new machinery; **Galaxy + Supercluster mostly compose it.** Cross-links: `AI-OBJECTIVE-ENGINE-DESIGN.md` (the actors), `AI-COMMAND-AND-COMMUNICATION-DESIGN.md` (traits/mood/the scored-choice engine), `DIPLOMACY-DESIGN.md` (the relationship substrate + treaties + casus belli, already built).

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

## 2. Open design questions (with leans)

1. **Perception — fogged or omniscient?** Does a faction read *estimates* of a rival's strength + *infer* intentions from observed behavior (fogged, like a player; misjudgment a feature; espionage sharpens the picture — the information ledger), or does it cheat and see true values? *(Lean: FOGGED — politics as imperfect-information is more interesting, and it's the same "eyes" the player uses.)*
2. **Alliances — first-class object or pure emergent alignment?** A lightweight **PACT object** (recorded in `DiplomacyDB`; breaking it carries a reputation/casus-belli cost) that each side **independently re-evaluates each cycle** — or purely "we happen to both fight Mars"? *(Lean: PACT-OBJECT-WITH-TEETH — a commitment that costs to break, but NOT a merge; it persists only while it scores as serving each side's goals, so it cracks via the re-plan.)*
3. **Balance of power — emergent or a designed rule?** *(Lean: STRONGLY EMERGENT — no balancing rule; it falls out of §1's structural-threat read. The only thing we build is the correct threat-gauge, not a "balance" behavior.)*
4. **Target / side selection — the same scored engine?** Each inter-faction stance (ally X / attack Y / appease Z / stay neutral) scored by **THREAT** (who endangers me) × **OPPORTUNITY** (who's weak/valuable enough to prey on) × **AFFINITY** (trait/ideology fit — a xenophobe won't ally aliens; shared enemies bond) × **HISTORY** (relationship score, past betrayals), weighted by the faction's traits + current tier. *(Lean: yes — it's the objective/transition engine with inter-faction options in the catalog.)*
5. **Reputation / trust — does history gate who will deal with you?** A reputation accumulated from **observed behavior** (broke treaties → untrustworthy; atrocities → pariah; kept its word → reliable) that gates others' willingness to ally/deal — giving the **Honor trait its ecosystem-scale TEETH** (a serial backstabber wins short-term betrayals but becomes a pariah nobody will ally, which costs it long-term). *(Lean: yes — this is where Honor stops being flavor and becomes a real strategic trade-off.)*

---

*The bet: specify §2's perception + tools + reputation, ensure §1's structural-threat read, and coalitions / balance-of-power / betrayal / the-alliance-that-cracks all emerge from the faction engine we already built — no central galaxy-AI. Galaxy (the arc/crisis) and Supercluster (franchise-staging) then compose this layer rather than adding new mechanics.*
