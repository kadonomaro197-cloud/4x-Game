# Sim-Driven Designer & Assembler Audit — what the sims proved needs fixing

**As of 2026-08-05.** A deep analysis triggered by the developer after the ground-battle sim exposed a real problem
(the sim had to *invent* ground-unit stats). Scope: the ~20 commits of sim work (the resolver sim, the Entity
Assembler builds, the environment layer, the jungle ground battle) treated as a **stress test** of the Component
Designer + Entity Assembler. Every place a sim had to fudge a number, flag a capability PENDING, or work around a
limitation is a **gauge reading** pointing at a tool gap. This doc traces each to a root cause and prescribes the fix.

**This is the sim-evidence VIEW. The canonical per-door reader trace already exists** —
`docs/assembler/06-OUTPUTS-BY-DOOR.md` (every dial → its live reader / PENDING / DEAD, at file:line, 2026-08-02).
This doc does **not** duplicate it; it re-prioritizes it by what the recent sims actually hit, and adds the
ground-assembler stat findings 06 doesn't cover. Verified by three parallel source audits (2026-08-06), each claim at
file:line.

---

## Why this matters (plain English)

A design tool is only honest if what you build in it behaves the way it reads. When I built the jungle ground battle I
had to make up the Tyranids' claw penetration, their HP, and their damage — and then I started *tweaking* those
numbers to make the fight balanced, which the developer rightly stopped. The reason I had to invent them is not a sim
problem. **It's a tool gap:** the Entity Assembler can't produce an authentic *ground* unit the way it produces an
authentic *ship*. The sim was the gauge that made the gap visible.

So the headline is simple and it's the #1 fix:

> **The assembler's SHIP path is complete; its GROUND path is behind its own base-mod path.** A ship built in the
> assembler gets a real firepower/toughness/evasion/shield/penetration profile the resolver reads. A ground weapon
> built in the assembler comes out with **penetration 0 and a single-lump shot**, because the two dials that give a
> ground weapon *teeth* aren't wired through the player-facing path — even though the old monolithic base-mod units
> already have them.

Everything else sorts into four more buckets, below.

---

## The fix list, ranked (the deliverable)

Ranked by **impact × cheapness**. Category codes: **[ASM]** the assembler drops/mis-produces a stat · **[DSGN]** the
designer offers a dial that writes to nothing (the north-star "dead knob" bug, `DESIGNER-NORTH-STAR.md:74-76`) ·
**[DATA]** a base-mod producer is missing · **[ENG]** the engine reader isn't built (a designer fix needs this, and
the tool must *flag* it meanwhile) · **[FLAG]** honestly-pending, keep it marked not sold.

