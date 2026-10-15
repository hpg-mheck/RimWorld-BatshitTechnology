# Sound Sources: Options for Music and Effects

Status: research for Phase 5000, slice 1000 (2026-10-15); a decision is
pending Matt "Breakpoint" Heck (`docs/AI-requests-for-humans.txt`, item
25). Companion to `docs/SOUND-PIPELINE.md`, whose "fully synthesized"
goal this document reopens.

## The Question

On 2026-10-15 the operator asked for options: "We have both sound effect
and music needs. For the music, since we're not doing dynamic
composition during actual gameplay, I'm open to either real-time
synthesis, or, and probably preferable, AI-guided composition resulting
in high-quality output. I'm open to using a service for this. ... Budget,
though, is $50 across the whole thing, sound effects and music, if
possible." The $50 is for services; Claude's own usage is not counted.

Four read-only research passes covered hosted music services, effect
services and libraries, local models with composed music, and the
licensing, platform and community rules. Their full notes, with about
200 dated sources, are in `docs/research/sound-sources-2026-10/`. The
facts that decide the choice were rechecked against the vendors' own
pages on 2026-10-15 (see "Sources").

## What the Mod Needs

- **Music:** the Drag Show's disco loop (60 to 90 s, 120 BPM, theremin
  hook; phase 8000) and Mirror Mirror's 2.5 s orchestral sting (phase
  7000). Possible extras: a 1950s educational-film theme for the
  showcase tour, and a lounge loop and rimshot for the Stand-Up Comedy
  showcase (phase 10000).
- **Effects:** about 20 now, 20 to 40 later. Signal tones (DTMF steps,
  beeps, the filmstrip chime, alarms) must be exact and stay synthesized
  in code in every option. The organic ones are where synthesis sounds
  cheap: a projector starting, whirring and running out, wind, a deep
  boom, a raspberry, the implosion's rumble, a glass shimmer, and later
  a keypad, a locker hinge and slam, and a blimp's engine.

## The Rules That Decide It

1. **Permission, not ownership.** Purely AI-generated audio has no
   copyright in the US or the EU, so nobody can license it; what counts
   is that the source's contract lets us redistribute the files publicly,
   worldwide and for ever, after any subscription ends.
2. **Nothing non-commercial.** A free mod is not a non-commercial chain:
   Ludeon's EULA takes a licence to every mod "for any purpose", Valve's
   Subscriber Agreement takes one for promotion, and players monetize
   their videos. That rules out every free or "personal" tier and every
   model whose weights are licensed non-commercially.
3. **No "royalty-free" library files.** Sonniss, Pixabay, Zapsplat,
   Mixkit, 99Sounds and Soundly all forbid redistributing the raw files,
   and a mod ships loose files in `Sounds/` and in a public repository.
   CC0 is the only library licence that clearly allows it.
4. **Nothing in YouTube Content ID.** Stock music that its authors
   register (AudioJungle, Pond5, Pixabay Music) or that only the
   subscriber may use (Epidemic Sound, Artlist) would get players'
   videos claimed. We never register our own tracks.
5. **The theremin is ours.** No service reliably renders a theremin
   lead, and the motif fixes its vibrato (5.5 Hz, ±30 cents); in every
   option it is synthesized and laid over the rest.

## The Options

### Option A: All Local, $0

- **Effects:** code synthesis, CC0 recordings (Freesound's CC0 filter,
  Kenney, the Versilian Community Sample Library), and **Stable Audio 3
  Small SFX**, Stability's open model of May 2026, run on this machine
  (0.6B parameters; runs on the CPU, faster on the GPU). Freesound's CC0
  filter alone found 58 film-projector takes (one, a 48 kHz recording,
  holds the start, whir and run-out), 46 lockers, 279 wind loops and
  1,001 explosions.
