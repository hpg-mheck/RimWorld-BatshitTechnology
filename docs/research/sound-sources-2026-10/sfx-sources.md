# Sound Effects Sources, October 2026

Phase 5000, Slice 1000 research for Batshit Technology. Desk research
only, read on 2026-10-15; nothing was signed up for, bought or
downloaded. Prices and terms are as read that day, with the page in the
sources list at the end. "Unverified" marks figures with no primary
source.

## Summary

- **Most of the list needs no money.** Code synthesis (already planned)
  plus CC0 recordings from Freesound and Kenney cover every effect in
  the brief. Freesound's CC0 filter alone turns up 58 film-projector
  recordings, including an 8 mm projector starting, running and running
  out at 48 kHz; 46 locker recordings; 164 propeller planes; mouth
  raspberries; and 76 rubbed wine glasses.
- **The cleanest AI option is free:** Stability AI's Stable Audio 3
  Small SFX (open weights, 20 May 2026), run locally. Under the Stability
  AI Community License the outputs belong to the user, with no ban on
  shipping them as files. Hosted on fal.ai it costs $0.0283 a generation.
- **ElevenLabs has the best name for one-shots but a new licence
  problem.** Its Sound Effects terms (updated 9 October 2026) forbid
  making output available "as isolated audio files or samples, or as
  part of any sound, sample, effects, or music library or other
  collection of sounds." A RimWorld mod ships loose WAVs, and a public
  GitHub repository is a collection of sounds. Do not ship ElevenLabs
  effects without ElevenLabs's written permission.
- **Recommended effects spend: $0, with up to $10 in reserve** (one month
  of Adobe Firefly Standard at $9.99, or ElevenLabs Starter at $6 if
  ElevenLabs approves). That leaves $40 to $50 for music.
- **Only CC0 survives a public repository.** Every "royalty-free"
  library read (Sonniss, Zapsplat, Pixabay, Mixkit, 99Sounds, Soundly)
  forbids redistributing the raw files on their own. Loose files in
  `Sounds/` and in an MIT-licensed public repository plausibly do
  exactly that.

## What the Effects Need

From the brief and `docs/SOUND-PIPELINE.md` (48 kHz mono WAV, mostly
under 3 s, normalized to −16 LUFS by `Tools/make_sounds.py`):

| Effect | Length | Kind |
|--------|--------|------|
| Projector start, whir | 1.5 s | Mechanical foley |
| Projector run-out | 1.5 s | Mechanical foley |
| Filmstrip advance chime | 0.4 s | Signal tone (synthesis) |
| Lull: wind | 3 s loop | Noise texture |
| Deep boom | 3 s | Impact with tail |
| Raspberry ("Thpppt") | 0.7 s ×3 | Human, comic |
| Implosion pull | 3 s loop | Rumble and whistle |
| Mirror shimmer | 3 s loop | Tonal texture |
| DTMF, TTMF, beeps, bass | short | Signal tones (synthesis) |
| Later: keypad | short | Click and tone |
| Later: locker hinge, slam | short | Mechanical foley |
| Later: alarms | short or loop | Signal tones |
| Later: blimp engine | loop | Engine drone |

## AI Text-to-Sound Services

### ElevenLabs Sound Effects

- **Model:** `eleven_text_to_sound_v2`, the API's only and default
  model. Durations 0.5 to 30 s (the product page says 0.1 s); a `loop`
  flag for seamless loops; `prompt_influence` 0 to 1 (default 0.3).
- **Formats:** the web app downloads "MP3 (44.1kHz) or WAV (48kHz)", and
  WAV only for non-looping effects, so a web loop arrives as MP3, whose
  encoder padding breaks a seamless seam. The API offers raw
  `pcm_48000` (headerless PCM, which `wave` can wrap) and MP3 and Opus.
  Neither matters much: `make_sounds.py` cuts its own crossfaded seams.
- **Cost on the web (ElevenCreative):** each click makes four
  variations. Letting the AI choose the length costs 200 credits a
  click; setting the length costs 40 credits a second.
- **Cost through the API (ElevenAPI):** one clip a call; the API
  pricing page lists Sound Effects at $0.12 a minute and "150
  generations" in Starter.
- **Plans** (pricing page, 2026-10-15):

  | Plan | Price a month | Credits | Commercial |
  |------|---------------|---------|------------|
  | Free | $0 | 10,000 | No |
  | Starter | $6 ($1 first month, API page) | 30,000 | Yes |
  | Creator | $22 ($11 first month) | 121,000 | Yes |
  | Pro | $99 | 600,000 | Yes |

