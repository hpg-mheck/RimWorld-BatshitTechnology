# Hosted AI Music Generation Services (October 2026)

Phase 5000, Slice 1000 research. Desk research only, read on 2026-10-15.
Nothing was signed up for, bought or downloaded. Prices are in US dollars
unless marked, exclude sales tax or VAT, and come from the page and date
in the Sources list. "Unverified" means no primary (vendor) source was
found or the vendor page would not render. Near the end of this research
the session's web-search budget ran out, so a few cross-checks rest on a
single secondary source; they are marked.

## Summary

- **Best value and fully scriptable: Google Lyria through the Gemini
  API.** $0.08 a full song (Lyria 3.5) or $0.04 a 30 s clip (Lyria 3
  Clip). The $10 minimum prepayment buys 125 songs or 250 clips. Google
  claims no ownership, sets no plan-by-plan restrictions and asks for
  no attribution. Output is 44.1 kHz stereo, with WAV available from
  Lyria 3.5. Every file carries an inaudible SynthID watermark.
- **Best for the sting and the loop seam: Stable Audio (Stability AI
  API, or fal.ai).** You set the length exactly, from 1 s up to 190 s
  (2.5) or 380 s (3.0). Output is WAV. Inpainting and audio-to-audio
  are available. A generation costs $0.20 (2.5), $0.26 (3.0 Large) or
  $0.04 (3.0 Medium on fal.ai). The training data is licensed (much of
  it production library music). Stability's terms assign you its
  rights in the outputs.
- **Best quality, but no API: Suno Pro, $10 for one month.** Suno v6
  is first on the October 2026 Artificial Analysis instrumental arena.
  A month gives 20 downloads, and commercial rights to downloaded
  songs are perpetual after cancelling. Against it: no public API,
  stings are impractical, and UMG and Sony's training-data lawsuit
  still appears to be pending.
- **ElevenLabs Eleven Music** (Starter, $6 a month, with API access)
  controls exact lengths best: 3 s to 10 min, section durations and a
  forced-instrumental flag. Its Music Terms bring three problems:
  "Studio Games" are excluded, a "Music Library" clause forbids making
  the output available for others to reuse, and Starter downloads are
  MP3 only. It is worth buying only if the sound-effects plan also
  uses ElevenLabs.
- **Udio is out:** downloads have been disabled since 29 to 30 October
  2025. **AIVA, Soundraw, Beatoven, Mubert and Loudly** keep the
  copyright or grant licences that cannot be passed on, so the files
  could not go under the mod's MIT terms and players' videos are not
  covered. AIVA's ownership tier (Pro, €49 a month) is also over the
  budget.

Suggested spend: Gemini API $10 and Stability API $10, so $20 for all the
music, leaving $30 for sound effects. Add ElevenLabs Starter ($6) only if
it also serves the effects, and Suno Pro ($10, one month) only as a
fallback for the disco.

## The Licence Question That Decides Most of This

The brief needs files that ship inside a free mod, worldwide and forever,
and that players can stream. Three tests separate the services:

1. **Who owns the output?** If the service assigns or disclaims
   ownership (Suno paid, Stable Audio, Google), the developer can give
   the files to players under the mod's terms. Open question 12 in
   `docs/SOUND-PIPELINE.md` proposes MIT, and MIT is possible only on
   these services. If the vendor keeps the copyright (AIVA below Pro,
   Soundraw, Beatoven, Mubert, probably Loudly), the licence is
   personal and non-sublicensable. Players' monetized videos are then
   not licensed, even though these vendors forbid Content ID
   registration and so make claims unlikely.
2. **Does it survive cancelling?** Suno, ElevenLabs, Stable Audio
   (paid), AIVA Pro, Soundraw and Beatoven say yes, for files made or
   downloaded while subscribed. Mubert is the exception: published
   projects stay, but old tracks may not be used in new projects after
   cancelling, which is awkward for a mod that keeps shipping updates.
   Loudly users report losing rights at cancellation (unverified).
3. **Is a free Workshop mod "commercial", or a "game"?** The mod is
   not sold or monetized. The safer reading is still to buy a paid
   tier wherever the free tier says non-commercial, because players
   monetize the videos they make with it. ElevenLabs defines "Studio
   Game" by monetization and "Indie Game" as one with under $500,000
   revenue. A free mod is plainly not the first and arguably the
   second, but this is an interpretation, not a quotation.

US law adds a twist. The Copyright Office's report on copyrightability
(Part 2, 29 January 2025) treats purely prompt-generated material as
lacking human authorship. Unedited AI music may therefore have no
copyright for anyone to own or register. That is good for players,
since nobody can legitimately claim it. It does not stop bad actors
from registering ripped files with Content ID.

## Service by Service

### Suno (v6, September 2026)

- **Plans** (suno.com/pricing, read 2026-10-15):
  - Free: 50 credits a day and the v6-mini model only. No downloads.
    The FAQ mentions 7 lifetime trial downloads, for personal,
    non-commercial use only.
  - Pro: $10 a month billed monthly ($8 a month annually). 2,500
    credits, 20 downloads a month, commercial rights, all v6 models,
    and two kinds of stem separation.
  - Premier: $30 a month ($24 annually). 10,000 credits, 60 downloads
    and Suno Studio, which adds MIDI and multitrack export.
  - A generation costs 10 credits and returns two songs, so Pro buys
    about 500 songs a month (secondary source).
  - Extra downloads cost about $2.99 each, with no bulk discount
    (secondary source).
