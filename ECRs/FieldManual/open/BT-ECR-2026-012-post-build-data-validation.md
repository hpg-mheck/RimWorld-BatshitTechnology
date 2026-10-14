# Engineering Change Request: Validate Data Files in the Build

- ECR ID: `BT-ECR-2026-012`
- Request revision: 1
- Source project or origin namespace: Batshit Technology (`BT`)
- Target project: FieldManual
- Target canonical locator:
  https://github.com/Hard-Problems-Group-LLC/FieldManual
- Target component, version, or revision:
  `knacks/dotnet/framework-builds/msbuild.knack.md`, as of `b981327`
- Request owner: Matt "Breakpoint" Heck
- Drafted: 2026-10-14
- Submitted: 2026-10-14, as pull request
  Hard-Problems-Group-LLC/FieldManual#3
- Submitted-content digest: branch `knacks/post-build-data-validation` at
  commit `8698b06`
- Source lifecycle: Open
- Target handling status as reported: Open pull request, awaiting review
- Target intake ID or authoritative reference:
  https://github.com/Hard-Problems-Group-LLC/FieldManual/pull/3
- Sensitivity and reuse classification: Public; neutral example names
- Related, duplicate, or superseded ECR IDs: BT-ECR-2026-001 (the .NET
  knacks)
- Origin and provenance: the operator suggested a build step after a
  silent runtime failure was confirmed in game (2026-10-14); drafted by
  Claude Code for the operator.

## Submitted Request

Freeze this section after first transport. Preserve a prior revision when a
material change must be resubmitted.

### Problem And Source Impact

Host plugins often ship data files that the host binds to the plugin's
types by name at load time. In the source project's host, a type name that
does not resolve compiles cleanly and then silently drops the whole data
definition when the host loads it (confirmed in game on 2026-10-14). The
project had a validator that catches this, but it ran only by hand, so the
mistake could reach a build that looked green. The MSBuild companion of the
`.NET Framework builds` knack does not describe wiring such a check into the
build.

### Requested Outcome

Add a section to `knacks/dotnet/framework-builds/msbuild.knack.md`: run a
data-file validator after `Build` against `$(TargetPath)`; print problems
in the canonical diagnostic form so they reach the error list; fail the
build through an `Error` task; keep the step cheap with an opt-out
property; prove it with a deliberately broken file. Cite the `Exec` task,
the diagnostic-format page, and target build order.

### Acceptance Criteria

- The section gives a working target with neutral names and the points
  above, within the file's size guidance.
- Links resolve; the review date is current.

### Non-Goals

- A validator for any particular host's data format.

### Constraints And Risks

- The pattern assumes a Windows build host for Windows PowerShell 5.1
  validators; the example's condition skips it elsewhere.

### Evidence At Submission

- The source project's `ValidateDefs` target (commit `6aea27b` on its
  trunk) runs its validator after every build in about four seconds; a
  misspelled type name in a data file failed the build with one error.
- Commit `8698b06` on branch `knacks/post-build-data-validation`.

## Source Tracking

After first transport, maintain this section as an append-only history.

### Local Mitigation

Already in place in the source project (`ValidateDefs`).

### Transport And Receipt Log

- 2026-10-14 — Drafted and submitted as pull request FieldManual#3 from
  branch `knacks/post-build-data-validation` at `8698b06`.

### Discussion And Amendments

### Target Disposition

### Source Closure
