# Realism vs Gameplay Audit — does each system earn its weight?

*Written 2026-06-26, from a full sweep of every subsystem (movement/supply, the economy chain, sensing/conflict/people) plus the combat work built this session.*

---

## The rule this doc enforces (the developer's call)

> **A mechanic earns its keep only when it is the source of a player DECISION or a felt CONSTRAINT.**
> Realism the player never has to act on isn't depth — it's *cost*: cost to build, cost to understand, cost to maintain, for zero return.

Two things get called "depth" and they are opposites:
- **Complexity** = how hard the machine is to understand. (A cost.)
- **Depth** = how many interesting decisions the machine spits out. (The goal.)

The cheap way to get depth is **stacking**: many simple, legible rules that *interact*. Realism almost never stacks; simple rules do. **"20 complex realistic systems that are hard to understand mean nothing next to 100 cool, simple, fun systems that stack."** This doc grades every system by that rule and ranks the cheap work that converts built realism into gameplay.

---

## The framework — three buckets

For every system, ask two questions: **(1) what decision-with-a-tradeoff does it hand the player? (2) does it STACK** — does its output create a choice in another system? Then bucket it:

- **EARNS WEIGHT** — the realism *is* a player decision with a tradeoff, and it stacks. Keep it; build on it.
- **PRETTY** — fidelity nobody acts on. Don't feed it more; hide or simplify it behind a legible number.
- **LATENT** — realism that *would* pay rent if a decision were hung on it. The opportunity list — usually one cheap UI/wiring change away from EARNS WEIGHT.

---

## The headline finding

**Pulsar4X is a fidelity showcase, not a decision engine — roughly 95% built, 5% wired.** Across all ~17 systems the simulation is real and often rigorous (Kepler orbits, Tsiolkovsky fuel math, Beer-Lambert armor absorption, EM-spectrum sensors, fuel-aware logistics bidding). Almost none of it hands the player a lever. **The engines are installed; the control panel is not.**

**The actual decision space today is narrow:** combat **doctrine** (the Fleet Combat tab), **fleet composition / ship design** (pre-battle), the **research queue**, **colonize-or-not**, and **which jump gate**. Everything else is flavor or invisible math.

**The reframe this forces on the objective.** "Improve the UI" was always on the goal list — this audit shows it is the **keystone, not polish.** The missing 5% — the decision-levers — is overwhelmingly a *UI-wiring problem on top of engines that already run*: a bricked maneuver planner, no sensor-mode control, no armor designer, no diplomacy screen, no commander assignment that changes anything. **The UI is the missing control panel that turns built realism into a game.** And "give ground combat the same depth as space combat" should mean *mirror the decision spine* — doctrine + composition + one reachable lever — **not** mirror the realism.

**The good news about this session's work:** the combat **doctrine** system (switchable, per-component) is exactly the right kind of mechanic — it *is* the one place real combat decisions live, and every scout independently named it as the game's actual decision space. The weapon-triangle/dodge pass earns weight at design time (rock-paper-scissors fleet building), and the combat **interrupt** built this session earns weight as a *decision-enabler* — it hands the player the *time* to make the doctrine call. We built the part that earns its weight. The audit just shows the rest of the game needs the same treatment.

---

## The verdict board

| System | Realism (fidelity) | Player decision today | Verdict | The missing lever → EARNS |
|---|---|---|---|---|
| Combat **Doctrine** | rules layer | offensive/defensive/etc, switchable, per-component | **EARNS** | *(the spine — the model to copy everywhere)* |
| Weapon triangle / dodge | Med | fleet composition / ship design (R-P-S) | **EARNS** (design-time) | fine as-is; in-battle expression is auto |
| Combat interrupt *(this session)* | — | gives TIME to make the doctrine call | **EARNS** (enabler) | — |
| Research / Tech | Med-High | what to queue, who to staff | **EARNS** (no tension) | add scarcity (tech competes for resources) |
| Weapons + Fire Control | High | assign weapons, pick target, fire/cease | **BARELY** | thermal-override cost; range trade; tracking limits |
| Jump points | Med | which gate | **EARNS** (weak) | pursuit/fuel teeth so the choice bites |
| Damage model | Very High | ship component selection (opaque) | **LATENT** | armor-design UI / in-battle damage-focus order |
| Navigation (`NavWindow`) | Very High | **none — the UI is bricked** | **LATENT** | expose the window (~2-line) → delta-V/intercept planning |
| Storage / Cargo | Med | none — limits too loose | **LATENT** | tighten capacity → forces mine/build choices |
| Movement (Newton/warp) | High | "go to body" only | **PRETTY** | surface fuel scarcity + a Newtonian/intercept order |
| Orbits (Kepler/SOI) | Very High | none — auto-capture | **PRETTY** | curve is flavor; gameplay rides fuel/intercept (above) |
| Logistics | High | none — auto-bidding | **PRETTY** | let player set/override routes + priorities; connect to wealth |
| Colonies / Population | Med | colonize y/n, then auto | **PRETTY** | ongoing scarcity (consumption / morale / workforce slots) |
| Energy / Power | Med-High | none — read-only gauge | **PRETTY** | weapons/engines DRAW power → "guns or thrust?" |
| Sensors | High (EM physics) | none — you see everything | **PRETTY** | active/passive + EMCON toggle (simple model) |
| Factions / Diplomacy / IFF | Low (binary hostile) | none; can friendly-fire | **PRETTY** | IFF check + relationship state + first-contact |
| Commanders | Med | view roster; scientists' research bonus only | **PRETTY** | combat skill read by the resolve; assign-to-fleet |