- **Licence by plan.** The free plan "does not include a commercial
  license", and published free-tier content must carry "elevenlabs.io"
  or "11.ai" in its title; that is unworkable for a mod, so free output
  is for auditioning prompts only. Paid plans include a commercial
  licence, and "content generated during a paid subscription can be used
  commercially, and indefinitely", so cancelling after a month is safe.
  Beta services are excluded.
- **The blocking clause.** The ElevenCreative Terms (updated 9 October
  2026), Exhibit 4 (Sound Effects), section 2, allow incorporating
  output into games, but: "You may exercise those rights only in respect
  of Output combined with other material, and you may not sell, resell,
  license, sublicense, distribute, or otherwise commercially exploit
  Output, or any part of it, on a standalone basis, or make Output
  available as isolated audio files or samples, or as part of any sound,
  sample, effects, or music library or other collection of sounds." It
  excepts "any other use ElevenLabs expressly permits in writing." The
  ElevenCreative Terms cover each offering "whether you use an offering
  on its own or through another ElevenLabs Service", so the API is
  probably covered too. RimWorld mods ship each effect as a loose file,
  and the repository publishes them under a root MIT licence. Reading
  the clause literally, both are a breach. Layering the output into our
  own composite may count as "combined with other material", but the
  file would still hold "any part of" the output. Ask ElevenLabs in
  writing before shipping anything; until then, treat ElevenLabs as a
  prototyping tool.
- **Community sharing:** by default ElevenLabs may share your effects in
  its community library; it can be switched off.
- **Quality.** Reviews rank it first for game one-shots, saying it
  understands "material, space and intensity cues" (Undetectr,
  2026-09-25) and game vocabulary (Cinevva, 2026-09-05). No listening
  report covers 1950s foley specifically. Expect clean, modern,
  full-band output that our tape treatment would then age.
- **What a spend buys** (assuming 6 clicks, 24 candidates, per effect):
  - Starter, $6 (web): 150 auto-length clicks (600 candidates), or 250
    clicks at a fixed 3 s and 500 at 1.5 s. That is about 25 effects at
    auto length, or 40 to 80 short ones.
  - Creator, $11 first month: 605 auto-length clicks, about 100
    effects. This is only worth it if Eleven Music also wins the music
    comparison, since credits are shared (music terms not read here).

### Stability AI: Stable Audio 3

- **Release:** 20 May 2026, four models: Small SFX (459 M parameters on
  the release page; 0.6 B on its model card), Small (music), Medium
  (1.4 B, open weights) and Large (2.7 B, API and enterprise only). Small
  SFX is "best suited for short clips, with 3-7 seconds", and the
  mirrored model config reads 44,100 Hz, 2 channels.
- **Training data** (model card): 1,278,902 recordings, 806,284 licensed
  from AudioSparx and 472,618 from Freesound (CC0, CC BY and Sampling+).
- **Licence (open weights):** the Stability AI Community License. "You
  own any outputs generated from the Models"; outputs are expressly not
  "Derivative Works", so the "Powered by Stability AI" notice applies
  only when the model itself is redistributed. Commercial users register
  for free while under $1 M annual revenue. Outputs may not be used to
  train a competing foundation model. There is no ban on shipping
  outputs as files.
- **Running it locally:** the weights are gated on Hugging Face. The
  operator, not an agent, must accept the terms, which ask for a name,
  email and country, and the Gemma Terms of Use ride along with the
  T5Gemma text encoder. The model is about 1.2 GB at 16-bit, positioned
  for "phones and consumer laptops", and fits the 8 GB RTX 5060 Ti
  easily. It needs a venv with a CUDA 12.8 PyTorch build, or ONNX or CPU
  ports.
- **Hosted:** fal.ai lists Small SFX at $0.0283 a generation, WAV
  available. The Stability API is reported at 26 credits ($0.26) a
  generation (secondary source; unverified). stableaudio.com plans are
  $12, $30, $90 and $199 a month for 660, 1,800, 6,000 and 14,000
  credits; two Stability pages name the tiers differently, and the
  credits per generation are unpublished (unverified). The free tier is
  non-commercial. Stability's Terms of Service (30 September 2026)
  assign outputs to the user and add no standalone restriction, but
  forbid removing watermarks or content credentials.
- **Quality:** reviewers call the family "strong on ambient and tonal
  textures; weaker on sharp transients" and "imprecise for punchy single
  hits", but "best for long ambience". Dubspot (2026-05-26) rates it
  7.8/10 and calls Small SFX "the workhorse of the family for sound
  designers". That is good for wind, rumble, engine drones and shimmer
  layers, and weaker for slams and clicks.
