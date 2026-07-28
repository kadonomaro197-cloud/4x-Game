# The Designer Multi-Pass Audit — 2026-07-28

**Why this exists.** The developer's call after the 17-pass resolver audit: *"if we're going to get combat to work we
need the designers fully functioning."* Correct — the resolver's job (canon **M19**) is to simulate *"any collection of
components"*, so a component that is recorded wrongly poisons every fix downstream.

**Same two standing rules as the resolver audit:**
> 1. **Every issue recorded, explained clearly and simply.**
> 2. **⛔ DO NOT flag an issue without reading the associated and relevant docs FIRST.**

**Severity key:** 🔴 **BLOCKER** · 🟠 **REAL** · 🟡 **DEBT** · 🔵 **NOTE** · ✅ **verified-good**.

---

## The framing: this audit asks a DIFFERENT question than the 2026-07-08 one

`docs/DESIGNER-AUDIT/` (8 files, ~140 KB, **as-of 2026-07-08**) is a thorough survey — and it asks
**"is the designer UNIVERSAL?"** — can a radar I design mount on a ship *and* a station *and* a ground unit. Its
verdict, still the best one-line summary of the designer: *"the universality is real in the basement and lost on the
main floor."*

**This audit asks the other question: is the designer FAITHFUL?** — does a dial you turn get **recorded correctly**
and **arrive** at the thing that reads it. That is the question the resolver audit forced (root cause **A**: a ground
weapon delivers 2 of 10 values, a missile 0 of 5, and the assembler never writes penetration or per-shot energy at
all).

**Both matter and they are independent.** A part can be perfectly universal and carry a wrong number, or perfectly
accurate and mountable nowhere.

---

## PASS 1 — Re-verify the 2026-07-08 audit at HEAD (20 days and many commits later)

**Docs read first:** `docs/DESIGNER-AUDIT/00-EXECUTIVE-SUMMARY.md` in full; the section maps of `01`–`07`;
`docs/economy/COMPONENT-DESIGNER-DIALS.md` header + progress table; `docs/economy/UNIVERSAL-ASSEMBLY-DESIGN.md`.

### ✅ Still true — do not re-derive these

| Claim (2026-07-08) | At HEAD |
|---|---|
| **`PDC` and `Fighter` are dangling mount flags** — defined, used by zero templates, served by no designer | ⚠ **HALF TRUE — corrected in Pass 2.** **`PDC` is still at zero.** **`Fighter` is NOT dangling** — **15** templates carry it (`passive-sensor`, `warp-stabilizer`, both engines, all three warp/reactionless/inertialess drives, all four power sources, both fuel tanks, `sensor-hardening-module`, `drive-reinforcement`). My Pass 1 row said “still zero” for both; that came from a bad count and is wrong. |
| **The mount-flag data is authored inconsistently** | ✅ still uneven — recounted properly over the **96** `ComponentTemplate` entries: ShipCargo 56 · ShipComponent 45 · PlanetInstallation 45 · GroundUnit 32 · Fighter 15 · Station 7 · Missile 3 · **PDC 0**. *(The Pass 1 numbers were undercounts — use these.)* |
| **A duplicate `spaceport` ID silently shadows itself** | ✅ **STILL PRESENT** — and it is worse (below) |

### Findings

