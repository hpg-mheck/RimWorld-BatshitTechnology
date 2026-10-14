# Engineering Change Request: Document Phase and Slice Numbering and Centre the Phase Window

- ECR ID: `BT-ECR-2026-008`
- Request revision: 1
- Source project or origin namespace: Batshit Technology (`BT`)
- Target project: Ubersight
- Target canonical locator:
  https://github.com/Hard-Problems-Group-LLC/Ubersight
- Target component, version, or revision: `docs/scripts/ubersight.py.md`
  ("Important Options", "Status Format"), `AGENTS.md` guidance for status
  writers, and `_visible_phase_stack` in `src/ubersight/dashboard.py`; as
  of `40fb384`
- Request owner: Matt "Breakpoint" Heck
- Drafted: 2026-10-14
- Submitted: Not yet submitted
- Submitted-content digest: Not yet recorded
- Source lifecycle: Open
- Target handling status as reported: Not submitted
- Target intake ID or authoritative reference: None
- Sensitivity and reuse classification: Public; no project-specific
  content beyond the example
- Related, duplicate, or superseded ECR IDs: None
- Origin and provenance: the operator found an agent's Ubersight status
  numbered wrongly and identified the cause as a documentation gap
  (2026-10-14); drafted by Claude Code for the operator.

## Submitted Request

Freeze this section after first transport. Preserve a prior revision when a
material change must be resubmitted.

### Problem And Source Impact

Ubersight's documentation tells status writers that a phase `ID` "should
normally be a compact phase number" and a slice `ID` "a compact slice number
within the active phase". It does not give the numbering convention the
operator uses, and it prescribes a phase window that differs from the one
the operator wants:

- **Numbering is undocumented.** The operator's convention is that phases
  are numbered in thousands (1000, 2000, ...), so an unexpected phase can be
  wedged in afterwards (for example 2500) without renumbering, and that
  slices are also numbered in thousands, starting again at 1000 in each
  phase. Without it, an agent in a consuming project used short words
  (`tractor`, `showcase-api`, `plan`, `core`) as IDs, which render as
  "Phase tractor:" and "Slice plan:", and another agent would likely have
  used 1, 2, 3, leaving no room for insertions.
- **The phase window is fixed and off-centre.** The documentation says to
  supply "the last two completed phases, the current phase, and the next
  five known phases", and `_visible_phase_stack` shows at most two rows
  before the active phase and five after it. The operator wants about seven
  phases with the current one in the middle (three before, three after,
  shifting toward one side near either end of the roadmap).
- **Use of space is not described.** The slice list already adapts to the
  pane's height and is windowed around the active slice, but the
  documentation does not say so, nor that writers should therefore supply
  every slice of the current phase. Nothing says how long titles may be in
  the 40 to 60 column pane the documentation targets.

Source impact: the consuming project had to record these conventions in its
own `AGENTS.md` and correct its status by hand, and its centred phase
window is cut to two earlier phases by the renderer.

### Requested Outcome

1. Document the numbering convention for phase and slice IDs in
   `docs/scripts/ubersight.py.md` and in the `AGENTS.md` guidance Ubersight
   gives to status writers: phases in thousands; slices in thousands,
   restarting at 1000 in each phase; IDs as bare numbers; gaps used for
   insertions instead of renumbering.
2. Change the phase window to centre the active phase: by default three
   rows before and three after (seven in all), taking extra rows from the
   other side when the active phase is near either end of the list. Prefer
   making the window grow with the pane's height when there is room, as the
   slice list already does.
3. Update the writer guidance to match: supply about seven phases with the
   current one in the middle (more is harmless), and every slice of the
   current phase, because the renderer windows them around the active
   slice.
4. State the practical title lengths for the target pane sizes, so writers
   keep each row on one line.
5. Optionally, have `--write-status` warn (not fail) when IDs are not
   numeric, or when no rows exist on one side of the active phase although
   the roadmap obviously continues.

### Acceptance Criteria

- The documentation and the status-writer guidance state the numbering
  convention with an example that uses thousands.
- With nine phases supplied and the fifth active, `ubersight --once` at
  40x40 shows phases two to eight (three before, the active one, three
  after); with the second active, it shows phases one to seven.
- With fifteen slices and the eighth active, a 40x40 snapshot shows the
  active slice with earlier and later slices around it, as today.
- Existing status files still load and render.

### Non-Goals

- Changing the `ubersight.status.v1` schema.
- Enforcing numeric IDs; existing files with other IDs keep working.

### Constraints And Risks

- Projects that already supply "two before, five after" would see one more
  earlier phase and one fewer later phase; both are already in their
  files, so nothing is lost.
- Tall panes could show more than seven phases if the window grows with
  height; the guidance should still say "about seven", so writers do not
  need to know the pane size.

### Evidence At Submission

- `docs/scripts/ubersight.py.md` lines 178 to 219 at `40fb384` (row
  formats and the phase stack), and `src/ubersight/dashboard.py`
  `_visible_phase_stack` (`start = max(0, active_index - 2)`,
  `end = min(len(status.phases), active_index + 6)`), and the slice budget
  `(usable_height - 8) // 3`, centred on the active slice.
- The consuming project's corrected guidance: Batshit Technology
  `AGENTS.md`, "FieldManual and Ubersight", and its `ROADMAP.md`, which
  number 13 phases from 1000 to 13000.

## Source Tracking

After first transport, maintain this section as an append-only history.

### Local Mitigation

Batshit Technology's `AGENTS.md` records the numbering and display rules,
its `ROADMAP.md` carries the numbers, and its status writes supply seven
phases centred on the current one. Until Ubersight changes, the renderer
shows two of the three earlier phases.

### Transport And Receipt Log

- 2026-10-14 — Drafted locally; not yet submitted.

### Discussion And Amendments

### Target Disposition

### Source Closure
