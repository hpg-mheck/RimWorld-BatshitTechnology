# Decision Log

Record durable decisions here with newest entries at the top.

For each entry, include:

- date
- decision
- status when relevant
- rationale
- related files, proposals, or backlog items

## 2026-10-13: Adopt FieldManual and publish Ubersight status

- Decision: add FieldManual as the `FieldManual/` submodule and run its
  bootstrap; publish agent status for Ubersight through
  `Tools/ubersight_status.py`.
- Status: done. Records migration is under review (BT-PROP-2026-001).
- Rationale: requested by Matt "Breakpoint" Heck. FieldManual is HPG's
  shared project-management and planning baseline, replacing TheKnowledge.
  Ubersight cannot yet run on Windows, so a dependency-free writer mirrors
  its `--write-status` rules and file format; a unit test reads the output
  with Ubersight's own reader when a checkout is available.
- Related: `AGENTS.md` ("FieldManual and Ubersight"), `FieldManual-config.toml`,
  `Tools/ubersight_status.py`, `Tools/tests/test_ubersight_status.py`.

