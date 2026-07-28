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
| **`PDC` and `Fighter` are dangling mount flags** — defined, used by zero templates, served by no designer | ✅ **STILL ZERO.** Confirmed by counting every `MountType` in the base mod. |
| **The mount-flag data is authored inconsistently** | ✅ still uneven — ShipComponent 44 · GroundUnit 22 · PlanetInstallation 14 · Cargo 11 · Missile 3 |
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
