# Sound Pipeline

Status: specified, not yet built; proposals pending Matt "Breakpoint" Heck.
Phase 5000 in `ROADMAP.md`, branch `feature/audio`. Points this document
proposes are marked *(proposal)* and gathered under "Open Questions"; they
stand unless he changes them.

## Summary

Batshit Technology has no sounds of its own. It borrows vanilla's:
`TrapArm` for the Tractor Bomb's protest, `HissSmall` for the
self-destruct alarm, `PsychicPulseGlobal` for singularities. The Colony
Bomb's countdown, the Mirror Mirror sting and the Drag Show's disco need
originals. This phase builds the audio twin of `Tools/make_textures.py`,
`Tools/make_sounds.py`, which synthesizes every sound from code, together
with SoundDef conventions, an audio section for `docs/ARTISTIC-MOTIF.md`,
the first family of sounds, and the tests that keep them honest.

## Goals

- **Fully synthesized:** no samples from anywhere, so licensing never
  arises.
- **Deterministic:** the same script renders the same audio on every
  machine, within one least significant bit.
- **One voice:** sounds follow the motif as the sprites do, at levels
  consistent with vanilla's.
- **Testable:** lengths, levels and signal frequencies are checked by
  unit tests; every SoundDef's clips by XML tests and a smoke check.
- **Cheap to run:** Python's standard library and nothing else.

## What the Game Accepts

From the 1.6 decompile (`.local/scratch/decomp`):

- **Folder and formats.** Mod audio lives under `Sounds/`; a clip path is
  relative to it, without extension. `ModContentLoader` accepts `.wav`,
  `.mp3`, `.ogg` and four tracker formats, but decodes only WAV, MP3 and
  Ogg Vorbis (through NAudio and NVorbis): tracker files pass the filter
  and then fail. The decoder's format switch is case sensitive, so `.WAV`
  fails too. A PCM or float WAV keeps its sample rate and channels.
- **Streaming.** A file over 300 KiB (307,200 bytes) is streamed from
  disk, through one reader shared by every source that plays it. Looping
  or overlapping a streamed clip is suspect until proved in game.
- **Lookup.** `ContentFinder` searches mods from last to first, then the
  base game, so paths must be unique to the mod.
- **Failures.** A missing clip logs an error, and so does an empty grain
  folder. A misspelled SoundDef in XML only warns and plays silence;
  `SoundDef.Named` in code is silent; a `[DefOf]` field logs an error.
- **Playback.** `volumeRange` is in percent. In-world sub-sounds are
  positional (linear roll-off over `distRange`, 25 to 70 by default)
  and need `context` MapOnly. `tempoAffectedByGameSpeed` raises pitch
  with game speed. Playing a sustainer as a one-shot, or the reverse, logs
  an error and plays nothing.

## The Generator

`Tools/make_sounds.py` follows the house style of `make_textures.py`:
palette constants at the top, small reusable helpers, one function per
asset, and a main block that renders everything.

### Recipes and Determinism

- **Recipes.** Each sound is a function registered with its output path,
  length and number of variants, as in
  `@recipe('Exotic/BT_ColonyBomb/BT_ColonyBomb_Bass', seconds=0.35)`.
- **Helpers:** oscillators (sine, triangle, band-limited saw and square),
  noise (white, pink, brown), envelopes, biquad filters, tape treatment
  (soft saturation, hiss, slight wow), echo, a small reverb, mixing,
  fades, a crossfaded loop seam, and the analysis functions below.
- **Randomness.** Each recipe seeds its own `random.Random` with an
  integer taken from the SHA-256 of its name (variant *i* hashes
  `name#i`). Only `random()` is called, the one method Python promises
  to repeat across versions; other distributions are built from it.
  Python's `hash()` is never used, since it changes per process.
