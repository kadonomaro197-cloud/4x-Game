# Designer-Docs Cleanup — Manifest (2026-08-03)

**What this is, in one breath.** The component-designer documentation had grown several *competing* descriptions of
the same thing — an older 37-door "dial spec," an even older phase-by-phase audit, dated dial-audits — written before
the current designer was locked. This cleanup moves those superseded docs into `docs/archive/` (a **move, not a
delete** — nothing is lost, and any move is one `git mv` to undo) so that future development has **one source of
truth**. This file is the record of exactly what moved, why, and what got repointed, so the developer can audit the
whole sweep after the fact.

**The ONE source of truth going forward (the canonical set — untouched except path/status repoints):**
- The **12 door HTMLs** in `docs/Actual HTMLs Of designers/` — the derived, player-facing component designers.
- **`docs/economy/DESIGNER-NORTH-STAR.md`** — the LOCKED method (2026-07-29) for *deriving* a dial from the sim
  variables it actually writes; it explicitly collapses the old "37 doors / 41 dial-groups" model down to a few
  choices + sliders per category.
- The **assembler suite** in `docs/assembler/` (00-STATE, the 01-IO census, 02-IO-MATRIX, 03–06 ledgers,
  `entityassembler.html`, `DESIGNER-DRIVER-PLAYBOOK.md`, the `*-BUILD.md` docs).
- **`docs/COMBAT-DESIGNER-GROUND-TRUTH-2026-07-28.md`** — the keystone verification/decision record (holds LOCKED
  developer decisions).

---

## ARCHIVED (moved to `docs/archive/`, banner added naming the replacement) — 5 items, 17 files

| From | To | Why | Replaced by |
|---|---|---|---|
| `docs/designers-audit/` (8 files + `04-MISSING-DESIGNS/` = 13 .md) | `docs/archive/designers-audit/` | The EARLIER "Designer Interconnection Audit" (2026-08-01), a completed phase-by-phase (00–06) audit. Point-in-time; predates the canonical door HTMLs. | door HTMLs + `DESIGNER-NORTH-STAR.md` + assembler suite. **STILL LIVE:** its six open developer rulings **R1–R6** (in `06-FINAL-REPORT.md`) — the cleanup did NOT resolve them. |
| `docs/economy/COMPONENT-DESIGNER-CATEGORIES.md` | `docs/archive/economy/` | The old "11 categories / 37 doors" taxonomy — the pre-collapse door model. | `DESIGNER-NORTH-STAR.md` + door HTMLs |
| `docs/economy/COMPONENT-DESIGNER-DIALS.md` | `docs/archive/economy/` | The old 37-door dial SPEC (581 KB, every door 🔒). The **design model** is superseded. Its `⚙ 1`–`⚙ 11` WIRING DOSSIERS + `§0d`/`§0d′` cost audits are **retained as a historical engine-wire reference** (the engine `CLAUDE.md` pointers resolve to the archived file — banner says so). | `DESIGNER-NORTH-STAR.md` + door HTMLs (design); `COMBAT-DESIGNER-GROUND-TRUTH` (decisions) |
| `docs/economy/COMPONENT-DESIGNER-DIAL-LEDGER.md` | `docs/archive/economy/` | The "what's actually WIRED" companion to the DIALS spec — a build-state tracker for the old 37-door model. | `DESIGNER-NORTH-STAR.md` + door HTMLs |
| `docs/economy/COMPONENT-DESIGNER-DIAL-AUDIT-2026-07-23.md` | `docs/archive/economy/` | A dated (2026-07-23) per-dial audit sweep of the old model. | `DESIGNER-NORTH-STAR.md` + door HTMLs |

## KEPT (verified NOT a superseded designer description — left in place)

| Path | Why kept |
|---|---|
| `docs/economy/UNIVERSAL-ASSEMBLY-DESIGN.md` | A governing **PRINCIPLE** ("one designer; everything buildable is a component"), with LOCKED developer decisions, cited by 4 engine `CLAUDE.md` files as the law. Not a competing dial spec — it is the principle the door HTMLs *realize*. |
| `docs/economy/CAPABILITY-BUILD-PLAN.md` | An **implementation build plan** (tracks 0–5) that spans the AI-capability brain, not a designer dial/door description. (Its one reference to the archived DIAL-LEDGER was repointed.) |
| `docs/showcase/` (8 files) + `docs/LITMUS-BLOOD-ANGELS-SQUAD.md` | Franchise **stress-tests** ("can I build a Stormtrooper out of the designer?") — a distinct deliverable category, not a designer spec. `showcase/LITMUS-VENATOR-BUILD.md` ≠ `assembler/VENATOR-BUILD.md` (different docs, both unique). |

## DELETED

None. No pure zero-unique-content duplicate was found; every candidate had unique content, so all were archived (reversible), not deleted.

---

## REFERENCES REPOINTED (same commit) — every `.md` pointing at a moved file

Path transforms applied: `docs/economy/COMPONENT-DESIGNER-*` → `docs/archive/economy/COMPONENT-DESIGNER-*`; `docs/designers-audit/` → `docs/archive/designers-audit/`.

- **`docs/DOCS-INDEX.md`** — the 4 economy rows + the whole §4a "Designer Interconnection Audit" section: paths repointed **and** each row's status flipped to 🗄 ARCHIVED with a replacement pointer.
- **Canonical (path-only, content untouched):** `docs/assembler/00-STATE.md`, `01-IO-industrial.md`, `02-IO-MATRIX.md` (→ designers-audit); `docs/economy/DESIGNER-NORTH-STAR.md` (→ CATEGORIES, DIALS); `docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md` (→ DIALS).
- **Engine `CLAUDE.md` (path-only — these are docs, not C#):** `Pulsar4X/GameEngine/Combat/CLAUDE.md`, `Pulsar4X/GameEngine/Weapons/CLAUDE.md`, `Pulsar4X/Pulsar4X.Tests/CLAUDE.md` (→ DIALS's `⚙1`/`§0d′` pointers); `Pulsar4X/Pulsar4X.Client/CLAUDE.md` (→ CATEGORIES).
- **Kept docs:** `docs/economy/CAPABILITY-BUILD-PLAN.md` (→ DIAL-LEDGER); `docs/showcase/LITMUS-TO-100-ROADMAP.md` (→ designers-audit); `docs/showcase/FRANCHISE-LITMUS-TEST.md` (→ CATEGORIES).
- **Historical audit records (path-only):** `docs/DOCS-AUDIT-2026-07-13.md`, `docs/DOCS-AUDIT-2026-07-27.md`.
- **Already-archived (also repointed for completeness):** `docs/archive/SYSTEMS-STATUS-AND-TEST-PLAN.md`.
- **Internal cross-links** among the moved files were repointed too, so links inside the archive still resolve.

**Verification after the sweep:** `grep` across all `.md` for the two old-path patterns returns **zero** hits outside this manifest — no dangling references remain.

## What was NOT touched
No `GameData/`, no engine C# code, no non-doc files. This was a docs-and-references-only move.

## Method note
Dispositions were decided by reading each candidate directly against the canonical set (structure, unique content,
locked decisions, inbound references). A parallel classification pass was run as an independent cross-check; the
executed dispositions rest on the direct reads.