| # | Sev | Finding |
|---|---|---|
| **D1-1** | 🟠 | **THE DUPLICATE-TEMPLATE-ID PROBLEM IS 5×, NOT 1×.** The prior audit found one (`spaceport`). At HEAD there are **five** ids defined twice, so in each case **one definition silently shadows the other and which one wins depends on mod load order**: `spaceport` (**`installations.json` + `storage.json`** — two different files claiming the same part), `hydrogen-sulphide` (**twice inside `atmosphereGases.json`** — the same file, plainly a copy-paste), and the three below. |
| **D1-2** | 🔴 | **⚠ `ground-balanced` / `ground-defensive` / `ground-offensive` ARE DEFINED IN BOTH `combatDoctrines.json` AND `groundStances.json` — so which behaviour a ground formation gets is decided by LOAD ORDER.** This is the **D2 slice's problem made concrete**: the plan says *"ground reads the unified catalog; retire `groundStances.json`"*, and until that lands **the legacy 3-entry stance file and the 25-entry unified catalog are actively colliding on three ids.** ⇒ **D2 is not tidying — it is resolving a live, silent behaviour determinant.** *(And it compounds **P2-2**: the two files can author the reciprocal pair `ToughnessMult`/`DamageTakenMult` differently for the same id.)* |
| **D1-3** | 🟡 | **THE PRIOR AUDIT'S PER-TEMPLATE ANALYSIS IS PROPORTIONALLY STALE.** It counted **67** base-mod component templates; the seven component-bearing files now hold **96** — roughly **+43%** in 20 days. *(Counting bases may differ slightly, so this is "clearly grown", not an exact delta.)* Its file `04-BASEMOD-TEMPLATES.md` enumerates templates one by one, so **its coverage is now partial by construction** — the newest templates were never surveyed. |
| **D1-4** | 🔵 | **THE PRIOR AUDIT'S DIAGNOSIS IS STILL THE RIGHT FRAME, AND ITS COUNTER-EXAMPLES ARE THE TARGET SHAPE.** Two locks — the **mount flag** (shallow: the one Component Designer honours it, the **four downstream assemblers each hardcode a single host test**) and the **processor reader** (deep: **nine duplicated ability pairs**). And three parts of the tree already do it right and should be copied: **industry**, **research/unlock**, and **`EnergyGenerationAtb`** (the one already-universal ability, read by both a space processor and a ground system). **Recorded so this audit builds on it rather than re-deriving it.** |

### What Pass 1 tells us about the two audits together

The 2026-07-08 audit's **Lock #2** (*"an ability only does something if a processor reads that attribute off that
host"*) and my resolver audit's root cause **A** (*"the designer's numbers don't arrive"*) are **the same wall seen
from two sides**: one asks *whether any reader exists on that host*, the other asks *whether the reader that exists
reads the value faithfully*. **A capability needs BOTH to be true, and today many pass one and fail the other** —
a ground weapon *is* read by a ground processor (Lock #2 satisfied) and still delivers 2 of its 10 values (fidelity
failed).

---

## PASS 2 — Does a dial the data AUTHORS actually ARRIVE at the code that reads it?

**The question.** Pass 1 asked whether the 2026-07-08 survey still holds. Pass 2 asks the fidelity question directly, and
it asks it **mechanically** — not by reading templates one at a time and forming an impression, but by checking every
single reference in the base mod against the C# it points at. Four checks, every `GameData/basemod/**` JSON file:

1. Does every `PropertyValue('X')` name a property that template actually defines?
2. Does every `TechData('x')` name a tech that exists?
3. Does every `AttributeType` string resolve to a real C# class **in the namespace it names**?
4. Does every `AtbConstrArgs(…)` supply a number of values that matches a real constructor?

**Docs read first (the standing rule):** `docs/economy/COMPONENT-DESIGNER-DIALS.md` (the dial ledger + the Logistical
cradle-to-grave section), `docs/economy/COMPONENT-DESIGNER-CATEGORIES.md` (the door map), `docs/economy/COMPONENT-DESIGNER-DIAL-AUDIT-2026-07-23.md`,
`docs/DESIGNER-AUDIT/04-BASEMOD-TEMPLATES.md`, `docs/ai/AI-CAPABILITY-CATALOG.md`, `docs/aurora/COMMANDERS-AND-OFFICERS.md`,
root `CLAUDE.md` gotcha #10, and the source of every attribute named below. Two of these findings were **already known
in the docs and are credited as such**; the rest are new.

### First, the good news — what PASSED (do not re-check these)

| Check | Result |
|---|---|
| `PropertyValue('X')` references | **675 of 675 resolve.** Every formula in the base mod points at a property that exists on its own template. |
| `TechData('x')` references | **58 of 58 resolve.** Every research gate names a real tech. |
| `AttributeType` **class names** | **70 of 70 exist** as real C# classes. |
| `AtbConstrArgs(…)` **arity** | **119 of 120 sites** match a real constructor. |

That is a genuinely healthy result and it kills a theory I was carrying into this pass. I had noticed that ten
attribute types are authored at **two different argument counts** in different templates and assumed the short ones
were accidentally starved. **They are not.** Every one is a deliberate, commented backward-compatibility overload —
e.g. `GroundArmorAtb.cs:45` *"The 3-arg ctor keeps every EXISTING base-mod plating template byte-identical"*. The
binder matches by **exact argument count** (`ComponentDesigner.cs:94` → `Activator.CreateInstance`), so adding a dial
means adding an overload, and that is exactly what was done. **Not a bug. Recorded so nobody re-derives it.**

