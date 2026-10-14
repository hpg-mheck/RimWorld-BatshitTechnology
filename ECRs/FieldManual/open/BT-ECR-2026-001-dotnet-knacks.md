# Engineering Change Request: Add .NET knacks for host plugins and Visual Studio

- ECR ID: `BT-ECR-2026-001`
- Request revision: 1
- Source project or origin namespace: Batshit Technology (`BT`)
- Target project: FieldManual
- Target canonical locator:
  https://github.com/Hard-Problems-Group-LLC/FieldManual
- Target component, version, or revision: `knacks/dotnet/` (new area), drafted
  on `04bcec5`
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
  BT-ECR-2026-002, BT-ECR-2026-003, BT-ECR-2026-004, BT-ECR-2026-005,
  BT-ECR-2026-006, BT-ECR-2026-007
- Origin and provenance: drafted by Claude Code agents for the
  operator from the source project's session transcripts, records and git
  history, then reviewed for consistency, links and neutrality.

## Submitted Request

Freeze this section after first transport. Preserve a prior revision when a
material change must be resubmitted.

### Problem And Source Impact

FieldManual has no .NET guidance. Batshit Technology builds a .NET Framework
4.7.2 plugin for a closed, Unity-based host on Windows, with host-free logic
in a netstandard2.0 library tested on net8.0. The lessons are general to any
.NET plugin for a closed host: language versions and polyfills on
netstandard2.0, building old-style projects with Visual Studio's MSBuild,
referencing host assemblies through an environment variable, inspecting closed
assemblies before relying on them, culture-dependent formatting that differs
between .NET Framework and .NET 8, and driving Visual Studio and its build
tools from scripts and agents. Without them, each project relearns them
through failed builds.

### Requested Outcome

- Add `knacks/dotnet/` with five hierarchies: `netstandard-shared-library`,
  `net-framework-builds`, `closed-assembly-inspection`,
  `culture-safe-formatting` and `visual-studio` (17 files, about 16,200
  words).
- Index the area in `knacks/README.md`.

The proposed files are commit `c702563` on the local branch
`knacks/from-batshit-technology` of a FieldManual checkout, based on
`04bcec5`. The branch is not pushed; it will be offered as a pull request when
the operator approves.

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

- Most claims were verified by running them on the workstation (Visual Studio
  2022 17.14, MSBuild 17.14, .NET 9 SDK, PowerShell 5.1 and 7.6), and the C#
  examples compiled for netstandard2.0.
- Verification corrected several lessons as first recorded: Microsoft advises
  against `LangVersion latest`; PowerShell 7, not 5.1, loaded every type of
  the host assembly tested; ilspycmd's newest releases need the .NET 10 SDK; a
  test project without `Microsoft.NET.Test.Sdk` failed with exit 1 here rather
  than passing with zero tests.
- Runtime differences measured: `0.1+0.2` formats as `0.3` on .NET Framework
  and `0.30000000000000004` on .NET 8; `double.Parse("1.5")` returns 15 under
  de-DE.

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
