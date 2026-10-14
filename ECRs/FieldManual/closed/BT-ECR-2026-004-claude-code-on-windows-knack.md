# Engineering Change Request: Add Claude Code on native Windows knacks

- ECR ID: `BT-ECR-2026-004`
- Request revision: 1
- Source project or origin namespace: Batshit Technology (`BT`)
- Target project: FieldManual
- Target canonical locator:
  https://github.com/Hard-Problems-Group-LLC/FieldManual
- Target component, version, or revision: `knacks/AI/Anthropic/` (new area),
  drafted on `04bcec5`
- Request owner: Matt "Breakpoint" Heck
- Drafted: 2026-10-14
- Submitted: 2026-10-14, as pull request
  Hard-Problems-Group-LLC/FieldManual#1
- Submitted-content digest: branch `knacks/from-batshit-technology`
  at commit `d354450`
- Source lifecycle: Closed
- Target handling status as reported: Accepted and merged,
  2026-10-14
- Target intake ID or authoritative reference:
  https://github.com/Hard-Problems-Group-LLC/FieldManual/pull/1
- Sensitivity and reuse classification: Public; stock reusable guidance
  with no project-specific content
- Related, duplicate, or superseded ECR IDs: siblings, one per knack area:
  BT-ECR-2026-001, BT-ECR-2026-002, BT-ECR-2026-003, BT-ECR-2026-005,
  BT-ECR-2026-006, BT-ECR-2026-007
- Origin and provenance: drafted by Claude Code agents for the
  operator from the source project's session transcripts, records and git
  history, then reviewed for consistency, links and neutrality.

## Submitted Request

Freeze this section after first transport. Preserve a prior revision when a
material change must be resubmitted.

### Problem And Source Impact

FieldManual covers OpenAI's Codex app server but not Claude Code, and nothing
covers running a coding agent on native Windows, where there is no command
sandbox, two shells with different rules, blocked sleep loops, a scratch
folder that vanishes with the session, and long sessions that a forced restart
can end. One purpose of the source project is learning to use Claude (and
later Codex) effectively for advanced Windows development.

### Requested Outcome

- Add `knacks/AI/Anthropic/` with a `claude-code/` subarea and
  `claude-code/windows.knack/`: the harness's tools on Windows, subagents and
  workflows, memory and durable state (6 files, about 5,900 words).
- Index it in `knacks/AI/README.md`.

The proposed files are on the branch `knacks/from-batshit-technology` of
Hard-Problems-Group-LLC/FieldManual (added in `4f7936e`, renamed to their
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

- Drawn from about 30 hours of Claude Code sessions on native Windows,
  including background subagents, multi-agent workflows, context compaction
  and a forced restart mid-agent.
- Version-sensitive behaviour is cited to the Claude Code documentation and
  dated; behaviour seen only in transcripts is labelled as observed.

## Source Tracking

After first transport, maintain this section as an append-only history.

### Local Mitigation

Batshit Technology keeps the same lessons in its own records (`docs/AI-*.txt`)
until the knacks are available from the FieldManual submodule.

### Transport And Receipt Log

- 2026-10-14 — Drafted locally; not yet submitted.
- 2026-10-14 — Submitted, revision 1, as pull request
  Hard-Problems-Group-LLC/FieldManual#1 from branch
  `knacks/from-batshit-technology` at `d354450`.
- 2026-10-14 — Pull request FieldManual#1 updated to `d5af082` after the
  operator's review.
- 2026-10-14 — Pull request history rebuilt so every commit adds files at
  their final paths; head `19fcd4f`. Content unchanged.
- 2026-10-14 — Pull request FieldManual#1 merged into `main` as `b981327` by
  the FieldManual owner; branch deleted.

### Discussion And Amendments

- 2026-10-14 — The operator returned FieldManual#1 because file names repeated
  their path. Under BT-ECR-2026-009 revision 2 the files were renamed in
  `d5af082`: no `.knack` directory suffix, and no topic or `api.` prefix in
  file names. The knacks are now at
  `knacks/AI/Anthropic/claude-code/windows/`. Their content is unchanged; the
  request stands as submitted.

### Target Disposition

- Accepted and implemented, 2026-10-14. FieldManual merged pull request #1
  (https://github.com/Hard-Problems-Group-LLC/FieldManual/pull/1) into `main`
  as `b981327`, after review and two rounds of requested naming changes, which
  are recorded above.

### Source Closure

- 2026-10-14 — Closed on merge by Claude Code for Matt "Breakpoint" Heck,
  who merged the pull request; disposition accepted and implemented. Batshit Technology picks the
  change up by advancing its `FieldManual/` submodule.