- **No numpy.** Pure Python renders a second of three triangle voices at
  48 kHz in about 0.03 s on the operator's machine, whose default `python`
  (3.12.10) has neither numpy nor Pillow. Analysis uses the Goertzel
  algorithm, which measures one frequency without an FFT.

### The Sonic Palette

Constants mirror the visual palette; reuse them by name.

| Role | Constant | Value |
|------|----------|-------|
| Sample rate | `SR` | 48,000 Hz |
| DTMF, low group | `DTMF_LOW` | 697, 770, 852, 941 Hz |
| DTMF, high group | `DTMF_HIGH` | 1209, 1336, 1477, 1633 Hz |
| TTMF, third group | `TTMF_TOP` | 2098, 2319, 2562, 2831 Hz |
| Warning bass | `BASS_HZ` | 55 Hz, with harmonics |
| Theremin vibrato | `THEREMIN_VIB` | 5.5 Hz, ±30 cents *(proposal)* |
| Period band | `PERIOD_BAND` | 90 Hz to 11 kHz *(proposal)* |
| Tape hiss | `HISS_DB` | 54 dB under the sound *(proposal)* |
| Asset loudness | `TARGET_LUFS` | −16 LUFS *(proposal)* |
| Ceiling | `CEILING_DBTP` | −1 dBTP |

The DTMF frequencies are ITU-T Q.23's; the TTMF group continues their
ratios, as `docs/COLONY-BOMB.md` describes.

### Output

- **Effects and UI:** 48 kHz, 16-bit PCM, mono WAV, written by the
  standard `wave` module. The game positions in-world sounds itself, so
  mono loses nothing. A second is 96 KB.
- **At most 3 s a clip,** which keeps every effect and loop under the
  streaming threshold.
- **Music:** stereo Ogg Vorbis, encoded by `ffmpeg` (on `PATH` or named
  by `FFMPEG`), for the disco milestone only.
- **Committed,** as the PNGs are, since players never run Python, within
  a 5 MB budget for effects *(proposal)*.
- **Why 48 kHz:** ITU-R BS.1770 publishes its loudness filter
  coefficients at that rate. Unity resamples on playback.

### Loudness

Every clip is normalized to one loudness; the mix lives in each
SoundDef's `volumeRange`, set by ear against vanilla neighbours.

- **The meter** follows ITU-R BS.1770, as EBU R 128 does: K-weighting by
  two biquads, 400 ms blocks with 75% overlap, an absolute gate at −70
  LUFS and a relative gate 10 LU down.
- **One-shots** are measured by their loudest 400 ms window, padded with
  silence if shorter; loops and music by integrated loudness.
- **Target:** −16 LUFS, with true peak (4× oversampled) at or below −1
  dBTP. A clip the ceiling holds under target is peak-bound, and its
  recipe must say so.
- **Starting mix levels** come from the vanilla defs a sound replaces or
  sits beside: `Click` 30, `TrapArm` 20, `Explosion_Bomb` 80.

### Listening

As each sprite is checked at full size and at 64 px:

- `python Tools/make_sounds.py` renders everything into `Sounds/` and
  prints each file's length, loudness and peak; `--only <pattern>`
  renders a subset, `--list` lists recipes, and `--play <pattern>` plays
  through the standard `winsound` module.
- `--reel` writes a review reel to `.local/tmp/sounds-reel/`: every
  sound, a second apart, once at full range and once through a
  laptop-speaker filter (high-passed at 250 Hz). The second pass is the
  64 px check: a sound that vanishes there has failed.
- In game, vanilla's dev action **Sound > Play sound...** lists every
  one-shot, so a new sound can be heard beside its neighbours.

## The Audio Motif

A proposed section for `docs/ARTISTIC-MOTIF.md`, added in the same
change. **The idea in one line:** a cheerful 1950s public-information
film, narrating a catastrophe with perfect composure.

### The Sonic Palette

- **Steel is the signal.** Earnest engineering sounds like the telephone
  company: clean sine and triangle tones, exact standards, relays and
  motors.