| # | Fix | Cat | Where (file:line) | Cost | Surfaced by | Unblocks |
|---|-----|-----|-------------------|------|-------------|----------|
| **1** | **Carry `Penetration` + `PerShotEnergy` through the ground assembler** — add both as `GroundWeaponAtb` dials, read/sum in `GroundUnitAssembly.Compute`, set on the design + per `GroundWeaponMount` in `ToGroundUnitDesign` | ASM | `GroundWeaponAtb.cs:33-45` (no field) · `GroundUnitAssembly.cs:278-301` (drops it) · reader already live `GroundForcesProcessor.cs:437` + `CombatKernel.cs:315` | **Low–med** (reader exists; 3-site wire + lockstep template bump) | **The jungle ground battle** (I invented claw pen 30 because an assembler claw is pen 0) | Genestealer *rending*, Clone DC-15A AP, AT-TE mass-driver — **3 of 7 franchise ground units** |
| **2** | **Employment producer** — add a base-mod component/template that writes `EmploymentAtbDB.Jobs > 0` | DATA | consumer live `PopulationProcessor.cs:74`; **no producer** (`06:156-157`) | **Cheapest** (whole consumer chain already wired) | Assembler colony builds (the morale term reads 0 everywhere) | The base-game colony morale loop — **every colony, every game** |
| **3** | **Guided-weapon damage: read the real warhead** instead of the flat stub in the firepower sum | ENG (tool must FLAG until fixed) | stub `ShipCombatValueDB.cs:38,446`; real data ignored `OrdnancePayloadAtb.cs:41-55` | **Low** (one read site) | **Every torpedo ship sim** — Sovereign, Miranda, ARC-170, the doubled-Federation fight (torpedoes under-counted) | Any missile/torpedo ship's true firepower |
| **4** | **Sensors band-match bug** — read the receiver's *upper* wavelength edge | ENG | `SensorTools.cs:147` | **Trivial** (one line) | The environment layer (detection is the first env lever) | Every EMCON/detection interaction — correctness |
| **5** | **`GroundMobility.SpeedMultForUnit` should SCALE the frame mode, not replace it** — today a designed drive makes Foot/Tracked/Walker/Hover dead weight | ASM/ENG (behaviour change, dev call) | `GroundMobility.SpeedMultForUnit` (flagged in `GroundCombat/CLAUDE.md`) | **Low** (needs a re-baselined gauge) | The ground battle (frame choice didn't matter) | Ground chassis frame choice becoming meaningful |
| **6** | **Console Space on a ship bridge** — stop offering it on ship mounts (its only reader is colony-gated), or build a ship-side `Span` reader | DSGN (or ENG) | dial `AdminSpaceAtb.cs:25`; reader `ColonyHexMapProcessor.cs:67` (`ColonyInfoDB`-gated); target `Sites/CommandBerthDB.cs:16 Span` | **Cheap** to relabel | The capital-ship builds (bridge dial did nothing) | Command depth on ships |
| **7** | **Chassis √-law structural-efficiency slider** — writes no engine field (`K_SHIP` = zero hits, research=0); drop it or build the research axis | DSGN | `06:139`; `02-IO-MATRIX.md:81` | Cheap to drop | The capital builds (a slider that costs nothing) | Removes a pure dead knob |
| **8** | **Fighter Construction Points** — no `fighter-construction` industry type; drop the dial or add the 6th type | DSGN | `06:194` | Cheap to drop | The carrier builds (Venator/ISD/Death Star) | Removes a dead knob (or enables fighter production) |
| **9** | **Carrier / parasite LAUNCH** — build the sortie order + processor that consumes the wing; the assembler now *designates* "deployable" but nothing acts on it | ENG (tool must FLAG) | `EventTypes.cs:400` (1 ref) · `ColonyInfoDB.cs:36` (write-only) | **Medium** (new order + processor) | The carrier-wing model (Venator/ARC-170/the 50-ARC-170 sortie) | Every Star Wars fighter carrier |
| **10** | **Chassis Substrate axis** — 8 substrate chips are offered in the designer UI but 7 write nothing; stop offering them now, build the `Substrate` field later | DSGN → ENG | UI `01-IO-chassis.md:24-31`; no field `FRANCHISE-UNITS-BUILD.md:749` | Cheap to hide; expensive to build | The Tyranid builds (organic = skin) | Organic/bio units (cosmetic today) |
| **11** | **Aura door** (synapse / buff auras) — entire door 0/4, no engine `aura` match; keep it an *unbuilt proposal*, build the neighbour-sweep pass when funded | FLAG → ENG | `06:259-263` | Most expensive (new per-tick pass) | The Tyranid synapse gap | Synapse, buff/debuff auras |

Items 6–8 and 10 are the **north-star dead-knob cleanup** (dials offered today that write nothing). Items 3, 4, 9,
11 are **engine-reader** work the tool must keep flagging, not selling, until built.

---

## The five buckets (the reasoning behind the ranking)

### A. The assembler's ground path is behind its own base-mod path — the ROOT cause `[ASM]`
The assembler sets **every** stat the ground resolver reads (Attack, Defense, HP, Range, Range_m, Evasion, Shield,
ShieldRegen, Ammo, DamageType, armour-by-nature, per-weapon loadout, **march speed**, **sealing**) — verified at
`GroundUnitAssembly.cs:278-301`. It drops **exactly two**: `Penetration` and `PerShotEnergy`, because
`GroundWeaponAtb` has no dial for them (`:33-45`). So an assembler-built weapon reads pen 0, single-lump shot —
it wins by undodgeable *volume*, never by cracking armour. Yet the **monolithic base-mod units** (`GroundUnitAtb`
infantry/armor/artillery) *do* carry them (armour 20/140, artillery 8/80 — the W1c/W2c dials). **The player-facing
tool is a rung behind the base data.** This is the exact thing that made me invent the Tyranids' rending claws.
*(Correction the audit forced: march-speed, shield-regen, and sealing are NOT dropped — the assembler sets all three.
The gap is precisely {Penetration, PerShotEnergy}, nothing more.)*

### B. The designer offers dials that write to nothing — the north-star "dead knob" bug `[DSGN]`
`DESIGNER-NORTH-STAR.md:74-76`: *"writes none and costs nothing ⇒ a bug in the design."* The genuine violations the
sims touched: the **√-law efficiency slider** (writes no field), **Console Space on a ship** (colony-gated reader),
**Fighter Construction Points** (no industry type), and **7 of 8 Substrate chips** (no field). These are cheap to
either drop or relabel. *The distinction that matters:* these are knobs **offered today** that write nothing — as
opposed to *honestly-flagged PENDING* items (Aura, self-repair, morale/fury), which are designed-but-unbuilt and
correctly marked, so they are **not** law violations. Fix the former; leave the latter flagged.

### C. A base-mod producer is missing — the invisible base-game bug `[DATA]`
The sharpest non-franchise find: the **Employment → morale** loop. The *consumer* is live
(`PopulationProcessor.cs:74` moves colony morale in a ±40 band on employment), but **no component writes
`EmploymentAtbDB.Jobs`**, so the term is **0 in every colony, every game**. Cheapest high-impact fix in the whole set —
the wire is entirely built except the producer.

### D. The engine reader isn't built — flag it, don't sell it `[ENG]`
Some things the assembler builds *correctly*; the resolver just won't read them. The **guided-damage stub** is the
headline: `ShipCombatValueDB.cs:446` assigns a torpedo a flat `0.1 MJ/s` and never reads the real warhead
(`OrdnancePayloadAtb.tntEqMass`, sitting right there), so every torpedo ship's firepower is grossly under-counted —
which is why the resolver sim ships a "warheads real" MODEL toggle and flags it. The **carrier launch** is the other:
`ParasiteLauncherReady` has zero emitters, `FighterStockpile` is write-only, no order sorties a wing — so the assembler
lets you *designate* a deployable wing the engine can't launch. The **Sensors band-match bug** (`SensorTools.cs:147`
ignores the receiver's upper edge) is a one-line correctness fix. For all of these, the tool's job is to **flag**, not
imply they work — exactly as the sim's Honesty panel does.

### E. Absent capabilities that are NOT dead dials — don't chase them `[≠]`
The audit refuted two dossier phrasings, which is useful: **medbay "+health"** and **propulsion
navigator/quietness/suppression** are **not** dials writing to nothing — *those fields don't exist at all*. "Quietness"
is already the wired **Sensor Signature** dial; a nav bonus is explicitly unbuilt. Likewise a **rear firing-arc** has
no dial and no model — and adding one would cut against the aggregate/whole-or-dead combat design (and *One Verb, Both
Seats*). So these are net-new builds *if ever wanted*, not fixes — there is nothing to remove or route.

---

## Explicitly OUT of scope — the pure sim artifacts (not designer/assembler problems)
For honesty about where the line is: several recent sim knobs are the **simulator's** own modelling, correctly kept
*out* of the unit specs — the developer's own instruction ("don't change unit specs for the sim's sake"):
- the **closing-speed** stand-in (a ship's sublight speed isn't a `ShipCombatValueDB` field; it's derived from
  thrust÷mass — the sim uses an editable stand-in);
- the metre-scale **`minGap`** and the **`salvoScale`** pace dial added for the ground battle (a ground firefight's
  tempo, tuned on the *sim's* pace dial, never on a unit);
- the **LD-30 arena** and **degrade-health** models (design targets the shipped resolver doesn't run yet);
- **space combat's environment-blindness** (the env layer is a design hook — see `ENVIRONMENT-CONDITIONS-DESIGN.md`).

None of these are designer/assembler fixes. They're the sim being honest about its own abstractions.

---

## The meta-lesson
The sims are a **gauge for the designer and assembler.** Every fudge, every PENDING flag, every workaround in the last
twenty commits was the tool telling on itself: an assembler that drops a stat (bucket A), a designer knob writing to
nothing (B/C), or an engine reader that isn't built (D). The single fix that would have prevented the specific "sin"
the developer caught — inventing and then tweaking ground-unit stats — is **#1: carry penetration and per-shot energy
through the ground assembler.** Do that, and the next ground sim gets its numbers from the tool, not from me.

*Companion trace: `docs/assembler/06-OUTPUTS-BY-DOOR.md` (canonical per-door reader ledger). Sourced backlog:
`docs/assembler/FRANCHISE-UNITS-BUILD.md:734-766`. The law: `docs/economy/DESIGNER-NORTH-STAR.md:74-76`.*
