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

---

## PASS 4 — The assembler → resolver hop: which designed numbers actually reach the fight

Pass 3 found the designer→assembler hand-off sound and pointed at this boundary. Pass 4 walks it exhaustively: for
**every weapon class on both seats**, which of `WeaponProfile`'s **ten** fields come from something the player designed,
and which are a constant compiled into the engine.

**Docs read first:** `Pulsar4X/GameEngine/Combat/WeaponProfile.cs` in full (all ten field doc-comments),
`docs/combat/WEAPONS-DESIGN.md` (the Nature × Delivery frame), `docs/combat/FLEET-COMBAT-CLOSING-DESIGN.md` §ROOT A
(the range decision), `docs/combat/RESOLVER-AUDIT-2026-07-28.md` root cause A, `GroundCombat/CLAUDE.md`, and
`ShipCombatValueDB.Calculate` / `GroundCombatant.ToWeaponProfile` line by line.

### The ledger — ✅ = from the design · ⬛ = a hardcoded constant · ⬜ = always zero

| Weapon | DPS | Velocity | Tracking | Saturation | **Range** | Nature | Delivery | Penetration | PerShotEnergy | Heat | **from design** |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **Ship — Beam** | ✅ | ✅ | ✅ | ✅ | ✅ `MaxRange` | ⬛ | ⬛ | ⬜ | ⬜ | ✅ | **7 / 10** |
| **Ship — Railgun** | ✅ | ✅ | ✅ | ✅ | ⬛ 500 km | ⬛ | ⬛ | ⬜ | ⬜ | ⬜ | **4 / 10** |
| **Ship — Flak** | ✅ | ✅ | ✅ | ✅ | ⬛ 50 km | ⬛ | ⬛ | ⬜ | ⬜ | ⬜ | **4 / 10** |
| **Ship — Plasma** | ✅ | ✅ | ✅ | ✅ | ⬛ *borrows* `RailgunRange_m` | ⬛ | ⬛ | ⬜ | ⬜ | ⬜ | **4 / 10** |
| **Ship — Disruptor** | ✅ | ⬛ light-speed | ⬛ 1.0 | ✅ | ⬛ 400 km | ⬛ | ⬛ | ⬜ | ⬜ | ⬜ | **2 / 10** |
| **Ship — Missile** | ⬛ 100 kJ/s | ⬛ 5 km/s | ⬛ 0.9 | ⬛ 1.0 | ⬛ 1000 km | ⬛ | ⬛ | ⬜ | ⬜ | ⬜ | **0 / 10** |
| **GROUND — any unit** | ✅ `Attack` | ⬛ per-mode | ⬛ per-mode | ⬛ per-mode | ✅ hexes × pitch | ✅ from `Mode` | ✅ from `Mode` | ✅ | ✅ | ⬜ | **6 / 10** |

### Findings

