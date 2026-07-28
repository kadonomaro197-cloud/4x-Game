# The Designer Retrofit — as-built, intent, and the gap

**What this is.** The developer's call after the 7-pass designer audit: *"now that we know what's wrong with it we need
to collect data on how it currently functions and what the design intent was… then retrofit the designer."* Seven
parallel surveys ran across the designer's whole surface — the formula engine, the template schema and loader, the four
assemblers, the client UI, research gating, the design lifecycle and saves, and a full docs sweep for intent.

**This document is the retrofit's ground truth.** The audit (`DESIGNER-AUDIT-2026-07-28.md`) said what is broken. This
says **how it works, what it was meant to be, and what the distance actually is** — so the retrofit aims at a target
instead of patching symptoms.

**Severity key:** 🔴 **BLOCKER** · 🟠 **REAL** · 🟡 **DEBT** · 🔵 **NOTE** · ✅ **verified-good**.

---

## 1. THE ONE-SENTENCE GAP

> **The taxonomy was built. The collapse was not.**

The design is that a component is *"not a type you pick off a shelf — it's a **family you design inside**… 'Laser,'
'phaser,' 'railgun' aren't authored parts — they're **slider settings** that fall out of the family"*
(`COMPONENT-DESIGNER-CATEGORIES.md` §1), and that **~37 doors replace 67 templates**.

**The measurement that settles it: ZERO dials are shared across any door.** Weapons ▸ Energy holds four templates whose
player-facing dial sets are **completely disjoint** — `laser-weapon` (21 dials), `pulse-laser` (4), `plasma-repeater`
(4), `energy-weapon` (4). Set intersection: **empty**. Weapons ▸ Ballistic, seven templates: also empty. So the design's
own acceptance bar — *"build a phaser, a lazpistol, and a Death-Star beam **from the same sliders**"* — **fails
literally.** You pick a Type from a dropdown and get that template's private slider wall; switching Type **discards
every dial you set**.

And the inverse fault is present too: `ground-rifle`, `ground-cannon` and `ground-autocannon` have **identical** dial
sets (CarryMass · Attack · Mode · Range_m) — three "types" that already *are* one parametric family, uncollapsed.

`ComponentDoors.cs:12-15` admits it in its own header: *"This is the WINDOW's view of the design; the parametric
'laser/railgun are just dial settings' collapse is a **later engine change**."* The navigation is real, correct and
cheap. The engine change is entirely ahead.

**⭐ The cheapest progress gauge for the whole retrofit: the template count.** The design says it should fall 67 → ~37.
It has *risen*: **67** (2026-07-08) → **89** (2026-07-13) → **96** today. Every new hand-authored template is motion
away from the target. Second gauge: **shared dials per door** — today 0.

---

## 2. WHAT THE RETROFIT IS AIMING AT (locked, do not re-litigate)

Thirty-six decisions are already locked in the design docs. The load-bearing ones:

