# Migrate Project Records into FieldManual's Layout

- ID: BT-PROP-2026-001
- Author: Claude (for Matt "Breakpoint" Heck)
- Date: 2026-10-13
- Status: Under Review
- Reviewers: Matt "Breakpoint" Heck
- Affected projects or audiences: Batshit Technology contributors, human
  and AI
- Related work: the FieldManual adoption commit; `ROADMAP.md`;
  `docs/AI-*.txt`; `AGENTS.md`

## Problem Statement

Batshit Technology adopted FieldManual on 2026-10-13. FieldManual expects
the backlog, active and completed work, bugs, proposals, reviews, decisions,
open questions and human requests under `project-management/`, and durable
contracts under `docs/specifications/`. The project's live records are
elsewhere:

- `ROADMAP.md`: order of work, branches, decisions per feature;
- `docs/AI-TODO.txt`: backlog and test justifications;
- `docs/AI-DECISIONS.txt`: decision history;
- `docs/AI-KNOWN-ISSUES.txt` and `docs/AI-FIXME.txt`: bugs and limits;
- `docs/AI-requests-for-humans.txt`: open questions for the operator;
- `docs/AI-TESTING-STRATEGY.txt`, `docs/AI-RECOMMENDATIONS.txt`,
  `docs/AI-scratchpad.txt`: practice notes;
- feature specifications: `docs/COLONY-BOMB.md`, `docs/TRACTOR-BOMB.md`,
  `docs/SHOWCASE.md` and the rest.

Two record systems side by side drift apart.

## Goals

- One place for each kind of record, in FieldManual's layout.
- No history lost: every decision and request survives the move, with
  its date.
- `AGENTS.md` and `ROADMAP.md` point to the new homes.

## Non-Goals

- Rewriting the feature specifications' content.
- Changing how features are built, tested or branched.

## Constraints And Assumptions

- Verified: the FieldManual bootstrap created the skeletons and touched no
  existing record.
- Verified: active feature branches (`feature/tractor-bomb`) edit
  `docs/AI-*.txt`. Moving files under them invites merge conflicts.
- Assumed: the operator wants FieldManual's layout to become the single
  source of truth, rather than a parallel one.

## Current Context

Until this proposal is decided, `AGENTS.md` records a transitional
exception: the existing records stay authoritative, and the new
`project-management/` skeleton is not yet used.

## Proposed Approach

Move, do not copy, in one change on `trunk` between feature merges:

| Today | Moves to |
|-------|----------|
| `docs/AI-TODO.txt` backlog | `project-management/backlog.md` |
| `ROADMAP.md` order and branches | `project-management/development-roadmap.md` |
| `docs/AI-DECISIONS.txt` | `project-management/decision-log.md` (newest first) |
| `docs/AI-KNOWN-ISSUES.txt`, `docs/AI-FIXME.txt` | `project-management/bugs/` |
| `docs/AI-requests-for-humans.txt` | `project-management/ai-human-requests.md` and `open-questions.md` |
| Feature specifications in `docs/` | `docs/specifications/` |
| `docs/AI-TESTING-STRATEGY.txt` | `docs/specifications/testing.md` |

Then:

- `ROADMAP.md` stays as a short pointer;
- `AGENTS.md` drops the transitional exception and its references to the
  old files;
- the XML tests that read `docs/TRACTOR-BOMB.md` are repointed.

## Alternatives Considered

- **Keep both** (today's transitional state): cheapest now, drifts later.
- **Migrate incrementally**, kind by kind: fewer conflicts at a time, but
  a longer period with two systems.

## Risks And Mitigations

- **Merge conflicts with feature branches:** migrate right after a feature
  merges, when no branch is open, or rebase open branches immediately.
- **Broken links in docs and tests:** a link check and the full test suite
  run before committing.

## Validation

- Every old record's content is present in its new home (diff review).
- Build, unit tests, the Def validator and the smoke test pass.

## Open Questions

1. Approve, defer, or reject? Decision authority: Matt "Breakpoint" Heck.
2. If approved, migrate after the Tractor Bomb merges (proposed), or now?

## Milestones

1. Decision on this proposal.
2. The migration commit on `trunk`, with all checks passing.

## Adoption And Rollout

- Communication: `AGENTS.md`, `README.md` and `ROADMAP.md` point to the
  new homes.
- Rollback: one revert of the migration commit.
- Retirement: the old `docs/AI-*.txt` files are removed by the move.

## Decision Log

- 2026-10-13: Proposed with the FieldManual adoption.
