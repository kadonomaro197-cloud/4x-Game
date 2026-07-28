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

---

## PASS 6 — How much of "ONE resolver" is actually one thing?

Pass 5 found two incompatible armour models living inside what the docs call a single resolver. That raised an obvious
question I had been assuming the answer to: **how far did the resolver merge actually get?** Pass 6 measures it —
every member of `CombatKernel`, counted by production callers — and then checks the shield, the one defence Pass 5
did not walk.

**Docs read first:** `CombatKernel.cs` class doc-comment in full (the "wiring status" paragraph),
`docs/combat/RESOLVER-DESIGN.md` §5, `GroundCombatant.cs` in full, `CombatEngagement.ApplyShield` (`:1576-1590`),
`FleetCombatStateDB.cs`, `GroundForcesProcessor.cs:390-400`, `GroundUnitAssembly.cs:190-300`, and the two gauge
fixtures (`GroundKernelBridgeTests`, `GroundUnitAssemblyTests` §shield-recharge).

### The measurement — `CombatKernel` members by PRODUCTION callers

| Kernel member | production refs | verdict |
|---|---|---|
| `ArmourSoak` | 6 | shared ✅ |
| `ArmourSoakBurst` | 4 | shared ✅ |
| `BurstShotCount` | 3 | shared ✅ |
| `HitFraction` · `ShieldSoakFraction` | 2 each | shared ✅ |
| `LandedFraction` · `SoakFractionOf` · `ResolveShield` | 1 each | shared ✅ |
| **`Combatant`** (the neutral view) | **4 — all inside `GroundCombatant.cs` itself** | **ZERO real consumers ❌** |
| **`ResolveSalvo`** (a shared salvo loop) | **0** | **DOES NOT EXIST ❌** |

### Findings