- **What counts as a download.** Each song counts once, whatever the
  format: MP3 and WAV, and all of its stems. Re-downloading is free and
  failed downloads do not count (Suno Help FAQ). Listening in the
  browser is unlimited, so you can audition many takes and download
  only the keepers.
- **Licence** (Terms, revised 10 August 2026, effective 3 September
  2026). On Pro and Premier, Suno "assigns to you all of its right,
  title and interest" in Output it owns. Commercial use requires the
  song to have been downloaded through an approved channel. The rights
  "are perpetual and are not affected by ... the expiry, cancellation,
  downgrade or suspension of your subscription". The free tier is
  personal and non-commercial. The terms contain no Content ID
  prohibition. Downloads carry C2PA Content Credentials naming Suno as
  the source.
- **Controls.** Instrumental mode; songs up to 8 minutes; extend;
  section editing in plain language (v6); audio, image and video
  references; stems; WAV on Pro; MIDI only in Studio (Premier). No
  exact-length control, no native loops, no exact BPM. Tempo can only
  be requested in the prompt.
- **Quality.**
  - Suno v6 is first on Artificial Analysis Music Arena v1.1, on the
    instrumental board (Elo 1140) and the vocal board, after a
    blind-panel rebuild with 1,000 prompts (reported 7 October 2026).
  - Reviews of v6 are split. Some call it "muffled, dull and strangely
    lifeless"; others hear cleaner mixes. v6 fixed v5.5's "shimmer"
    (rattling highs) but is "more commercially templated" (eesel.ai,
    BoingBoing headline).
  - Disco is a core Suno genre with many published prompts. I found no
    listening report on a theremin lead specifically.
  - A 2.5 s sting is impractical. You would cut it from a full song
    and lose the natural envelope.
- **API.** None public. An invite-only partner programme was announced
  in July 2026 (search summary). Unofficial wrappers breach the terms
  and carry no commercial rights, so Claude agents cannot drive Suno.
- **Risk.** Warner settled in November 2025, and v6 was built "with ...
  Warner Music Group, BMG and Believe". As of 26 May 2026 UMG and Sony
  were still suing, seeking to add 61,000 recordings found in Suno's
  training data (Music Business Worldwide). Suno's terms protect the
  user's own rights, but there is reputational and legal tail risk.

### Udio

- Downloads of audio, video and stems have been disabled on every plan
  since the UMG settlement of 29 to 30 October 2025. A 48-hour export
  window closed on 5 November 2025. The promised licensed platform had
  not launched by the end of September 2026 (aitools-directory, 5
  October 2026). **Not usable:** files cannot leave the service.

### ElevenLabs Eleven Music (v2 May 2026, v2.5 11 September 2026)

- **Plans** (elevenlabs.io/pricing and /pricing/api):
  - Starter $6, Creator $22, Pro $99 a month.
  - Music costs 900 credits a minute in the app; the API page says
    $0.15 a minute.
  - Monthly music allowance on Starter: the API page says 40 minutes,
    but the Music Terms' Exhibit A says 17 minutes of generation and 30
    minutes of downloads. Treat 17 minutes as the binding figure.
  - fal.ai resells Eleven Music at $0.80 a minute.