- **What a spend buys:** locally, nothing but time. On fal.ai, $5 buys
  about 175 tries (about 29 effects at 6 tries each). On the Stability
  API, $10 buys about 38 tries (about 6 effects).

### Adobe Firefly Generate Sound Effects

- **Out of beta and available to all users**, on the Firefly Audio
  Model. Text prompts of up to 750 characters, optional voice or
  reference-audio timing guides, several variations each (four of up to
  30 s each, per a secondary source); downloads as WAV or MP4.
- **Plans:** Standard $9.99 a month, 2,000 credits, "up to 200" sound
  effects (10 credits each); Pro $19.99 a month, 4,000 credits. There is
  a free tier with daily generations; its commercial status for audio
  is unverified.
- **Licence:** "Outputs from Adobe Firefly AI models are safe for
  commercial use", and beta features likewise "unless explicitly stated
  otherwise." Adobe staff on its forum (2026-09-25) said the terms
  "do not explicitly prohibit resale of generated audio as standalone
  assets, but they also do not explicitly confirm it." No standalone
  clause appears in the User Guidelines (2026-05-15). The product
  specific terms (effective 23 April 2026) were not readable here, so
  read them before buying.
- **Quality:** trained on licensed and public-domain material; reviews
  praise performance timing (act out the rhythm with your voice). No
  listening reports for vintage foley were found.
- **What a spend buys:** Standard, $9.99, gives 200 generations, about
  33 effects at 6 tries.

### Google

Google offers no standalone text-to-effects model as of October 2026.
Lyria 3.5 makes music only (44.1 kHz stereo, SynthID watermark). Veo 3
generates effects only as a video's soundtrack. Gemini 3.8 TTS is speech.
Google is not a candidate.

### Other Contenders

- **Suno Sounds (Beta, January 2026):** one-shots and loops with BPM and
  key, 2 credits each, for Pro and Premier subscribers. It is musical in
  bent (risers, stingers, loops). Its terms for Sounds were not found
  (unverified). It is a candidate only if Suno is bought for music.
- **Krotos Studio:** not generative but performed presets over recorded
  libraries. Games need the Pro plan ($34.99 monthly, or $23.33 a month
  billed yearly). Post-cancellation rights are not stated. Too dear, and
  its library licence would carry standalone limits.
- **MMAudio:** the code is MIT, but the weights are CC BY-NC 4.0.
  Non-commercial; avoid.
- **HunyuanVideo-Foley (Tencent):** its licence excludes the EU, the UK
  and South Korea, which fails "worldwide". Avoid.
- **Meta AudioGen:** CC BY-NC weights and a generation behind. Avoid.
- **Mirelo SFX, Sonilo:** video-to-effects tools for editors; nothing
  they offer beats the above for text-only one-shots.
- **Freepik:** sells AI audio in its credit plans; its effects terms
  were not reached (unverified). Not pursued.

## Libraries

### Licence Comparison

"Loose files OK?" means: may the raw file ship unmerged in `Sounds/`
and sit in a public repository?

| Source | Licence | Loose files OK? | Credit |
|--------|---------|-----------------|--------|
| Freesound CC0 | CC0 | Yes | None |
| Freesound CC BY | CC BY 3.0/4.0 | Yes, with notice | Required |
| Freesound CC BY-NC | CC BY-NC | Avoid | Required |
| Kenney | CC0 | Yes | None |
| OpenGameArt CC0 | CC0 | Yes | None |
| Sonniss GDC | Own EULA | No: standalone ban | None |
| Pixabay | Own licence | Risky if unedited | None |
| BBC (RemArc) | Non-commercial | No | Required |
| Zapsplat | Own licence | No | Free tier: yes |
| Mixkit | Own licence | No | None |
| 99Sounds | Own licence | No | None |
| Soundly | Own EULA | No | None |
| Internet Archive | Per item | Rarely usable | Varies |

### Freesound

- **Licences:** each sound carries the uploader's choice: CC0, CC BY,
  CC BY-NC, or the retired Sampling+ on old uploads. The licence appears
  on each sound's page, and the search sidebar filters by it; the URL
  form is `f=license:"Creative Commons 0"`. The Terms of Use (4 April
  2022) say sounds "are distributed under the Creative Commons license
  indicated for each item", with no extra restriction.
- **Telling them apart:** use only sounds whose page says "Creative
  Commons 0". Treat CC BY as acceptable with a credit line, and refuse
  CC BY-NC and Sampling+.