- **Music:** composed. Claude writes each piece as a Python script that
  emits MIDI on an explicit bar grid; Surge XT (free synthesizer) plays
  the disco bass, string machine and theremin; free sampled orchestras
  (Virtual Playing Orchestra, Sonatina, VSCO 2 Community Edition) play
  the stabs, brass and timpani; a tape plug-in (ChowTape) and the
  pipeline's band-limit give the 1950s finish, all run headless from
  Python by Spotify's `pedalboard`. For the light-orchestra theme,
  **ACE-Step 1.5** (MIT licence, under 8 GB, takes tempo and key) and
  **Stable Audio 3 Medium** (2B, about 5 to 6.5 GB) generate
  candidates, and ACE-Step's "cover" mode can restyle a composed
  mock-up.
- **Quality:** effects good. Composed disco "a competent 1978
  library-record pastiche" (space-disco is synthesizer music, so that
  idiom suits synthesis); the sting good; the orchestral theme medium
  to good; the generators may beat it there.
- **Costs and friction:** $0. A Python venv with PyTorch for CUDA 12.8
  (the RTX 5060 Ti needs it), the instruments and models: tens of
  gigabytes by estimate, against 183 GB free.
  Stable Audio 3 is gated on Hugging Face: the operator accepts its
  terms with his own account (name, email, country) and registers for
  the free commercial licence (under $1M a year in revenue). Stable
  Audio 3 Medium wants FlashAttention, fragile on Windows; ComfyUI or
  WSL2 are the fall-backs.

### Option B: Local First, Then About $20 of Hosted Music (Recommended)

Option A, then a small hosted spend only on the pieces the local
candidates lose in a blind listening trial:

- **Google Lyria 3.5 through the Gemini API:** $0.08 a song (a Lyria 3
  Clip, 30 s, is $0.04), so $10 buys about 125 songs. 44.1 kHz stereo,
  WAV. Google "won't claim ownership" of what it generates, and every
  file carries an inaudible SynthID watermark. Scriptable, so agents can
  run the prompts. Reported weaknesses: it adds vocals unasked, follows
  prompts loosely and leans to "Muzak", which suits library music.