- **The theremin is batshittium:** a wavering sine with wide vibrato and
  glide, on the one glowing thing in each device.
- **Hazard is the alarm.** Warning tones and the bass pulse mean real
  danger, like yellow and black stripes.
- **Comfort is the vibraphone:** soft mallets and celesta for anything
  that promises comfort.
- **The narrator's equipment** belongs to the showcase: the filmstrip
  advance chime, the projector's whir, a little tape hiss.
- **The punchline is brass.** Comic sounds (the raspberry, a muted
  trombone) land once, at the end, after a beat of silence.

### Rules

1. **The theremin means batshittium, and nothing else,** at most once
   per device, like the focal glow.
2. **Signals are exact.** Tonal signals use real frequencies and are never
   pitch-randomized: the deadpan manual's accurate numbers.
3. **Straight until the punchline.** One comic sound per event, after the
   serious ones, as each sign gets one undercut.
4. **Every death sounds distinct,** as every death looks distinct.
5. **Survive a laptop speaker.** Anything below 120 Hz carries harmonics
   above 200 Hz.
6. **Vintage, not broken.** Band-limited, lightly saturated and hissed;
   never crackling or dropping out in a way that reads as a fault.
7. **Silence is an instrument.** Pauses in a sequence stay quiet.
8. **Original only.** No samples, and no quotation of any film, show or
   record, including the 1967 episode Mirror Mirror borrows from.

## SoundDef Conventions

- **Names.** Defs and files are `BT_<Thing>_<Event>`. Files live in
  `Sounds/<vanilla category>/BT_<Thing>/` (`Exotic`, `Ambience`, `UI` and
  so on), with lowercase `.wav` or `.ogg`. A variant set is a folder of
  its own, read by `AudioGrain_Folder`; a single clip uses
  `AudioGrain_Clip`. The empty `Sounds/RandomReplicator/` placeholder is
  retired.
- **Defs** go in `Defs/SoundDefs/BT_Sounds_<Feature>.xml`, with abstract
  parents for families. Code reaches them through a `[DefOf]` class,
  never `SoundDef.Named`.
- **One-shot or sustainer.** Discrete events are one-shots, scheduled on
  game ticks so they pause with the game. Anything that lasts (wind, the
  pull, the shimmer, music) is a sustainer, spawned, maintained each tick
  and ended, as `CompSelfDestruct` does today, with `muteWhenPaused` and a
  short `sustainFadeoutTime`.
- **Randomization.** Mechanical and noisy sounds get 3 to 5 variants and
  a pitch range of about 0.95 to 1.05. Signal tones get one clip,
  `pitchRange` 1, and never `tempoAffectedByGameSpeed`, which would
  detune them.
- **Context.** In-world sounds are MapOnly; UI sounds play on camera.
  `maxSimultaneous` is set per def, for overlap at high game speed.

## The First Sounds

One-shots unless marked; each def has its own file or folder.

- **Colony Bomb** (`Sounds/Exotic/BT_ColonyBomb/`):
  - `BT_ColonyBomb_Bass`, 350 ms: 55 Hz with harmonics, a hard attack.
  - `BT_ColonyBomb_Dtmf00` to `Dtmf19`, 250 ms each: 770 and 1336 Hz
    triangles, step *n* raised by 2^(*n*/20).
  - `BT_ColonyBomb_Ttmf`, 200 ms: 941, 1633 and 2831 Hz.
  - `BT_ColonyBomb_Echo`, 1.2 s: the last moment four times, 6 dB
    quieter each, 250 ms apart.
  - `BT_ColonyBomb_Lull`, a 3 s sustainer loop: soft wind.
  - `BT_ColonyBomb_Thpppt`, 0.7 s, three variants: a buzzy low saw with
    fast flutter.
  - `BT_ColonyBomb_Boom`, 3 s: the deep boom under vanilla's blasts.