- **The CC0 caveat:** CC0 is asserted by the uploader, not checked.
  Example: a CC0 "Wild Wind Chimes" says it combines "two loops from
  HBSamples", a commercial vendor. Prefer field recordings with a
  described microphone and place, and record each sound's URL, uploader,
  licence and download date in a manifest.
- **Account:** downloading needs a free account; that is the operator's
  step.
- **Found under the CC0 filter (2026-10-15):**
  - "film projector": 58 results. Among them, Stefan021's "Film
    Projector - Reel Runs Out" (sound 411981: 39.3 s, 48 kHz, 32-bit,
    stereo, CC0, 8.9 k downloads, praised in comments): "An 8mm film
    projector starting and running before the reel begins to run out
    and the last of the film is pulled through." That is start, whir and
    run-out in one take. Also "Film Projector - Long Run with Finish",
    espectral's "Proyector16mm-2.wav" (16 mm, with power transitions),
    and two Super 8 machines by StefanoPTesta.
  - "raspberry lips": 14 results, including craigsmith's "R15-37-Fart
    Noises.wav" (sound 482824, pack "Vintage Voices - Mixed"): "Man
    making sputtering/fart noises with his lips. Blowing raspberries.
    Comedic." Also Reitanna's "raspberry.wav" (323710).
  - "wine glass rub": 76 results (ani_music, spectral9's tuned F#6
    glasses, and others). "glass harmonica" returns nothing; "glass
    harp" returns a few.
  - "wind loop": 279 results.
  - "explosion boom": 1,001 results, field and synthesized, such as
    misosound's "BOOMING PUNCHY EXPLOSION" (a recorded fireball with
    "natural rolling decay").
  - "locker door": 46 results, slams and a rusty-hinge squeal by
    minimilka.
  - "keypad button": 14 results. "propeller plane": 164 results, flybys
    and a looped seaplane drone.
- **Quality for vintage foley:** the best free source for real
  mechanical sounds, though uneven: check noise floor, clipping and
  reverb on each file.

### Sonniss GDC Game Audio Bundles

- **Licence:** "Version 2.0, effective 27 August 2026". Use is unlimited,
  for life, commercial, with no attribution. But: "Licensee may not
  distribute, publish, sub-license or otherwise supply the sound effects
  as sound effects to any other person, without the Licensor's prior
  written permission", nor "sell any of the sound effects as they come";
  AI training is "expressly prohibited". Use is granted "for the
  purposes of synchronization with audio and visual projects", which
  includes games.
- **For this mod:** a heavily layered composite inside a game is
  synchronization. A trimmed raw file sitting in `Sounds/`, and
  committed to a public MIT repository, is close to supplying sound
  effects "as sound effects". The sources also cannot be committed, which
  breaks the generator's reproducible-render test for anyone else.
  Avoid; at most, a minor layer that is never committed.
- **Quality:** professional (the 2026 bundle is 7.47 GB from 17
  vendors, on top of a multi-year archive of 200+ GB). It would be the
  best quality if the licence fitted.

### Kenney Audio Packs

All Kenney assets are "public domain licensed (CC0)", "even in
commercial projects", with no attribution required (credit "Kenney" if
you wish, without the logo). The ten audio packs (Impact Sounds,
Interface Sounds, UI Audio, Sci-fi Sounds, Digital Audio, RPG Audio,
Casino Audio, Music Jingles and two voiceover packs) are clean and
modern: good for clicks and button presses (the keypad), not vintage
character. No account is needed.

### OpenGameArt (CC0 Subset)

Licences accepted: CC0, CC BY 3.0/4.0, CC BY-SA 3.0/4.0, GPL 2.0/3.0 and
OGA-BY. The advanced search filters by art type "Sound Effect" and
licence "CC0" (for example "Dull explosion", "Door open, door close").
Hobbyist quality; little period material. Avoid CC BY-SA and GPL, which
add obligations and DRM questions.

### Pixabay Sound Effects

The licence (terms of 18 November 2024) is "irrevocable, worldwide,
perpetual ... royalty-free", with no attribution. But "You cannot sell
or distribute the Content ... on a Standalone basis", and "Standalone"
means "where no creative effort has been applied to the Content and it
remains in substantially the same form". An unedited Pixabay WAV in
`Sounds/` is arguably distributed standalone; a substantially reworked
composite is not. Uploads are not vetted, and reports describe Content
ID claims on Pixabay music registered after upload. Usable only as an
edited layer, and it adds nothing over CC0.

### BBC Sound Effects (RemArc)

Over 33,000 effects under the RemArc licence: personal, educational and
research use only. Commercial use is licensed through Pro Sound Effects.
The BBC site refused automated reading, so this rests on press coverage
(unverified against the licence text). Superb period material, but not
usable.