| # | Sev | Finding |
|---|---|---|
| **D4-1** | 🔴 | **YOU CANNOT DESIGN A WEAPON'S RANGE — except on a beam. Five of the six ship classes get a number compiled into the engine.** `ShipCombatValueDB.cs:52, 62, 68, 73`: flak **50 km**, disruptor **400 km**, railgun **500 km**, missile **1000 km**; plasma has no constant of its own and **borrows the railgun's** (`:435`). None of those five templates even offers a Range dial — check `railgun-weapon`: its four dials are Muzzle Velocity, Kinetic Energy Per Shot, Rounds Per Second, Tracking. **There is no way, anywhere in the game, to design a long-ranged railgun.** ⚠ **Why this is a headline and not a tuning note:** the developer's own X9 ruling is that *"the range at which the battle commences is the range of the highest-range weapon on the unit of the group"* — so **the opening range of every battle is set by a fixed ladder (missile > railgun > disruptor > flak) that no design decision can reorder.** The standoff-vs-brawl choice that `docs/combat/FLEET-COMBAT-CLOSING-DESIGN.md` calls the anchor of the whole closing model is therefore made by *which class you mount*, not by anything you designed. The code knows: *"v1 class-default; a per-design field (paid-for in the designer, like beam's MaxRange) is the next step."* |
| **D4-2** | 🔴 | **A MISSILE CARRIES ZERO DESIGNED NUMBERS INTO COMBAT — 0 of 10.** `ShipCombatValueDB.cs:446-448`: damage, velocity, tracking, saturation and range are **all five** stubs (`MissileLauncherFirepowerStub` 100 kJ/s, `MissileVelocityStub_mps` 5 000, `MissileTrackingStub` 0.9, `MissileSaturationStub` 1.0, `MissileRange_m` 1e6). **Every missile in the game fights identically**, whatever warhead, engine or seeker you put in it. *(Confirms the resolver audit; quantified here.)* **And Pass 2 explains why it was never noticed: the missile-warhead designer does not open at all (D2-1).** The two findings compound — **you cannot design a missile, and it would not matter if you could.** Fixing D2-1 without this leaves the player a designer whose dials go nowhere. |
| **D4-3** | 🟠 | **`Penetration` and `PerShotEnergy` are hardcoded 0 for EVERY ship weapon — the armour-matchup dials work on the ground only.** Both are passed as literal `0` at all six ship construction sites. On the ground they come straight from the design (`GroundCombatant.cs:84, 114`). `WeaponProfile.cs` admits the cause in its own doc-comment: *"the ship path folds armour into Toughness, so penetration reaches ships only once that per-source armour reconcile lands."* So the alpha-vs-chip and armour-cracking decisions — `COMPONENT-DESIGNER-DIALS.md` ⚙1 backlog #1 and #2 — **exist in half the game.** Under canon **M19** (one resolver, "it should not matter" whether the fight is planetary or in space) that asymmetry is a straight violation, not a phasing choice. |
| **D4-4** | 🟠 | **A GROUND WEAPON'S VELOCITY, TRACKING AND SATURATION ARE NOT DESIGNABLE — they are three constants picked by the `Mode` dropdown.** `GroundCombatant.cs:66-85`: Melee → (1, 1.0, 1); Artillery → (300, 0.0, **100 000**); everything else → (1000, 0.0, 1). So a ground weapon has **no rate of fire**, no muzzle velocity, no accuracy — the four dials on `ground-rifle` are CarryMass, Attack, Mode, Range_m. Compare `railgun-weapon`, where Muzzle Velocity / Rounds Per Second / Tracking are all real dials the player sets. **This is deliberate and commented** (*"chosen so the kernel reproduces the ground dodge semantics for that mode"*) and it was the right call to get ground onto the shared kernel — but it is precisely the *"same depth space combat has"* gap the fork exists to close, and it is now the ground half of it. |
| **D4-5** | 🟠 | **GROUND CARRIES MORE DESIGN FIDELITY INTO THE FIGHT THAN MOST SHIP WEAPONS DO — the opposite of what everyone assumed.** Ground: **6 of 10**, including the two fields (`Penetration`, `PerShotEnergy`) that **no ship weapon carries at all**, plus a real designed range. Ship railgun/flak/plasma: **4 of 10**. Ship missile: **0**. Only the beam (7) beats ground. **The resolver audit's root cause A — "the designer's numbers don't arrive" — is therefore a SPACE problem at least as much as a ground one, and the missile is the worst offender in the game.** The plan currently reads as though ground is the lossy side. It isn't. |
| **D4-6** | 🟡 | **`HeatPerSecond` is fed by exactly one weapon type in the entire game.** Only beams pass `beam.CombatHeat_kJps` (`:359`); all five other ship classes and every ground weapon pass nothing (0). Combined with **D2-7** — the only beam template that dials combat heat is `pulse-laser`, since `laser-weapon` is 7-arg and defaults it to 0 — **the fleet heat pool is fed by ONE part in the base mod.** The burst-vs-sustained decision the W5 heat model was built for is, in data terms, not in the game yet. |
| **D4-7** | 🟡 | **A doc has gone stale under the code.** `docs/combat/FLEET-COMBAT-CLOSING-DESIGN.md:110-112` still says *"railgun/flak/missile default to 0 (rangeless) — their own range fields are the immediate follow-up"* and describes its gauge as *"railgun = 0"*. **The code moved on and the test moved with it** — `FleetAggregationTests.cs:61` now asserts `Is.EqualTo(ShipCombatValueDB.RailgunRange_m)`. The doc did not. Anyone reading it would plan against a rangeless railgun that hasn't existed for a month. *(Doc-currency job — `docs/DOCS-INDEX.md`.)* |

### What Pass 4 changes about the plan

**One sentence: the lossy side is SPACE, and range is the load-bearing hole.**

- **Re-point root cause A.** Pass 3 cleared designer→assembler; Pass 4 shows the loss is concentrated in
  `ShipCombatValueDB.Calculate`, not in the ground path. The ground path's gap is different in kind — its numbers
  arrive faithfully, there are just fewer of them to send (D4-4).
- **Range is the one to fix first**, because the developer's own X9 ruling routes the *entire* engagement-commencement
  rule through a number that five of six weapon classes cannot express. Give railgun / flak / disruptor / plasma /
  missile a real `Range` dial (a `MaxFormula` of a tech, exactly as `laser-weapon` does it — which also starts closing
  **D3-1**), then feed it instead of the constant. That is one dial per template plus one line per class in
  `Calculate`, and it turns a fixed ladder into a design decision.