- **The implosion:** `BT_Implosion_Pull`, a 3 s sustainer loop, a rumble
  and a rising whistle, its pitch mapped to its age as `HissSmall`'s is.
  One shared mechanic, one shared sound.
- **Mirror Mirror** (`Sounds/Exotic/BT_MirrorMirror/`):
  - `BT_MirrorMirror_Shimmer`, a 3 s sustainer loop while a pawn gazes:
    glass-harmonica partials and a theremin glint.
  - `BT_MirrorMirror_Sting`, 2.5 s: brass stab, string tremolo, timpani.
- **Showcase** (`Sounds/UI/BT_Showcase/`): `BT_Showcase_Projector` at
  Start (1.5 s), `BT_Showcase_Advance` at each scene (0.4 s), and
  `BT_Showcase_RunOut` at the end (1.5 s). The chime was the audible
  "advance now" tone of classroom filmstrip records. The windows keep
  vanilla's clicks *(proposal)*, matching their vanilla dialog style.
- **Tractor Bomb:** `BT_TractorBomb_Protest`, 120 ms, a two-tone beep
  replacing `TrapArm`.

About 27 s in all, or 2.6 MB. The DTMF steps are pre-rendered
*(proposal)*, so each is exact and lasts 250 ms; a run-time
`pitchFactor` would shorten the higher beeps. Other stand-ins (the
self-destruct alarm, the shelter's failure alert, the Delivery Locker's
keypad, hinge and slam) are replaced in their own phases.

### Later: The Disco Track

An original track for the Drag Show, from the same script: about 120
beats a minute, four on the floor, an octave-bouncing bass, off-beat open
hi-hats, string stabs and a theremin hook (the stage light runs on
batshittium). It is a 60 to 90 s seamless stereo loop in Ogg Vorbis,
about 1.5 MB, played as a sustainer on the stage. It belongs to phase
8000; this phase only proves the Ogg path with a short test loop.

## Tests

**Generator** (`Tools/tests/test_make_sounds.py`, run by
`python -m unittest discover -s Tools/tests`):

- **Determinism:** identical bytes from two renders, and under two
  `PYTHONHASHSEED` values.
- **Committed files match:** a fresh render equals `Sounds/` within one
  least significant bit, so nothing is hand-edited or stale.
- **Format:** 48 kHz, 16-bit, mono (stereo for music), the `BT_` prefix,
  lowercase extensions, the declared length to the sample, at most 3 s.
- **Levels:** within 0.5 LU of target unless peak-bound; true peak at or
  below −1 dBTP; no DC offset; quiet first and last samples; continuous
  loop seams.
- **DTMF,** by Goertzel: the first step is strongest at 770 and 1336 Hz,
  every other DTMF frequency is at least 30 dB down, the twist is within
  Q.24's −4 to +8 dB, and step *n* peaks at 770 × 2^(*n*/20) within
  0.1%. The TTMF clip carries its three tones.
- **The meter:** a full-scale 1 kHz sine reads −3.01 LUFS, as BS.1770
  specifies; where `ffmpeg` is installed, its `ebur128` filter agrees
  within 0.5 LU.

**XML** (`XmlDefinitionTests`, with `Sounds/` copied as a fixture like
`Textures/`): every `BT_` clip path resolves to a file and every folder
path to a non-empty folder; every file is used; in-world sub-sounds are
MapOnly; signal tones have a pitch range of 1. `Tools/validate-defs.ps1`
checks clip paths as it does textures, against `Sounds/` and the
2,409 vanilla `sounds/` paths listed in `globalgamemanagers`.

**Smoke:** every `BT_` SoundDef plays once (sustainers spawned,
maintained and ended); each sub-sound's duration is within 10 ms of its
file; nothing logs an error or warning.

## Order of Work