### Zapsplat

- **Standard (free) licence:** "You must provide clear credit to
  'ZapSplat'"; free downloads are MP3, and WAV needs Premium (about
  £4.79 a month or £41.99 a year, from page data). Premium downloads
  stay attribution-free "for life" after cancelling.
- **Prohibited:** "Redistribute our sounds in any form (e.g., sound
  libraries, file sharing, apps, social networks, or physical media
  ...)". Its own guide says embedding in "a game" is fine, but taking
  "the audio file as-is and sharing it ... on ... any other file sharing
  platform" is not.
- **For this mod:** loose files on GitHub resemble file sharing. Avoid.

### Mixkit

The Sound Effects Free License permits commercial use with no
attribution, but users must not "make Mixkit or any item available to
any third party", nor sell copies "without first altering them by
applying human skill and effort, and incorporating other elements."
Loose files fail. Avoid.

### 99Sounds

"You do not have the right to sell or redistribute (even for free) the
audio files ... on their own or as a separate attachment." Game use is
allowed, but loose files in a mod are separate attachments. Avoid.

### Soundly and Pro Sound Effects

- **Soundly** (EULA of 11 November 2024): its free tier gives 2,000
  sounds, cleared for games. But the EULA bans use "in any way that is
  intended to allow third parties to download ... the Licensed Works ...
  on a standalone basis". It also says that for a game, only versions
  made during the licence term are covered, and you "must exclude the
  Licensed Works if you create any new major version of that video game
  after the term." That is a poor fit for a mod updated for years.
  Avoid.
- **Pro Sound Effects:** its free-samples page returned 404; it is
  commercial, and its standard EULA is not read here (unverified).
  Likely the same standalone ban.

### Internet Archive (Public-Domain 1950s Material)

- **Prelinger films** (ephemeral films 1927 to 1987) carry a Creative
  Commons public-domain dedication. But their soundtracks are narration
  and library music, not clean foley, and the music compositions may
  still be in copyright. A projector's own noise is not in the film.
- **1950s sound-effects records are not public domain** in the United
  States. Under the Music Modernization Act, recordings published in
  1947 to 1956 are protected for 110 years, and later pre-1972 ones
  until 15 February 2067. A search of IA audio for "projector" returned
  1,117 mostly unrelated items, many with no licence field.
- **Use:** as a listening reference for band limit, hiss and room tone,
  not as a source.

## Procedural Tools and Pure Synthesis

- **sfxr** (MIT code), **jsfxr** (Unlicense), **Bfxr/bfxr2** (MIT) and
  **ChipTone** ("All sounds created using ChipTone are FREE to use for
  any purpose, commercial or otherwise, under the CC0 licence") all
  leave output to the user. Their sound is chiptune: wrong period for
  this motif, and already covered by `make_sounds.py`.
- **Synthesis does well:** exact tones (DTMF, TTMF, beeps, the filmstrip
  chime), alarms (klaxon from a filtered saw, siren sweeps, an electric
  bell from inharmonic partials with a striker buzz), wind (band-passed
  pink noise with slow modulation is the classic success case), the
  rising whistle, sub-bass thumps, glass-harmonica tones (near-sine
  partials, slow swell, gentle beating), and the theremin.
- **Synthesis does poorly:** the projector's clatter (claw, sprockets,
  fan and motor together; a 24 Hz click train with hum sounds like a
  toy), the raspberry (a buzzy saw reads as a synth fart, not a mouth),
  hinge creaks (stick-slip friction), slams and their rattles, and a
  piston engine at close range. Explosions are half-way: a synthesized
  sub works, but the crackle and rolling debris of a real tail sell it.

## Licence Questions from the Brief

- **Is a free Workshop mod "commercial"?** CC BY-NC defines
  NonCommercial as "not primarily intended for or directed towards
  commercial advantage or monetary compensation", which a free mod
  probably meets. Service terms are not CC, though, and a donation link
  or a fork could change the answer. Treat the mod as commercial and
  avoid non-commercial grants (free AI tiers, CC BY-NC, BBC).
- **Can a credits file satisfy attribution?** For CC BY 4.0, yes: one
  "may satisfy the conditions in any reasonable manner based on the
  medium, means, and context" (section 3(a)(2)). A `Sounds/CREDITS.txt`
  plus a line in the Workshop description is reasonable. Zapsplat names
  "documentation" as acceptable. Free ElevenLabs output is not
  satisfiable this way, since it requires the credit in the title.