| # | Sev | Finding |
|---|---|---|
| **D6-1** | 🔴 | **THE "ONE SALVO KERNEL" SHARES THE ARITHMETIC, NOT THE STRUCTURE — and that is precisely why D5-1 is possible.** `CombatKernel.Combatant` is the neutral view the class doc calls the load-bearing idea (*"written to a neutral view that a ship OR a ground unit can present, so neither the hex board nor the ship `Entity` leaks into the math"*). **Nothing in production ever builds one.** Its only four references are inside `GroundCombatant.cs` — the doc-comment, the method signature, and the `new` — and the sole *caller* of `ToCombatant` is a single test (`GroundKernelBridgeTests:141`) that asserts the mapping and stops there. **The ship side never builds one at all.** There is also **no `ResolveSalvo`** — the kernel has no salvo-level entry point, so **each domain still runs its own loop over its own types** and simply calls the same helper functions. **Plain English: the two resolvers agree on the formulas and disagree on the shape of the fight.** That is exactly how you get two incompatible armour models (**D5-1**) while both sides honestly call `CombatKernel.ArmourSoak` — sharing a subtraction does not force you to agree on what you are subtracting from. **The merge reached the arithmetic layer and stopped before the structure layer, and the docs read as though it reached both.** |
| **D6-2** | 🟠 | **THE DEAD BRIDGE WOULD REGRESS A WORKING FEATURE IF SOMEONE WIRED IT UP AS-IS.** `ToCombatant` hardcodes `ShieldRegen = 0` (*"regen 0 in v1"*, `GroundCombatant.cs:130`). But the live ground resolver **already** regenerates shields per unit from a **designed** dial — `GroundForcesProcessor.cs:395`, `u.CurrentShield + u.Shield × u.ShieldRegenFraction × (dt/3600)` — fed part → design → raised unit and gauged by a named test (`GroundUnitAssemblyTests:346-382`: the Ward Projector's 1.0 vs the standard generator's 0.34, *"a real burst-durability-vs-one-hit-buffer choice"*). **So the obvious next merge step — route ground through the neutral view — silently deletes a dial that works today.** This needs flagging **on the bridge**, not discovered afterwards. |
| **D6-3** | 🟠 | **SHIELDS ARE ONE FLEET-WIDE POOL IN SPACE AND PER-UNIT ON THE GROUND — the same split as armour.** `CombatEngagement.ApplyShield` (`:1576-1590`) drains **`FleetCombatStateDB.ShieldPool_J`**, a *single* pool seeded from the summed capacity of every ship in the fleet. So one battleship's shield generator absorbs damage aimed at the fleet's unshielded freighters, and the pool shrinks only when ships are lost. On the ground every unit owns its own pool and its own regen. ⚠ **That makes THREE things now splitting the same way — the battlefield container (resolver audit root cause B), armour hardening (D5-2), and the shield.** These are not three defects; they are **one unstated design decision — space AGGREGATES, ground INDIVIDUATES — surfacing in three places.** It should be settled once, deliberately, because M19 says the two should not differ at all. |
| **D6-4** | ✅ | **THE GROUND SHIELD CHAIN IS THE BEST-BUILT THING THE AUDIT HAS FOUND — copy it.** `GroundAugmentAtb.ShieldRegenFraction` (a real JSON dial, 6-arg ctor) → `GroundUnitAssembly.cs:190-241` (a **Shield-weighted** average across mounted augments, not a naive mean) → `GroundUnitDesign.ShieldRegenFraction` → `GroundForcesDB.cs:627` onto the raised unit → `GroundForcesProcessor.cs:395` in the resolve — with a test that asserts the *decision* (fast small ward vs slow big generator), not just the plumbing. **Every rung present, gauged, and traceable in one grep.** *(The ship shield's regen is designed too — `ShieldAtb.RegenRate_Jps` → `ShieldRegen_Jps`; the difference from ground is **scope** (D6-3), not fidelity. Stated precisely so the fix targets scope.)* This is the shape to hold every other chain against. |

### What Pass 6 changes about the plan

**"One resolver" is currently true of the formulas and false of the fight.** Every fix proposed in Passes 4 and 5 — the
armour reconcile, penetration on ships, per-source alpha — quietly assumes a shared structure that does not exist. So
the ordering changes:

1. **The structural merge is the prerequisite, not the follow-up.** Give the kernel a real salvo entry point and make
   **both** sides present `CombatKernel.Combatant`. Until that lands, "fix penetration on ships" has nowhere to land,
   and every future divergence is free to reappear — nothing in the build stops it.
2. **Settle the aggregate-vs-individual question ONCE** (D6-3), before wiring anything else. Three systems already
   split on it; a fourth will follow. M19 says they should not differ, and that is a decision the developer owns, not
   something to infer from whichever side is cheaper to change.
3. **Carry D6-2 as a hard note on the bridge itself** so the merge does not cost a working, tested, authored dial on
   its way past.
4. **Hold every chain against D6-4.** It is the only end-to-end example in the codebase of a dial that is designed,
   assembled, delivered, resolved, *and* gauged on the decision it creates. It is what "done" looks like.

---

## PASS 7 — Doctrine: canon says it is THE ONLY THING that changes how forces act. Does it?

The developer's canon is unambiguous: *"the combat doctrine is **THE ONLY THING** that changes how those forces will
act when combat begins."* And `CombatDoctrineBlueprint`'s own class doc-comment agrees — *"it is the container for
every combat BEHAVIOUR decision — not just a strength multiplier."* Pass 7 checks that claim field by field.

**Docs read first:** `CombatDoctrineBlueprint.cs` in full (all 14 fields + the reciprocal-trap note),
`Combat/TargetPriority.cs` in full, `CombatDoctrine.cs`, `FleetDoctrine.cs`, `FleetDoctrineDB.cs`,
`GroundFormationDoctrine.cs`, `GroundTacticalBrain.cs`, `GroundStanceBlueprint.cs`, `docs/combat/COMBAT-DESIGN.md`
System 4, `docs/ground/GROUND-GAMEPLAY-DECISIONS-2026-07-24.md` (rulings #15, #18, #19 — the three this pass turns on),
and `UnifiedDoctrineTests.cs` in full.

### The ledger — all 14 blueprint fields (all 14 ARE authored in the 25-entry catalog)

| Field | Authored | Reaches the fight? |
|---|---|---|
| `FirepowerMult` | 25/25 | ✅ space + ground |
| `ToughnessMult` | 22/25 | ✅ space + ground |
| `DamageTakenMult` | 6/25 | ✅ ground (the reciprocal encoding) |
| `CooldownSeconds` | 28/28 | ✅ all three switch sites |
| `IsRetreat` | 25/25 | ✅ space |
| **`RetreatCasualtyThreshold`** | 25/25 | ❌ **CORRECTED — inert.** `EffectiveRetreatCasualtyThreshold` has **zero callers anywhere, including tests**; the resolver reads a `const 0.5` swung by personality (see **C-1**) |
| `EngagementPosture` | 25/25 | ✅ **space only** (ruling #19) — see D7-3 |
| `Domain` · `Family` · `DisplayName` | 25/25 | ✅ filtering + UI |
| **`TargetPriority`** | 25/25 | ❌ **parser only — no selector exists** (ruling #18) |
| **`BreakAwaySeconds`** | 25/25 | ❌ **accessor only, zero callers** (ruling #15) |
| **`Pursues`** | 25/25 | ❌ **accessor only, zero callers** (ruling #15) |
| **`SpeedMult`** | 25/25 | ❌ **read by no movement code — but PRINTED in the client** |

### Findings

| # | Sev | Finding |
|---|---|---|
| **D7-1** | 🔴 | **⚠ CORRECTED (cross-audit, below): FIVE — not four — OF THE FOURTEEN DOCTRINE DIALS NEVER REACH THE FIGHT. `RetreatCasualtyThreshold` belongs in this list too (see **C-1**); I wrongly cleared it in D7-5 by counting references to a same-named CONSTANT. FOUR OF THE FIVE ARE THE 2026-07-24 BEHAVIOUR RULINGS.** **`TargetPriority` (ruling #18):** the only non-test code is `CombatDoctrine.ParseTargetPriority` — a string parser. **No selector function exists anywhere**, and nothing calls the parser outside tests. Both resolvers still spread fire by current health, which is precisely what the enum's own doc-comment says it was created to end: *"damage preferentially lands on the HEALTHIEST target and a cripple is never finished — the opposite of how anyone actually fights."* **`BreakAwaySeconds` (ruling #15):** only `CombatDoctrine.EffectiveBreakAwaySeconds` — an accessor with zero callers. Retreat is still instant. **`Pursues` (ruling #15):** only `CombatDoctrine.Pursues(bp)` — an accessor with zero callers. Disengage is still free. **So doctrine today changes firepower, toughness, whether you shoot first, and when you break off. It does NOT change who you shoot, whether you chase, or how long breaking away takes.** Against the canon — *"the ONLY thing that changes how those forces act"* — **doctrine is still mostly a stat multiplier with two behaviours bolted on, not the behaviour container the ruling asked for.** |
| **D7-2** | 🔴 | **A GREEN CI TEST IS ASSERTING THESE EXACT FEATURES WORK.** `UnifiedDoctrineTests.cs:226` is described as *"The catalog actually delivers the behaviours the rulings asked for: something pursues, …"* — and its assertions are `all.Any(CombatDoctrine.Pursues)` (`:233`), `…ParseTargetPriority(b.TargetPriority) == FinishWounded` (`:237`), `…EffectiveBreakAwaySeconds(b) > 0` (`:239`), and `:280` *"Break and Roll chases the rout"*. **Every one of those asserts that a JSON file contains a value.** The word *"delivers"* is doing work the test does not do — nothing chases anything. **This is the Visibility Gate's failure mode INVERTED: a gauge reading normal while the system is inert.** It is worse than having no test, because it converts *"not built"* into *"verified built"* on the CI dashboard — and CI is the only correctness gauge this project has. **Any fix here must include re-pointing these four assertions at behaviour, or they will keep certifying the gap closed.** |
| **D7-3** | 🟠 | **"ENGAGEMENT" MEANS TWO DIFFERENT THINGS, ONE PER DOMAIN.** Space: `EngagementPosture` — *WeaponsFree / WeaponsHold / ReturnFire* — **will you shoot first.** Ground: `GroundEngagementStance` (`GroundForcesDB.cs:275-285`) — *HoldGround / CloseToEngage / StandOff* — **will you advance or kite.** Different enums, different questions, and **ground never reads the doctrine's `EngagementPosture` at all** (grep across `GroundCombat/`: zero hits). So the "one unified doctrine catalog" carries a posture field that **half the game ignores**, while that half runs its own parallel mechanism. **This is One Verb, Both Seats violated inside the doctrine layer itself** — and it is the **fourth** instance of the same space/ground split, after the battlefield container (root cause B), armour hardening (**D5-2**) and the shield (**D6-3**). |
| **D7-4** | 🟡 | **`SpeedMult` is INERT AND THE UI STATES IT AS FACT.** It is copied blueprint → `FleetDoctrineDB` (`FleetDoctrine.cs:60`) and printed twice in the client — the active-doctrine line (`FleetWindow.cs:724`: *"Firepower x…, Toughness x…, **Speed x…**"*) and the selection preview (`:767`). **No movement code reads it** — every other `SpeedMult*` hit in the engine is `GroundMobility.SpeedMultFor`, an unrelated locomotion multiplier. Separated from D7-1 because it is a worse class of fault: an inert dial the player cannot see is a wasted opportunity; **an inert dial the UI reports as an active effect is misinformation.** |
| **D7-5** | ✅ | **⚠ CORRECTED to NINE of fourteen (was ten — `RetreatCasualtyThreshold` does NOT land; see C-1).** **The catalog is still real work and the gap is specifically the behaviour half.** Wired and verified: `FirepowerMult`/`ToughnessMult` on **both** domains (`FleetDoctrine` for space; `GroundFormationDoctrine.AttackMult` at `GroundForcesProcessor.cs:490, 523` for ground), `DamageTakenMult` on ground (`:541`), `IsRetreat` (the space retreat — the *threshold* it fires at is a const, not the doctrine's), `EngagementPosture` (space weapons-release — ruling #19 genuinely built), `CooldownSeconds` enforced at **all three** switch sites (`FleetDoctrine.cs:65`, `GroundFormationDoctrine.cs:52`, `GroundTacticalBrain.cs:239`), and `Domain`/`Family`/`DisplayName` for filtering and UI. **Recorded precisely so nobody rebuilds the ten that work while chasing the four that don't.** |

### What Pass 7 changes about the plan

**Three of the four missing dials are cheap; the fourth is the one that matters.**

- `Pursues` and `BreakAwaySeconds` are small: both hang off the retreat path that **already** reads
  `RetreatCasualtyThreshold`, so the trigger exists and only the consequence is missing.
- `SpeedMult` is either wired to closing speed or **removed from the two client lines** — but it cannot stay as a
  number the UI asserts and the engine ignores.
- **`TargetPriority` is the real work and the biggest single behaviour win available.** There is no selector function
  to call — it has to be written, and it has to be written **once, in the shared kernel**, or it will land twice and
  diverge exactly the way armour did (**D5-1**). It is also the dial that makes **D4-3's penetration** worth having:
  `Heaviest` targeting is meaningless until penetration works, and penetration is dull until something chooses to aim
  at armour.
- **Fix D7-2 in the same slice as any of the above.** Four assertions currently certify these features as delivered.
  Leaving them pointed at the JSON while wiring the behaviour means the tests stay green either way — which is the
  same as having no gauge at all.

---
---

# CONSOLIDATION — 40 findings, 7 root causes, 3 decisions that block the rest

**Why stop at seven passes.** The developer's standing rule for these audits is *"keep making passes until the amount
of issues you find drops off to negligible levels."* The **count** has not dropped — Pass 7 found five. What has
dropped to zero is **novelty of kind**. Every finding in Passes 5, 6 and 7 turned out to be another expression of a
root cause already on the board; the space/ground split alone surfaced **four times from four independent directions**
(the battlefield container, armour hardening, the shield, the word "engagement"). That is the real stopping signal:
more passes would keep producing findings, and they would keep landing in the same seven buckets. **The work now is
deciding and fixing, not looking.**

**Tally:** 40 findings — **9 🔴 BLOCKER · 15 🟠 REAL · 8 🟡 DEBT · 5 🔵 NOTE · 3 ✅ verified-good.**
Companion to `docs/combat/RESOLVER-AUDIT-2026-07-28.md` (49 findings, 5 root causes); the two overlap deliberately and
the cross-references are noted below.

---

## The seven root causes

### RC-1 · The base mod is a large body of untyped text that nothing checks
*(D1-1, D1-2, D2-1, D2-3, D2-4, D2-5, D2-6)*

The compiler cannot see inside a JSON string and the test suite does not walk the templates. So a namespace, an
argument count, a material id and a mount flag are all just text that happens to be right — until it isn't.
**Six `AttributeType` strings name a namespace that does not exist (`Pulsar4X.Atb`), taking down four live designer
doors including the entire missile-warhead designer.** Around it: one arity mismatch, three undefined materials
silently dropped from build costs, one mount flag written as a magic number, and five duplicate template ids whose
winner is decided by load order — including three ground stances defined in *two* files at once.

> **This is the cheapest root cause to close and the one that pays first.** One ~20-line test that constructs a
> `ComponentDesigner` for all 96 templates catches every binding failure here permanently.

### RC-2 · One unstated decision: SPACE AGGREGATES, GROUND INDIVIDUATES
*(D5-2, D6-3, D7-3, + resolver root cause B)*

Four systems split the same way and nobody chose it out loud. In space the **fleet** is the unit of account: one
shield pool for everyone, armour hardening averaged across the fleet, the whole star system as one battlefield. On the
ground everything is per-unit. It even reaches the vocabulary — *"engagement"* means *will you shoot first* in space
and *will you advance or kite* on the ground, and ground never reads the space field.

> **This is a developer decision, not a defect to fix.** M19 says the two should not differ. Four systems already
> encode a difference; a fifth will follow whichever way it is left.

### RC-3 · The resolver merge reached the ARITHMETIC and stopped before the STRUCTURE
*(D6-1, D5-1, D4-3, D6-2)*

`CombatKernel`'s shared formulas are real (`ArmourSoak` 6 production callers, `ArmourSoakBurst` 4, `BurstShotCount` 3,
`HitFraction` 2). But its **neutral `Combatant` view has zero production consumers** and there is **no shared salvo
loop** — each domain still runs its own. That is exactly how two incompatible armour models coexist while both sides
honestly call the same function: **on the ground armour stops a fixed amount of every hit; in space armour is extra
hit points.** Which is *why* `Penetration` and `PerShotEnergy` are hardcoded 0 on ships — not un-wired, **undefined**.

> **Prerequisite, not follow-up.** Several Pass 4/5 fixes assume a shared structure that does not exist yet.

### RC-4 · Dials that do not arrive — or do not exist to be turned
*(D4-1, D4-2, D4-4, D3-2, D3-3, D2-7, D7-1, D7-4, D4-6)*

The audit's founding question. Three flavours, worth keeping apart:
- **Not designable at all.** Weapon range on five of six ship classes (engine constants; **no template even offers the
  dial**). Velocity/tracking/saturation on every ground weapon (three constants picked by a dropdown).
- **Designed but discarded.** A missile carries **0 of `WeaponProfile`'s 10 fields** from its design — every missile in
  the game fights identically. Four of fourteen doctrine dials never reach the fight, three of them the 2026-07-24
  behaviour rulings.
- **Built, costed, and inert.** `Amphibious` **doubles the part's mass** and is read by zero lines of code.
  `SpeedMult` is **printed in the client** as an active effect and read by nothing.

> **The sharpest single instance: `Range`.** The X9 ruling routes *every battle's opening range* through the group's
> longest-ranged weapon — which today is a fixed ladder (missile > railgun > disruptor > flak) no design can reorder.

### RC-5 · The combat value is computed once and never again
*(D5-3, D5-4, D5-5, + resolver X14 / P11-1)*

`ShipCombatValueDB.Calculate` has **exactly one production call site** — `ShipFactory.cs:144`, at construction — and
nothing ever invalidates the blob. So every `comp.HealthPercent` multiply inside it is frozen at build-time health.
Reach: it silently falsifies the cradle-to-grave **"grave rung" written into six attribute doc-comments**; any future
**refit** is stale; and `FactionRollup.MilitaryStrength` feeds that frozen number to the **AI's objective selection,
its threat assessment, and the decision log its behaviour is audited from.**

> **One fix, three consumers.** Not a decision — but do it once, deliberately, with a gauge, rather than three times
> from three directions.

### RC-6 · Gauges pointed at the wrong thing — the most dangerous cause here
*(D7-2, D2-6, and the pattern behind both)*

CI is the only correctness gauge this project has, and in two places it is measuring the wrong thing.
**`UnifiedDoctrineTests` is described as proving *"the catalog actually delivers the behaviours the rulings asked
for"* and instead asserts that a JSON file contains the values** — it is green while nothing pursues, nothing targets
the wounded, and nothing takes time to break away. And **no test constructs a designer for all 96 templates**, which is
why RC-1 went unseen. **A gauge reading normal while the system is inert is worse than no gauge: it converts "not
built" into "verified built."**

> Every fix below must land with its gauge re-pointed at **behaviour**, or the same blindness returns.

### RC-7 · Ground combat has no research tree — the missing cradle-to-grave rung
*(D3-1, D3-6)*

Only **18 of 96** templates carry any tech gate; **42 are both free to research and completely ungated** — and *every
ground part is among them*. A turn-one ground rifle can be dialled to 5000 attack and 100 km range. Contrast
`laser-weapon`, whose Range ceiling **is** `TechData('tech-beam-range')`: research literally widens what you may
design. **This is not a wiring fault — it is an absent system**, and it is the honest answer to *"does planetary
combat have the depth space combat has?"* Not yet, and this is why.

---

## The three decisions that block everything else

Ordinary work can proceed on RC-1, RC-5, RC-6 and RC-7 today. These three cannot, and they are the developer's calls:

| # | Decision | Why it can't be inferred |
|---|---|---|
| **Q-A** | **Does the fight aggregate or individuate?** (RC-2) | Four systems already differ. Making them agree means changing one side or the other — a gameplay decision (does one shielded battleship cover the fleet?), not a refactor. |
| **Q-B** | **Which armour model wins?** (RC-3 / D5-1) | Flat per-source soak (ground, and what the shared kernel implements) or armour-as-hit-points (space). Penetration and alpha-vs-chip only mean anything under the first. There is no third option that keeps both. |
| **Q-C** | **Damage bucketing vs real targeting** — the July health-weighted/bucketed ruling against the 2026-07-28 no-roll-over/real-targeting ruling | *(Carried from the resolver audit as **P1-4** — still unanswered, and `TargetPriority` cannot be built until it is.)* |

*Also still open and unchanged: **#21**, what a planet capture transfers — deliberately left for the developer.*

---

## Ranked action list — what to do, in this order

1. **Build the designer gauge** (D2-6). ~20 lines: loop all 96 templates, construct a `ComponentDesigner`, call
   `SetAttributes()`, collect failures. Catches RC-1 permanently. **Do this first — it proves every fix below.**
2. **Fix the six namespace strings** (D2-1) — restores four dead designer doors, including the missile warhead.
3. **Re-point the four doctrine assertions at behaviour** (D7-2). Cheap, and until it happens CI actively lies.
4. **Recompute the combat value** (RC-5) — one change, three consumers, one gauge.
5. **Small data fixes**: the arity gap (D2-3), three undefined materials (D2-4), five duplicate ids (D1-1/D1-2),
   `solarArray`'s magic mount flag (D2-5). Decide `Amphibious` and `Size` — wire or remove, but stop charging for
   `Amphibious` (D3-2, D3-3).
6. **Give the five ship weapon classes a real `Range` dial** (D4-1) — one dial per template, one line per class. Author
   the ceiling as `TechData(...)` and it starts closing RC-7 in the same stroke.
7. **Ground research tree** (D3-1) — real `ResearchCost` plus `TechData(...)` ceilings across the ground stack; new
   techs to author.
8. **— gated on Q-A + Q-B —** the structural merge: a real `ResolveSalvo`, both sides presenting `Combatant`, one
   armour model. Carry D6-2 as a hard note so the merge does not delete the working ground shield-regen dial.
9. **— gated on Q-B + Q-C —** `TargetPriority`: write the selector **once, in the shared kernel**. Biggest behaviour
   win available, and it is what finally makes penetration worth having.
10. **Missiles** (D4-2) — only after 2 and 8; opening the warhead designer while the launcher still contributes five
    stubs ships a designer whose dials demonstrably do nothing.

---

## What the audit confirmed is GOOD — hold new work to these

- **D6-4 — the ground shield chain.** Part dial → *shield-weighted* assembly average → design → raised unit → resolver
  regen, **with a test that asserts the decision** (fast small ward vs slow big generator), not the plumbing. The only
  end-to-end example in the codebase of a dial that is designed, assembled, delivered, resolved *and* gauged on the
  choice it creates. **This is what "done" looks like.**
- **D3-4 — the designer → assembler hop.** 41 of 43 ground dials have a reader. The expensive half is already correct.
- **D7-5 — ten of fourteen doctrine dials land**, on both domains, cooldown enforced at all three switch sites.
- **D5-6 — the ship armour NATURE matchup is real and authored**, in Earth's starting items and on a base-mod ship.
- **The formula layer** — 675/675 `PropertyValue` references, 58/58 `TechData` references, 119/120 ctor arities.
- **`laser-weapon`** — the one weapon whose research gate raises its design ceiling. The shape to copy for RC-7.

---
---

# CROSS-AUDIT RECONCILIATION — the resolver audit vs this one

**Why do this at all.** Two audits ran days apart over overlapping ground with **deliberately different lenses**: the
17-pass **resolver audit** (`docs/combat/RESOLVER-AUDIT-2026-07-28.md`, 49 findings) read the **code and the design
docs**; this 7-pass **designer audit** read the **base-mod data and the code that consumes it**. Where two independent
methods cover the same system, the places they **disagree** are worth more than the places they agree — a disagreement
is either a stale finding, a mis-scoped claim, or a defect that neither lens could see alone. All three turned up.

**Result: 1 error in this audit, 2 stale/mis-scoped claims in the resolver audit, 1 genuinely NEW blocker that
required both, and 2 places where a ✅ in one document could suppress a 🟠 in the other.**

| # | Sev | Reconciliation |
|---|---|---|
| **C-1** | 🔴 | **THE RESOLVER AUDIT WAS RIGHT AND THIS ONE WAS WRONG — `RetreatCasualtyThreshold` IS INERT.** Resolver **P2-1** listed it among four doctrine dials with *"nowhere to put them"*, and **P6-2** said the retreat decision is *"a hardcoded constant modulated by personality — not by doctrine."* My **D7-5** cleared it as wired. **P2-1/P6-2 are correct.** `CombatDoctrine.EffectiveRetreatCasualtyThreshold` has **zero callers anywhere — not even a test**, and every one of the ten `RetreatCasualtyThreshold` references I counted in `CombatEngagement.cs` is to the **`public const double = 0.5`** and its personality modulation (`:48, 1657, 1667`), not to the doctrine's authored value. **I counted a name collision as a wire.** ⇒ **The doctrine ledger is 9 of 14 landing and FIVE inert** — `TargetPriority`, `RetreatCasualtyThreshold`, `BreakAwaySeconds`, `Pursues`, `SpeedMult`. Resolver P2-1 named exactly the right four; Pass 7 swapped one out instead of adding `SpeedMult` to it. **D7-1 and D7-5 corrected in place above.** |
| **C-2** | 🔴 | **⭐ NEW — NEITHER AUDIT COULD FIND THIS ALONE. THE PARTS-BASED UNIT DESIGNER CANNOT EXPRESS PENETRATION OR ALPHA AT ALL — AND THE PLAN IS TO DELETE THE ONLY PATH THAT CAN.** The two audits appeared to contradict: **X5** says *"penetration + per-shot energy are never written by the assembler"*; **D3-4/D4-5** say both arrive at the `WeaponProfile` from the design. **Both are true, of DIFFERENT PATHS**, and the collision is the finding. **Parts path:** `GroundWeaponAtb` has exactly five fields — Mass, Attack, Range, Range_m, Mode — **none of them penetration or per-shot energy**; `GroundUnitAssembly.ToGroundUnitDesign` never sets either, so a designed unit keeps the 0 default. **Prebuilt path:** `GroundUnitAtb` carries both as trailing ctor args (`:38, 50-60`), and **only three templates use it — `infantry-unit`, `armor-unit`, `artillery-unit`** — which all supply the full 7 values. ⇒ **A unit you build in the designer is strictly weaker IN KIND than a prebuilt one: it can never be armour-piercing and never lands an alpha strike.** ⚠ **And `docs/economy/COMPONENT-DESIGNER-CATEGORIES.md` files those three prebuilts under *"Chassis ▸ Prebuilt Units — the whole-unit shortcuts the design doc marks for eventual removal."* Removing them without first adding the two dials to `GroundWeaponAtb` deletes armour penetration from the ground game entirely.** |
| **C-3** | 🟠 | **THE TWO AUDITS' HEADLINE FEED NUMBERS DISAGREE — AND BOTH ARE WRONG.** Resolver **X5/P7-1** rank the feed as *ground weapon **2 of 10** · railgun **"all designed"** · missile **0 of 5***. This audit's per-field ledger (Pass 4) gives: **ground 6 of 10** *(prebuilt)* or **4 of 10** *(parts-built — C-2)* · **railgun 4 of 10** · **missile 0 of 10**. **A railgun is not "all designed":** its range is an engine constant, and penetration, per-shot energy and heat are all hardcoded 0. **This mis-ranking is load-bearing** — it is what put *"the ground feed is the lossy one"* into the plan, when in fact **a prebuilt ground unit carries more designed fidelity into the fight than any ship weapon except the beam** (D4-5). |
| **C-4** | 🟡 | **RESOLVER P5-3 IS REAL IN CODE BUT UNREACHABLE WITH BASE-MOD DATA.** P5-3 — *"an all-beam fleet closes to point-blank"* — needs a beam whose `Range_m` is 0 (the unbounded sentinel), because `FleetDesiredRange` lets unbounded never raise the preferred range. **Neither base-mod beam can be authored at 0:** `laser-weapon` and `pulse-laser` both carry `"MinFormula": "1000"` on their Range dial. **The code path and the X13 convention problem are both genuine and still worth fixing** — but the *scenario* requires a mod or a hand-built design, so this is a latent trap, not a live misbehaviour. Data knowledge the code-only lens could not supply. |
| **C-5** | 🔵 | **A ✅ IN ONE AUDIT CAN SUPPRESS A 🟠 IN THE OTHER — and this pair nearly does.** Resolver **P17-1** records *"a built ship keeping its build-time numbers is **correct behaviour** (researching a better gun does not retrofit ships already flying)"* — **✅ verified-good, "recorded so a later pass does not mistake this for a gap."** This audit's **D5-3/D5-4/D5-5** records the *same frozen value* as a defect that falsifies six documented grave rungs, staleness-locks any refit, and feeds the AI a wrong strength. **Both are right, about different inputs.** The distinction has to be written down or the ✅ will win a skim: **freeze on RESEARCH · refresh on DAMAGE and REFIT.** |
| **C-6** | 🔵 | **"PROPERLY FLAGGED" IS NOT "IN THE RIGHT PLACE."** Resolver **P16-1** counts 49 `FLAGGED` balance constants and calls it *"the flagging discipline working exactly as intended"* — correct. This audit's **D4-1** finds the weapon-range constants a blocker — also correct, and `RailgunRange_m`/`DisruptorRange_m` are literally among those 49 (`ShipCombatValueDB.cs:72` carries the `FLAGGED` marker). **The convention has a blind spot: `FLAGGED` says *this number is provisional*; it does not say *this should not be a constant at all*.** A number can be impeccably flagged and still be in the wrong layer. Worth a second marker, or at least the awareness. |
| **C-7** | 🔵 | **WHAT EACH LENS WAS BLIND TO — read together, this is the coverage map.** The **resolver audit never systematically read the base-mod JSON**, so it could not have found RC-1: six `AttributeType` strings naming a namespace that does not exist, four dead designer doors, the missile-warhead designer that does not open. The **designer audit never ran a battle forward**, so it would never have found **P6-5** (disengage refills ammo/shields/heat/manoeuvre and deletes partial damage), the dictionary-iteration determinism risk (**P4-3**), or **P15-1** (the battle-report fog leak). **Neither swept the engine↔client seam** — the resolver audit said so explicitly and got a 🔴 the moment it crossed. **That seam remains the least-swept surface in the project and is where a Pass 8 should point.** |
| **C-8** | ✅ | **THE STRONGEST EVIDENCE IN EITHER DOCUMENT: two independent methods converged on one sentence.** The resolver audit, reading code, concluded: *"**The designer models COMPONENTS; the resolver models TOTALS** — every defect is a designed detail flattened into a number before the fight starts, and can never influence it again."* This audit, reading data, produced **RC-4** — the same sentence with the data half filled in, plus **RC-3** naming the structural reason it is *possible* (the merge shares arithmetic, not structure). **Neither borrowed the other's framing.** When two differently-pointed methods land on the same sentence, that sentence is the real defect — not an artefact of how either looked. |

## What the comparison changes

1. **The doctrine fix list grows by one and matches P2-1 exactly** — five inert dials, and the fix for four of them is a
   **save-schema change** (`FleetDoctrineDB` needs the fields), which P2-1 identified and Pass 7 did not. That is the
   correct characterisation: not a copy bug, **nowhere to copy to**.
2. **C-2 is a new blocker and it changes a planned deletion.** Retiring the three prebuilt whole-unit templates is
   already on the roadmap; doing it before `GroundWeaponAtb` gains penetration and per-shot-energy dials removes
   armour-piercing from the ground game. **Sequence it: add the dials, then retire the prebuilts.**
3. **Re-point the plan's "the ground feed is lossy" framing** (C-3). The prebuilt ground path is the *second-best* feed
   in the game. The worst are the missile (0 of 10) and the parts-built ground unit (4 of 10) — and the latter is a
   *designer* gap, not a resolver one.
4. **Write down "freeze on research, refresh on damage and refit"** (C-5) wherever the recompute slice is specified, or
   P17-1's ✅ will be read as blessing the frozen value.
5. **Point the next pass at the engine↔client seam** (C-7) — the one surface both audits left, and the only one that
   yielded a 🔴 on first contact.