Slices: 1000, branch and this specification; 2000, generator core, meter
and tests; 3000, the motif section and the countdown family; 4000,
SoundDefs, XML tests and the validator; 5000, the other first sounds;
6000, smoke checks and a listening review with Matt "Breakpoint" Heck;
7000, docs and the merge into `trunk`.

## Risks

- **It may sound cheap.** Simple synthesis leans toward chiptune. Tape
  treatment and band limits help; the listening review decides.
- **The sting and the disco** stretch simple synthesis. Mirror Mirror
  allows commissioning; this document prefers an honest radiophonic sound
  (early studio electronics) to a fake orchestra.
- **Game speed.** At the second speed (3×) the 500 ms DTMF interval is
  167 ms of real time, at the third (6×) 83 ms, and the 250 ms beeps
  pile up; `maxSimultaneous` caps them until question 8 is settled.
- **Silent typos.** A misspelled SoundDef only warns; the XML test and
  the smoke check's warning scan catch it.
- **Short sounds.** BS.1770 was made for programmes, not 200 ms beeps;
  the ear has the last word.
- **Shrillness.** An octave up, the TTMF's top tone is near 5.7 kHz;
  hearing comfort caps its level.

## Open Questions

1. **Dependencies:** the standard library only, or numpy? *Recommended:*
   the standard library; the benchmark above shows it is fast enough.
2. **Formats:** 48 kHz, 16-bit mono WAV for effects; Ogg Vorbis through
   `ffmpeg` for music? *Recommended:* yes.
3. **Loudness:** −16 LUFS and −1 dBTP for every clip, with the mix in
   `volumeRange`? *Recommended:* yes.
4. **Committed audio:** commit the WAVs within a 5 MB budget?
   *Recommended:* yes, as with the textures.
5. **Layout:** `Sounds/<vanilla category>/BT_<Thing>/`, retiring the
   `Sounds/RandomReplicator/` placeholder? *Recommended:* yes.
6. **The theremin rule:** reserve the theremin for batshittium, as
   magenta is? *Recommended:* yes.
7. **DTMF steps:** 20 pre-rendered clips, or one pitched at run time?
   *Recommended:* pre-rendered. Whether the pitch climbs per beep or
   glides remains the Colony Bomb's open question 2.
8. **Game speed in the countdown:** force normal speed for its 15 s, as
   Anomaly's monolith does (`TimeSlower.SignalForceNormalSpeed`)?
   *Recommended:* yes, to be settled in phase 6000.
9. **Showcase sounds:** vanilla clicks in the windows; projector, chime
   and run-out in the tour? *Recommended:* yes.
10. **Mirror Mirror's sting:** synthesized and radiophonic, commissioned
    only if rejected? *Recommended:* yes.
11. **The shared pull:** give `BT_Implosion_Pull` to the Tractor Bomb and
    the grenade, replacing `PsychicPulseGlobal`, once phase 3000 merges?
    *Recommended:* yes, as upkeep.
12. **Licence:** generated audio under the mod's MIT terms, and any
    future sample CC0 only, credited in `Sounds/CREDITS.txt`?
    *Recommended:* yes.
13. **The disco track:** built in phase 8000, with only the Ogg path
    proved here? *Recommended:* yes.

## References

- ITU-T Q.23 (DTMF frequencies; senders within 1.8%) and Q.24
  (receivers accept 1.5%, reject beyond 3.5%; twist −4 to +8 dB).
- ITU-R BS.1770-4 and EBU R 128 (loudness and true peak; −23 LUFS for
  broadcast); Sony's ASWG-R001 applies them to games.
- Python documentation for `wave` and `random` (reproducibility).
- The 1.6 decompile: `ModContentLoader`, `ContentFinder`,
  `AudioGrain_Clip`, `AudioGrain_Folder`, `SoundDef`, `SubSoundDef`,
  `Sample`, `SoundStarter`, `DefOfHelper`, `TimeSlower`, and
  `RuntimeAudioClipLoader.Manager`.