- **Music Terms** (updated 9 October 2026), Exhibit A:
  - Eligibility: Free to Pro are "For Individual Use Only".
  - Downloads: "Not permitted" on Free; Starter 30 min a month.
  - Media rights, Free to Business: "All online and offline commercial
    use permitted, except film, TV, radio, and Studio Games".
  - "Music Library Creation and Distribution": prohibited below
    Enterprise. Section 4.8(f) defines a Music Library as any
    collection of Output "made available to third parties for those
    third parties to use, license, download, or incorporate into their
    content, whether or not for payment".
  - Attribution: required on Free ("Created in collaboration with
    ElevenLabs" or similar); not required on Starter and above.
  - High-quality (lossless) downloads: Pro and above only.
  - API access: Starter and above.
  - Output "remains subject to the plan in effect when that Output was
    created", so rights survive a downgrade.
- **What this means for the mod.**
  - The game clause is probably satisfied: a free mod is not monetized,
    and it qualifies as an Indie Game by revenue (my reading).
  - The Music Library clause rules out putting the tracks under MIT or
    CC0, or licensing them for players to reuse. The files would need
    an "all rights reserved, for use with this mod" notice, which
    conflicts with open question 12.
  - Even a plain promise to players that they may use the music in
    their videos is a grey area worth asking ElevenLabs about.
- **ElevenMusic app** (separate consumer app, Music v2.5 announcement of
  11 September 2026):
  - Free: "five lossless downloads a day", usable commercially with
    credit to ElevenMusic.
  - Pro: $9.99 a month in the US App Store (secondary), 400 lossless
    downloads a month.
  - "Rights attach the moment you create a track, and cancelling or
    downgrading doesn't change what's yours."
  - This free tier contradicts Exhibit A ("Free: downloads Not
    permitted"); the app may run under different terms. Unverified: do
    not rely on it without reading the in-app terms.
- **Controls** (API documentation):
  - `music_length_ms` from 3,000 to 600,000 ms, so the minimum is 3 s.
  - `force_instrumental`.
  - Composition plans with per-section styles and durations (always
    enforced on v2 and v2.5).
  - Models `music_v1`, `music_v2` and `music_v2_5`.
  - Output formats: MP3 at 128 kbps on Starter (192 kbps from Creator),
    PCM 44.1 or 48 kHz from Pro, plus Opus.
  - `store_for_inpainting`, stems and C2PA signing. The API page lists
    "in-painting access" under Enterprise, so section inpainting may be
    Enterprise-only through the API (unverified).
- **Quality.** Ranked 8th on the Artificial Analysis instrumental board
  (Elo 1063) for v2. v2.5 claims gains in "orchestral and cinematic"
  music. An April 2026 roundup found it good for ambient and corporate
  beds and literal in following prompts, but behind Suno and Udio for
  orchestral depth. Training is licensed (Merlin, Kobalt; later
  Believe and UMG).
- **Content ID.** The terms say nothing on registration. Licensed
  training lowers the risk of matching existing records.

### Google Lyria (Gemini API, Vertex AI, Flow Music)

- **Gemini API pricing** (pricing page updated 2026-10-15). There is no
  free tier for Lyria.
  - Lyria 3.5: $0.08 a song (launched 4 September 2026).
  - Lyria 3 Pro Preview (legacy): $0.08.
  - Lyria 3 Clip Preview: $0.04 for a 30 s clip.
  - New accounts must prepay at least $10. Credits expire after 12
    months. These billing rules come from search summaries (not
    re-read on Google's billing page), so they are unverified.
- **Controls** (music-generation guide):
  - Lyria 3.5 makes songs "a couple of minutes" long, with structure
    set by timestamps such as "[0:00 - 0:10] Intro".
  - "Instrumental only, no vocals" is set in the prompt, and BPM, key
    and instrumentation are prompt hints only.
  - Up to 10 reference images.
  - Output is 44.1 kHz stereo MP3, or WAV via `response_format` on
    Lyria 3.5. Clip is MP3 only and "optimized for ... short musical
    clips, loops, and previews".
  - Generation is single-turn: no editing, extend or inpainting.
  - Every file carries an inaudible SynthID watermark. Prompts naming
    artists or copyrighted lyrics are blocked.
- **Lyria RealTime** (`lyria-realtime-exp`, experimental):
  - Steerable: BPM 60 to 200, key and scale, density, brightness, and
    muting of bass or drums.
  - 48 kHz stereo 16-bit PCM; instrumental only; watermarked.
  - Price not in the pricing table (unverified).
  - A locked tempo makes capturing a loop easy, but its quality is
    Lyria 2 class. (The CC BY 4.0 notice on that page covers the
    documentation, not the outputs.)
- **Vertex AI `lyria-002`.** Instrumental 30 s clips, 48 kHz WAV,
  negative prompts and seeds. About $0.06 for 30 s (CometAPI, September
  2026; unverified). fal.ai resells Lyria 2 at $0.10 and Lyria 3 Pro at
  $0.08.
- **Terms** (Gemini API Additional Terms, effective 23 March 2026):
  "Google won't claim ownership over that content", and "Google may
  generate the same or similar content for others". On paid services
  Google does not use prompts or responses to improve products. There
  are no tiered media restrictions and no attribution beyond what the
  law requires. I found no Google indemnity for individual API users
  (unverified).
- **Flow Music** (flowmusic.app):
  - Google bought ProducerAI (formerly Riffusion) in February 2026 and
    rebranded it in April 2026. It has run Lyria 3.5 since 29 July
    2026. MusicFX and MusicFX DJ closed on 31 July 2026.
  - Pricing page: Free (daily credits); Starter $6 a month for 3,000
    credits (about 600 songs); Plus $18; Member $48.
  - Downloads are "mp3, wav, m4a" plus stems on every plan, with no
    download cap. It also offers BPM controls and upload-and-remix.
  - Google lists commercial-use rights as a paid-tier benefit. The free
    tier is unclear (secondary sources). The terms link redirects to
    Google's general Terms of Service.
  - No public API was found.
- **Quality.**
  - Lyria 3 Pro and 3.5 sit at about Elo 1076 to 1078 on the
    instrumental arena (3rd to 6th, statistically tied with Mureka).
  - r/SunoAI users praised the audio quality, but said that "3/4th of
    the times it just adds vocals" despite instrumental prompts, that
    it sounds "plain", like "Muzak", and that "it takes work to make
    it actually follow a prompt".
  - For 1950s library music, Muzak-like competence is close to the
    target. For the disco, prompt adherence is the risk.

### Stability AI Stable Audio (2.5 hosted; 3.0 from 20 May 2026)

- **Web app** (stableaudio.com). Stability's own explainer (9 September
  2026) lists Solo $12 a month (660 credits), Session $30 (1,800),
  Producer $90 and Studio $199, plus a free tier of 50 credits a month.
  Credits per track are not published, and the pricing page would not
  render. Older reviews list Pro $11.99 for 250 tracks; that may be a
  previous plan set. The free tier is described as non-commercial.
- **API.**
  - Stability platform: $10 buys 1,000 credits. Stable Audio 2.5 costs
    20 credits ($0.20) and 3.0 costs 26 ($0.26). New accounts get 25
    free credits (secondary sources).
  - fal.ai: SA 2.5 $0.20 for text-to-audio and $0.20 for inpaint; SA
    3.0 Medium $0.0376 a generation, 1 to 380 s, WAV, FLAC or OGG.
- **Controls.**
  - Exact duration: 1 to 190 s (2.5) or 1 to 380 s (3.0).
  - Seed, steps and guidance; audio-to-audio.
  - **Inpainting** with `mask_start` and `mask_end` from 0 to 190 s.
  - WAV at 44.1 kHz; experimental stems in the web app.
  - Instrumental only, no vocals.
- **Licence.** Stability's Terms (effective 30 September 2026): "we
  assign to you all of our right, title, and interest (if any) in the
  Outputs". Paid web-app tiers grant commercial use and survive
  cancellation (Stability's explainer and reviews). The 3.0 Small and
  Medium open weights are under the Community License, which is free
  below $1M a year in revenue. Training is "fully licensed": AudioSparx
  production-library audio and CC-licensed Freesound.
- **Quality.**
  - Strong on "cinematic and atmospheric material", loops, beds and
    sound effects.
  - Rhythmic genres can be "correct without feeling interesting".
  - "Without a BPM the rhythm wanders. A number anchors it."
  - SA 3.0 Medium scores Elo 998 on the instrumental arena, about Lyria
    2 level and well below Suno and Lyria 3.
  - Its library-music training data suits the 1950s library brief and
    orchestral hits; it is weaker for a convincing disco groove.
- **Content ID.** No registration rule was found. Licensed training
  lowers the risk of matching existing records.

### AIVA

- **Plans.** Free: 3 downloads a month (MP3 and MIDI); AIVA owns the
  copyright; non-commercial; credit to AIVA required. Standard: €15 a
  month (€11 annually); 15 downloads; AIVA owns the copyright;
  monetization "only on YouTube, Twitch, TikTok and Instagram". Pro:
  €49 a month (€33 annually); 300 downloads; WAV; "Copyright owned by
  YOU", and the ownership lasts "forever".
- **Licence.** The licence agreement (undated) ends the rights
  automatically on breach, after which users must delete compositions.
  It does not address cancellation, though the help pages say Pro
  ownership is forever. The non-commercial licence lets users
  "distribute ... in any Content that the Licensee holds rights over",
  which might cover a free mod with credit. Players' videos would still
  be unlicensed.
- **Controls and quality.** Style presets, influences, key and tempo;
  tracks of 3 to 5.5 minutes; **MIDI export on every tier**. Strongest
  at orchestral and cinematic music. Pro is the only clean tier, and
  at about $57 a month it is over the whole budget. The MIDI could be
  re-voiced through `make_sounds.py`, but under Free or Standard AIVA
  still owns the composition. No public API was found (unverified).

### Soundraw

- **Plans.** Creator $16.99 a month ($11.04 annually), unlimited
  downloads, MP3 only. Artist Pro $35.99 adds WAV and stems; Artist
  Unlimited is $50. API access is Enterprise only.
- **Licence.** Terms (updated 12 June 2025): a "non-exclusive,
  non-transferable, and non-sublicensable" licence. "Selling,
  distributing without charge, or granting a sublicense" is forbidden
  without consent. Registering any song with Content ID is prohibited
  on every plan. The Creator plan allows background music in games and
  apps; rhythm games must contact support. Downloads stay licensed
  after cancelling. Soundraw keeps the master.
- **Fit.** You choose genre, mood, length, BPM and instruments rather
  than writing a prompt, and the output is built from in-house material.
  I expect no theremin and no 1950s library idiom (unverified). Shipping
  extractable files for free distribution sits uneasily with "distributing
  without charge". Not recommended.

### Beatoven.ai

- **Plans.** Creator $10 a month (30 download-minutes); Visionary $20
  (60); pay as you go at $3 a download-minute. The free tier is
  preview-only (Costbench, checked 23 August 2026).
- **Licence.** A "non-exclusive perpetual licence for the generated and
  downloaded track". Beatoven keeps the copyright. Games are listed as
  permitted, streaming distribution is not, Content ID registration is
  not allowed, and Beatoven offers help clearing claims. It is Fairly
  Trained certified.
- **Fit.** Its Maestro model aims at background music; it also offers
  text-to-SFX, stems and loops. It is not built for period pastiche.
  The licence cannot be passed to players, and the API is
  enterprise-only. Low priority.

### Mubert

- **Plans.** Ambassador (free): 25 generations, MP3, attribution with
  a link and "#mubert". Creator $14 a month. **Pro $39 a month** is
  required for indie games; Business $199 for larger developers and
  apps.
- **Licence.** "All the copyright is owned by Mubert Inc." Tracks may
  not be sold, distributed commercially, streamed or registered with
  Content ID. After cancelling, published projects remain, but "You
  cannot use previously generated tracks in new projects".
- **Controls.** Track, **Loop** ("designed to loop without
  artifacts"), **Jingle** (up to 40 s) and Mix modes, set from 5 s to
  25 min. Generation is sample-based. An API is available on
  Business or API subscriptions.
- **Fit.** The Loop mode is attractive, but $39 for one month and the
  "new projects" rule (a mod keeps releasing updates) count against
  it.

### Loudly

- **Plans.** Personal $14.99 a month (300 downloads, 7-minute tracks);
  Pro $39.99. The game use is under Pro and Enterprise (Costbench, July
  2026).
- **Licence.** Governed by a separate Licence Agreement that I could
  not read. The Terms forbid claiming exclusive ownership or using
  Content ID against Loudly or other users. Users report YouTube claims
  even on paid plans and losing rights at cancellation (unverified,
  aggregated reviews). Avoid.

### Mureka (Kunlun)

- **Plans.** Basic $8 a month (400 songs, MP3); Pro $24 (WAV, stems).
  The API is per song, about $0.045 to $0.07 through aggregators;
  official pricing is unpublished.
- **Licence.** "Full commercial rights" are claimed from Basic. The
  training data is undisclosed.
- **Quality.** V9 and V9.5 are tied with Lyria for 3rd to 6th on the
  instrumental arena, and the API has a dedicated instrumental
  endpoint. The rights rest on the vendor's assurance alone, and its
  pricing pages failed to load. Possible, not preferred.

### Other Contenders Seen

- **MiniMax Music 2.6 and 3.0** cost $0.15 a generation on fal.ai. Its
  API documentation publishes no commercial-use terms (InVideo, August
  2026).
- **Sonilo** is trained on licensed Shutterstock music. It offers exact
  duration control at $0.0025 a second on fal.ai, but outputs m4a and
  is aimed at video scoring.
- **ACE-Step** costs $0.0002 a second hosted on fal.ai, and its weights
  are MIT-licensed. Quality is lower, and local use belongs to the
  local-models report.
- **CassetteAI** costs $0.02 a minute on fal.ai, with WAV output. I
  found no quality reports for this motif.

## Controllability Compared

- **Exact length:**
  - Stable Audio: 1 s and up.
  - ElevenLabs: 3 s and up, per section.
  - Lyria Clip is fixed at 30 s; Lyria 3.5 takes length from the
    prompt.
  - Mubert: 5 s and up.
  - Suno: none.
- **Locked tempo:** Lyria RealTime (60 to 200 BPM) and Mubert. Elsewhere
  BPM is a prompt hint, so measure the result and time-stretch it.
- **Seamless loops:** only Mubert's Loop mode claims them, and Lyria 3
  Clip is "optimized for ... loops". For the rest, make the loop by
  editing (see the plan below).
- **Inpainting and extend:** Stable Audio inpaint (API); ElevenLabs
  section inpainting (possibly Enterprise-only through the API); Suno
  section editing and extend (app only). Lyria has none.
- **Stems:** Suno Pro (two types), Flow Music (every plan), ElevenLabs
  self-serve, Soundraw Artist Pro, Mureka Pro, Stable Audio web app
  (experimental).
- **MIDI:** AIVA (every tier) and Suno Studio (Premier).
- **Lossless output:** Lyria 3.5 WAV, Stable Audio WAV, Flow Music WAV,
  Suno Pro WAV. ElevenLabs offers lossless only from Pro ($99).

## Quality for This Motif

The evidence specific to this motif is thin. I found **no listening
report** on a theremin lead or 1950s educational-film library music from
any service. Plan a cheap bake-off rather than trusting any ranking.

- **Disco at 120 BPM:** Suno v6 is the best bet: it is first on the
  arena and disco is a mainstream genre for it. Lyria 3.5 and Mureka
  follow. Stable Audio may sound stiff in rhythmic genres.
- **Theremin hook:** every model risks rendering it as a generic synth
  lead or a whistle. The motif's own rules (`THEREMIN_VIB`, 5.5 Hz,
  ±30 cents) argue for **synthesizing the theremin in
  `make_sounds.py` and laying it over an AI-made bed with no lead**.
  That keeps rule 1 exact, makes the track more distinctive, and
  lowers the chance of fingerprint collisions.
- **2.5 s orchestral sting:** Stable Audio (exact 3 to 4 s takes, WAV,
  library-music training) is best. ElevenLabs can do 3 s at minimum.
  Lyria Clip and Suno would need trimming from longer music, losing the
  natural decay. A sound-effects generator may also handle a musical
  stinger well; compare against the effects report.
- **1950s library theme and lounge loop:** Lyria's "Muzak" tendency and
  Stable Audio's library training both point the right way. ElevenLabs
  is described as good at "corporate" beds. AIVA is strong at
  orchestral writing but blocked on licence and price.
- **Known artifacts:**
  - Suno v5.5's "shimmer" (largely fixed in v6, which may sound duller).
  - Lyria adds vocals unasked.
  - ElevenLabs Starter is limited to MP3 at 128 kbps.
  - All of them: tempo drift and reverb tails that block clean loops.

## Content ID Risk

- No service found **registers users' outputs** with Content ID itself.
- **Forbid users from registering:** Soundraw, Mubert and Beatoven.
  Loudly forbids using Content ID against Loudly or its users.
- **Silent on registration:** Suno, ElevenLabs, Stable Audio and Google.
  Google does warn that "similar content" may be generated for others.
- **Practical risks:**
  - Someone rips the mod's tracks and registers them through a
    distributor.
  - Another user's similar AI output, once registered, matches ours.
  - Training-data echoes match a real record (the risk is lower with
    licensed training: ElevenLabs, Stable Audio, Beatoven).
- **Mitigations:**
  - Never register the tracks ourselves.
  - Keep generation records: prompts, seeds, dates, plan invoices and
    raw files under `.local/`.
  - State in `Sounds/CREDITS.txt` and on the Workshop page that the
    music is free for players to use in videos and streams.
  - Make each track distinctive: the code-made theremin, the tape
    treatment, an edited arrangement.

## API Availability (For Claude-Driven Generation)

- **Yes:**
  - Gemini API (Lyria 3.5, 3 Clip, RealTime).
  - Vertex AI (`lyria-002`).
  - ElevenLabs (Starter and above).
  - Stability API.
  - fal.ai, a single key for Stable Audio, Lyria, Eleven Music,
    MiniMax, Sonilo and ACE-Step. Rights then rest on fal's terms and
    each model's licence.
  - Mureka (pricing unpublished).
- **Enterprise or sales only:** Soundraw, Beatoven, Mubert (Business or
  API) and Loudly.
- **None:** Suno (invite-only partners; unofficial wrappers breach its
  terms), Udio, Flow Music and AIVA (the last two unverified).

## Ranked Shortlist for a $50 Total Budget

1. **Google Lyria via the Gemini API: $10 prepayment.** It buys 125
   Lyria 3.5 songs at $0.08, or 250 Lyria 3 Clips at $0.04, or a mix.
   It is scriptable, WAV from 3.5, with clean ownership and no tiers.
   Use it for the disco beds, the educational-film theme and the lounge
   loop. Weaknesses: unasked vocals, loose prompt adherence, no
   editing.
2. **Stable Audio through the Stability API: $10 for 1,000 credits.**
   That is 50 Stable Audio 2.5 generations, or 38 of 3.0 Large. fal.ai
   is an alternative with SA 3.0 Medium at about $0.04 a generation.
   Use it for the sting (exact 3 to 4 s WAV takes), for inpainting the
   loop seam, and for orchestral and library beds. Rights are assigned,
   training is licensed, everything is scriptable.
3. **ElevenLabs Starter: $6 for one month, only if the effects plan
   also uses ElevenLabs.** Precise section lengths at 120 BPM, a
   forced-instrumental flag, and music v2.5 with licensed training. The
   catches: MP3 at 128 kbps, about 17 minutes of generation, the Music
   Library clause (no MIT or CC0 for these files) and the game-clause
   interpretation.
4. **Fallback: Suno Pro, $10 for one month,** if the disco bake-off from
   1 to 3 falls short. It has the best groove quality, 20 downloads
   (enough for the keepers) and perpetual rights. It is browser-only:
   the operator drives it by hand, not agents.

Not recommended: Udio (no downloads), AIVA (ownership costs €49), and
Soundraw, Beatoven, Mubert and Loudly (vendor-owned, non-sublicensable
or game use behind a $39 tier).

## A Concrete Plan

**Budget:** Gemini $10 and Stability $10, so $20 committed, plus $6
(ElevenLabs) or $10 (Suno) held in reserve. That leaves $20 to $30 for
effects.

### Step 0: Free Preparation

- Write prompt sheets in `.local/scratch/sound5000/`. Avoid any show,
  composer or artist name: no "Star Trek", no composer of the 1967
  episode. Describe instead, for example: "1960s television orchestral
  sting, brass stab, string tremolo, timpani roll, ominous, vintage mono
  recording".
- Disco prompt: "Instrumental only, no vocals. 1977 disco, exactly 120
  BPM, A minor, four-on-the-floor kick, octave-bouncing bass, off-beat
  open hi-hats, string stabs, warm analog tape." Ask for **no lead
  melody** in half the takes, leaving room for the code-made theremin.
  In the other half, ask for "a theremin lead, wavering sine with wide
  vibrato".
- Set up a venv with numpy, soundfile and librosa (allowed by the
  brief) for beat tracking and seam analysis. Use ffmpeg to convert,
  stretch and encode.

### Step 1: Lyria ($10)

- Disco: 30 Lyria 3.5 songs, WAV, $2.40.
- 1950s library theme: 20 songs, $1.60.
- Lounge: 20 songs, $1.60.
- Sting candidates: 25 Lyria 3 Clips at $0.04 each, $1.00, prompted
  for "a single orchestral sting at the start, then silence".
- That is about $6.60, leaving about $3.40 for a second round on
  whatever comes closest.

### Step 2: Stable Audio ($10, 1,000 credits)

- Sting: 15 to 20 SA 2.5 takes at a duration of 3.5 s, $3 to $4.
- Loop seam inpainting: 5 to 10 passes, $1 to $2 (see below).
- Library-music beds or alternative disco beds: 15 to 20 takes, about
  $4.

### Step 3: Only If Needed

- ElevenLabs Starter: a composition plan of 40 bars at 120 BPM (80 s,
  exact), `force_instrumental`. About 10 takes of 90 s fit inside the
  17-minute cap.
- Or Suno Pro for one month, auditioning freely and downloading at most
  20 keepers.

### From Raw Generations to a 60 to 90 s Seamless Loop

1. **Choose by ear** the take with the steadiest full-arrangement
   section and no vocals. Convert it to 48 kHz float WAV.
2. **Measure the tempo** by beat-tracking the downbeats. If the tempo is
   not 120.0 BPM within ±0.1%, stretch it with ffmpeg's `rubberband`
   filter (or `atempo`) so that 1 bar is exactly 2.000 s, which is
   96,000 samples at 48 kHz. Re-check that the tempo does not drift
   across the section.
3. **Cut whole bars:** 32 bars (64 s), 40 (80 s) or 44 (88 s). Start on
   a downbeat in the steady part and end where the harmony returns to
   the opening chord. Make the loop a whole number of hook phrases: an
   8-bar hook fits 32 or 40 bars.
4. **Close the seam,** easiest method first:
   - **Overlap and crossfade.** Keep one bar beyond the cut. Mix that
     tail into the loop's first bar with an equal-power crossfade of
     one beat (0.5 s), so reverb and hi-hat tails carry across.
   - **Rotate and inpaint.** Rotate the loop by half its length, which
     moves the seam to the middle. Send it to Stable Audio inpaint with
     a mask of 2 to 4 s across the seam and the same style prompt. Then
     rotate it back. Listen for a garbled hook.
   - **Regenerate the last bars** (ElevenLabs or Suno section editing)
     so that they lead into bar 1.
5. **Treat it to the motif:** band-limit it to `PERIOD_BAND` (90 Hz to
   11 kHz), add light saturation, and add tape hiss at `HISS_DB`. Make
   the hiss a loop of the same length, so the seam does not show in the
   hiss. Overlay the synthesized theremin hook from `make_sounds.py`,
   with `THEREMIN_VIB`, in bars chosen so that the hook phrase repeats
   cleanly.
6. **Check the seam:** no sample-level step, no spectral-flux spike, and
   three back-to-back repeats auditioned at full range and through the
   laptop filter (`--reel`).
7. **Set loudness:** integrated −16 LUFS, true peak at or below −1 dBTP.
8. **Encode** Ogg Vorbis at about q5 (80 s is about 1.6 MB). Decode it
   again and confirm the sample count is unchanged. Test the loop in
   game: files over 300 KiB stream, and looping a streamed clip is an
   open risk in `SOUND-PIPELINE.md`.

### From Raw Generations to a 2.5 s Sting

1. Generate 3 to 4 s takes (Stable Audio, exact length, WAV), or trim
   the opening of Lyria Clips.
2. Choose by ear for weight and originality. Check it against the
   motif's rule 8: it must not quote the 1967 episode.
3. Trim to the first transient with about 5 ms of pre-roll. End at
   2.5 s with a 200 to 300 ms fade, or let the timpani decay inside the
   window. Add a short tail from the project's own reverb if the cut
   sounds abrupt.
4. Downmix to mono (effects are mono, 48 kHz, 16-bit WAV) and check
   that nothing cancels when the channels are summed. Normalize the
   loudest 400 ms window to −16 LUFS at −1 dBTP.
5. Keep the winning raw take, the prompt and the seed under `.local/`
   as provenance.

## Sources

All read on 2026-10-15. Dates in brackets are the page's own date where
it gave one.

Suno:
- https://suno.com/pricing
- https://suno.com/terms (revised 10 August 2026, effective 3 September
  2026)
- https://help.suno.com/en/articles/13614785 (downloads FAQ)
- https://help.suno.com/en/articles/13924929 (length up to 8 minutes)
- https://suno.com/blog/introducing-v6 (9 September 2026)
- https://aimusicdaily.com/suno-download-limits (September 2026)
- https://roo.beehiiv.com/p/suno-download-prices-explained (4 September
  2026)
- https://jackrighteous.com/en-gb/blogs/ai-music-distribution-guide/suno-ai-watermark-download-policy
- https://www.eesel.ai/blog/suno-v6-review
- https://www.musicbusinessworldwide.com/umg-and-sony-seek-to-add-61000-copyrighted-works-to-suno-lawsuit-after-discovery-reveals/
  (26 May 2026)
- https://www.opus.pro/blog/ai-music-news-april-2026
- https://www.digitalmusicnews.com/2026/07/03/suno-is-opening-an-api-partner-program/
  (search summary only)

Udio:
- https://www.aitools-directory.com/blog/udio-ai-review-2026/ (5 October
  2026)

