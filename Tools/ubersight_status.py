"""Publish agent status for Ubersight, without needing Ubersight installed.

Ubersight (Hard-Problems-Group-LLC/Ubersight) is a terminal sidecar that
shows what a long-running development session is doing. It reads
``.local/ubersight/status.json`` in its ``ubersight.status.v1`` format.
Ubersight's own ``ubersight --write-status`` is the preferred writer, but it
does not yet run on Windows, so this dependency-free script writes the same
file with the same rules, mirroring Ubersight's ``write_status_file`` and
its ``--write-status`` validation at the revision recorded below.

Usage, from anywhere (the default path is this repository's
``.local/ubersight/status.json``)::

    python Tools/ubersight_status.py \\
        --phase-row "tractor:active:Tractor Bomb" \\
        --phase-row "showcase-api:pending:Showcase API" \\
        --slice "barn:active:Build the Tractor Barn" \\
        --slice "physics:pending:Multi-bomb physics" \\
        --notes "Step 8 is running in the background."

Rows use ``ID:STATE:TITLE``; states are pending, active, done, failed,
blocked and skipped. A write needs at least one phase row, exactly one
active phase, at least one slice, at most one active slice, and one active
slice unless ``--complete`` is given. Never put prompts, transcripts,
credentials, hostnames, network or location details in status text.

Mirrors Ubersight 0.1.0 at fe9db310cf8b663e81de2bd29b505140de1e1837.
"""

from __future__ import annotations

import argparse
import datetime
import json
import os
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Sequence

SCHEMA = "ubersight.status.v1"
"""The status schema Ubersight reads."""

VALID_STATES = ("pending", "active", "done", "failed", "blocked", "skipped")
"""Row states Ubersight accepts, for phases and slices alike."""

MAX_NOTES = 2000
"""A bound on note length: status is a glance, not a transcript."""

DEFAULT_PATH = (
    Path(__file__).resolve().parent.parent / ".local" / "ubersight" / "status.json"
)
"""This repository's checkout-local status file."""


@dataclass(frozen=True)
class Row:
    """One phase or slice row.

    Attributes:
        row_id: A compact, project-owned identifier.
        state: One of :data:`VALID_STATES`.
        title: A one-line title; the pane clips rather than wraps it.
    """

    row_id: str
    state: str
    title: str

    def to_mapping(self) -> dict[str, str]:
        """Return the row as Ubersight serialises it.

        Returns:
            A mapping with ``id``, ``title`` and ``state``.
        """
        return {"id": self.row_id, "title": self.title, "state": self.state}


def parse_row(value: str, kind: str) -> Row:
    """Parse an ``ID:STATE:TITLE`` row, as Ubersight's CLI does.

    Args:
        value: The command-line text.
        kind: "phase" or "slice", for error messages.

    Returns:
        The parsed row.

    Raises:
        ValueError: If the text is malformed, the ID or title is empty, or
            the state is not one of :data:`VALID_STATES`.
    """
    parts = value.split(":", 2)
    if len(parts) != 3:
        raise ValueError(f"{kind} row must use ID:STATE:TITLE: {value!r}")
    row_id, state, title = (part.strip() for part in parts)
    if not row_id:
        raise ValueError(f"{kind} ID must not be empty: {value!r}")
    if state not in VALID_STATES:
        raise ValueError(
            f"{kind} state must be one of {', '.join(VALID_STATES)}: {value!r}"
        )
    if not title:
        raise ValueError(f"{kind} title must not be empty: {value!r}")
    return Row(row_id, state, title)


def validate(
    phases: Sequence[Row], slices: Sequence[Row], complete: bool, notes: str
) -> Row:
    """Apply Ubersight's ``--write-status`` rules.

    Args:
        phases: Phase rows, in display order.
        slices: Slice rows, in display order.
        complete: Whether the work is complete.
        notes: High-level notes.

    Returns:
        The single active phase.

    Raises:
        ValueError: If any rule is broken.
    """
    if not phases:
        raise ValueError("a status needs at least one phase row")
    active_phases = [row for row in phases if row.state == "active"]
    if len(active_phases) != 1:
        raise ValueError("a status needs exactly one active phase row")
    if not slices:
        raise ValueError("a status needs at least one slice row")
    active_slices = [row for row in slices if row.state == "active"]
    if len(active_slices) > 1:
        raise ValueError("a status allows at most one active slice row")
    if not active_slices and not complete:
        raise ValueError("a status needs one active slice unless --complete")
    if len(notes) > MAX_NOTES:
        raise ValueError(f"notes are limited to {MAX_NOTES} characters")
    return active_phases[0]


def build_payload(
    phases: Sequence[Row],
    slices: Sequence[Row],
    notes: str = "",
    blocked: bool = False,
    complete: bool = False,
    now: datetime.datetime | None = None,
) -> dict:
    """Build the ``ubersight.status.v1`` document.

    Args:
        phases: Phase rows, in display order.
        slices: Slice rows, in display order.
        notes: High-level notes.
        blocked: The global blocked flag.
        complete: The global complete flag.
        now: The time to stamp; the local, timezone-aware time if omitted.

    Returns:
        The document, keyed exactly as Ubersight writes it.

    Raises:
        ValueError: If the rows break Ubersight's rules.
    """
    notes = notes.strip()
    active = validate(phases, slices, complete, notes)
    stamp = (now or datetime.datetime.now()).astimezone()
    return {
        "schema": SCHEMA,
        "updated_at": stamp.isoformat(timespec="seconds"),
        "phase": {"id": active.row_id, "title": active.title},
        "phases": [row.to_mapping() for row in phases],
        "slices": [row.to_mapping() for row in slices],
        "notes": notes,
        "blocked": blocked,
        "complete": complete,
    }


def write_status(payload: dict, path: Path) -> None:
    """Write the document atomically, as Ubersight does.

    A same-directory temporary file is written and then moved over the
    target, so a reader never sees a half-written file.

    Args:
        payload: The document from :func:`build_payload`.
        path: The status file.
    """
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp_path = path.with_name(f".{path.name}.{os.getpid()}.tmp")
    try:
        tmp_path.write_text(
            json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8"
        )
        os.replace(tmp_path, path)
    finally:
        if tmp_path.exists():
            tmp_path.unlink()


def main(argv: Sequence[str] | None = None) -> int:
    """Parse the command line and write the status file.

    Args:
        argv: Arguments, without the program name; ``sys.argv`` if omitted.

    Returns:
        The process exit code: 0 on success, 2 on invalid input.
    """
    parser = argparse.ArgumentParser(
        description="Write .local/ubersight/status.json for Ubersight."
    )
    parser.add_argument("--phase-row", action="append", default=[],
                        metavar="ID:STATE:TITLE", help="phase row; repeatable")
    parser.add_argument("--slice", action="append", default=[],
                        metavar="ID:STATE:TITLE", help="slice row; repeatable")
    parser.add_argument("--notes", default="", help="high-level notes")
    parser.add_argument("--blocked", action="store_true", help="mark blocked")
    parser.add_argument("--complete", action="store_true", help="mark complete")
    parser.add_argument("--path", type=Path, default=DEFAULT_PATH,
                        help="status file (default: this repository's)")
    args = parser.parse_args(argv)
    try:
        phases = [parse_row(value, "phase") for value in args.phase_row]
        slices = [parse_row(value, "slice") for value in args.slice]
        payload = build_payload(
            phases, slices, args.notes, args.blocked, args.complete)
    except ValueError as error:
        print(f"ubersight_status: {error}", file=sys.stderr)
        return 2
    write_status(payload, args.path)
    print(f"ubersight_status: wrote {args.path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
