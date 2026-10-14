# Engineering Change Request: Note That a Television Can Reboot During Automated Game Launches

- ECR ID: `BT-ECR-2026-011`
- Request revision: 1
- Source project or origin namespace: Batshit Technology (`BT`)
- Target project: FieldManual
- Target canonical locator:
  https://github.com/Hard-Problems-Group-LLC/FieldManual
- Target component, version, or revision:
  `knacks/windows/event-logs/background-noise.knack.md`, "Auto HDR
  Notifications During Automated Game Launches", as of `b981327`
- Request owner: Matt "Breakpoint" Heck
- Drafted: 2026-10-14
- Submitted: 2026-10-14, as pull request
  Hard-Problems-Group-LLC/FieldManual#2
- Submitted-content digest: branch `knacks/display-reboot-on-game-launch`
  at commit `39e6835`
- Source lifecycle: Open
- Target handling status as reported: Open pull request, awaiting review
- Target intake ID or authoritative reference:
  https://github.com/Hard-Problems-Group-LLC/FieldManual/pull/2
- Sensitivity and reuse classification: Public; no device identifiers
- Related, duplicate, or superseded ECR IDs: BT-ECR-2026-002 (the Windows
  knacks); BT-ECR-2026-010 (the same incident, filed against the
  television)
- Origin and provenance: an incident on 2026-10-14 during automated test
  runs; drafted by Claude Code for the operator.

## Submitted Request

Freeze this section after first transport. Preserve a prior revision when a
material change must be resubmitted.

### Problem And Source Impact

The event-logs knack explains Auto HDR notifications (Display event 4125)
as noise from automated game launches. It does not warn that the same
launches can disturb the display. On 2026-10-14 a television used as the
monitor rebooted its own operating system during an automated launch,
while the PC logged nothing but event 4125. The operator lost the picture
mid-session, and the desktop afterwards ran at an unusual 24 Hz.

### Requested Outcome

Add the observation to the Auto HDR section: what was seen, that the
trigger is uncertain (the game's start-up mode change, the HDR switch, or
the refresh rate), a check of the desktop mode after such a reboot, and
advice for machines that drive a television (turn Auto HDR off for the game
under test, launch test runs windowed, avoid long unattended runs on a
display that has done this).

### Acceptance Criteria

- The section states the observation as observed once, with its
  uncertainty, and gives the advice above.
- The file stays within the size guidance and keeps its review date
  current.

### Non-Goals

- Naming the television's make or model in stock guidance.
- Changing the existing remedies.

### Constraints And Risks

- One observation in about a hundred launches; the advice is cautious, not
  a diagnosis.

### Evidence At Submission

- Commit `39e6835` on branch `knacks/display-reboot-on-game-launch`.
- The System log on the affected workstation: event 4125 at the launches
  around the reboot, and no other display events.

## Source Tracking

After first transport, maintain this section as an append-only history.

### Local Mitigation

BT-ECR-2026-010 records the same incident against the television and lists
local mitigations.

### Transport And Receipt Log

- 2026-10-14 — Drafted and submitted as pull request FieldManual#2 from
  branch `knacks/display-reboot-on-game-launch` at `39e6835`.

### Discussion And Amendments

### Target Disposition

### Source Closure