- **Missiles need D2-1 and D4-2 fixed TOGETHER.** Opening the warhead designer while the launcher still contributes
  five stubs would ship a designer whose dials demonstrably do nothing — the exact "pretty" failure
  `docs/REALISM-VS-GAMEPLAY-AUDIT.md` exists to prevent.
- **`Penetration` / `PerShotEnergy` on ships is a real M19 violation**, not a phasing choice, and it is blocked behind
  the ship-side per-source armour reconcile. Worth writing up as its own slice rather than leaving it as a comment in
  a doc-string.

---

## PASS 5 — The armour reconcile D4-3 is blocked behind, and the value that never updates

Pass 4 ended on two open questions: *why* are `Penetration` / `PerShotEnergy` hardcoded 0 on ships, and what happens
to a ship's combat value after it is built. Pass 5 answers both.

**Docs read first:** `CombatKernel.cs` in full (the `Combatant` view, all four `ArmourSoak` overloads, `BurstShotCount`,
`ArmourSoakBurst`), `CombatEngagement.cs` §salvo (`:755-780`) and `FleetArmourSoakFraction` (`:1525-1557`),
`ShipCombatValueDB.Calculate` (`:281-560`), `docs/combat/RESOLVER-DESIGN.md` §3 pt 4 + §5 (the reconcile the kernel
doc-comment points at), `docs/combat/RESOLVER-AUDIT-2026-07-28.md` **X14** and **P11-1** (already recorded — extended,
not re-flagged here), `docs/combat/WEAPONS-DESIGN.md` §6.

### Findings