### Findings

| # | Sev | Finding |
|---|---|---|
| **D2-1** | 🔴 | **SIX `AttributeType` strings name a namespace that DOES NOT EXIST — `Pulsar4X.Atb` — so FOUR component templates blow up the instant their designer opens.** The loader does `Type.GetType("Pulsar4X.Atb.LogiBaseAtb")` (`ComponentDesignProperty.cs:103`); there is no `Pulsar4X.Atb` namespace anywhere in the solution (grep: zero hits); `Type.GetType` returns null; **line 105 throws unconditionally** — it is in the property's *constructor*, so it fires before any "is this dial enabled" check and cannot be dodged by leaving the option unselected. **The four dead templates and their real namespaces:** `logistics-office` → `LogiBaseAtb` is in `Pulsar4X.Logistics`; `naval-academy` → `Pulsar4X.People`; `missile-electronics-suite` → `ElectronicsSuite` is in `Pulsar4X.Weapons`; `missile-payload` → **all three** payload attributes are in `Pulsar4X.Weapons`. **All four are wired as live doors** in the client's designer (`ComponentDoors.cs:59, 74, 130, 137` — Weapons/Guided, Sensors/Detection, Logistical/Transfer, Civic/Development), and the designer path has **no `try`/`catch`** (`ComponentDesignDisplay.SetTemplate`, `:84-92`). *(Only the read-only browse card catches — `ComponentsWindow.cs:320-328` degrades to "Cannot evaluate template".)* **Plain English: click "Missile Payload", "Missile Electronics Suite", "Logistics Office" or "Naval Academy" in the component designer and the game throws.** ⚠ **Credit where due: the docs already caught ONE of the six** — `docs/ai/AI-CAPABILITY-CATALOG.md:49` and `docs/aurora/COMMANDERS-AND-OFFICERS.md:66` both name the `naval-academy` FQN typo exactly. **What nobody spotted is that it is a CLASS of six, not a one-off** — and that it kills the entire missile warhead designer. |
| **D2-2** | 🟠 | **An orphan starting design points at one of the four dead templates.** `ScenarioFiles/designs/componentDesigns.json:390-394` defines `default-design-logistics-office` (`"TemplateId": "logistics-office"`). **No scenario's `componentDesigns` list references that id today**, so it is never instantiated and New Game survives. It is a loaded gun: the day any faction file lists it, New Game throws during faction setup — the exact gotcha-#10 shape (a JSON reference that `dotnet test` never walks). |
| **D2-3** | 🟠 | **The missile SHAPED-CHARGE warhead is under-supplied by one argument — it will still throw after D2-1 is fixed.** `missile-payload`'s `ShapeDataBlob` formula passes **5** values (`Trigger Type, Mass1, TNT Equivalent, Liner Radius, Liner Cone Height`) but `OrdnanceShapedPayload` has exactly **one** public constructor and it takes **6** (`OrdnancePayloadAtb.cs:63` — the template never supplies `linerThickness`). Exact-arity binding ⇒ `MissingMethodException`, rethrown as a plain `Exception` by `ComponentDesigner.cs:98-110`. **Plain English: pick "shaped charge" as your warhead type and it throws.** The other two warhead types are fine (Explosive 6-for-6, Submunitions 4-for-4) — which is why the default (Explosive, `Payload Type = 0`) hides it. **This is the ONLY arity mismatch in the entire base mod.** |
| **D2-4** | 🟠 | **THREE build-cost materials do not exist, and the designer SILENTLY DROPS them — the part is just cheaper than it was authored to be.** `ComponentDesigner.cs:61-63` keeps a `ResourceCost` entry only `if (factionDataStore.CargoGoods.GetAny(kvp.Key) != null)` — no log, no warning, no skipped-entry record. The three: **`gallicite`** on `missile-electronics-suite` *(already known — root `CLAUDE.md` gotcha #10 and `docs/DESIGNER-AUDIT/07-RESEARCH-AND-UNLOCKS.md:136`)*, and **NEW: `duranium` and `mercassium`** on `ordnance-cargo-hold`. Both new ones are Aurora mineral names that were never defined here. **347 of 350 cost references are good** — this is a narrow leak, but it is the *exact* failure mode this audit exists to find: a number the author wrote that never reaches the thing that reads it, with nothing anywhere saying so. |
| **D2-5** | 🟡 | **`solarArray` is the worst-authored template in the base mod, and the fault is player-visible.** (a) It is the **only** one of 96 templates whose mount flags are a **raw number** — `"MountType": 1` (`energy.json`) — which deserializes to `ShipComponent` **and nothing else**. So a **solar array cannot be installed on a colony or a station.** It *is* in Earth's `StartingItems` (`sol/earth.json:178`) and in three other scenarios, and it has a design in `componentDesigns.json:679` — the player unlocks a solar panel they can only bolt to a ship. *(The ground-mount exclusion is deliberate and asserted by `PowerPlantGroundMountTests.cs:43`; the **colony/station** exclusion is not discussed anywhere and looks like an accident of the magic number.)* (b) It also carries the base mod's **only duplicate property name** — `Area` (the real slider) and `Area ` **with a trailing space** (a `GuiTextDisplay` that just echoes `PropertyValue('Area')`), so the designer shows Area twice. ⚠ **Latent landmine:** `ComponentDesigner.cs:73` does `ComponentDesignProperties.Add(name, …)` on a `Dictionary` — an **exact** duplicate name would throw `ArgumentException` at New Game. The trailing space is the only thing keeping this one harmless. |
| **D2-6** | 🔴 | **THE MISSING GAUGE — no test anywhere constructs a `ComponentDesigner` for every base-mod template.** `BaseModIntegrityTests` walks the *starting designs*; `WeaponScaleGateTests` / `GroundUnitPartsBaseModTests` / `PowerPlantGroundMountTests` each construct **one named** template. **Nothing walks all 96.** A ~20-line NUnit test — loop the base mod, `new ComponentDesigner(t, …)`, `SetAttributes()`, collect failures — catches **every** finding above (D2-1's six namespace typos, D2-3's arity gap) and would have caught them the day they were authored. This is the Visibility Gate verbatim: *you cannot control what you cannot measure.* **Build this gauge before fixing anything else in this list** — it is what proves the fixes landed and stops the next one. |
| **D2-7** | 🔵 | **A set of dials is BUILT, READ BY THE RESOLVER, AND TURNED BY NO PART IN THE GAME** — the data half of "pretty". The dial exists, the resolver reads it, and every base-mod part sits at the neutral default, so **the decision the dial was built to create does not exist for the player yet.** The list: **flak recoil** (`FlakWeaponAtb.Recoil` — the only flak template is 5-arg ⇒ recoil is always 0; the 6-arg ctor is unreachable from data); **beam combat heat** (`laser-weapon` is 7-arg ⇒ `CombatHeat_kJps = 0`; only `pulse-laser` dials it); **shield recharge rate** (3 of 4 augments sit at the 0.34 default; only `ward-projector` dials it); **armour nature-tuning** (`ground-plating` is neutral 1.0 across all four — that one **is** intentional, "a plain plate", and `ablative-plating`/`reactive-plating` *do* author all seven). Not bugs. **Authoring work**, and cheap: one number in one JSON file turns each of these from an engine feature into a player choice. |
| **D2-8** | 🔵 | **Cosmetic, recorded so it isn't mistaken for a bug later.** `weapons.json`'s `genericWpndbargs` carries `"DescriptionFormula": "'int magSize, int reloadPerSec, int amountPerShot, int minShotsPerfire, WpnTypes type"` — **five** names for a **four**-value formula, an unterminated quote, and a `WpnTypes type` parameter that no `GenericWeaponAtb` constructor has (`GenericWeaponAtb.cs:29,38`). It is a stale human note in a field nothing parses. Harmless; misleading to read. |

### What Pass 2 changes about the plan

The resolver audit's root cause **A** was *"the designer's numbers don't arrive."* Pass 2 splits that in two, and the
split matters because the two halves cost wildly different amounts to fix:

- **The formula layer is SOUND.** 675/675 property references, 58/58 tech references, 119/120 arities. Whatever is
  wrong downstream, it is *not* that the base mod is full of typos in its dials.
- **The BINDING layer has four holes** — and every one is a *string* or a *count* that no compiler and no test ever
  checks: a namespace nobody verifies (D2-1), an argument count nobody counts (D2-3), a material id nobody resolves
  (D2-4), a mount flag written as a magic number (D2-5).

**That is one shape, not four bugs: the base mod is a large body of untyped text that the build cannot check and the
test suite does not walk.** So the fix order is not "fix the four" — it is **D2-6 first (build the gauge), then the
four**, which is the Visibility Gate's rule applied exactly as written. The gauge is a single small test file and it
turns this whole class of failure from invisible into a red ✗ in CI.

**And one hard consequence for combat, which is why this audit was started:** the *missile warhead designer does not
open at all* (D2-1) and its shaped-charge branch is broken underneath that (D2-3). Any plan that relies on the player
designing missiles — the resolver audit's missile-stub findings included — is blocked behind these two.

---

## PASS 3 — Does the dial reach the UNIT, and does RESEARCH gate any of it?

Pass 2 checked whether the data's references bind. Pass 3 walks the two rungs on either side of that: **can the player
reach the part at all** (the designer doors), **does the dial reach the thing that uses it** (designer → assembler),
and **does research gate any of it** (the missing cradle-to-grave rung).

**Docs read first:** `docs/economy/COMPONENT-DESIGNER-CATEGORIES.md` §2 (the locked 11-category × 37-door taxonomy),
`docs/economy/COMPONENT-DESIGNER-DIAL-AUDIT-2026-07-23.md` (the prior dial audit — its **T2a hardcoded-Mass** finding is
credited below), `GroundCombat/CLAUDE.md`, `docs/ground/GROUND-SURFACE-MAP-DESIGN.md` (the unit-designer section),
`docs/combat/RESOLVER-AUDIT-2026-07-28.md` root cause **A**, and the source of every attribute and reader named.

### Findings

| # | Sev | Finding |
|---|---|---|
| **D3-1** | 🔴 | **THE ENTIRE GROUND STACK HAS NO RESEARCH GATE — you can design the best ground unit the game allows on turn one.** Only **18 of 96** templates have *any* tech gate (`TechData(…)`/`TechLevel(…)`), and **42 are BOTH free to research (`ResearchCost: "0"`) AND tech-ungated.** Every ground part is in that 42: all four frames (`human-frame`, `vehicle-frame`, `walker-frame`, `swarm-frame`), all five ground weapons (`ground-rifle`, `ground-cannon`, `ground-autocannon`, `energy-weapon`, `claw-weapon`), `ground-plating`, `ground-radar`, `ground-locomotion`, `ground-magazine`, all three augments (`power-armor`, `shield-generator`, `reflex-booster`), `ground-constructor`, and all three prebuilt units. **Concretely: on turn one, with zero research, the ground-rifle designer will let you build a rifle with `Attack` 5000 and `Range_m` 100 km** — its ceilings are hardcoded constants, not tech. **Now the contrast that shows what right looks like:** `laser-weapon` costs `[Mass]` to research **and its Range ceiling IS the tech** (`"MaxFormula": "TechData('tech-beam-range')"`) — research literally widens what you may design. `railgun-weapon` does the same on `tech-kinetic-yield`. **This is the single largest gap between "planetary combat" and "the depth space combat has":** space earns its numbers, ground is handed them. It is also a straight cradle-to-grave failure — the **research** rung is absent for the whole ground chain. |
| **D3-2** | 🟠 | **`Amphibious` is a dial the player PAYS FOR that does NOTHING.** It is a live slider on `ground-locomotion` (`installations.json:3460`), and turning it on **doubles the part's mass** — the template's own Mass formula is `100 * SpeedFactor * (1 + RoughHandling) * (1 + Amphibious)`. It is passed into `GroundLocomotionAtb` correctly, stored, cloned, and printed in the part description (*"…, amphibious"*). **It is read by zero lines of code in the entire solution.** `HexPathfinder.IsImpassable` returns true for `Ocean` unconditionally (`HexPathfinder.cs:42`), and the method's own comment says it: *"Amphibious/transport gating that would let some units cross is a cradle-to-grave follow-on."* **So the player buys an amphibious unit, pays double the locomotion mass for it, and it still cannot enter a water hex.** Of everything found in three passes this is the cleanest example of the fault this audit exists to catch: a costed decision with no effect. |
| **D3-3** | 🟡 | **`GroundChassisAtb.Size` is a dial that does nothing — and is FREE, which is the only reason it's less bad than D3-2.** A live `GuiSelectionMaxMin` slider on all four frames (human 1, vehicle 6, walker 4, swarm 1; max 10), passed into the attribute, stored, echoed by `Clone()` — and **`grep '\.Size\b'` over the whole GameEngine returns ZERO reads.** `GroundUnitAssembly.cs:95` fetches the chassis and reads `BaseStrength` / `BaseHP` / `CarryClass` / `Locomotion` — never `Size`. It doesn't even cost anything, because the four frames have **hardcoded** masses (20 / 4000 / 2500 / 5) rather than a formula of their dials — *which is the prior audit's **T2a** finding, `COMPONENT-DESIGNER-DIAL-AUDIT-2026-07-23.md:138`, confirmed still true.* |
| **D3-4** | ✅ | **THE DESIGNER → ASSEMBLER HOP IS SOUND — and this MOVES the resolver audit's root cause A.** I built the full ledger: **43 dials across 13 ground attributes**, each checked for a reader. **41 of 43 are read** (the two exceptions are D3-2 and D3-3). Every value on `GroundWeaponAtb`, `GroundArmorAtb` (including all four nature factors), `GroundAugmentAtb` (including the new shield-regen dial), `GroundUnitAtb` (including `Penetration` and `PerShotEnergy`) reaches the assembler. **So the resolver audit's root cause A — "the designer's numbers don't arrive" — is NOT a designer→assembler problem.** The loss is one hop further downstream, at **assembler → resolver** (the `WeaponProfile` hand-off). That is a materially different place to fix than the plan currently assumes, and it is good news: the expensive half is already correct. |
| **D3-5** | 🔵 | **Door coverage is good — 91 of 95 templates are mapped, and the 4 unmapped ones are NOT lost.** `ComponentDoors.Classify` has an explicit fallback to an **"Other"** category keyed by the template's raw `ComponentType`, so `ground-constructor`, `ground-training-cadre`, `sealed-systems` and `stainless-steel-fuel-tank` still appear — just filed under "Construction" / "Augment" / "Fuel Storage" instead of a designed door. Zero doors point at a template that doesn't exist. *(95 unique ids across 96 entries — the one-off is the duplicate `spaceport` from **D1-1**.)* Cheap tidy-up, not a defect. |
| **D3-6** | 🟡 | **Research where it EXISTS is mostly a price tag, not a ceiling.** Of the 18 tech-gated templates, most gate a single dial. `flak-weapon` is the clearest half-measure: it **does** cost `[Mass]` to research, but **every one of its five dial ceilings is a hardcoded constant** (`Muzzle Velocity` 100000, `Damage Per Pellet` 100000, `Rounds Per Second` 100, `Pellets Per Shot` 500, `Tracking` 1), so no amount of research ever widens a flak design. Paying for research that doesn't change what you may build is the weakest form of the gate — the `laser-weapon` shape (`MaxFormula = TechData(...)`) is the one to copy. |

### What Pass 3 changes about the plan

Three things, and the first two point in opposite directions — which is why the pass was worth running.

1. **Good news that redirects a fix.** The designer → assembler hand-off is essentially complete (41/43). The resolver
   audit's root cause **A** should be re-scoped from *"the designer's numbers don't arrive"* to *"the numbers arrive at
   the unit and are then dropped at the resolver boundary."* Fixing the `WeaponProfile` hand-off is a smaller, more
   contained job than rebuilding the designer path — and the plan should say so.

2. **Bad news that is bigger than any single bug.** Ground combat has **no research at all**. Not a weak tree — none.
   That is not a wiring defect to patch; it is a missing rung of the cradle-to-grave chain for the whole ground stack,
   and it is the honest answer to *"does planetary combat have the depth space combat has?"* — **not yet, and this is
   the reason.** The fix is data, not engine: give the ground templates a real `ResearchCost` and replace their
   hardcoded dial ceilings with `TechData(...)`, exactly the way `laser-weapon` already does it. New ground techs will
   need authoring in `techs.json`.

3. **Two dials should be decided, not left.** `Amphibious` is **costed and inert** — either build the pathfinder gate
   (`HexPathfinder.IsImpassable` already documents where it goes) or drop the dial, but do not keep charging for it.
   `Size` is **free and inert** — either give it a meaning (it is the natural driver of the frame's mass, which would
   also close the prior audit's T2a) or remove the slider.
