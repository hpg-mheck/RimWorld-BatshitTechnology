# Engineering Change Request: Name Knacks Without Repeating Their Path

- ECR ID: `BT-ECR-2026-009`
- Request revision: 2 (revision 1 is preserved in this repository's
  commit `200fa4b`)
- Source project or origin namespace: Batshit Technology (`BT`)
- Target project: FieldManual
- Target canonical locator:
  https://github.com/Hard-Problems-Group-LLC/FieldManual
- Target component, version, or revision: `knacks/authoring-guide.md`,
  "Naming and Layout", drafted on `04bcec5`
- Request owner: Matt "Breakpoint" Heck
- Drafted: 2026-10-14
- Submitted: 2026-10-14, as pull request
  Hard-Problems-Group-LLC/FieldManual#1
- Submitted-content digest: revision 1 at `d354450`; revision 2 at
  `d5af082`, both on branch `knacks/from-batshit-technology`
- Source lifecycle: Open
- Target handling status as reported: Open pull request, awaiting
  review
- Target intake ID or authoritative reference:
  https://github.com/Hard-Problems-Group-LLC/FieldManual/pull/1
- Sensitivity and reuse classification: Public; guidance only
- Related, duplicate, or superseded ECR IDs: applies to BT-ECR-2026-001 to
  BT-ECR-2026-007, which were renamed to follow it
- Origin and provenance: the operator, reviewing the pull request for
  BT-ECR-2026-001 to 007, found some names too long and asked for a better
  directory structure and clearer guidance (2026-10-14); after revision 1,
  returned the pull request because file names still repeated their path.
  Drafted by Claude Code for the operator.

## Submitted Request

Freeze this section after first transport. Preserve a prior revision when a
material change must be resubmitted.

### Problem And Source Impact

The authoring guide named fileset files `<topic>.overview.knack.md` and
`<topic>.api[.<subsystem>][.<Language>].knack.md` inside a
`<topic>.knack/` directory. Every file therefore repeats its directory, and
the topic appears two or three times in one path:
`knacks/windows/shells.knack/shells.api.PowerShell.knack.md`. The first
draft of the knacks in BT-ECR-2026-001 to 007 reached 133-character paths,
such as
`knacks/software-engineering/procedural-generation/procedural-sprite-generation.knack/procedural-sprite-generation.api.Python.knack.md`.
Revision 1 of this request shortened topics but kept the pattern. The
operator's rule is that a knack's full path should make its purpose and
scope clear without repeating anything, because paths otherwise grow very
long.

### Requested Outcome

Revise "Naming and Layout" in `knacks/authoring-guide.md` to say:

- a single-document knack is `<topic>.knack.md`;
- a fileset is a directory named for its topic, without a `.knack`
  suffix, holding `overview.knack.md` and companions named for their part
  only, `<part>[.<Language>].knack.md`; the `api.` token is dropped;
- no path component is repeated in any name beneath it;
- because a topic appears once, it is named for clarity, using no word the
  path already supplies;
- topics sharing a qualifier get a subarea directory; a knack is filed
  where readers look for its subject; every area directory has a
  `README.md`, and a fileset's overview indexes its companions; and
- older filesets in the previous pattern are renamed when next revised.

### Acceptance Criteria

- The guide states the rule with examples.
- The knacks of BT-ECR-2026-001 to 007 follow it: their full paths run 35
  to 78 characters, with no component repeated, and every relative link
  resolves.

### Non-Goals

- Renaming existing filesets outside BT-ECR-2026-001 to 007 in this
  change (`UI/terminal/terminal-ui-design.knack/`,
  `virtualization/virtualbox-control.knack/`,
  `AI/OpenAI/codex-app-server.knack/`); the guide schedules them for their
  next revision.

### Constraints And Risks

- A file read outside its directory (`PowerShell.knack.md`) loses its
  context; its title and "When to Load" section must carry the full
  subject.
- Links into renamed filesets from other repositories break; the pull
  request rewrites every link inside FieldManual and the supplemental
  RimWorld pack.

### Evidence At Submission

- Revision 1: commits `9300fb0` and `d354450`. Revision 2: commit
  `d5af082`, which rewrites the guide's section, renames the new knacks,
  and rewrites and checks every relative link (565, none broken).

## Source Tracking

After first transport, maintain this section as an append-only history.

### Local Mitigation

None needed; the change travels with the knacks' pull request.

### Transport And Receipt Log

- 2026-10-14 — Drafted locally; not yet submitted.
- 2026-10-14 — Submitted, revision 1, as pull request
  Hard-Problems-Group-LLC/FieldManual#1 from branch
  `knacks/from-batshit-technology` at `d354450`.
- 2026-10-14 — Revision 2 sent: pull request FieldManual#1 updated to
  `d5af082`.

### Discussion And Amendments

- 2026-10-14 — Clarification from the operator, folded into the guide in
  the same pull request: a part that outgrows one file is promoted to a
  subdirectory named for it, whose main document is its
  `overview.knack.md` (`shells/PowerShell/overview.knack.md`). A part with
  one file stays a file. This extends the rule without changing it, so it
  is recorded here rather than as a new revision.
- 2026-10-14 — Revision 2. The operator returned FieldManual#1: file names
  still repeated their path. The requested rule changed materially (no
  `.knack` directory suffix, no topic or `api.` prefix in file names), so
  the request was revised rather than amended.

### Target Disposition

### Source Closure