*(Galaxy/system generation and the time model aren't decision systems — they're the board and the clock. Not graded for "decision.")*

---

## Stop feeding the pretty

Do **not** add more realism to: sensor spectrum math, orbital-integration detail, the power-budget histogram sim, logistics-bidding internals, damage-geometry physics, commander career sims. They are already past the point of gameplay return. When we wire their lever, the right move is usually to *simplify them behind a legible number*, not deepen them.

---

## The cheap-wiring list — ranked by leverage (decision installed ÷ effort)

Each item turns built realism into a decision, and most make systems **STACK** (the multiplier). Almost all are *install a lever on an engine that already runs* — UI + a little wiring + a test — not build a new engine.

1. **Refined materials → component costs — MOSTLY ALREADY DONE (verified 2026-06-26, *contra* this doc's first draft).** Ground truth from the JSON: weapons / sensors / reactors / installations **already** cost refined materials (e.g. laser-weapon → stainless-steel + plastic + electronics + ree-magnetics, `weapons.json:17-27`; passive-sensor → stainless-steel + plastic), the starting colony stocks all six required refined materials, and `BaseModIntegrityTests` + CI **enforce** the chain — so mining → refining → production → combat **is** wired (the earlier "refined materials feed nothing" read was wrong; the chain was completed, partly during the railgun/flak six-point JSON pass). What actually remains is smaller cleanup + tuning: **(a) ~8 dead-weight refined materials** (grade-A/D steel & electronics, nickel-steel, lithium-battery, electricity) that are refinable but required by **nothing** — *pretty*, to CUT or CONNECT to higher-tier components; **(b) tension** — is refining a *felt* bottleneck, or are starting materials over-stocked? (a tuning pass). **The big lever was already pulled; the leftover is cleanup + tuning, not a build.** (This is also why detection is already cradle-to-grave on its material rung — its sensor component already costs refined materials.)
2. **Energy powers weapons & engines.** Cut power → guns don't fire / thrust drops. Turns the existing power gauge into "guns or thrust?" mid-combat and makes reactor/battery ship-design choices matter.
3. **Active/passive + EMCON on a simple detection model.** The ONE detection lever worth having: see far but get seen, or go dark and go blind. Gives fog-of-war, ambush, first-strike — the actual *gameplay* of detection — **without** the spectrum math. *(This is the answer to "what should detection do.")*
4. **IFF check + relationship state.** Fire control refuses friendlies; hostility becomes friendly/neutral/hostile instead of binary; a first-contact event fires. Cheap correctness that unlocks the whole diplomacy axis.
5. **Expose the `NavWindow`.** A complete maneuver/intercept/delta-V planner already exists and is unreachable — no button opens it. ~2-line wiring makes fuel/intercept a real decision and unbricks movement.
6. **Tighten storage + surface fuel scarcity.** Makes "what do I mine," "can this ship make the trip," "do I need tankers" real. Stacks movement × logistics × economy.
7. **Commander combat skill, read by the resolve + assign-to-fleet.** Officers stop being flavor; promotion/assignment becomes a decision that stacks with doctrine.
8. **Armor design / in-battle damage-focus.** Turns the sophisticated damage model into a ship-design and a tactical decision.

---

## How to use this doc

Run every future system — and every existing one *before you touch it* — through one question:

> **What decision-with-a-tradeoff does this hand the player, and what does it stack with?**

If the honest answer is "none," it's *pretty*: don't build it, or install the lever instead. **Name the decision before you build the realism.** This is the firewall that keeps the game from becoming a beautiful simulation nobody plays — the same job `docs/MVP.md` does for scope, this doc does for *weight*.

The "…and what does it stack with?" half is **Connect** — the fourth verb in the per-system discipline (root `CLAUDE.md` → "Keep / Cut / Add / Connect"). EARNS/PRETTY/LATENT grades a system *alone*; Connect is the rule that a system is only as done as its **connections** (code AND data), and the real test is always the cross-system *stack*, never the unit in isolation. Run all four every time a system is touched — that's how the game tightens.