- **Stable Audio through the Stability API:** about $10 for 1,000
  credits (roughly 38 to 50 generations; the per-generation price is
  from a secondary source). Exact lengths from 1 s, WAV, inpainting
  (useful for a loop's seam), the same Community License as the open
  models, and training on licensed library music. Best hosted source for
  the sting; stiffer for disco.
- **Spend:** at most $20, leaving $30 in reserve. Agents would use API
  keys the operator creates, kept under `.local/` and never committed.

### Option C: Suno Pro for the Disco, $10

Suno v6 (September 2026) has the best groove and ranks first on the
October 2026 instrumental leaderboard reported by the research. One
month of Pro is $10 for 20 downloads; commercial rights in songs
downloaded while subscribed survive cancelling. The catches: no API, so
the operator drives it by hand; it cannot practically make a 2.5 s
sting; its terms (3 September 2026) say outputs "may not be unique across
users" and that "applicable rights holders may also have the right to
collect revenue related to distribution of Outputs on third party
platforms", which is a Content ID worry for players; and Universal and
Sony's suit against it was still pending in May 2026. A fallback for
the disco only, if Options A and B fall short.

### Not Recommended

- **ElevenLabs:** its Sound Effects terms (9 October 2026) forbid making
  output "available as isolated audio files or samples, or as part of
  any sound, sample, effects, or music library or other collection of
  sounds", which a mod's `Sounds/` folder is; its Starter music is MP3 at
  128 kbps with a similar library clause. Usable only with its written
  permission.
- **Udio:** downloads disabled since its label settlement (October
  2025); outputs personal and non-commercial.
- **AIVA** below its €49 Pro plan, **Soundraw, Beatoven, Mubert and
  Loudly:** the vendor keeps ownership or the licence cannot pass to
  players.
- **Open models with non-commercial weights:** Meta's MusicGen and
  AudioGen, MMAudio, AudioLDM 2, Tango 2 and others; Tencent's
  HunyuanVideo-Foley licence excludes the EU, UK and South Korea.
- **Adobe Firefly** effects are "safe for commercial use" but their
  product terms were not readable; a candidate only after reading them.

## Comparison

| | A: All local | B: Local, then hosted | C: Suno Pro |
|---|---|---|---|
| Cost | $0 | $0 to $20 | $10 (plus A for the rest) |
| Effects | Synthesis, CC0, SA3 | Same as A | Same as A |
| Disco | Composed | Composed or Lyria bed | Suno bed |
| Sting | Composed or SA3 | Stability API if needed | Not suitable |
| Theme | ACE-Step, SA3, composed | Lyria if needed | Suno bed |
| Agents can drive it | Yes | Yes | No (by hand) |
| Licence for shipping | Clean | Clean | Clean, with caveats |
| Content ID risk | Lowest | Low | Highest |
| Operator's steps | HF gate | HF gate, API keys | Suno account |

## Consequences for the Pipeline

Whichever option is chosen:

- **SOUND-PIPELINE's goals change.** "No samples from anywhere" and the
  motif's rule 8 ("No samples") become "original or CC0, every source
  recorded; no quotation of any film, show or record".
- **Two kinds of asset.** Code-synthesized sounds keep the determinism
  test (a fresh render equals the committed file). Generated or
  recorded material becomes a committed source asset under
  `Tools/sound_sources/`, with a manifest giving its origin, licence,
  model and version, seed, prompt and SHA-256; the generator treats,
  layers, trims and normalizes it deterministically from there. AI
  output cannot be re-rendered bit for bit across GPUs.
- **The audio's licence is CC0 1.0,** separate from the code's MIT,
  replacing open question 12's "MIT". CC0 holds whether or not copyright
  exists in a given country, frees players to use the music anywhere,
  and makes a third party's Content ID registration a violation of
  YouTube's own rules. `Sounds/CREDITS.txt` is generated from the
  manifest.
- **Disclosure.** One plain sentence on the Workshop page and in the
  README says which sounds were generated by which tools. A Workshop
  collection already lists 705 mods by AI use, and r/RimWorld's rule 8
  (seen only in a search snippet) restricts AI mods' release posts that
  carry a donation link; the
  research found disclosed AI audio in RimWorld mods drawing thanks,
  not attacks, while concealment is punished hardest.
- **Protecting players.** The masters are published early in a dated
  GitHub release with their hashes, never registered with Content ID,
  and the README states the audio is CC0 and safe for monetized videos.
  Before shipping any music, the operator uploads an unlisted test video
  with it and watches for a Content ID match.
- **One personal touch.** The raspberry is the effect synthesis does
  worst; three raspberries recorded on the operator's phone would be
  original, his, and funnier than any library take.

## Recommendation and Next Slice

**Option B, staged.** Slice 2000 is a listening trial on this machine,
about two hours of agent time: the three pieces (disco, sting, theme)
composed as code and rendered two or three ways; ACE-Step 1.5 and, if
the gate is accepted, Stable Audio 3 on the same briefs; and eight effect
prompts on Stable Audio 3 Small SFX beside the best CC0 takes. Every
candidate is loudness-matched and played in a blind reel, once at full
range and once through a laptop-speaker filter, and the operator marks
each keep, maybe or reject. Money is spent only where the free
candidates lose, Lyria and the Stability API first, Suno for the disco
last. Then the pipeline's build slices are planned around the winners.

## Sources

Rechecked on 2026-10-15:

- Gemini API pricing (Lyria 3.5, Lyria 3 Clip and Pro):
  https://ai.google.dev/gemini-api/docs/pricing
- Gemini API Additional Terms (ownership; effective 23 March 2026):
  https://ai.google.dev/gemini-api/terms
- Stable Audio 3 models: https://huggingface.co/stabilityai and
  https://huggingface.co/stabilityai/stable-audio-3-small-sfx
- Stability AI Community License (5 July 2024):
  https://stability.ai/community-license-agreement
- ACE-Step 1.5 (MIT): https://github.com/ace-step/ACE-Step-1.5
- ElevenLabs Sound Effects terms (9 October 2026):
  https://elevenlabs.io/sound-effects-terms

The full research notes in `docs/research/sound-sources-2026-10/` list
the rest, including Suno's terms, the label settlements, the Copyright
Office's 2025 report, Valve's and Ludeon's terms, Freesound's licences
and the instrument libraries' licences.
