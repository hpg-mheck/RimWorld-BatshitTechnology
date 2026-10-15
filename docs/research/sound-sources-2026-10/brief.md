# Phase 5000, Slice 1000: Sound and Music Options (Research Brief)

Date: 2026-10-15. Read-only desk research. Every agent reads this brief.

## The Project

Batshit Technology is a free RimWorld 1.6 mod (Steam Workshop and GitHub,
code under MIT), by one developer, built largely by Claude agents. It
has no sounds of its own yet. `docs/SOUND-PIPELINE.md` (read it) planned
to synthesize everything in pure Python from code; the operator has now
opened the door to services and AI:

> "We have both sound effect and music needs. For the music, since we're
> not doing dynamic composition during actual gameplay, I'm open to
> either real-time synthesis, or, and probably preferable, AI-guided
> composition resulting in high-quality output. I'm open to using a
> service for this. ... Budget, though, is $50 across the whole thing,
> sound effects and music, if possible."

The $50 is for services, subscriptions and purchases only (not Claude's
own usage). All files are rendered offline and shipped with the mod; no
generation happens during play.

## The Look and Sound (the Motif)

`docs/ARTISTIC-MOTIF.md` and SOUND-PIPELINE's "The Audio Motif": a
cheerful 1950s public-information film narrating a catastrophe with
perfect composure. Telephone-company signal tones (exact DTMF), a
theremin for the glowing batshittium, vibraphone and celesta for
comfort, the narrator's projector and filmstrip chime, a muted trombone
or raspberry as the one punchline. Vintage, band-limited, a little tape
hiss; never broken-sounding. Original only: no quotation of any film,
show or record.

## What Is Needed

Music (offline, shipped as Ogg Vorbis):
- **Drag Show disco loop** (phase 8000): about 120 BPM, four on the
  floor, octave-bouncing bass, off-beat open hi-hats, string stabs, a
  theremin hook; a 60 to 90 s seamless stereo loop.
- **Mirror Mirror sting** (phase 7000): about 2.5 s, a 1960s orchestral
  sci-fi sting with ominous gravitas (brass stab, string tremolo,
  timpani), original, evoking but not quoting a 1967 TV episode.
- **Possible extras:** a short tour theme or bed for the showcase (1950s
  educational-film library music: light orchestra, vibraphone, xylophone);
  a lounge loop and rimshot for a stand-up comedy feature.

Sound effects (48 kHz mono WAV, mostly under 3 s):
- Signal tones that must be exact and testable (DTMF steps, beeps, the
  filmstrip chime): code synthesis is already planned for these.
- Organic or complex sounds where synthesis may sound cheap: a 1950s
  film projector starting, whirring and running out; wind (a 3 s loop);
  a deep boom; a comic raspberry; an implosion's rumble with a rising
  whistle (3 s loop); a glass-harmonica shimmer (loop); later a keypad,
  a locker hinge and slam, alarms, a blimp's engine.
- Roughly 20 effects now and 20 to 40 more over later phases.

## Constraints

- **Budget:** $50 total for services, both music and effects. Prefer one
  month of one subscription, pay-as-you-go credits, or free tiers.
- **Redistribution:** files ship inside a free mod on Steam Workshop and
  GitHub. The licence must allow redistributing the audio files
  themselves inside the mod, worldwide, forever, after any subscription
  ends. Free tiers that are non-commercial or require attribution need
  close reading: is a free Workshop mod "commercial"? Can a credits file
  satisfy attribution?
- **Content ID:** players stream and post RimWorld videos. Music that a
  service or a third party has registered with YouTube Content ID (or
  that could be) would get players' videos claimed.
- **Formats:** RimWorld 1.6 decodes WAV, MP3 and Ogg Vorbis.
- **Hardware for local tools:** Windows 11, NVIDIA RTX 5060 Ti with 8 GB
  (Blackwell, needs CUDA 12.8 or later builds of PyTorch), i7-11700,
  128 GB RAM, 183 GB free disk, ffmpeg installed, Python 3.12 without
  numpy or torch (installing them into a venv is acceptable).

## Rules for Research Agents

- Web research and reading only. Do not sign up, log in, buy, download
  models, or install anything. Never enter or send the operator's email
  or any personal detail anywhere.
- Prices, plans and terms change: give the figure, the date of the page
  you read, and the URL. Say "unverified" where you could not find a
  primary source. Prefer the vendor's own pricing and terms pages over
  blogs; use reviews and forums for quality and community signal.
- Quality matters most after licence: say what each option is good and
  bad at for THIS motif (vintage library music, disco, orchestral stings,
  foley-like effects), citing listening reports or demos where you can.
- Write your report to the file named in your task, in Markdown, wrapped
  at about 78 columns, and return a short summary (top picks and the
  three most important facts) as your final answer.