ElevenLabs:
- https://elevenlabs.io/pricing
- https://elevenlabs.io/pricing/api
- https://elevenlabs.io/music-terms (updated 9 October 2026)
- https://elevenlabs.io/eleven-music-model-specific-terms
- https://elevenlabs.io/docs/api-reference/music/compose
- https://elevenlabs.io/eleven-music-api
- https://elevenlabs.io/blog/music-v2-5-model (11 September 2026)
- https://elevenmusic.io/blog/introducing-music-v2-5
- https://the-decoder.com/elevenlabs-makes-music-v2-5-available-via-app-and-api-with-free-and-pro-tier-options/
- https://www.therundown.ai/tools/eleven-music (29 August 2026)
- https://appliedai.tools/elevenlabs/elevenmusic-ai-music-tool-review/
- https://invideo.io/blog/elevenlabs-music-ai-generator/ (6 August
  2026)

Google:
- https://ai.google.dev/gemini-api/docs/pricing (updated 15 October
  2026)
- https://ai.google.dev/gemini-api/docs/music-generation
- https://ai.google.dev/gemini-api/docs/models/lyria-3-clip-preview
- https://ai.google.dev/gemini-api/docs/realtime-music-generation
- https://ai.google.dev/gemini-api/terms (effective 23 March 2026)
- https://invideo.io/blog/lyria-ai-music-generator/ (6 August 2026)
- https://aicybr.com/blog/google-lyria-3-5-gemini-api-guide (6
  September 2026)