- **The repository problem.** The root `LICENSE` is MIT and covers
  everything not marked otherwise. A third-party file under a
  no-standalone licence committed there invites the reading that it is
  MIT. CC0 files and our own outputs can be committed; anything else
  needs a carve-out and still risks "file sharing".
- **Content ID.** The risk for effects is low. AI services do not
  register outputs. CC0 effects are rarely registered, but anyone can
  upload anything. Prefer well-established CC0 uploads, and keep the
  manifest so a false claim can be disputed.
- **AI output and copyright.** Purely generated audio may not be
  copyrightable in the United States, so others could reuse it. That
  matters little for a free mod, but it means we cannot license it
  exclusively.

## Recommendations, Item by Item

Costs assume the $0 baseline. "Local SA3" is Stable Audio 3 Small SFX
on the operator's GPU.

| Effect | Best source | Fallback | Cost |
|--------|-------------|----------|------|
| Projector start | CC0 recording, treated | Local SA3 | $0 |
| Projector run-out | Same take (411981) | Local SA3 | $0 |
| Filmstrip chime | Synthesis | — | $0 |
| Wind loop | Synthesis | CC0 wind layer | $0 |
| Deep boom | Synth sub + CC0 tail | Local SA3 tail | $0 |
| Raspberry ×3 | Own or CC0 recording | Local SA3 | $0 |
| Implosion pull | Synthesis + CC0 rumble | Local SA3 rumble | $0 |
| Mirror shimmer | Synthesis | CC0 wine-glass layer | $0 |
| Signal tones | Synthesis | — | $0 |
| Keypad | Synth DTMF + CC0 click | Kenney click | $0 |
| Locker hinge, slam | CC0 recordings | Local SA3, Firefly | $0 |
| Alarms | Synthesis | CC0 bell layer | $0 |
| Blimp engine | CC0 prop-plane loop | Local SA3 | $0 |
| Rimshot (comedy) | CC0 drum hit | Synthesis | $0 |

Notes on the less obvious choices:

- **Projector.** One CC0 take holds all three phases at 48 kHz. Cut the
  1.5 s start, a whir bed and the 1.5 s run-out, band-limit to
  `PERIOD_BAND`, and add hiss. An 8 mm machine is lighter than a 1950s
  classroom 16 mm one: pitch it down a few percent or layer espectral's
  16 mm take.
- **Raspberry.** The funniest and most original source is the operator
  blowing three raspberries into a phone, which he owns outright (rule
  8, original only, satisfied). Otherwise cut three variants from
  craigsmith's "Vintage Voices" take. Synthesis is the weakest option
  here.
- **Deep boom.** Rule 5 (survive a laptop speaker) favours a recorded
  tail, whose harmonics carry. Synthesize the 55 Hz body and mix a CC0
  fireball's rolling decay above it.
- **Implosion pull.** Its pitch maps to age in the SoundDef, so the
  loop must be pitch-steady. Synthesize the whistle; take the rumble
  from synthesis or a CC0 recording.
- **Shimmer.** The theremin glint must be synthesized, since it is the
  batshittium rule. A wet-finger wine glass adds the real "bowed"
  friction under the partials.
- **AI's role:** variants (3 to 5 per noisy effect, per the pipeline),
  and any item where CC0 search fails. Use Local SA3 first, for $0 and
  owned output. Firefly Standard ($9.99) is next, if its product terms
  read clean. ElevenLabs Starter ($6) only with written permission for
  loose files.

## Budget

| Spend | Buys | Effects (6 tries each) |
|-------|------|------------------------|
| $0: synthesis + CC0 | Everything above | All |
| $0: Local SA3 | Unlimited tries | All gaps |
| $5: fal.ai SA3 | ~175 generations | ~29 |
| $6: ElevenLabs Starter | 150 to 500 clicks ×4 | ~25 to 80 |
| $9.99: Firefly Standard | 200 generations | ~33 |
| $10: Stability API | ~38 generations | ~6 |
| $11: ElevenLabs Creator | 605 clicks ×4 | ~100 |

Recommendation: spend nothing on effects at first. Hold $10 in reserve
for one month of one service if the listening review finds gaps, and
put $40 to $50 toward music. If the music research picks ElevenLabs and
ElevenLabs permits loose effect files in writing, one Creator month
($11) could serve both.

## Consequences for the Sound Pipeline

