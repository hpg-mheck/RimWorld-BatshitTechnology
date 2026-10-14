# Engineering Change Request: Television Reboots When a PC Game Starts

- ECR ID: `BT-ECR-2026-010`
- Request revision: 1
- Source project or origin namespace: Batshit Technology (`BT`)
- Target project: Samsung TV (Tizen firmware)
- Target canonical locator: none; Samsung support (the TV's "Contact
  Samsung" menu or the Samsung Members app) is the only intake channel
- Target component, version, or revision: a 2020 Samsung television used
  as a PC display over HDMI. EDID: manufacturer `SAM`, product code `7017`,
  name `SAMSUNG`, year of manufacture 2020. Firmware version not recorded.
- Request owner: Matt "Breakpoint" Heck
- Drafted: 2026-10-14
- Submitted: Not yet submitted
- Submitted-content digest: Not yet recorded
- Source lifecycle: Open
- Target handling status as reported: Not submitted
- Target intake ID or authoritative reference: None
- Sensitivity and reuse classification: Public; names a consumer device
  model, nothing else
- Related, duplicate, or superseded ECR IDs: None
- Origin and provenance: written at the operator's request as an exercise
  in the ECR format, from one observed failure; drafted by Claude Code.

## Submitted Request

Freeze this section after first transport. Preserve a prior revision when a
material change must be resubmitted.

### Problem And Source Impact

On 2026-10-14 at about 01:25 to 01:32 PDT, while automated tests were
starting a DirectX game (RimWorld, a Unity title) on the connected PC, the
television stopped displaying and rebooted. When the operator powered it
back on, it showed the Tizen boot logo, so the set had restarted its
operating system rather than entering standby.

The PC was unaffected: no restart, no display-driver reset, and no error in
its System log. The only display events in that window are two Windows
Auto HDR notifications (event 4125, "Good news—%1 has been enhanced for a
brighter and more detailed picture"), at 01:25:25 and 01:31:51, each marking
a game start. The same notification fired 105 times in the preceding 24
hours, once per automated launch; the reboot was seen once.

After the reboot, Windows was driving the set at 3840x2160 at 24 Hz, an
unusual desktop rate. Whether that mode preceded the reboot or was
negotiated afterwards is not known.

Source impact: automated test runs cannot be left unattended on this
display, and the operator loses the picture mid-session.

### Requested Outcome

1. The television should survive HDMI signal changes made by a PC at
   application start (resolution or refresh-rate changes, HDR metadata
   changes, brief signal loss) without rebooting its operating system. At
   worst it should blank and resynchronise.
2. If a reboot is unavoidable, the set should record the cause in a form
   the owner can retrieve (for example in its self-diagnosis or support
   menu).

### Acceptance Criteria

- One hundred consecutive launches of a DirectX game from a PC with Windows
  HDR and Auto HDR on, over HDMI at 3840x2160, produce no television reboot.
- If the set does reboot, its support menu shows the time and cause.

### Non-Goals

- Changes to Windows, the graphics driver, or the game.
- Picture quality, HDR tone mapping, or input lag.

### Constraints And Risks

- The cause is not established. The evidence is one reboot correlated in
  time with a game start, against 105 game starts without one. Candidate
  triggers include the game's start-up resolution change, an HDR metadata
  change from Auto HDR, and a mode change involving the 24 Hz output; none
  has been reproduced.
- The firmware version, picture mode, input settings (such as Input Signal
  Plus or Game Mode) and HDMI port were not recorded. Samsung support would
  ask for them first.
- The workstation's GPU is an NVIDIA GeForce RTX 5060 Ti (driver
  32.0.16.1088); an Intel UHD Graphics 750 and a virtual display adapter
  are also present. The signal path is otherwise unknown.

### Evidence At Submission

- Windows 11 Pro build 26300, System log, provider `Display`, event 4125
  at 01:25:25 and 01:31:51 PDT on 2026-10-14; 105 such events in 24 hours.
- No System log events between 01:20 and 01:40 other than those two and an
  unrelated network event.
- `WmiMonitorID`: manufacturer `SAM`, product `7017`, year 2020.
- The operator's report of the Tizen boot logo on power-up.

## Source Tracking

After first transport, maintain this section as an append-only history.

### Local Mitigation

- Turn off Auto HDR for games launched by automation (Settings, System,
  Display, Graphics, the game's executable), or disable the television's
  automatic HDR mode switching, so test launches change the signal less.
- Prefer windowed test launches that do not change the display mode.
- Check the desktop refresh rate after any television reboot.

### Transport And Receipt Log

- 2026-10-14 — Drafted locally as an exercise; not submitted, and not
  planned for submission until the trigger is reproduced and the
  television's firmware version and settings are recorded.

### Discussion And Amendments

### Target Disposition

### Source Closure