| # | Locked | Source |
|---|---|---|
| **L1** | **ONE designer for every buildable thing.** *"DO NOT build, propose, or document a separate per-domain designer window."* | `UNIVERSAL-ASSEMBLY-DESIGN.md` §0, 2026-07-14 |
| **L2** | Two tools: **Component Designer** (makes PIECES) + **Entity Assembler** (`ShipDesignWindow`, retitled — *"It is NOT a ship designer"*) | ibid. |
| **L3** | Chassis + components, **stats emerge from the sum**, gated by physics, at any scale | ibid. §1 |
| **L7** | **Name the 1–2 AXES first; the role is a COMPUTED READOUT of the specs**, never a picked label | ibid. §2b |
| **L10** | Chassis **kills** the whole-unit shortcuts (`infantry-unit`/`armor-unit`/`artillery-unit`) | `CATEGORIES.md` §2 |
| **L11** | **The parallel ground attribute systems die by DELETION, not merger** — `GroundWeaponAtb`, `GroundSensorAtb`, `GroundLocomotionAtb`, `GroundAugmentAtb`, `GroundArmorAtb`, `GroundMagazineAtb` | `CATEGORIES.md` §3 |
| **L13/L29** | **Anti-dominance + transparency:** *"if an option has no catch, it's a bug in the design"* | `DIALS.md` §1.1 · `CONVENTIONS.md` §16 |
| **L14** | **No authored requirement graph** — mass/volume is the one hard wall, a physical container not a rule | `DIALS.md` §0b |
| **L15** | Under-supply **THROTTLES** visibly; it never hard-fails | ibid. |
| **L17** | ***"Never ship a dead knob."*** | `DIALS.md` §0d |
| **L20** | **THREE** acceptance criteria: **Reachable · Mirrored · Observable.** *"A door that passes only #1 is a display piece."* | `DIALS.md` §0g |
| **L22** | **COUNT RESOLVERS, NOT DOORS** — a new door with no new resolver is DATA | `DIALS.md` §0i |
| **L25** | **THE NAMED KEYSTONE: port the ground carry gate to ships + stations.** *"Do that, and 'the numbers force the build' — the founding promise of the entire designer — becomes real across all scales."* | `DIALS.md` §11 |
| **L36** | **Do NOT big-bang refactor.** Converge incrementally; UX last. | `UNIVERSAL-ASSEMBLY-DESIGN.md` §4 |

⚠ **Two of my own audit's proposed slices collide with these and are withdrawn.** DS-3 proposed *adding*
`Penetration`/`PerShotEnergy` dials to `GroundWeaponAtb` so the three prebuilt units could be retired. **Both ends are
condemned by L10 + L11.** The real target is also locked — *"one universal weapon design (TYPE + specs) that BOTH
resolvers read; the mount decides the setting. **This is the architectural piece — it needs a design doc + phased plan
before code, not a blind refactor. Sequenced with the developer.**"* (`UNIVERSAL-ASSEMBLY-DESIGN.md` §2a).

---

## 3. AS-BUILT — the eight things that most change the plan

