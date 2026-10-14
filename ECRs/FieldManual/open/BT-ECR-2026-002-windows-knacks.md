# Engineering Change Request: Add Windows knacks for AI agents and developers

- ECR ID: `BT-ECR-2026-002`
- Request revision: 1
- Source project or origin namespace: Batshit Technology (`BT`)
- Target project: FieldManual
- Target canonical locator:
  https://github.com/Hard-Problems-Group-LLC/FieldManual
- Target component, version, or revision: `knacks/windows/` (new area),
  drafted on `04bcec5`
- Request owner: Matt "Breakpoint" Heck
- Drafted: 2026-10-14
- Submitted: Not yet submitted
- Submitted-content digest: Not yet recorded
- Source lifecycle: Open
- Target handling status as reported: Not submitted
- Target intake ID or authoritative reference: None
- Sensitivity and reuse classification: Public; stock reusable guidance
  with no project-specific content
- Related, duplicate, or superseded ECR IDs: siblings, one per knack area:
  BT-ECR-2026-001, BT-ECR-2026-003, BT-ECR-2026-004, BT-ECR-2026-005,
  BT-ECR-2026-006, BT-ECR-2026-007
- Origin and provenance: drafted by Claude Code agents for the
  operator from the source project's session transcripts, records and git
  history, then reviewed for consistency, links and neutrality.

## Submitted Request

Freeze this section after first transport. Preserve a prior revision when a
material change must be resubmitted.

### Problem And Source Impact

FieldManual has no guidance for working on a Windows workstation as, or
alongside, an AI coding agent. A long multi-agent session on native Windows
hit quoting and encoding traps in Git Bash and both PowerShell editions, hung
network Git commands, an unexplained forced restart, notification sounds from
automated game launches, files locked by background processes, cropped screen
captures on a high-DPI display, and child processes surviving their launcher.
Each cost time to diagnose, and each applies to any Windows project.

### Requested Outcome

- Add `knacks/windows/` with six hierarchies: `shells`, `git`, `event-logs`,
  `processes-and-files`, `desktop` and `porting` (21 files, about 24,100
  words).
- Index the area in `knacks/README.md`; link the existing
  `authentication/GitHub-SSH-multi-org-sanity` knack rather than repeating it.

The proposed files are on the branch `knacks/from-batshit-technology` of
Hard-Problems-Group-LLC/FieldManual (added in `2c18bfa`, renamed to their
final paths in `d354450`), offered as a pull request against `main` at
`04bcec5`.

### Acceptance Criteria

- Every file follows `knacks/authoring-guide.md`: naming, a `<topic>.knack/`
  hierarchy with an overview near 1,250 words and companions of at most 2,500,
  load triggers, sharp edges, a checklist and sources.
- All relative links resolve, including links into the other areas added by
  sibling ECRs (merge them together, or adjust the links).
- No project, client or person names, no workstation paths, and no
  credentials; examples are fictional.
- Version-sensitive claims carry a review date and are labelled as documented,
  observed or inferred.
- The area README and `knacks/README.md` index every topic and companion.

### Non-Goals

- Project-specific guidance: game-specific material goes to the supplemental
  pack hpg-mheck/FieldManual-RimWorld instead.
- Changes to FieldManual's standards, templates or bootstrap.

### Constraints And Risks

- Several claims are observed on one Windows 11 workstation (build 26300) in
  October 2026 and are labelled as such; they may not hold on other builds or
  editions.
- The knacks cross-link each other across the seven sibling ECRs; accepting
  only some of them leaves links to adjust.
- Tools change quickly (Claude Code, Visual Studio, the Sandbox app, Steam);
  each file carries a review date so stale guidance can be found.

### Evidence At Submission

- The PowerShell examples ran under Windows PowerShell 5.1 and PowerShell 7.6;
  the Git Bash, Python and cmd behaviours described were reproduced, including
  a batch-shim argument running a second command and a child surviving a
  killed launcher until placed in a job object.
- Reboot forensics come from a real Windows Update restart, read back from the
  System log (events 1074, 6006, 109, 12 and 13).
- Windows OpenSSH resolving its configuration from the registered profile
  path, not `HOME` or `USERPROFILE`, was observed and matches the
  Win32-OpenSSH source.

## Source Tracking

After first transport, maintain this section as an append-only history.

### Local Mitigation

Batshit Technology keeps the same lessons in its own records (`docs/AI-*.txt`)
until the knacks are available from the FieldManual submodule.

### Transport And Receipt Log

- 2026-10-14 — Drafted locally; not yet submitted.

### Discussion And Amendments

### Target Disposition

### Source Closure
