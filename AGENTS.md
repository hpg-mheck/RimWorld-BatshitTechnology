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

Party on, but always double-check the references.
