# The $0 Routes: Local Models and Composed Music

Phase 5000, Slice 1000 research. Read-only desk research, 2026-10-15.
Topic: what the operator's own machine can make for nothing, as of
October 2026: (A) open-weight generative audio models run locally, and
(B) music that Claude writes as notation or code, rendered through good
free instruments; then (C) a hybrid recommendation and a listening trial.

The machine: Windows 11, RTX 5060 Ti 8 GB (Blackwell, sm_120), i7-11700,
128 GB RAM, 183 GB free, ffmpeg, Python 3.12 without numpy or torch.

Nothing was downloaded, installed or signed up for. Figures marked
*(secondary)* come from blogs or search summaries, not the vendor;
*(unverified)* means no primary source was found. All pages were read on
2026-10-15.

## Summary

**Top picks.**

1. **Effects (organic):** Stability's **Stable Audio 3** open models
   (May 2026): *Small-SFX* runs on the CPU, *Medium* peaks at about
   6.5 GB of VRAM. They were trained on licensed and Creative Commons
   audio, you own the outputs, and commercial use is free under
   US$1 million a year in revenue. **Stable Audio Open 1.0** (2024; via
   Diffusers, about 6 GB) is the same licence and a proven fallback. Mix
   in CC0 recordings from the **Versilian Community Sample Library**
   (ratchet, slapstick, flexatone, wine glasses, siren, anvil).
2. **Disco loop and sting:** composed, not generated. Claude writes the
   notes as a Python script (MIDI); **Surge XT** plays the bass, string
   machine and theremin, sampled CC0 or near-free orchestras (**VSCO 2
   CE**, **Sonatina**, **Virtual Playing Orchestra**) play the stabs,
   brass and timpani, and **ChowTape** plus band-limiting supply the
   period finish. The whole chain is headless Python (Spotify's
   **pedalboard** hosts the VST3 plug-ins) and every instrument licence
   allows shipping the rendered audio.