- https://benchlm.ai/md/media-pricing/lyria.md
- https://reddit.sentinel-team.org/posts/1s3ipvg/snapshots/2026-03-27T18%3A20%3A17.4177Z
  (r/SunoAI thread on Lyria 3 Pro)
- Gemini prepay ($10 minimum, credits expire after 12 months): search
  summaries citing agentdeals.dev and tokencost.app; unverified
- https://www.flowmusic.app/pricing
- https://www.flowmusic.app/terms (redirects to
  https://policies.google.com/terms)
- https://rgb.ir/en/ai/flow-music/ (12 August 2026)
- https://jackrighteous.com/en-us/blogs/music-creation-process-guide/google-flow-music-rights-commercial-use-2026
  (3 September 2026)
- https://mlq.ai/news/google-releases-lyria-35-in-flow-music-with-upgraded-vocals-and-song-controls/
  (30 July 2026)
- https://runtimewire.com/article/google-lyria-3-5-flow-music-bpm-controls
- MusicFX shutdown on 31 July 2026: search summary of
  https://alphasignal.ai/news/google-kills-musicfx-to-go-all-in-on-flow-music

Stability AI:
- https://stability.ai/explainers/stable-audio-vs-competitors-licensing-export-rights-and-self-hosting-compared
  (9 September 2026)
