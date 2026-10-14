<!-- FIELDMANUAL_MANAGED_HEADER_START -->
# FieldManual Project Instructions

This project uses FieldManual for shared development, testing, documentation,
review, and project-management practices.

Before substantial work:

1. Read `FieldManual-config.toml` and, when it exists,
   `.local/FieldManual-localconfig.toml`.
2. Confirm the configured `FieldManual_ProjectRoot` and
   `FieldManual_FrameworkRoot` identify the project being changed and the
   FieldManual checkout being used.
3. Read `FieldManual/README.md` and
   `FieldManual/standards-and-practices/core/README.md`.
4. Load only the language, technology, technique, and knack documents that
   apply to the work.
5. Read the project-specific instructions between this header and the managed
   footer.

---

Put project-specific `AGENTS.md` content below this line and above the managed
FieldManual footer.
<!-- FIELDMANUAL_MANAGED_HEADER_END -->

# Repository Guidance for R. Talon

## Scope
These instructions apply to the entire `RimWorld-BatshitTechnology`
repository unless superseded by a more specific `AGENTS.md` file in a
subdirectory.

## Engineering Expectations
- Follow Admiral Grace Hopper's Standing Orders: take initiative, document
  decisions, and keep stakeholders informed. Capture noteworthy choices in
  `docs/AI-DECISIONS.txt` when appropriate.
- Align testing and project management habits with Joel Spolsky's widely
  accepted advice. Prefer automated checks, maintain TODO hygiene, and keep
  builds green.
- Write code that is production-ready: idiomatic C#, defensive, and
  pessimistic about failure states. Document public members with XML comments
  and inline remarks when behaviour is surprising.
- Ensure every function, including private helpers and unit tests, is preceded
  by an XML documentation block that explains purpose, parameters, and return
  values.
- Every code change must be paired with unit tests or, where not feasible, a
  written justification in the PR description and `docs/AI-TODO.txt`.
- When editing documentation, favor the Chicago Manual of Style, apply
  consistent headings, and keep language precise.

## Formatting Standards
- Wrap plain-text documentation, including markdown, at column 78.
- Keep source code and structured data at or below 78 columns when practical.
  Lines up to 118 columns are tolerated, but exceeding 118 requires a strong
  justification. Document any such necessity in code comments or commit notes.

## Mod Structure
- Keep RimWorld metadata (`About`, `Defs`, etc.) synchronised with code. When
  you add assets or XML definitions, link them into the project so Visual
  Studio users can browse the full mod tree.
- Never hard-code absolute paths to the RimWorld installation; rely on the
  `RIMWORLD_DIR` environment variable already referenced in the project file.

## Art and Written Content
- Follow `docs/ARTISTIC-MOTIF.md` for everything players see or read:
  textures, icons, effects, labels, descriptions and flavour text. Read it
  before generating any new material.
- Generate textures through `Tools/make_textures.py`, reusing its palette
  constants and helpers; name them `BT_<Thing>`. Check each new sprite at
  full size and downsampled to 64 px against its neighbours.
- When new material needs a colour, part or rule the motif does not cover,
  extend `docs/ARTISTIC-MOTIF.md` in the same change.

## Repository Hygiene
- Do not commit build artifacts (`bin`, `obj`, `Assemblies`, `.vs`, etc.).
  Update `.gitignore` as needed.
- Prefer descriptive commit messages summarising intent and key changes.
- Build each feature on its own branch, `feature/<name>`, cut from
  `trunk`, and merge it back into `trunk` when it is complete (all tests
  and the smoke test passing, docs updated). `ROADMAP.md` lists the
  features in order with their branches.

## Communication
- Address the operator as Matt “Breakpoint” Heck whenever notes require human
  attention.
- When raising questions for humans, log them in
  `docs/AI-requests-for-humans.txt`.

## FieldManual and Ubersight
- This project adopted FieldManual (the `FieldManual/` submodule) on
  2026-10-13. **Transitional exception:** until proposal BT-PROP-2026-001
  (`project-management/proposals/under-review/`) is decided, the existing
  records stay authoritative: `ROADMAP.md`, `docs/AI-*.txt` and the
  feature specifications in `docs/`. Do not start using the new
  `project-management/` skeleton files for live records before then.
- Publish status for Ubersight at meaningful transitions with
  `python Tools/ubersight_status.py` (phases are roadmap features, slices are
  the current feature's steps). It writes `.local/ubersight/status.json` in
  Ubersight's `ubersight.status.v1` format, because Ubersight itself does
  not yet run on Windows. Status text is a glance: no prompts, transcripts,
  credentials, hostnames, or network or location details. See
  `FieldManual/knacks/software-engineering/development-observability/`.

Party on, but always double-check the references.


<!-- FIELDMANUAL_MANAGED_FOOTER_START -->
---

## Field Manual Managed Guidance

- Treat the configured project root as the boundary for project-owned files,
  local state, and project-management records.
- Treat the configured framework root as read-only guidance when it is a
  submodule. Do not write project state into the FieldManual subtree.
- Use `project-management/` for the backlog, active and completed work, bugs,
  proposals, reviews, decisions, open questions, and bounded human requests.
- Keep durable behavior and interface contracts in `docs/specifications/`.
- Keep checkout-local state and policy inputs under `.local/`, and keep the
  entire `.local/` tree ignored by version control.
- Put disposable test workspaces and temporary project artifacts in uniquely
  named children of the project root's `.local/tmp/` by default. Clean up
  only paths created or explicitly acquired by the current operation.
- Put cross-project change requests under `ECRs/<target-project>/`. Use the
  ready `ECRs/FieldManual/` tree for FieldManual requests and copy
  `ECRs/_target-template/` for another target.
- FieldManual supplies prose standards, not language-specific verification
  programs. Use the consuming project's declared build, test, lint, security,
  and release commands.
- Project-specific instructions may specialize FieldManual defaults. Record
  intentional exceptions explicitly instead of allowing silent drift.
<!-- FIELDMANUAL_MANAGED_FOOTER_END -->