| # | Sev | Finding |
|---|---|---|
| **D5-1** | 🔴 | **"ARMOUR" MEANS TWO DIFFERENT MECHANICS INSIDE THE ONE RESOLVER — and that is what blocks D4-3.** **Ground:** armour is a **flat per-source soak.** `CombatKernel.ArmourSoak(armour, sourceDamage, penetration, natureFactor)` subtracts `(armour − penetration) × 1.5 × natureFactor` off **each incoming source**, and `ArmourSoakBurst` first splits a salvo into `BurstShotCount` chunks so **each chunk is soaked separately**. **Ship:** armour is a **health pool.** `ShipCombatValueDB.cs:518-520` adds `Armor.thickness × ArmorHitPointsPerThickness_J` **straight into Toughness** (joules), and nature-hardening is applied as a **fleet-averaged percentage** off the salvo (`CombatEngagement.cs:770-771`). **Plain English: on the ground, armour stops a fixed amount of each hit; in space, armour is extra hit points.** ⚠ **This is why `Penetration` and `PerShotEnergy` are literal 0 on ships — they are not lazy, they are UNDEFINED against a pool.** There is no flat number for penetration to subtract from, and no per-hit boundary for alpha-vs-chip to matter at. The code says so at `CombatEngagement.cs:1366`: *"penetration/perShotEnergy are ground-side (the ship salvo folds armour into Toughness), so 0 here."* **Under canon M19 — one resolver, "it should not matter" whether the fight is planetary or in space — this is the deepest violation the audit has found: not a missing wire, but two incompatible models of the same word.** Fixing D4-3 means picking one, and the flat-per-source model is the one the shared kernel already implements. |
| **D5-2** | 🟠 | **A SHIP'S ARMOUR HARDENING PROTECTS THE WHOLE FLEET, NOT THE SHIP THAT PAID FOR IT.** `FleetArmourSoakFraction` (`:1525-1557`) takes a **toughness-weighted average** of every ship's per-nature soak across the entire fleet, then multiplies the **whole incoming salvo** by `(1 − thatAverage)`. So bolt ablative plating onto one cruiser and **every ship present takes proportionally less damage** — including the unarmoured freighters. On the ground the same dial is strictly per-unit (each unit's own `Defense` soaks its own hits). **Same designed part, opposite scope**, and it is root cause **B** (the battlefield as one container) surfacing in the armour model. It also inverts a real decision: *"armour the ships that will be shot at"* becomes *"put one hardened hull anywhere in the fleet."* |
| **D5-3** | 🟠 | **EVERY "GRAVE RUNG" CLAIM THAT SCALES BY `comp.HealthPercent` IS INERT — the combat value is computed once and never again.** *(Extends the resolver audit's **X14**; the new evidence is the exhaustive call-site proof and the doc-comment fallout.)* `ShipCombatValueDB.Calculate` has **exactly one production call site: `ShipFactory.cs:144`, at construction.** Every other site is `TryGetDataBlob(...) ? cached : Calculate(...)` — a fallback, not a refresh. And **nothing anywhere invalidates it**: zero `RemoveDataBlob<ShipCombatValueDB>` in the solution, one `SetDataBlob`. So every `comp.HealthPercent` multiply inside `Calculate` is permanently frozen at build-time health (always 1.0). **That silently falsifies the cradle-to-grave "grave rung" written into at least six attribute doc-comments** — `RadiatorAtb`, `ShipMagazineAtb`, `PointDefenseAtb`, `UnitCaliberAtb`, `ArmourHardeningAtb`, and `ShieldAtb`'s *"a damaged/shot-off generator projects a weaker/no shield — the grave rung"* (`ShipCombatValueDB.cs:454`). **The grave rung is documented six times over and cannot fire once.** *(Honest scope: the auto-resolver removes ships whole, so this does not bite an auto-resolved kill. It bites wherever component health actually moves — a missile impact the ship survives, and the case below.)* |
| **D5-4** | 🟠 | **A REFIT IS STALE TOO — installing a component on a built ship does not change what it is worth in a fight.** `Entity.AddComponent(...)` (`Engine/Entities/Entity.cs:125-155`, four public overloads) installs components on an already-built entity and **never** recomputes `ShipCombatValueDB`. So a ship that is refitted with better guns fights with its original numbers, permanently. *(Related: `ShipRefitBegan` / `ShipRefitCompleted` exist as event types in `EventTypes.cs:46-47` and are **published by nothing** — grep returns zero producers. A refit pipeline was scaffolded and never built, which is why nobody hit this yet. Recorded so it is fixed **with** the recompute, not after.)* |
| **D5-5** | 🟠 | **THE AI'S OWN-STRENGTH AND THREAT ASSESSMENT ARE FROZEN AT BUILD TIME.** `FactionRollup.MilitaryStrength` (`:78-86`) sums `cv.Firepower + cv.Toughness` over every ship carrying the blob — i.e. the frozen value. It feeds **three** consumers: `NPCDecisionProcessor.cs:328` (the brain's own-strength input to objective selection), `ThreatAssessment.cs:74` (who to fear), and `AIDecisionRecorder.cs:39` (the decision log the AI's behaviour is *audited* from). **So a faction that has had half its navy shot up still rates itself — and is still rated by its rivals — at full strength**, and the decision log records that wrong number as the reason for the choice. This is D5-3's operational cost on the *strategic* side, the twin of the resolver audit's **P11-1** on the tactical side. |
| **D5-6** | 🔵 | **Credit where due — the ship armour NATURE model is real and IS authored.** I nearly wrote this up as "ships have no armour-nature matchup." They do: `ArmourHardeningAtb` → `ShipCombatValueDB.ArmourSoakVs{Kinetic,Energy,Explosive,Exotic}` (`:470-476`, best installed part per nature, health-scaled) → `FleetArmourSoakFraction`. The `armour-hardening` template exists, has a starting design, is in **Earth's** `StartingItems` and `ComponentDesigns` (`sol/earth.json:213, 315`), and a base-mod ship design mounts one (`shipDesigns.json:407`). **The ship/ground gap is the SHAPE of the armour model (D5-1) and its SCOPE (D5-2) — not the presence of the nature matchup.** Recorded so the fix targets the right thing. |

### What Pass 5 changes about the plan

**The D4-3 fix is bigger than a wiring job, and it has a clear right answer.**

Penetration and per-shot alpha cannot be "wired up" on the ship side, because there is nothing on the ship side for
them to act on. The choice is structural: either ships adopt the kernel's **flat per-source armour** (armour comes back
out of Toughness and becomes a `Combatant.Armour` the kernel soaks per source — the model `CombatKernel` already
implements and `RESOLVER-DESIGN.md` §3 pt 4 already names as the reconcile), or the two domains stay permanently
different and **M19 is not true**. There is no third option that keeps both. The kernel's own doc-comment has been
pointing at this since slice 2: *"Ship toughness folds armour into Health today; the shared per-source model is what
slice 2+ reconciles."*

**And the frozen combat value is now a three-consumer problem, not a combat-only one.** X14 recorded it; Pass 5 shows
its reach: the tactical resolve (P11-1), the **strategic AI** (D5-5), and any **refit** the game ever gains (D5-4).
The fix is small and obvious — recompute on component change and on damage, or drop the cache and compute on read —
but it should be done once, deliberately, with a gauge, rather than three times from three directions. **And it should
be done before the six "grave rung" doc-comments are believed by anyone**, because right now the docs describe a
cradle-to-grave loss that the engine cannot perform.
