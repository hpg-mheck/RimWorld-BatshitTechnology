# Engineering Change Request: Clarify Knack Naming: Short Topics, Context in Directories

- ECR ID: `BT-ECR-2026-009`
- Request revision: 1
- Source project or origin namespace: Batshit Technology (`BT`)
- Target project: FieldManual
- Target canonical locator:
  https://github.com/Hard-Problems-Group-LLC/FieldManual
- Target component, version, or revision: `knacks/authoring-guide.md`,
  "Naming and Layout", drafted on `04bcec5`
- Request owner: Matt "Breakpoint" Heck
- Drafted: 2026-10-14
- Submitted: Not yet submitted
- Submitted-content digest: Not yet recorded
- Source lifecycle: Open
- Target handling status as reported: Not submitted
- Target intake ID or authoritative reference: None
- Sensitivity and reuse classification: Public; guidance only
- Related, duplicate, or superseded ECR IDs: applies to BT-ECR-2026-001 to
  BT-ECR-2026-007, which were renamed to follow it
- Origin and provenance: the operator, reviewing the pull request for
  BT-ECR-2026-001 to 007, found some names too long and asked for a better
  directory structure and clearer guidance (2026-10-14); drafted by Claude
  Code for the operator.

## Submitted Request

Freeze this section after first transport. Preserve a prior revision when a
material change must be resubmitted.

### Problem And Source Impact

The authoring guide's naming rules fix the pattern (`<topic>.overview`,
`<topic>.api[.<subsystem>][.<Language>]`, `<topic>.knack/`) but say nothing
about how long a topic or subsystem token should be, or when to use
subdirectories. Because every file in a fileset repeats its topic, long
topics multiply: the first draft of the knacks in BT-ECR-2026-001 to 007 had
names such as
`integrating-unverified-agent-work.api.instructions.knack.md` (59
characters) and
`closed-assembly-inspection.api.reflection.PowerShell.knack.md` (61), and
topics that repeated their area (`windows/windows-agent-isolation`).
Without guidance, each writer chooses differently, and areas grow flat
lists of long, prefixed names.

### Requested Outcome

Add a "Short Names, Context in Directories" subsection to "Naming and
Layout" in `knacks/authoring-guide.md`, saying:

- choose the shortest topic that is unambiguous in its directory, usually
  one to three words, and do not repeat what the path already says;
- keep subsystem tokens to a word or two;
- aim for file names of about 40 characters or fewer, a language tag
  allowing a little more;
- give topics that share a qualifier (platform, product, tool) a subarea
  directory instead of a shared prefix, and split an area into subareas
  once it holds more topics than a reader can scan, roughly eight;
- file a knack where a reader would look for its subject, not where its
  author found it; and
- give every directory level a `README.md` index.

### Acceptance Criteria

- The guide states the rules above with examples.
- The knacks of BT-ECR-2026-001 to 007 follow them: after renaming on the
  same branch, the longest new file name is 54 characters (a companion with
  a language tag) and most are 25 to 41, against 61 and mostly 40 to 59
  before.

### Non-Goals

- Renaming existing stock knacks outside BT-ECR-2026-001 to 007.
- Changing the file-name pattern itself.

### Constraints And Risks

- Short topics rely on their directories for meaning; a file copied out of
  its tree loses context. The overview's title and "When to Load" section
  carry the full subject, so the file still reads correctly.

### Evidence At Submission

- Commit `9300fb0` adds the subsection; commit `d354450` applies it to the
  new knacks, rewriting and checking every relative link (551, none
  broken). Both are on the branch `knacks/from-batshit-technology`.

## Source Tracking

After first transport, maintain this section as an append-only history.

### Local Mitigation

None needed; the change travels with the knacks' pull request.

### Transport And Receipt Log

- 2026-10-14 — Drafted locally; not yet submitted.

### Discussion And Amendments

### Target Disposition

### Source Closure