- **Goals and motif change.** `SOUND-PIPELINE.md` says "Fully
  synthesized: no samples from anywhere", and its proposed motif rule 8
  says "No samples". Mixing in recordings needs Matt "Breakpoint"
  Heck's decision, and an update to `docs/ARTISTIC-MOTIF.md` in the same
  change. A suitable wording: originals only; CC0 or our own
  recordings, and AI output we own, always processed through
  `make_sounds.py`. This extends open question 12 ("future sample CC0
  only").
- **Committed sources keep renders reproducible.** Put the CC0 and
  owned sources under, say, `Tools/sound_sources/`, with a manifest
  (URL, uploader, licence, date fetched, SHA-256). Then "a fresh render
  equals `Sounds/`" still holds, and `Sounds/CREDITS.txt` can be built
  from the manifest.
- **Import with ffmpeg, once.** Convert each source to 48 kHz PCM WAV
  before committing. Python 3.12's `wave` reads integer PCM only (the
  projector take is 32-bit), and MP3 decoding is not bit-identical
  across decoders. Sources are inputs only; outputs stay at most 3 s and
  under the 300 KiB streaming threshold.
- **Trim the sources.** Commit only the excerpt used (a few seconds,
  not 14 MB takes), to respect the 5 MB budget's spirit.
- **Disclosure.** If any AI output ships, say so in `Sounds/CREDITS.txt`
  and on the Workshop page.

## Open Points

- ElevenLabs: whether loose effect files in a mod and a public repository
  are allowed. Only ElevenLabs can answer, in writing. This is a
  request for Matt "Breakpoint" Heck to send, if ElevenLabs is wanted.
- Adobe's Generative AI product-specific terms (23 April 2026): check
  for a standalone clause before buying Firefly.
- Stable Audio web credits per generation, and the Stability API price
  of 26 credits: unverified.
- The ElevenLabs Starter "$1 first month" appears on the API pricing
  page only; treat it as promotional.
- BBC RemArc wording, and Pro Sound Effects' free packs: not read.

## Sources

All read on 2026-10-15.

- ElevenLabs pricing: https://elevenlabs.io/pricing
- ElevenLabs API pricing: https://elevenlabs.io/pricing/api
- ElevenLabs sound effects docs:
  https://elevenlabs.io/docs/overview/capabilities/sound-effects
- ElevenLabs sound generation API reference:
  https://elevenlabs.io/docs/api-reference/text-to-sound-effects/convert
- ElevenLabs playground (four variations, WAV 48 kHz):
  https://elevenlabs.io/docs/eleven-creative/playground/sound-effects/llms.txt
- ElevenLabs cost help article:
  https://elevenlabs.io/docs/help-center/product/content-production/sound-effects/how-much-does-it-cost-to-generate-sound-effects
- ElevenLabs publishing help article:
  https://elevenlabs.io/docs/help-center/legal/can-i-publish-the-content-i-generate-on-the-platform
- ElevenLabs Terms of Service (9 October 2026):
  https://elevenlabs.io/terms-of-use
- ElevenCreative Terms, Exhibit 4 (9 October 2026):
  https://elevenlabs.io/sound-effects-terms
- ElevenAPI Terms (9 October 2026): https://elevenlabs.io/elevenapi-terms
- The Decoder on SFX v2:
  https://the-decoder.com/elevenlabs-releases-version-2-of-its-ai-sound-effects-model-with-longer-clips-and-better-audio-quality/
- Stability AI, Stable Audio 3 announcement:
  https://stability.ai/news-updates/meet-stable-audio-3-the-model-family-built-for-artistic-experimentation-with-open-weight-models
- Stability AI explainer (9 September 2026):
  https://stability.ai/explainers/stable-audio-vs-competitors-licensing-export-rights-and-self-hosting-compared
- Stability AI knowledge base, plans:
  https://kb.stability.ai/knowledge-base/stable-audio-subscription-plans-and-credit-system
- Stability AI Community License:
  https://stability.ai/community-license-agreement
- Stability AI Terms of Service (30 September 2026):
  https://stability.ai/terms-of-service
- Hugging Face, Small SFX card and API metadata:
  https://huggingface.co/stabilityai/stable-audio-3-small-sfx
- Mirrored Small SFX base config (44.1 kHz, stereo):
  https://huggingface.co/cocktailpeanut/stable-audio-3-small-sfx-base
- fal.ai, Stable Audio 3 Small SFX:
  https://fal.ai/models/fal-ai/stable-audio-3/small/sfx/base/text-to-audio
- The Rundown, Stable Audio 3 (26 credits; secondary):
  https://www.therundown.ai/tools/stable-audio-3-0
- Art in Tech, Stable Audio 3 in five figures:
  https://artintech.substack.com/p/stable-audio-3-explained-in-5-figures
- AI Video Sensei, local setup:
  https://aivideosensei.com/guides/stable-audio-3-local-setup
