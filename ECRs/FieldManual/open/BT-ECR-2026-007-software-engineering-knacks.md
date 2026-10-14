# Engineering Change Request: Add testing, procedural generation and demonstration knacks

- ECR ID: `BT-ECR-2026-007`
- Request revision: 1
- Source project or origin namespace: Batshit Technology (`BT`)
- Target project: FieldManual
- Target canonical locator:
  https://github.com/Hard-Problems-Group-LLC/FieldManual
- Target component, version, or revision: `knacks/software-engineering/`
  (three new areas), drafted on `04bcec5`
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
  BT-ECR-2026-001, BT-ECR-2026-002, BT-ECR-2026-003, BT-ECR-2026-004,
  BT-ECR-2026-005, BT-ECR-2026-006
- Origin and provenance: drafted by Claude Code agents for the
  operator from the source project's session transcripts, records and git
  history, then reviewed for consistency, links and neutrality.

## Submitted Request

Freeze this section after first transport. Preserve a prior revision when a
material change must be resubmitted.

### Problem And Source Impact

Plugins for closed host applications cannot be unit tested where it matters,
generated content needs testable plans, and demonstrations for humans are
usually timer scripts that drift from what actually runs. FieldManual has no
guidance on in-host smoke testing, data-first generation, procedural sprites,
or directed showcases.

### Requested Outcome

- Add `testing/in-host-smoke.knack/`, `testing/proving-checks.knack/`,
  `procedural-generation/data-first.knack/`,
  `procedural-generation/sprites.knack/` and
  `demonstrations/showcases.knack/`, with area READMEs (21 files, about 22,900
  words).
- Update `knacks/software-engineering/README.md`.

The proposed files are on the branch `knacks/from-batshit-technology` of
Hard-Problems-Group-LLC/FieldManual (added in `0486ac3`, renamed to their
final paths in `65cd769`), offered as a pull request against `main` at
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

- Sabotage runs in the source project exposed several vacuous checks; 8 of 9
  behavioural sabotage runs logged no error, so a clean log is no test.
- Derived from an in-host smoke runner that grew to 137 checks, a procedurally
  planned town with unit-tested invariants, and screen-captured showcases.
- The sketch code ran: the summed-area table against brute force, the flood
  fill, and the Pillow behaviours described (anti-aliasing, alpha
  premultiplication, Lanczos overshoot).

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