- https://stability.ai/terms-of-service (effective 30 September 2026)
- https://musicbusinessworldwide.com/stability-ai-launches-new-audio-models-that-can-generate-6-minute-music-tracks
- https://undetectr.com/blog/stable-audio-review (25 September 2026)
- https://amrytt.com/stable-audio-review/ (9 July 2026)
- https://costbench.com/software/ai-music-generators/stable-audio/ (24
  August 2026)
- https://dynamoi.com/learn/ai-music-distribution/can-i-distribute-stable-audio-commercially
- https://blog.picassoia.com/stable-audio-3-vs-2-5-open-models-api-and-prompt-tips
  (8 October 2026)
- https://fal.ai/models/fal-ai/stable-audio-25/text-to-audio/llms.txt
- https://fal.ai/models/fal-ai/stable-audio-25/inpaint/llms.txt
- https://fal.ai/models/fal-ai/stable-audio-3/medium/text-to-audio/llms.txt
- Stability credits ($10 for 1,000, 25 free): search summaries; the
  pricing page did not render

AIVA:
- https://www.aiva.ai/pricing
- https://www.aiva.ai/legal/1
- Monthly prices (€15 and €49): search summary of Costbench

Soundraw:
- https://soundraw.io/pricing
- https://soundraw.io/terms (updated 12 June 2025)
- https://docs.channel.io/soundraw-faq/en/articles/Licensing-criteria-for-the-Creator-Plan-eafb81c7
- https://soundraw.io/blog/post/soundraw-licensing-explained (30 August
  2024)