| # | Sev | Finding |
|---|---|---|
| **R-1** | 🔴 | **ADDING A CONSTRUCTOR OVERLOAD BREAKS SAVE-LOAD ON MOST `*Atb` CLASSES.** Newtonsoft auto-uses a parameterized constructor **only when there is exactly one public ctor**. Two public ctors + no parameterless ⇒ `JsonSerializationException: Unable to find a constructor to use`. **At risk (one public ctor, no parameterless): `AdminSpaceAtb`, `CargoStorageAtb`, `CargoTransferAtb`, `EnergyStoreAtb`, `EnergyGenerationAtb`, `EnergySolarGenerationAtb`, `GeoSurveyAtb`, `GravSurveyAtb`, `LocalConstructionAtb`, `NewtonionThrustAtb`, `ReactionlessThrustAtb`, `OrdnancePayloadAtb`+subclasses.** ⚠ **This makes DS-4 (add a Range dial to five weapon classes via a new overload) UNSAFE AS WRITTEN.** Mitigation is one line per class, in the same change: `[JsonConstructor] private XAtb() {}` — the pattern `IndustryAtb.cs:20-21` already uses. |
| **R-2** | 🔴 | **FIXING THE SIX NAMESPACE STRINGS UNMASKS TWO WORSE BUGS.** (a) **`ComponentDesign.AttributesByType` is NEVER CLEARED** (written only at `ComponentDesigner.cs:95`). `missile-payload` has three mutually-exclusive payload attributes gated by `GuiIsEnabledFormula`; switching payload type **adds** the new one and **leaves** the old ⇒ **a design carrying two warheads.** (b) **`NavalAcademyAtb` has three public ctors and no parameterless** ⇒ R-1's load break, live. Both are masked *only* because those templates throw first. **DS-0 must fix all three together or it ships worse than it removes.** |
| **R-3** | 🔴 | **A DEBUG-BUILD CLIENT BRICK, 16 WINDOWS WIDE.** `Window.cs:22` `_beginCount` is **static**, incremented in `ValidateBeginCall`, decremented only in `End()`, **with no per-frame reset**. Sixteen windows — including `ComponentDesignWindow.cs:120`, `ComponentsWindow.cs:203`, `ColonyManagementWindow.cs:108`, `ToolBarWindow.cs:277` — put `Window.End()` **inside** the `if (Window.Begin(...))` block. So in a Debug build (`dotnet run`'s default): **collapse the Component Designer once, or let it throw once, and every subsequent `Window.Begin` in the process throws forever — blanking all 52 wrapper windows for the rest of the session.** Release compiles the counter out. ⚠ `Pulsar4X.Client/CLAUDE.md` states the wrapper *"calls `End` unconditionally"* — **that is false**; `Window.cs:57-69` requires the caller to call it. |
| **R-4** | 🔴 | **THE PER-DESIGN RESEARCH PATH ENDS IN A CRASH.** `ResearchProcessor.cs:133` registers a completed design as `IndustryDesigns[tech.UniqueID]` — i.e. keyed `"tech-<guid>"` — while **every consumer keys by the design's own `UniqueID`**. A player who designs a component, researches it, and clicks "+ New Job" hits a **`KeyNotFoundException`**. Invisible today because everything base-mod is `StartResearched`. **This blocks charging research for any part — i.e. it blocks the ground research tree (DS-5).** One-line fix: key by `tech.Design.UniqueID`. |
| **R-5** | 🟠 | **THE LOADER HAS NO IDENTITY CONTRACT, AND THE ONE INTEGRITY SENSOR WATCHES THE WRONG FAILURE.** A duplicate `UniqueID` is a silent **per-field merge** (last-non-null wins, written onto the first object); because a value type always boxes non-null, **`MountType` is overwritten even when the second entry omits it** — a partial override silently zeroes it to `None`. `SkippedEntries` records **exactly one** failure mode (a null key), so **`BaseModIntegrityTests` passes with a live collision.** Live cost: `modInfo.json` loads `installations.json` (#8) before `storage.json` (#9), so storage wins, and **Earth's `default-design-spaceport` builds the Space Port — which has no `CargoStorageAtb`.** The planetary spaceport complex's storage is unreachable data. |
| **R-6** | 🟠 | **TWO DESIGNERS EXIST IN FACT — One Verb, Both Seats violated inside the designer.** Three constraints are **client-only**: the paired-range gap (`SetMaxRange`'s sole caller is `ComponentDesignDisplay.cs:629`), step granularity (`SetStep` uncalled by the engine outside the enum branch), and the fuel-type safety filter (`:950-955` vs the unguarded `ExhaustVelocityLookup`). ⇒ **the JSON/AI path is unbounded, unstepped, unfiltered, and can crash on a fuel the UI would have hidden.** The rich path for the player, the crude path for the AI — exactly what the law forbids. |
| **R-7** | 🟠 | **THE EVALUATOR HAS FIVE LATENT BUGS, HELD BACK ONLY BY WHAT THE DATA HAPPENS NOT TO USE.** `CreditCost` returns `ResearchCostValue` (`ChainedExpression.cs:374`) · `Volume_km3` returns **m³** (`:334`) · `MineralCosts` is a byte-identical duplicate of `ResourceCosts` (`:363`) · `SetPropertyValue` **always throws after its side effect** (never assigns `args.Result`) · **no cycle guard** (`:179-187`) ⇒ a formula cycle is an uncatchable `StackOverflowException`. Usage census: only `Mass` (659 sites) and `GuidDict` (5) are live. ⚠ **My audit's "the formula layer is SOUND" was half right — true of the authored DATA (675/675, 58/58, 119/120), false of the ENGINE. A retrofit that starts authoring new formulas walks straight into these.** |
| **R-8** | 🟠 | **`_isDependant = false` IS SET ON THE WRONG OBJECT** (`ChainedExpression.cs:529` sets `this`, not the new `argExpression`), so the one-shot temp expressions **do** register themselves — the exact failure the code comment forbids. `DependantExpressions` is append-only and never cleared, and `SetAttributes()` runs **every frame of a slider drag**. ⇒ unbounded growth per design session, plus a re-entrant add into the list being iterated = `InvalidOperationException: Collection was modified` mid-designer. The "chained" half of `ChainedExpression` does not work. |

---

## 4. THE STRATEGIC FINDING — the retrofit needs far LESS new schema than the docs imply

**The template schema already contains the parametric mechanism. Almost nothing uses it.**

| Schema feature | What it is | Templates using it |
|---|---|---|
| **`GuiIsEnabledFormula`** | **Conditional dials — the mechanism that makes ONE template behave as SEVERAL. This is exactly what "37 parametric doors" needs.** | **2** — and *both are in the broken-namespace set* |
| `GuiSelectionMinMaxRange` + `PairedPropertyName` + `MaxRangeFormula` | a paired tolerance *band* | **1** (`infrastructure`) |
| `GuiTechSelectionList` · `GuiTextSelectionFormula` · `GuiDisplayBool` | implemented on both sides | **0** |
| `EnumTypeName` | typed option lists | 5 |
| `TechData()` in a Min/MaxFormula | research widens the envelope | 13 |
| `DataDict` | 40 blocks authored | **only 9 reachable** — 31 are inert allocations |

> **"The retrofit needs far less NEW schema than the docs imply — it needs the EXISTING schema exercised, and the two
> templates that already exercise it fixed."**

Corollary: **`ComponentType` cannot be the grouping key** for a category retrofit — 19 free-string values with
near-synonyms (`Energy Generator` / `Energy Generation`). `ComponentDoors` (the client's hand-maintained map) exists to
paper over exactly that, and is a **4th registration point with no test** (4 templates already fall to "Other").

---

## 5. THE SAVE-COMPATIBILITY RISK REGISTER (consult before every slice)

| Change | Safe? | Why / mitigation |
|---|---|---|
| Add a public field to `ComponentDesign` or a **plain** `*Atb` | ✅ | Opt-out serialization picks it up; give it a sane initializer — that initializer **is** the migration |
| Add a field to a **`BaseDataBlob`-derived** `*Atb` | ⚠ | `BaseDataBlob` is `MemberSerialization.OptIn` — **needs `[JsonProperty]`** or it silently is not saved |
| Add a field but not update `Clone()` | ⚠ **#1 retrofit trap** | 19 of 20 DataBlob `*Atb` `Clone()`s enumerate ctor args (`=> new XAtb(a,b,c)`); a new field is silently dropped. Prefer the copy-ctor form (`GroundLocomotionAtb.cs:45`) |
| **Add a ctor OVERLOAD** | ⛔ | **R-1.** Add `[JsonConstructor] private XAtb() {}` in the same change |
| **Add a ctor PARAMETER** | ⛔ | Exact-arity binding — every base-mod template binding that atb must change in lockstep. `BaseModIntegrityTests` is the sensor |
| **Rename / move an `*Atb` class** (namespace counts) | ⛔ | Breaks **twice**: the `$type` on the value *and* the assembly-qualified `Type` **key**. Needs a converter |
| Change an NCalc formula in a template | ✅ | **Existing saved designs are frozen** — values are serialized, formulas are not re-run on load. Note: *reopening* a design DOES recompute, producing a differently-costed twin |
| Remove a field | ✅ | Old key ignored on load |

✅ **Good news: landmine L12 does not currently bite.** All 20 `BaseDataBlob`-derived `*Atb` classes have a `Clone()`.

**Three pre-existing save landmines found:** `OrdnanceDesign` **cannot survive `Game.Load`** (it implements
`ISerializable`, writes only 3 doubles, and has **no deserialization constructor**; CI misses it because the harness
loads no `ordnanceDesigns`, but a menu-started game does) · `NavalAcademyAtb`'s three-public-ctor load break (R-2b) ·
`OrdnanceShapedPayload`'s stats are **private fields** and round-trip as **zero**.

---

## 6. WHAT IS VERIFIED-GOOD (do not rebuild these)

- ✅ **All 20 DataBlob `*Atb` classes implement `Clone()`** — L12 is clean today.
- ✅ **The authored data is consistent** — 675/675 `PropertyValue`, 58/58 `TechData`, 119/120 arities, all 8 required
  `Formulas` keys present in all 96 templates, no duplicate property names, all enum properties supply Min/Max/Step.
- ✅ **Research → designer ceiling works end to end, live, with no caching**, and is CI-proven twice
  (`tech-beam-range`, `tech-kinetic-yield`). One JSON line is the whole mechanism.
- ✅ **The ship assembler is genuinely the good one** — 11 of 15 design surfaces + 100% of attributes, the only host
  with a real engine budget gate and a physical damage layout derived from part volume.
- ✅ **The 11-category taxonomy is correct, cheap and worth keeping** — it makes the shelf navigable. The fault is that
  the collapse behind it never happened.
- ✅ **The description fallback** (an unparseable description degrades to raw text) is **deliberate and well-commented**
  — an apostrophe once corrupted the ImGui window stack. Not a bug.
- ✅ **`TechData`/`TechLevel` degrading an unknown tech to 0** is deliberate and documented — it stopped a designer crash.

---

## 7. THE RE-CUT PLAN

The audit's DS-0→DS-7 track was written against the *symptom list*. Against the *target*, it re-cuts:

**Phase A — make it safe to touch (nothing else can proceed until these land)**
- **A1** — the designer smoke test over all 96 templates **+ the six namespace strings + the `AttributesByType` clear
  + `NavalAcademyAtb`'s `[JsonConstructor]` + the shaped-charge 6th arg.** *(was DS-0; R-2 forces the extra three)*
- **A2** — `ResearchProcessor.cs:133` key fix *(R-4 — unblocks all research work)*
- **A3** — the `Window.End()`-inside-the-`if` fix across 16 windows *(R-3 — a client-wide brick, and the highest-value
  non-designer fix this survey found)*
- **A4** — make the loader's silent drops LOUD: duplicate ids and unresolved cost ids into `SkippedEntries`; wire
  `ChainedExpression.HasErrors()` (**already written, zero callers**) into a validate-all-at-load pass *(R-5, R-7)*

**Phase B — the cheap correctness wins (each independent, each with a gauge)**
- **B1** — the five evaluator bugs *(R-7)* · **B2** — `_isDependant` *(R-8)* · **B3** — the spaceport collision and the
  three undefined materials · **B4** — hoist the paired-range gap, step granularity and fuel filter into the engine
  *(R-6)* · **B5** — decide `Amphibious` / `Size` / `SpeedMult`: wire or remove, but stop charging for `Amphibious`

**Phase C — the keystone (locked as the single highest-leverage build)**
- **C1** — **port the ground carry/mass gate to ships and stations** *(L25)*. This is what makes *"the numbers force the
  build"* true at every scale instead of only on the ground.
- **C2** — give the station and building assemblers somewhere to **put** their computed mass/crew/validity, and give a
  finished building a **post-build identity** *(it currently dissolves into loose colony components)*

**Phase D — the parametric collapse (the actual retrofit; needs a design doc first)**
- **D1** — the one universal weapon design both resolvers read, `GroundWeaponAtb` et al. deleted *(L11)*, the three
  prebuilt units retired *(L10)*. **Explicitly locked as "needs a design doc + phased plan BEFORE code, sequenced with
  the developer."** Start with the three ground weapons that **already have identical dial sets** — the cheapest
  possible proof of the pattern.
- **D2** — ground research gating *(blocked on A2)*
- **D3** — weapon range as a design dial *(blocked on R-1's `[JsonConstructor]` mitigation)*

**Gauge the retrofit on two numbers:** the **template count** (96 → ~37) and **shared dials per door** (0 → the whole
point). Both are one script away and neither can rot.