3. **Light-orchestra bed:** a contest between **MuseScore 4 with Muse
   Sounds** (the best free orchestral playback, scriptable from the
   command line, but the free sounds' exact licence text was not found)
   and the generators: **ACE-Step 1.5** (MIT licence; the developers
   state outputs may be used commercially; runs on 8 GB) and **Stable
   Audio 3 Medium**, which was trained largely on AudioSparx stock music
   and so knows library music.
4. **Hybrid worth trying:** render Claude's arrangement as a plain mock-up,
   then have ACE-Step 1.5 *cover* or *repaint* it, or Stable Audio 3
   *inpaint* the loop seam. Composition and the theremin rule stay under
   control; the model adds realism. Nobody has reported results for this
   motif, so it belongs in the trial.

**The facts that matter most.**

- **Most famous open audio models are non-commercial.** MusicGen,
  AudioGen, MAGNeT and JASCO (Meta), MMAudio, Sony's Woosh, AudioLDM 2,
  Tango 2, TangoFlux and YuE2 carry CC BY-NC (or NC-SA, or "research
  only") weights. Meta's maintainer said the MusicGen rights "were
  negociated for a research purpose" and cannot be relicensed. A free
  Workshop mod is arguably non-commercial, but players monetize videos
  of it, and NC files cannot ride inside an MIT repository cleanly.
  **Do not ship their outputs.**
- **Stable Audio 3 changed the picture in May 2026,** but with
  friction. The models are gated on Hugging Face, so the operator must
  accept the terms himself. They bundle Google's T5Gemma encoder under
  the Gemma Terms of Use. Medium needs FlashAttention 2, which is
  fragile to build for sm_120 on Windows; ComfyUI or WSL2 are the
  practical routes.
- **The Tencent Hunyuan licence excludes the EU, UK and South Korea.**
  That rules out HunyuanVideo-Foley for a worldwide mod. Tencent's
  SongGeneration uses its own custom terms, and needs 10 GB or more of
  VRAM anyway.
- **The composed route is legally the cleanest.** VSCO 2 CE and VCSL are
  CC0. Sonatina (CC Sampling Plus 1.0) explicitly allows commercial
  transformation into new works. Virtual Playing Orchestra's author
  "enforce[s] no restrictions on using this library to make music."
  GeneralUser GS allows commercial music, and FluidR3 and MuseScore
  General are MIT. Spitfire LABS recordings are royalty-free, but its
  sounds may not be used "to create another sample library, or
  something that can be construed as such", which makes single-note
  effects a grey area. LABS is now account-gated and moving to Splice
  INSTRUMENT.
- **Determinism.** The pipeline's "fresh render equals committed file"
  test holds for composed renders made with pinned tools. It does not
  hold for diffusion models across GPUs and driver versions. Treat
  AI-made audio as source assets, like CC0 samples, recorded in a
  manifest with model, version, seed and prompt.

## The Machine and Its Software Stack

- **PyTorch on Blackwell.** sm_120 needs PyTorch 2.7.0 or later built
  for CUDA 12.8. Windows `cu128` wheels exist on `download.pytorch.org`
  for current releases, for example 2.11.0 *(secondary: install
  instructions quoted in a search summary)*. Any model that pins an
  older torch will not run on this card: AudioCraft pins
  `torch==2.1.0`.
- **FlashAttention 2 on Windows and sm_120** is the main trap. A
  Dao-AILab issue reports nvcc crashing when building for arch 120;
  community Windows wheels exist for some torch and CUDA pairs *(all
  secondary)*. Stable Audio 3 Medium **requires** FlashAttention 2 in
  its own repository. ACE-Step merged Blackwell detection to avoid vLLM
  hangs (PR #825, March 2026).
- **ComfyUI** is the least painful single host. The maintainers
  recommend the current Windows portable build for RTX 50 cards (CUDA
  12.8 PyTorch), and it has native nodes for ACE-Step 1.5, Stable Audio
  Open and Stable Audio 3. It runs as a local server, so an agent can
  queue workflows over HTTP, headless. Whether its Stable Audio 3 nodes
  avoid the FlashAttention requirement is *(unverified)*.
- **JAX** (Magenta RealTime) has no native CUDA support on Windows;
  plan on WSL2 *(general knowledge, not re-checked)*.
- **8 GB is enough** for everything recommended here. 128 GB of system
  RAM makes CPU offloading painless, at some cost in speed.

## Part A: Open-Weight Generative Models

### Reading the Licences

- **CC BY-NC 4.0** defines NonCommercial as "not primarily intended
  for or directed towards commercial advantage or monetary
  compensation." A free mod with no donations fits that reading, but
  three things still argue against shipping NC-model output:
  - Whether a weights licence binds outputs at all is unsettled. A
    community thread argues it does not; the maintainers never said so,
    and Meta's maintainer declined commercial licensing because "the
    rights were negociated for a research purpose."
  - Players' monetized streams and videos would carry the audio, and an
    NC audio file inside a mod is a trap for them.
  - The mod's code is MIT; NC assets would need a separate,
    incompatible licence line in `Sounds/CREDITS.txt`.
- **Stability AI Community License.** "You own any outputs generated
  from the Models or Derivative Works." Commercial use is free for
  anyone under US$1M in annual revenue, with registration "if You are
  using or distributing the Stability AI Materials for a Commercial
  Purpose." The "Powered by Stability AI" notice is triggered by
  distributing the Materials (weights, software) or Derivative Works
  (modified or fine-tuned models), not plain outputs. A line in the
  credits costs nothing and removes doubt. There is one restriction on
  outputs: they may not be used to build or improve another foundation
  model, which is irrelevant here. The Acceptable Use Policy applies.
- **Gemma Terms of Use** ride along with Stable Audio 3, because of its
  T5Gemma encoder. Google's usual position is that it claims no rights
  in outputs; the restrictions are on prohibited uses.
- **Tencent Hunyuan Community License:** "DOES NOT APPLY IN THE
  EUROPEAN UNION, UNITED KINGDOM AND SOUTH KOREA." Tencent claims no
  rights in outputs, but a worldwide mod should not rely on a model not
  licensed in three of its markets.
- **Copyright in outputs.** Purely machine-made audio may not be
  protected by copyright in the US. Two consequences: the MIT notice
  may not bind it (harmless), and nobody can validly register it with
  Content ID either (though bad actors sometimes try).

### The Models, One by One

VRAM is peak during generation unless noted.

**Stable Audio 3 (Stability AI, released 2026-05-20).** The new
default for effects and a serious music contender.
- *Variants:* Small-Music and Small-SFX (paper: 459M parameters; the
  Small-SFX card says 0.6B) run on a CPU and generate up to 2 minutes.
  Medium (1.4B) needs a GPU and generates up to 6 min 20 s. Large
  (2.7B) is API-only.
- *Licence:* the Community License above. Gated on Hugging Face (accept
  the terms; the operator must do this). Stability's announcement:
  "you own your outputs and can distribute and commercialize them."
- *Training data:* 1,278,902 recordings: 806,284 licensed from
  AudioSparx and 472,618 from Freesound (CC0, CC BY, CC Sampling+), with
  copyrighted material screened out (paper and model card).
- *VRAM* (Stability's own repository): Small 1.69 to 2.40 GB; Medium
  5.07 to 6.52 GB, or about 5.14 GB with chunked decoding. **Fits
  8 GB.** The repository installs CUDA 12.6 torch by default (install
  cu128 by hand for Blackwell) and needs FlashAttention 2 for Medium.
- *Output:* 44.1 kHz stereo, variable length (no padding, so a 3 s
  effect costs 3 s of compute). Inpainting, multi-segment editing and
  continuation are supported, useful for loop seams.
- *Quality:* the paper claims the best scores among open models for
  instrumental music (over ACE-Step 1.5 and DiffRhythm 2) and for
  effects (over Woosh, TangoFlux and Stable Audio Open). These are the
  developers' own benchmarks. Dubspot's review (2026-05-26): it "excels
  with ambient drones, foley, sound effects, and instrumental loops";
  "rhythmic genres flatten out"; "the music can feel correct without
  feeling interesting"; there is little variety between seeds; and it
  has no vocals at all, which is irrelevant here. The AudioSparx
  training data is stock and library music, a good omen for the 1950s
  bed. No listening reports were found for disco or vintage cues.

**Stable Audio Open 1.0 (2024) and Open Small (May 2025).**
- *Licence:* the Community License; both gated.
- *Training data:* 486,492 Freesound and Free Music Archive recordings
  (CC0, CC BY, CC Sampling+).
- *Output:* 1.0 makes up to 47 s and Small up to 11 s, at 44.1 kHz
  stereo.
- *VRAM:* 1.0 peaks at about 5.9 GB in a benchmarked "balanced" preset
  *(secondary: audiyo)*. Small (0.5B) runs on a CPU.
- *Quality:* the card says it is better at sound effects than music,
  "not able to generate realistic vocals", and "does not perform
  equally well for all music styles."
- *Use:* runs through Hugging Face Diffusers on current torch, so it
  avoids version pins. Good for foley, wind and booms, and superseded
  by Stable Audio 3 for most purposes.

**Foundation-1 (RoyalCities, 2026-03-17).** A fine-tune of Stable Audio
Open for producers' loops.
- *Licence:* the Community License, inherited from its base.
- *Control:* tempo (100 to 150 BPM, including 120), 4 or 8 bars that
  loop exactly, key and mode, and 10 or more instrument families
  (synth, keys, bass, bowed strings, mallet, brass and more) with
  timbre and effects tags. Prompt pattern: `Bass, Synth, Warm, Analog,
  120 BPM, 8 Bars, A minor`.
- *VRAM:* about 7 GB *(secondary)*. Tight but feasible on 8 GB.
- *Use:* directly relevant to the disco loop. It can make a 120 BPM
  string-stab or bass layer that loops by construction. There are no
  listening reports for this motif.

**ACE-Step 1.5 (StepFun and ACE Studio; tech report 2026-01-31; XL
4B released 2026-04-02).** The best-licensed music model for this
machine.
- *Licence:* MIT, and not gated. The card says: "You can strictly use
  the generated music for commercial purposes." It also urges users to
  verify originality and disclose AI involvement.
- *Training data:* "licensed professional music tracks", "royalty-free
  and public domain music", and "synthetic audio from MIDI-to-Audio
  conversion." It is presented as a legally compliant dataset; there
  is no warranty.
- *VRAM* (the developers' table): 6 GB or less runs the 2B turbo model
  with INT8 and CPU offload and no language model; 6 to 8 GB runs the
  2B turbo with the 0.6B language-model planner on the PyTorch backend.
  XL needs 12 GB or more even with offloading, so **not XL here**.
- *Windows:* a portable package with `.bat` launchers. Blackwell fixes
  are merged; RTX 50 users report setup snags with CUDA libraries
  (issues #1078, #1079, both closed).
- *Control:* `bpm` (30 to 300), `keyscale`, `timesignature`,
  `duration` (10 to 600 s), an `instrumental` flag, seeds, and
  deterministic ODE sampling. Task types: text2music, cover (restyle
  existing audio while keeping its structure), repaint (regenerate a
  time span), and, on the base model only, extract, lego (add one
  instrument in context) and complete.
- *Quality:* the developers report a SongEval score of 8.12 against
  Suno v4.5's 7.85 for XL, measured on songs. Reports of instrumental
  quality are sparse, and none were found for disco, vintage library
  music or stings.
- *Limit:* the 10 s minimum means a 2.5 s sting has to be cut from a
  longer take.

**MOSS-SoundEffect (OpenMOSS, Fudan University).**
- *Licence:* Apache 2.0 on the Hugging Face card.
- *Size:* the card describes an 8B autoregressive model, too big for
  8 GB without quantization; a community GGUF build exists. A secondary
  article describes a v2.0 (2026-05-26) as a 1.3B flow-matching model
  with 48 kHz stereo output and up to 30 s per take *(secondary;
  reconcile against the card before use)*.
- *Use:* the most permissively licensed dedicated effects model found.
  Worth a trial if the v2.0 details hold.

**Magenta RealTime and RealTime 2 (Google, 2025; RT2 June 2026).**
- *Licence:* code Apache 2.0, weights CC BY 4.0, which allows
  commercial use with attribution. On outputs, v1's card says: "Google
  claims no rights in outputs you generate."
- *Output:* 48 kHz stereo instrumental, chiefly Western styles. RT2 adds
  MIDI conditioning (harmony) at the frame level.
- *Platform:* real-time use targets Apple Silicon. Offline use works on
  NVIDIA through JAX, so WSL2 on this machine.
- *Use:* Claude's chords could steer an audio model, but expect
  setup friction. A curiosity, not a first choice.

**Song models built around lyrics.** These are mostly a poor fit: they
are built for vocals and need more VRAM.
- *HeartMuLa 3B* (Apache 2.0): about 6.2 GB with `--lazy_load`
  *(secondary)*; 48 kHz output. Strong on lyrics, not instrumentals.
- *DiffRhythm 1.2* (Apache 2.0, code and weights): 8 GB minimum with
  `--chunked`; Windows `.bat` scripts; makes pure music from a prompt;
  about 95 s per take (285 s for the full model). DiffRhythm 2 exists
  as a research release. The provenance of its training data was not
  documented in what was read.
- *YuE v1 7B* (Apache 2.0): the authors encourage commercial use of
  outputs, and "YuE by HKUST/M-A-P" credit is recommended. It needs
  16 to 24 GB; a quantized UI claims 8 GB *(secondary)*.
  *YuE2-3B* (2026-09-09): **CC BY-NC 4.0** and 24 GB. It writes an
  editable ABC melody and chord plan before rendering, an interesting
  idea, but it is NC and too big.
- *SongBloom* (Tencent): the licence is in conflict. A mirror's card
  says Apache 2.0, while others report non-commercial weights. It is
  lyrics-driven and wants bf16 on a 4090-class card. Skip.
- *SongGeneration 2 / LeVo 2* (Tencent, 2026-03-01): custom Tencent
  terms ("NOASSERTION" on GitHub); 10 GB minimum (16 GB with a prompt);
  a `--bgm` mode for instrumentals. Does not fit, and the licence needs
  reading.
- *MiniMax Music 3* (August 2026): 57.4 GB of weights; 8 GB only with
  group offloading. Its licence is reported both as CC BY-NC 4.0 and as
  a community licence with UI attribution and a US$20M revenue cap.
  Skip.

**Non-commercial or territorial models: do not ship their output.**
- *MusicGen, AudioGen, MAGNeT, JASCO* (Meta AudioCraft): code MIT,
  weights CC BY-NC 4.0. MusicGen was trained on 20K hours of licensed
  music (an internal set, ShutterStock and Pond5); it makes 32 kHz
  clips of up to 30 s each, and reviewers found it "decent at making
  simple, short melodies" but repetitive. AudioGen is 16 kHz.
  AudioCraft pins torch 2.1.0, so only the Hugging Face Transformers
  port would run on Blackwell. Useful only as a private reference.
- *MMAudio:* weights CC BY-NC 4.0; about 6 GB; strong video-to-audio
  foley, and text-to-audio also works. Tested on Ubuntu only.
- *Woosh* (Sony AI, April 2026): weights CC BY-NC; a dedicated effects
  model.
- *AudioLDM 2:* CC BY-NC-SA 4.0, 16 kHz. *Tango 2:* CC BY-NC-SA 4.0,
  16 kHz. *TangoFlux:* "non-commercial research use only."
- *HunyuanVideo-Foley:* the Tencent Hunyuan licence (excludes the EU,
  UK and South Korea); its XL model fits 8 GB only with offloading, and
  it is conditioned on video.

### Fit to the Motif, Asset by Asset (Generators Only)

- **Projector start, whir and run-out; wind; boom; implosion rumble:**
  this is what Stable Audio 3 Small-SFX and Medium, and Stable Audio
  Open, do best. Generate 3 s takes, downmix to mono, resample to
  48 kHz with ffmpeg, then apply the pipeline's band-limit and loudness
  steps. Make loops with inpainting: rotate the clip so the seam falls
  in the middle, then inpaint across it.
- **Raspberry:** generators are unreliable for comic mouth sounds.
  Synthesis (as specified) or a recording is safer.
- **Glass-harmonica shimmer:** the CC0 VCSL "Wine Glasses" samples, or
  synthesis, beat a generator here; the motif wants exact partials.
- **Disco loop:** ACE-Step 1.5 with `bpm=120` and the instrumental flag,
  or Foundation-1 for 8-bar layers, can make convincing material. But
  the theremin rule (it means batshittium, and nothing else), the exact
  loop seam and the "original only" rule all argue for composition,
  with generators supplying layers at most.
- **The 1960s sting:** a generator will happily produce "brass stab,
  string tremolo, timpani", but 2.5 s with ominous gravitas and no
  resemblance to the 1967 cue is a precision job. Generate many takes,
  keep the best, or compose it.
- **The 1950s light-orchestra bed:** the generators' strongest music
  case, given training data dominated by stock music. It is still worth
  comparing against Muse Sounds.

## Part B: AI-Guided Symbolic Composition

### How Claude Writes Music

- **Formats.** MIDI written with `mido` or `pretty_midi` (the most
  direct); `music21` to MusicXML for MuseScore; ABC notation through
  `abc2midi`; or synthesis code (SuperCollider, Csound, Faust, or the
  project's own Python).
- **Give the agent a grid and a critic.** *Libretto* (arXiv 2606.22708,
  June 2026) gave a Claude Code agent an explicit bar and onset-slot
  text grammar, plus statistical checks against a reference corpus.
  Full-piece pass rates rose from 62% to 94%, and gap-filling from 12%
  to 39%. The failures it fixed are the ones to expect: degenerate
  density, unstable harmony, weak genre adherence, atypical rhythm, and
  copying. Practical lessons:
  - write music as a bar grid in code (pattern tables), not as raw
    event lists;
  - check it automatically (density, range, voice leading, loop-seam
    continuity, loudness);
  - keep the operator's ear as the final judge, since Claude cannot
    hear. It can inspect spectrograms and the pipeline's metrics.
- **Originality.** Composing note by note makes "no quotation" checkable
  in a way generation is not. The 1967 sting's intervals can be
  avoided deliberately.

### Renderers and Instruments

**FluidSynth with SoundFonts.** The fastest baseline.
- FluidSynth (LGPL) has Windows binaries on GitHub and renders files
  from the command line (`fluidsynth -ni -F out.wav -r 48000 font.sf2
  song.mid`), deterministically for the same version.
- Fonts: *GeneralUser GS* v2.0.3 (30.7 MB; 261 presets and 13 drum
  kits). Its licence: "You may use GeneralUser GS without restriction
  for your own music creation, private or commercial." *FluidR3_GM* and
  *MuseScore General* are MIT.
- **Quality ceiling:** a General MIDI demo. Mallets, harp, pizzicato and
  timpani are passable; brass and string sustains sound like a 1990s
  sound card. Fine for drafts, and for the drum kit; not for the final
  sting.

**SFZ sample libraries.** The orchestral quality step.
- *VSCO 2 Community Edition:* CC0, about 3 GB. Folders: Brass (F horn,
  old and tenor trombones, trumpet, tuba); Strings (violin, viola and
  cello sections, solo violin and contrabass, harp); Percussion
  (glockenspiel, marimba, timpani, xylophone, bass drum, snare,
  ratchet, anvil, sleigh bells and more); Woodwinds; Keys (organ,
  uprights). No vibraphone or celesta.
- *Versilian Community Sample Library (VCSL):* CC0 ("you can do
  whatever you want with these sounds"). Struck idiophones include
  **Vibraphone**, Xylophone, Glockenspiel, Marimba, Tubular Bells,
  Slapstick, Ratchet, Flexatone, Anvil, Woodblock, Hi-Hat, Claps,
  Tambourine and Vibraslap. Also Friction Idiophones/**Wine Glasses**, a
  **Siren**, harmonicas, pianos and harpsichords, and TX81Z FM patches.
  A goldmine for the comic and mechanical sounds as well as the music.
- *Sonatina Symphonic Orchestra* (maintained by Peter Eastman): CC
  Sampling Plus 1.0. Includes **celeste**, vibraphone, glockenspiel,
  xylophone, marimba, chimes, crotales, timpani, full brass and
  woodwinds, strings and harp. The deed allows you "to sample, mash-up,
  or otherwise creatively transform this work for commercial or
  noncommercial purposes"; only verbatim copies of the library must be
  non-commercial, and the work may not advertise anything else.
  Rendered music is a transformation. The licence is retired but still
  valid.
- *Virtual Playing Orchestra 3.3* (Paul Battersby): built from
  Sonatina, VSCO 2 CE, No Budget Orchestra, University of Iowa,
  Philharmonia and others, with performance scripts and articulations
  (tremolo strings among them). The author: "I enforce no restrictions
  on using this library to make music, even for commercial purposes."
  The usual first choice among free SFZ orchestras.
- *AVL Drumkits* (Black Pearl, Red Zeppelin): CC BY-SA 3.0 for the kits;
  music made with them may be distributed freely *(secondary: the
  author's page)*.
- **Players.** *sfizz* (BSD) has a `sfizz_render` command-line renderer
  (MIDI plus SFZ in, WAV out). But the repository was **archived
  2026-06-21** (last release 1.2.3, January 2025), and whether the
  Windows zip includes `sfizz_render` is *(unverified)*. Alternatives:
  - sfizz or Plogue sforzando as VST3 plug-ins inside pedalboard or
    DawDreamer. Loading the `.sfz` path may need a saved plug-in state;
    *(unverified)* headless.
  - A small pure-Python SFZ-subset sampler: regions, key ranges,
    velocity layers, release. About a day's work. It is deterministic,
    fits the house style of `make_textures.py`, and would keep the
    "committed equals fresh render" test valid.
- **Quality ceiling:** good for short, percussive and staccato material:
  stabs, pizzicato, mallets, timpani, harp. Weakest at exposed legato
  lines and solo brass sustains, because free libraries have little or
  no legato. A 1950s band-limited, mono-ish tape treatment hides much of
  this.

**MuseScore 4 (MuseScore Studio) with Muse Sounds.**
- *Command line:* `MuseScore4.exe -o out.wav score.mscz` "switches to
  converter mode and avoids the graphical user interface." Batch jobs
  run with `-j job.json`, and `--sound-profile "Muse Sounds"` works with
  `-o .mp3` or `-j`. WAV, OGG and FLAC exports are listed. Claude can
  write MusicXML with `music21`, and MuseScore imports and renders it.
- *Instruments:* Muse Sounds Core is a free bundle (orchestra, choir,
  keyboards, percussion) with expressive, legato-aware playback. It is
  widely considered the best free notation playback. It needs a free
  MuseHub account and install, which the operator must do.
- *Licence:* MuseScore itself claims no rights in exported audio. Muse
  Sounds Pro's FAQ (2026-05-12) allows commercial use, video games
  included: "Individual samples may not be redistributed or resold on
  their own." A quoted Muse Sounds term limits use to "your own
  newly-created sound recording(s) and/or performances", combined with
  other sounds. The exact terms for the **free** Core bundle were not
  found *(unverified)*. Read them before shipping.
- **Quality ceiling:** the best free option for the light-orchestra
  cue and the sting's string tremolo and brass. Less suited to disco:
  it is a notation tool, with no synths and limited drum-groove
  feel.

**Spitfire LABS, now Splice INSTRUMENT, and BBC SO Discover.**
- *Licence:* Spitfire's support article (2025-07-21): recordings are
  "royalty free" for free licences including LABS and Discover, and
  incidental music for video games is a listed compliant use. The ban:
  "not permitted to use the sounds to create another sample library, or
  something that can be construed as such." Shipping single-note
  effects could be read that way; shipping music is fine.
- *Status:* Splice bought Spitfire (April 2025). Splice INSTRUMENT
  (2026-05-10), a free VST3 plug-in, "replaces" LABS and needs a Splice
  account; the existing LABS packs keep working. BBC SO Discover
  (33 instruments, 47 techniques) is free after a questionnaire and a
  14-day wait *(secondary)*.
- *Headless:* none of this is designed for it. As VST3 plug-ins they
  can probably be loaded by pedalboard or DawDreamer, with presets
  chosen once in the editor and the state saved *(unverified)*.
  Downloads go through a GUI app with an account.
- **Quality ceiling:** high for strings and textures; Discover is a
  real orchestra in miniature. Worth it only if Muse Sounds and VPO
  fail the ear test, given the account friction.

**Synths: Surge XT, Vital, Dexed.**
- *Surge XT* (GPL-3; the licence does not reach audio you render): a
  VST3 on Windows, and it exposes every synthesis parameter through
  VST3. It has Python bindings (`surgepy`, built from source with
  `-DSURGE_BUILD_PYTHON_BINDINGS=ON`; no prebuilt wheel found), but
  hosting the VST3 in pedalboard is simpler, and an MCP server already
  does exactly that for effects. It covers the octave-bouncing **disco
  bass**, the **string machine** (Solina-style ensemble), the
  **theremin** (sine plus 5.5 Hz vibrato plus portamento: the motif's
  constant) and Moog-ish leads.
- *Vital:* free, but the download needs an account and it has a GUI;
  only via a plug-in host. Not needed.
- *Dexed* (GPL-3, a DX7 emulation): FM bells, celesta-like and
  vibraphone-like tones. A fallback for the comfort palette.
- **Quality ceiling:** for synth parts, these are the real thing.
  1970s disco basses, string machines and theremin substitutes *were*
  synthesizers.

**Hosts and effects (all headless Python).**
- *pedalboard* (Spotify, GPL-3): version 0.9.26 (2026-10-07); Windows
  wheels for Python 3.10 to 3.15. It hosts VST3 instruments and
  effects, renders MIDI messages through instrument plug-ins, and has
  built-in reverb, filters, compression, distortion, convolution, and
  OGG and WAV input and output.
- *DawDreamer* (GPL-3; Python 3.11 to 3.14, Windows): VST2 and VST3
  hosting, MIDI and automation, plug-in state save and load, an editor
  window for one-off preset setup, and built-in **Faust**.
- *ChowTape (AnalogTapeModel)* (GPL-3, VST3 on Windows): physical tape
  model with saturation, wow and flutter. The period finish.

**Synthesis languages.**
- *SuperCollider* (GPL-3): non-real-time (NRT) rendering from a score
  is deterministic with fixed seeds, and works on Windows. Excellent
  for the radiophonic sting, theremin, and early-electronic effects.
  Effort: a day to become productive.
- *Csound* (LGPL): renders from the command line
  (`csound -o out.wav file.csd`), deterministic, with decades of
  orchestra-style instrument code. Similar strengths.
- *Faust* (GPL compiler; generated code unrestricted): best used inside
  DawDreamer.
- *Sonic Pi* (MIT): there is "no official 'headless mode'"; rendering
  goes through OSC recording in real time. **Not recommended.**
- *Strudel* (AGPL, in the browser): its default sample map draws on
  Dirt-Samples, of "unknown/dubious origin", and VCSL. Offline,
  headless rendering is awkward. **Not recommended.**
- **Quality ceiling:** the same as the current pure-Python plan for
  effects, with better tools. Using these languages (or numpy) gives
  little musical gain over Surge XT plus samples.

### Quality Ceilings for the Three Pieces (Judgement)

- **Disco loop (120 BPM).** *Composed:* high, if the idiom is
  late-1970s **space or synth disco**: sequenced octave bass, string
  machine, synth stabs, programmed or tightly played drums, a theremin
  hook. In that idiom, synthesis is the authentic sound (no quotation
  of any record intended). Orchestral string stabs from VPO or Sonatina
  are short and therefore convincing. The weak spot is live-drummer
  feel: use humanized timing, a good sampled kit (VSCO or VCSL hits,
  AVL Black Pearl, or GeneralUser GS) and a little swing on the hats.
  Ceiling: "a competent 1978 library-record pastiche." *Generated:*
  ACE-Step or Stable Audio 3 might sound more like a real record, but
  less controllable, and the seam and the theremin would need
  post-work.
- **The 1960s sting (2.5 s).** *Composed:* good. A brass stab (VPO or
  Muse Sounds), string tremolo (VPO, Muse Sounds, Discover), timpani
  (VSCO 2 CE or Sonatina) and a theremin glint, through a mono,
  band-limited 1960s TV chain. Period scores were small ensembles
  recorded close and dry-ish, which forgives samples. Or the honest
  radiophonic version from SuperCollider or Surge XT, as
  SOUND-PIPELINE prefers.
- **The 1950s light-orchestra bed.** *Composed:* medium to good. Write
  for the medium: vibraphone, xylophone, celesta, harp, pizzicato,
  muted brass, flute, and string *pads* rather than exposed legato
  melody. Muse Sounds lifts the melodic lines. *Generated:* possibly
  the best result of all, because stock "library music" is what Stable
  Audio 3 learned from; the trial decides.

### Licences for Shipping Rendered Audio

| Source | Licence | Ship rendered audio? |
|--------|---------|----------------------|
| VSCO 2 CE, VCSL | CC0 | Yes, no conditions |
| Sonatina | CC Sampling+ 1.0 | Yes (transformation) |
| Virtual Playing Orch. | Mixed; author permits music | Yes |
| GeneralUser GS | Own | Yes, "private or commercial" |
| FluidR3, MuseScore General | MIT | Yes |
| AVL Drumkits | CC BY-SA 3.0 | Music yes *(secondary)* |
| Muse Sounds (free Core) | MuseHub terms | Likely; *verify text* |
| LABS / Splice / Discover | Spitfire EULA | Music yes; not sample-like |
| Surge XT, Dexed, ChowTape | GPL-3 software | Yes (audio not covered) |

Credit everything in `Sounds/CREDITS.txt` regardless; it costs nothing.

One engineering note: a repository tool that imports pedalboard or
DawDreamer (GPL-3) is fine to keep under MIT, since the project does
not redistribute those libraries. If the operator wants zero ambiguity,
keep the render script in `Tools/` and commit only its outputs, as with
textures.

### Effort

- FluidSynth and GeneralUser GS baseline: under an hour.
- pedalboard with Surge XT (bass, strings, theremin) and effects:
  half a day to build helpers in `make_sounds.py` style.
- A pure-Python SFZ sampler for VSCO, VCSL and Sonatina: about a day.
- MuseScore command line with Muse Sounds: one to two hours, after the
  operator installs it through MuseHub.
- SuperCollider or Csound: one to two days to become fluent; only for
  the radiophonic variant.
- Composition itself: about an hour of agent time per piece per draft,
  plus listening rounds with the operator.

## Part C: The Hybrid Recommendation

### By Asset

- **Signal tones** (DTMF, beeps, filmstrip chime): code synthesis, as
  specified. Nothing here improves on exactness.
- **Organic effects** (projector, wind, boom, implosion, blimp engine,
  locker slam): **Stable Audio 3** Small-SFX first, then Medium, with
  Stable Audio Open 1.0 as fallback. Generate four to eight takes and
  choose by ear. Record each kept file's model, version, seed and
  prompt in a manifest beside `Sounds/CREDITS.txt`. Layer and treat
  with the pipeline's band-limit, hiss and loudness steps.
- **Comic and mechanical effects** (raspberry, slapstick, ratchet,
  alarm, glass shimmer): synthesis plus **VCSL CC0** recordings
  (Slapstick, Ratchet, Flexatone, Siren, Wine Glasses).
- **Disco loop:** **composed**. Claude writes MIDI; Surge XT plays bass,
  string machine and theremin; VPO, Sonatina or VSCO play the string and
  brass stabs; a sampled or synthesized kit plays the drums; pedalboard
  mixes; ChowTape finishes. Optional: a Foundation-1 or ACE-Step
  *lego* layer, if the trial shows it adds realism.
- **Mirror Mirror sting:** **composed**, two variants for the operator:
  orchestral (VPO or Muse Sounds) and radiophonic (Surge XT or
  SuperCollider). Generated takes from Stable Audio 3 Medium are a third
  candidate.
- **Light-orchestra bed and lounge loop:** the trial decides among Muse
  Sounds (composed), Stable Audio 3 Medium and ACE-Step 1.5 (generated),
  and the hybrid: a composed mock-up restyled with ACE-Step *cover*.

### Where a Small Paid Service Is Still Worth It

- **Probably nowhere for effects.** Stable Audio 3 plus synthesis plus
  CC0 should cover them.
- **Possibly for the light-orchestra cue,** if neither Muse Sounds nor
  the local generators sound like a real 1950s record. Stable Audio 3
  **Large** (API only; same licence family; trained on the same licensed
  data) is the natural upgrade. Stability bills credits at US$0.01
  each, with a US$10 top-up *(secondary; the per-generation audio price
  was not found)*. Spend from the US$50 only after the trial shows the
  gap. The sister research reports cover subscription services and
  their terms.
- **A free check worth doing before shipping any music:** the operator
  uploads an unlisted test video to YouTube containing the candidate
  track, and watches for a Content ID match. This is an operator action,
  with his own account.

### The Listening Trial (One to Two Hours on This Machine)

Run by a Claude agent **after the operator approves the installs and
downloads** (which this research did not do). Gated models need the
operator to accept their terms on Hugging Face and supply his own
token; skip them if he has not. Everything goes under
`.local/tmp/listening-trial-<date>/`. Each render is loudness-matched
to −16 LUFS with ffmpeg's `loudnorm`, so the comparison is fair.

1. **Setup (about 30 min).** Create a venv on Python 3.12, then install
   torch (cu128, 2.7 or later), `mido`, `pretty_midi`, `numpy`,
   `soundfile`, `pedalboard` and `diffusers`. Install the FluidSynth
   Windows binary and GeneralUser GS, the Surge XT VST3, and ChowTape.
   Clone ACE-Step 1.5 (its portable package or pip) and download the
   2B turbo model and the 0.6B language model. Download VPO (or
   Sonatina) and VCSL. If approved: Stable Audio 3 Small-SFX and Medium
   (gated), through ComfyUI portable or the `stable-audio-3`
   repository. If FlashAttention fails on sm_120, run Medium in
   ComfyUI or skip it.
2. **Composed set (about 30 min).** Claude writes three scripts, each a
   bar grid producing MIDI:
   - `disco.py`: 16 bars at 120 BPM in A minor. Kick on every beat, open
     hat on the off-beats, clap on 2 and 4, octave-bouncing bass,
     string stab on the "and" of 4, an 8-bar theremin hook in the second
     half. Loop length 32 s.
   - `sting.py`: 2.5 s. A brass stab chord (an original voicing, not the
     1967 intervals), string tremolo swelling, a timpani roll to a hit,
     and a theremin glint at the tail.
   - `bed.py`: 16 bars at about 112 BPM in F major, light orchestra.
     Vibraphone melody, xylophone answers, pizzicato bass, harp
     arpeggios, muted-trumpet countermelody, string pad.

   Render each three ways:
   - **A:** FluidSynth with GeneralUser GS (the baseline).
   - **B:** pedalboard with Surge XT for the synth parts, plus the best
     available sampler route for VPO, Sonatina, VSCO or VCSL (sfizz or
     sforzando VST3 if they load headless; otherwise skip the orchestra
     in B for the disco and use A's stems), mixed, then ChowTape, a
     plate-style reverb, and a 90 Hz to 11 kHz band-limit.
   - **C (optional):** `music21` to MusicXML, then
     `MuseScore4.exe -j job.json --sound-profile "Muse Sounds"` for the
     sting and the bed, if the operator has Muse Sounds installed.
3. **Generated set (about 30 min).**
   - *ACE-Step 1.5 turbo*, `instrumental=True`, four seeds each:
     - Disco: "1970s disco instrumental, four-on-the-floor kick,
       octave-bouncing bass, off-beat open hi-hats, orchestral string
       stabs, warm analog recording", `bpm=120`, `keyscale="A minor"`,
       `timesignature="4/4"`, `duration=64`.
     - Bed: "1950s educational film library music, light orchestra,
       vibraphone, xylophone, pizzicato strings, harp, cheerful and
       composed, vintage mono recording", `bpm=112`, F major,
       `duration=40`.
     - Sting: "1960s television orchestral science-fiction sting,
       brass stab, string tremolo, timpani roll, ominous",
       `duration=10`; cut the best 2.5 s.
   - *Hybrid:* ACE-Step `cover` on render B of `disco.py` and of
     `bed.py`, with the same captions.
   - *Stable Audio 3 Medium*, if available: the same three prompts (the
     sting at 3 s directly), and loop-seam inpainting on the best disco
     take.
   - *Stable Audio 3 Small-SFX* (CPU), four seeds of 3 s each: "1950s
     16 mm film projector motor starting up, shutter clatter"; "film
     projector running, steady whir and clatter"; "film reel running
     out, tail flapping, motor winding down"; "steady gentle wind over
     open ground"; "deep distant explosion boom"; "low rumble with a
     rising whistle"; "metal locker door slams shut"; "keypad buttons
     pressed". Stable Audio Open 1.0 on the same prompts, for
     comparison.
4. **Reel (about 15 min).** Build one review reel per asset with
   anonymized labels: each candidate once at full range and once
   through the laptop filter (high-passed at 250 Hz), as
   SOUND-PIPELINE's `--reel` specifies. Write a key file mapping labels
   to sources, unseen by the operator until he has scored them. Note
   generation times and peak VRAM for each model, so the cost of the
   full pipeline is known.

The operator scores each candidate (keep, maybe, reject) on three
questions: Does it sound like the 1950s film? Does it survive the
laptop filter? Does anything in it sound like an existing record? The
scores choose the route per asset.

## Open Points and Unverified Items

- The free Muse Sounds Core licence text (not found; the Pro FAQ and
  quoted MuseHub terms suggest music use is allowed).
- Whether ComfyUI's Stable Audio 3 nodes need FlashAttention 2, and
  whether a FlashAttention 2 wheel for Windows, sm_120 and the
  installed torch exists.
- MOSS-SoundEffect v2.0's size and licence on its own card (the card
  read shows 8B and Apache 2.0; a blog describes a 1.3B v2.0).
- Whether `sfizz_render` ships in the Windows zip; whether sfizz,
  sforzando or LABS VST3s accept their content paths headless in
  pedalboard.
- SongBloom's and MiniMax Music 3's licences (conflicting reports).
- Listening reports for disco, vintage library cues and stings from
  any open model: none found. Hence the trial.

## Sources

All read 2026-10-15. *Search* marks facts taken from a search-engine
summary of the page rather than the page itself.

Models and licences:
- https://github.com/facebookresearch/audiocraft
- https://github.com/facebookresearch/audiocraft/issues/198
- https://huggingface.co/spaces/facebook/MusicGen/discussions/8
- https://www.hypebot.com/metas-musicgen-ai-trained-on-20k-licensed-tracks-but-how-good-is-it/
  (search)
- https://huggingface.co/stabilityai/stable-audio-open-1.0
- https://huggingface.co/stabilityai/stable-audio-open-small
- https://stability.ai/community-license-agreement
- https://stability.ai/news-updates/meet-stable-audio-3-the-model-family-built-for-artistic-experimentation-with-open-weight-models
- https://arxiv.org/html/2605.17991v1 (Stable Audio 3 paper)
- https://huggingface.co/stabilityai/stable-audio-3-medium
- https://huggingface.co/stabilityai/stable-audio-3-small-sfx
- https://github.com/Stability-AI/stable-audio-3
- https://docs.comfy.org/tutorials/audio/stable-audio/stable-audio-3.md
- https://comfy.org/hub/models/local/stable-audio-3-medium.md
- https://blog.dubspot.com/stable-audio-3-review
- https://chatforest.com/reviews/stability-ai-stable-audio-3-open-weight-music-sfx-generation/
- https://artintech.substack.com/p/stable-audio-3-explained-in-5-figures
- https://pypi.org/project/audiyo/ (search; Stable Audio Open VRAM)
- https://huggingface.co/AEmotionStudio/foundation1-models/blob/main/README.md
- https://rits.shanghai.nyu.edu/ai/foundation-1-a-producer-focused-ai-model-for-structured-music-sample-generation/
- https://github.com/ace-step/ACE-Step-1.5
- https://github.com/ace-step/ACE-Step-1.5/blob/main/docs/en/INFERENCE.md
- https://github.com/ace-step/ACE-Step-1.5/issues?q=RTX+50
- https://huggingface.co/ACE-Step/Ace-Step1.5
- https://ace-step.github.io/ace-step-v1.5.github.io/
- https://docs.comfy.org/tutorials/audio/ace-step/ace-step-v1-5
- https://huggingface.co/OpenMOSS-Team/MOSS-SoundEffect
- https://studio.aifilms.ai/blog/moss-soundeffect-v2-open-source-sound-design
- https://huggingface.co/google/magenta-realtime
- https://github.com/magenta/magenta-realtime
- https://mer.vin/2026/06/magenta-realtime-2-open-local-live-music-model-with-midi-and-200ms-latency/
- https://github.com/HeartMuLa/heartlib
- https://hermes-agent.nousresearch.com/docs/user-guide/skills/bundled/media/media-heartmula
  (search; VRAM)
- https://github.com/ASLP-lab/DiffRhythm
- https://github.com/multimodal-art-projection/YuE
- https://huggingface.co/m-a-p/YuE-s1-7B-anneal-en-cot
- https://comfyui-wiki.com/en/models/yue2/yue2-3b
- https://huggingface.co/rtikw/SongBloom (mirror)
- https://gaga.art/blog/songgeneration-2/
- https://mer.vin/news/minimax-music-3-is-open-weight-but-not-open-for-business/
- https://www.opensourceforu.com/2026/08/minimax-releases-minimax-music3-open-weight-model/
- https://boppy.me/blog/best-open-source-ai-music-models
- https://www.spheron.network/blog/deploy-open-source-ai-music-generation-gpu-cloud-2026/
  (secondary; contains errors, used for orientation only)
- https://github.com/hkchengrex/MMAudio
- https://github.com/SonyResearch/Woosh
- https://huggingface.co/cvssp/audioldm2
- https://huggingface.co/declare-lab/tango2
- https://huggingface.co/declare-lab/TangoFlux
- https://github.com/Tencent-Hunyuan/HunyuanVideo-Foley
- https://raw.githubusercontent.com/Tencent-Hunyuan/HunyuanVideo-Foley/main/LICENSE
- https://creativecommons.org/licenses/by-nc/4.0/legalcode.en
- https://developer.puter.com/tutorials/stability-ai-api-pricing/
  (search; credit price)

Platform:
- https://pytorch.org/blog/pytorch-2-7/ (search)
- https://pytorch.org/get-started/previous-versions/ (search)
- https://github.com/Comfy-Org/ComfyUI/discussions/6643
- https://github.com/Dao-AILab/flash-attention/issues/2361 (search)
- https://www.kombitz.com/2026/05/03/how-to-install-flashattention-2-on-windows/

Composition, renderers and instruments:
- https://huggingface.co/papers/2606.22708.md (Libretto)
- https://github.com/sgossner/VSCO-2-CE (and its Brass, Keys, Strings
  and Percussion folders)
- https://github.com/sgossner/VCSL (and its Idiophones, Aerophones,
  Chordophones, Membranophones and Electrophones folders)
- https://github.com/peastman/sso
- https://creativecommons.org/licenses/sampling+/1.0/
- http://virtualplaying.com/virtual-playing-orchestra/
- https://www.bandshed.net/avldrumkits/ (search)
- https://raw.githubusercontent.com/mrbumpy409/GeneralUser-GS/main/documentation/LICENSE.txt
- https://github.com/mrbumpy409/GeneralUser-GS
- https://src.fedoraproject.org/rpms/mscore/blob/f36/f/MuseScore_General_License.md
  (search; MIT)
- https://github.com/FluidSynth/fluidsynth/releases
- https://github.com/sfztools/sfizz/releases and
  https://api.github.com/repos/sfztools/sfizz/releases/tags/1.2.3
- https://man.archlinux.org/man/sfizz_render.1.en (search)
- https://handbook.musescore.org/appendix/command-line-usage
- https://support.musehub.com/en/articles/15070610-musesounds-pro-frequently-asked-questions
- https://support.musehub.com/en/articles/15070587-frequently-asked-questions
- https://forum.audacityteam.org/t/can-i-monetize-podcasts-that-use-sounds-from-muse-hub/110486
- https://www.spitfireaudio.com/bbc-symphony-orchestra-discover/ (search)
- https://support.spitfireaudio.com/en/articles/11815239-are-spitfire-audio-sample-libraries-royalty-free-and-can-i-use-them-on-commercial-recordings
- https://www.musicmanta.com/news/splice-acquires-spitfire-audio (search)
- https://www.musicmanta.com/news/splice-instrument-launches-spitfire-labs-home
- https://pypi.org/project/pedalboard/
- https://github.com/DBraun/DawDreamer
- https://github.com/surge-synthesizer/surge
- https://lobehub.com/mcp/bazfer-surge-mcp (search; Surge XT via
  pedalboard)
- https://github.com/jatinchowdhury18/AnalogTapeModel (search)
- https://github.com/asb2m10/dexed (search)
- https://in-thread.sonic-pi.net/t/export-to-wav-with-command-line/4674
  (search)
- https://strudel.cc/learn/samples/ (search)
- https://club.tidalcycles.org/t/clean-samples/3020 (search)