- Dubspot review (26 May 2026):
  https://blog.dubspot.com/stable-audio-3-review
- Amrytt review (9 July 2026): https://amrytt.com/stable-audio-review/
- Adobe Firefly plans: https://www.adobe.com/products/firefly/plans.html
- Adobe Firefly effects page:
  https://www.adobe.com/ca/products/firefly/features/sound-effect-generator.html
- Adobe Generative AI User Guidelines (15 May 2026):
  https://www.adobe.com/legal/licenses-terms/adobe-gen-ai-user-guidelines.html
- Adobe community, reselling Firefly effects (25 September 2026):
  https://community.adobe.com/questions-404/can-i-sell-adobe-firefly-generated-sound-effects-as-an-asset-pack-1643734
- iTWire on Firefly audio (21 August 2026):
  https://itwire.com/business-it-news/business-software/adobe-makes-firefly-a-full-audio-studio-and-the-licence-is-the-actual-product
- Google Lyria 3.5 docs:
  https://ai.google.dev/gemini-api/docs/music-generation
- Suno Sounds help: https://help.suno.com/en/articles/10625537
- Krotos Studio pricing: https://krotos.studio/pricing
- Cinevva, AI SFX for games (5 September 2026):
  https://app.cinevva.com/guides/ai-sound-effect-generators
- Undetectr, AI SFX (25 September 2026):
  https://undetectr.com/blog/ai-sound-effect-generator
- Sonilo comparison (19 July 2026; vendor, partly wrong):
  https://sonilo.com/blog/comparisons/video-to-sound-effects-api-comparison-2026
- MMAudio weights licence (mirror card):
  https://huggingface.co/AEmotionStudio/mmaudio-models/blob/main/README.md
- Freesound FAQ: https://freesound.org/help/faq/
- Freesound Terms of Use: https://freesound.org/help/tos_web/
- Freesound searches (CC0 filter), for example:
  https://freesound.org/search/?q=film+projector&f=license%3A%22Creative+Commons+0%22
- Freesound sound 411981:
  https://freesound.org/people/Stefan021/sounds/411981/
- Sonniss bundle page: https://sonniss.com/gameaudiogdc
- Sonniss licence 2.0 (27 August 2026):
  https://sonniss.com/gdc-bundle-license/
- Rekkerd on GDC 2026 bundle:
  https://rekkerd.org/sonniss-releases-gdc-2026-game-audio-bundle/
- Kenney support (licence): https://kenney.nl/support
- Kenney audio packs: https://kenney.nl/assets/category:Audio
- OpenGameArt FAQ: https://opengameart.org/content/faq
- OpenGameArt advanced search: https://opengameart.org/art-search-advanced
- Pixabay licence summary: https://pixabay.com/service/license-summary/
- Pixabay terms (18 November 2024): https://pixabay.com/service/terms/
- Zapsplat standard licence:
  https://www.zapsplat.com/license-type/standard-license/
- Zapsplat licensing guide:
  https://zapsplat.com/copyright-and-audio-licensing-for-beginners
- Mixkit licence: https://mixkit.co/license/
- Mixkit LLM info page: https://mixkit.co/llm-info/
- 99Sounds licence: https://99sounds.org/license/
- Soundly FAQ, games:
  https://getsoundly.com/faq/can-i-use-the-sounds-in-a-video-game
- Soundly EULA (11 November 2024):
  https://getsoundly.com/assets/Soundly-EULA.pdf
- BBC RemArc coverage (secondary):
  https://electronicgroove.com/bbc-expands-sound-effect-library-to-33000-free-samples/
- US Copyright Office, pre-1972 recordings:
  https://copyright.gov/music-modernization/pre1972-soundrecordings/
- Open Culture on Prelinger reuse:
  https://www.openculture.com/2016/05/download-6600-free-films-from-the-prelinger-archives-and-use-them-however-you-like.html
- Internet Archive search API (projector, audio):
  https://archive.org/advancedsearch.php
- CC BY 4.0 legal code:
  https://creativecommons.org/licenses/by/4.0/legalcode.en
- CC BY-NC 4.0 legal code:
  https://creativecommons.org/licenses/by-nc/4.0/legalcode.en
- sfxr: https://www.drpetter.se/project_sfxr.html
- jsfxr: https://github.com/chr15m/jsfxr
- bfxr2: https://github.com/increpare/bfxr2
- ChipTone: https://sfbgames.itch.io/chiptone
- Tencent HunyuanVideo licence territory (commit note):
  https://huggingface.co/tencent/HunyuanVideo/commit/a4ed25a99d7eb9cf21e12f5fd8052d266f2105fc