Beatoven.ai:
- https://www.beatoven.ai/
- https://www.costbench.com/software/ai-music-apis/beatoven-ai/ (23
  August 2026)

Mubert:
- https://mubert.com/render/pricing
- https://mubert.com/render/faq
- https://mubert.com/render/cases/game-loop-music-generator

Loudly:
- https://www.loudly.com/terms
- https://costbench.com/software/ai-music-generators/loudly/ (3 July
  2026)

Mureka and MiniMax:
- https://invideo.io/blog/mureka-ai-music-generator/ (August 2026)
- https://invideo.io/blog/ai-music-copyright-comparison/ (6 August
  2026)

Comparisons and rankings:
- https://www.cometapi.com/ai-music-api-pricing/ (3 September 2026)
- https://fal.ai/learn/tools/best-text-to-music-apis-2026 (27 June 2026)
- https://alphasignal.ai/news/artificial-analysis-music-arena-v1-1-crowns-suno-v6-across-1-000-harder-prompts
  (7 October 2026)
- https://benchmarklist.com/arenas/artificial_analysis_music_instrumental/
  (data of 2 September 2026)
- https://app.cinevva.com/guides/ai-music-generators-games (20 September
  2026)

Content ID and copyright:
- https://jackrighteous.com/en-gb/blogs/ai-music-distribution-guide/stolen-ai-music-content-id-claim-recovery
  (22 August 2026)
- https://www.copyright.gov/ai/ (Part 2, Copyrightability, 29 January
  2025; its conclusion is summarized from the report as widely
  reported, not re-read today)
