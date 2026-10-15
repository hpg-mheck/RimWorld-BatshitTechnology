# Sound and Music: Legal, Platform and Community Rules

Phase 5000, Slice 1000 research. Topic: the cross-cutting rules that decide
which sound and music sources are safe for a free RimWorld mod shipped on
Steam Workshop and GitHub. Read-only desk research, 2026-10-15. Not legal
advice. Every source in the list at the end was read on 2026-10-15 unless
it says otherwise; "secondary" marks a fact taken from press or a blog
rather than the primary document, and "unverified" marks one I could not
confirm.

## Summary: the Rules That Should Decide the Choice

1. **Permission, not ownership, is what the mod needs.** In the US, the EU
   and (soon) the UK, purely AI-generated audio has no copyright, so nobody
   can license it, including the operator. What matters is (a) a contract
   with the service that lets the operator redistribute the files publicly,
   forever, after the subscription ends, and (b) freedom from anyone else's
   claim on the sound. Choose sources on those two tests.
2. **Never use a non-commercial or "personal use" tier, or NC licences.**
   Three things a free mod cannot avoid are commercial uses: Ludeon's EULA
   takes an irrevocable licence to every mod "for any purpose" with the
   right to sell or sublicense it; Valve's Subscriber Agreement takes a
   licence to use uploads to promote Steam and Steam games; and the RimWorld
   EULA lets players monetize gameplay videos, which Creative Commons itself
   treats as commercial use of NC music in them. A free mod is not enough to
   make the whole chain non-commercial.
3. **Never use music that is, or could be, registered in YouTube Content ID
   by someone else.** That rules out subscription libraries cleared by
   channel "safelisting" (Epidemic Sound and its peers), marketplaces whose
   authors register tracks (AudioJungle, Pond5, Pixabay Music) and, for
   now, carries a named risk for Suno (its terms say rights holders "may
   also have the right to collect revenue related to distribution of
   Outputs on third party platforms").
4. **Dedicate the mod's own audio to CC0 1.0, and keep MIT for code.** CC0
   works whether or not copyright exists in a given country (it waives
   where rights exist and licenses as a fallback), it makes Content ID
   registration by a troll a plain policy violation, and it needs no notice
   in players' videos. MIT is a software licence with a notice condition
   that fits audio badly. Dedicate only what the operator owns outright.
5. **Disclose AI use plainly in the Workshop description and README.** Steam's
   AI disclosure form does not reach Workshop items, but the RimWorld
   community catalogues AI-made mods anyway (a 705-mod Workshop collection
   built from self-disclosure and source-code reading), r/RimWorld limits
   AI posts tied to donation links, and the wider scene punishes concealed
   AI far more than disclosed AI.
6. **Keep the audio's licence separate from the repository's.** A public
   GitHub repository ships every WAV and Ogg as a loose, downloadable file.
   Any third-party audio needs a per-file licence and credit, and licences
   that forbid supplying sounds "as sound effects" (Sonniss GDC bundles)
   need care.

**Unsafe or doubtful sources:** Udio (no downloads at all since
30 October 2025; its terms now say Udio and its licensors own outputs and
allow personal, non-commercial use only); any free tier of Suno or any
other service that limits use to personal or non-commercial purposes;
subscription stock music cleared by safelisting (Epidemic Sound, Artlist,
Envato/AudioJungle, Pond5); Pixabay Music (any contributor can register
Content ID); anything CC BY-NC or CC BY-ND. **Suno Pro or Premier** is
usable for music only with the caveats in section 6. Code synthesis and
CC0 samples remain the cleanest.

## 1. Copyright in AI-Generated Audio

### United States

- **Copyright Office, Part 2 report (29 January 2025).** Outputs of
  generative AI "can be protected by copyright only where a human author
  has determined sufficient expressive elements": human work perceptible in
  the output, or "creative arrangements or modifications of the output",
  but "not the mere provision of prompts". Using AI as a tool, or including
  AI material in a larger human work, does not bar copyright in the human
  parts. Secondary summaries add the report's view that even extensive,
  iterated prompting does not yet give enough control. Registration
  applicants must disclose AI material that is more than de minimis and
  claim only the human contribution.
- **No later Office guidance found.** The Office's AI page lists nothing
  newer than Part 2 and the May 2025 pre-publication Part 3 (training).
- **Thaler v. Perlmutter.** The D.C. Circuit's human-authorship ruling
  stands: the Supreme Court denied certiorari on 2 March 2026.
- **Allen v. Perlmutter** (D. Colo., No. 1:24-cv-02665, the Midjourney
  image made with 624 prompts plus Photoshop work) tests whether heavy
  prompting is authorship. Cross-motions for summary judgment were briefed
  by early 2026; secondary sources say no ruling had issued as of
  16 September 2026 (unverified against the docket).

### European Union

- Copyright requires the "author's own intellectual creation", which EU
  courts read as human. The Municipal Court in Prague (case 10 C 13/2023,
  decided October 2023, reported 2024) refused protection to an
  AI-generated image and held that a prompt alone does not make the
  prompter an author.
- The European Parliament's resolution on copyright and generative AI
  (2025/2058(INI), adopted 10 March 2026, 460 to 71) says "content fully
  generated by AI which does not meet the established criteria for
  copyright protection should remain ineligible for such protection" and
  that "the public domain status of such outputs should be clearly
  determined". A resolution is not law, but it states the settled reading.
- **AI Act, Article 50.** Since 2 August 2026, providers of generative
  systems must mark audio outputs as machine-detectable (systems already
  on the market have until 2 December 2026); deployers must disclose
  "deep fakes". Original music and effects are not deep fakes. The rule
  matters only if the mod ever imitates a real person's voice, such as a
  recognisable newsreel narrator: do not.

### United Kingdom

- Section 9(3) of the Copyright, Designs and Patents Act 1988 still gives
  "computer-generated works" with no human author a 50-year copyright, owned
  by the person who made "the arrangements necessary" for the work. The
  government's Report on Copyright and AI (18 March 2026) proposes repealing
  it, keeping protection for AI-assisted works, but sets no date. Until
  then, the operator may hold a UK copyright in AI audio he arranged.

### Elsewhere

- China: the Beijing Internet Court (Li v. Liu, 27 November 2023) held a
  Stable Diffusion image made with 150-odd prompts and parameter choices to
  be the prompter's work. Protection therefore varies by country.

### What This Means for the Mod

**(a) Claiming a licence over AI audio.** Where no copyright exists (US,
EU), a licence over pure AI audio grants nothing and binds nobody; where it
does exist (UK, perhaps China), a licence works. Creative Commons says its
licences "should not be applied to works in the worldwide public domain"
and "encourage[s] the use of CC0 for those works that do not involve a
significant degree of human creativity". MIT is worse still: it is a
software licence whose condition (keep the notice "in all copies or
substantial portions of the Software") reads oddly for a loop heard in a
player's video. The same human-authorship question applies to audio
rendered by code that Claude agents wrote and tuned: the operator's
direction and selection may or may not count as authorship.

Human editing helps but does not settle it: cutting a seamless loop,
re-orchestrating, layering several generations or adding synthesized parts
may give a thin copyright in the arrangement, never in the generated
material itself.

**(b) Practical recommendation.**

- Dedicate all of the mod's own audio (code-synthesized, AI-generated and
  edited) to **CC0 1.0** in `Sounds/LICENSE` or `Sounds/CREDITS.txt`. CC0
  waives whatever rights exist in each country and falls back to a
  permissive licence where waiver fails, so it is correct whatever the
  courts decide. This replaces SOUND-PIPELINE.md open question 12's
  "generated audio under the mod's MIT terms".
- Dedicate only what the operator owns outright: code-synthesized audio,
  and service outputs whose terms assign the output to the user (Suno's
  paid tiers do; see section 6). Where a service only licenses outputs, or
  keeps ownership, CC0 would overreach; such sources should not be used at
  all.
- Record provenance per file, not as a licence: service, model, plan,
  generation date, download date and the human edits made. This supports
  Steam and community disclosure, Content ID disputes and any later
  question about the plan in force.
- Do not write "provided under the service's terms". Players and video
  makers are not parties to the operator's contract with the service, the
  phrase grants nothing, and it implies restrictions a Content ID claimant
  could exploit.
- Keep third-party CC0 or CC BY samples under their own licences, listed
  per file with credit.

## 2. Steam

### AI Disclosure Rules

- **January 2024.** Valve's "AI Content on Steam" (Steamworks Development,
  9 January 2024) added an AI section to the Content Survey, with two
  categories: Pre-Generated (made with AI during development) and
  Live-Generated (made while the game runs, which needs guardrails).
  Disclosures appear on the store page as "AI Generated Content
  Disclosure".
- **January 2026 rewrite.** Reported 16 to 19 January 2026 (secondary):
  disclosure now covers only AI content that ships and that players
  consume. The current Steamworks page reads: "Efficiency gains through
  the use of these tools is not the focus of this section. Instead, it is
  concerned with the use of AI in creating content that ships with your
  game, and is consumed by players. This includes content such as artwork,
  sound, narrative, localization, etc."
- **Workshop items are not covered.** The survey is filled in by a
  Steamworks partner for "your game" or "your product"; the page never
  mentions the Workshop or user-generated content, and no Valve statement
  extends it to Workshop items (none found; the absence is unverified).
  RimWorld's Workshop has no AI tag, and RimWorld's tags are Ludeon's.
  Disclosure in the item description is the only mechanism.

### Workshop Terms (Steam Subscriber Agreement, Section 6)

The Subscriber Agreement was last updated 10 September 2026. Its Section 6
governs Workshop uploads (`/workshop/workshoplegalagreement` now carries no
separate text):

- **Licence to Valve.** Uploading grants Valve and affiliates "the
  worldwide, non-exclusive right to use, reproduce, modify, create
  derivative works from, distribute, transmit, transcode, translate,
  broadcast, and otherwise communicate, and publicly display and publicly
  perform" the content, "for the purpose of the operation, distribution,
  incorporation as part of and promotion of the Steam service, Steam games
  or other Steam offerings", "for the entire duration of the intellectual
  property rights", free of charge.
- **Workshop specifics.** "Workshop Contributions are in principle made
  available to Subscribers for free." Subscribers get the same rights as in
  any other Subscription. If the author removes an item, Valve's rights end
  except that "your removal will not affect the rights of any Subscriber
  who has already obtained access to a copy".
- **Warranties.** "You represent and warrant to us that you have sufficient
  rights in all User Generated Content to grant Valve and other affected
  parties the licenses described", and "that the Workshop Contribution was
  originally created by you (or ... by you and the other contributors ...)".
  The Online Conduct rules list "uploading materials that you do not own
  the rights to" as an IP violation.

**Implications.** Any third-party audio's licence must be broad enough for
Valve's promotional and broadcast use and for copies that survive removal:
perpetual and irrevocable. NC licences fail the promotion test. Creative
Commons notes its licences "do not allow sublicensing, which means you
cannot grant a license to a platform with respect to the rights in any
CC-licensed content you do not own"; Valve, like any recipient, gets
CC BY rights directly from the licensor, so CC BY works in practice, though
the "originally created by you" wording reads strictly against it. CC0 and
the operator's own generations satisfy the warranty most cleanly. Valve
enforces through DMCA notices and removal, not pre-screening.

## 3. RimWorld and Ludeon

From the RimWorld EULA (last updated 7 April 2022, at
rimworldgame.com/eula):

- **Mods must be free.** "Your Mods must be distributed for free, period.
  ... Commercial exploitation includes but is not limited to in-game
  advertising, or monetizing your Mod through any other forms of
  advertising or marketing." General, unobtrusive donations are allowed;
  none inside the mod.
- **No infringement.** "Your Mods must not contain any material that
  violates copyright, trademark, or other intellectual property rights of
  Ludeon or any other person or parties." Also: mods "must not
  misrepresent what they are or their purpose, or deceive players".
- **Licence to Ludeon.** "You own the Derivative Works you create. However,
  by creating any Derivative Work, you agree to grant Ludeon a licence to
  use, copy, modify, adapt, distribute, and publicly display all those
  Derivative Works for any purpose, without any payment or notice to you,
  anywhere throughout the universe ... This Derivative Licence is
  irrevocable and lasts forever. You also agree to let Ludeon sell, assign,
  or sublicense the Derivative Licence to other people". Ludeon is "not
  required to credit or acknowledge you".
- **Players' videos.** "Within reason, you are allowed to make User Content
  such as videos of the gameplay of the Software and distribute them. ...
  you're allowed to monetize this content through advertising."
- **AI.** The EULA does not mention AI. No Ludeon or Tynan Sylvester
  statement on AI content in mods was found (searches of Ludeon's site,
  the forums and the press; absence unverified). Ludeon's FAQ adds only
  that anyone may monetize gameplay videos.

**Implications.** The Derivative Licence (any purpose, sublicensable, no
credit) is incompatible with NC material and strains attribution licences;
CC0 or owned material fits it exactly. Because Ludeon expressly invites
monetized videos, the mod's music must be safe in them (section 5).

## 4. Community Reception

### RimWorld

- **r/RimWorld.** The subreddit added a Rule 8 on generative AI (post
  1kpebdd, apparently May 2025; read through a search index of a Reddit
  mirror, secondary, date inferred). AI art must be paired with the
  screenshot it illustrates; there must be "no association between posts
  on the sub, related AI art, and compensation", explicitly including "a
  new Mod Release post that uses Generative AI and has a ko-fi in the
  workshop page"; mod authors are whitelisted case by case. Moderators
  chose not to ban AI outright. Reddit itself was not readable for this
  research, and several mirrors block AI readers; I respected that.
- **The Workshop catalogues AI.** A collection "Mods made with AI"
  (Halicade; 705 items; updated 12 October 2026; 1,778 visitors) lists
  mods "made using AI ... concluded either by the user disclosing it
  themselves, or looking through the provided source code". A companion,
  "Mods that incorporate AI" (86 items), covers LLM features such as RimGPT
  and RimSaga. Batshit Technology, built largely by agents, is a candidate
  for the first list whether or not it discloses.
- **Disclosed AI music mods meet little resistance.** Rimworld Music
  Expanded ("An AI-assisted music expansion", posted 25 March 2026, 113
  subscribers): three positive comments, one saying "Thanks for coming
  clean about your methods btw". Rimforged Music, Volumes 1 and 2 (July
  2026, Suno links on the page, 34 and 25 subscribers): only praise.
  Relic Blessings (27 September 2026, 209 subscribers, music "made with
  Suno"): comments discuss gameplay only. The samples are small, and
  authors can delete comments.
- **LLM gameplay mods are popular.** RimTalk (Gemini-driven pawn dialogue,
  August 2025) drew press in Japan and many add-ons; older Steam forum
  threads on AI in RimWorld (2023 and 2024) run mixed to sceptical, with
  "art theft" and cost the main objections.
- **A false lead, corrected.** Search summaries claimed that "AI Upscaled
  Textures - Core" (45,934 subscribers) and the AI collections had been
  removed by Valve. The removal banner in those pages' HTML is a hidden
  template (`display: none`); the items are live.

### The Wider Mod and Indie Scene

- **Nexus Mods** (policy reported 31 July 2026): mandatory tags in three
  classes. "AI-Generated Content" explicitly includes "voices, dialogue,
  translations, music, in-game AI-generated assets"; "AI Assisted" covers
  "AI audio upscaling/cleaning". "Failure to correctly tag mods, including
  undisclosed generative AI usage, may be subject to moderation action."
- **itch.io** (since November 2024): a required "Generative AI disclosure"
  field for assets (graphics, sound, text, code) with an "AI Generated"
  tag and filter; untagged AI asset packs are dropped from browse pages.
- **Concealment is punished.** The Indie Game Awards stripped Clair
  Obscur: Expedition 33 of Game of the Year on 20 December 2025 after AI
  textures surfaced despite a submission saying none were used.
  Vapor World (August 2025) fell to "mostly negative" over visibly
  inconsistent AI art.
- **Disclosed, unobtrusive AI survives.** My Winter Car's store page
  discloses "Some in-game radio music is AI generated" (plus textures and
  TV speech) and stands at Very Positive, 93 per cent of 6,700 English
  reviews (read 2026-10-15).

**Does disclosure help?** The evidence is anecdotal but consistent:
disclosed AI music in small RimWorld mods drew thanks, not attacks;
discovered concealment draws the worst reactions; and quality matters more
than provenance. Music and foley are scrutinized less than visual art, but
platforms now list music explicitly. Recommended wording is factual and
short, for example: "Some music and sound effects were generated with
[service] and edited by hand; signal tones are synthesized from code. All
mod audio is CC0 and safe for streams and videos."

## 5. YouTube Content ID and Streaming

### The Rules

YouTube's "Content eligible for Content ID":

- "You must have exclusive rights to the material in the reference file."
- Not eligible: "Content licensed non-exclusively from a third party";
  "Content released under Creative Commons or similar free of charge/open
  licenses"; "Public domain footage, recordings, or compositions"; and,
  as not "sufficiently distinct", "sound effects, and production loops".
- "Original Video Game Soundtracks are only valid references when owned by
  the video game's publisher."
- Content "sold or licensed at scale for incorporation into other works",
  such as royalty-free production music, "must be routed for review"
  before it may claim.

Twitch mutes VOD segments matched by Audible Magic, whose database holds
the recorded-music industry's catalogue, in-game music included.

### How Music Ends Up Claimed

- **Libraries register their own catalogues.** AudioJungle authors
  commonly register through Identifyy/HAAWK, and Envato sells "Claim Clear"
  to undo the claims. Epidemic Sound claims every video not on an active
  subscriber's safelisted channel: content published after a subscription
  ends "will result in copyright claims". A mod's players are not
  subscribers, so such music would be claimed in their videos.
- **Open marketplaces.** Pixabay lets anyone upload; some contributors
  register their tracks, and users must dispute with the licence
  certificate.
- **Third-party claimants on game music.** In 2018, "Bicycle Co. Ltd"
  claimed Falcom's Trails of Cold Steel music in a stream, though Falcom
  uses no licensing agent. In 2026, an impostor uploaded AI voice-cloned
  covers of folk singer Murphy Campbell's songs and filed Content ID claims
  through the distributor Vydia against her own public-domain recordings
  (claims filed 25 March 2026, released after publicity, user banned).
  Vydia's founder: "Likely people noticed she was not in the ACR database
  ... and exploited the system to file the song first." Indie composers
  answer the dilemma by publishing music while deliberately not
  registering it ("not registered for Content ID, meaning platforms like
  YouTube and Twitch cannot detect their usage").
- **AI-specific risks.**
  - Suno's terms (effective 3 September 2026): "Applicable rights holders
    may also have the right to collect revenue related to distribution of
    Outputs on third party platforms"; "Output may not be unique across
    users and the Service may generate the same or similar output for a
    third party"; Suno may add "a fingerprint, watermark, or metadata
    indicating the applicable service tier".
  - Labels reportedly pushed for Content ID-style fingerprinting in their
    licensing talks with AI platforms (Music Business Worldwide), and
    Believe/TuneCore now distribute eligible Suno music (September 2026,
    secondary). No case was found of Suno or a label claiming a third
    party's video through Content ID because it contained a user's Suno
    track (none found; unverified), but the terms leave room for it.
  - Distributors' AI rules vary (DistroKid requires disclosure; TuneCore
    and CD Baby require human input; secondary), so a Suno user who
    distributes a near-identical generation could create a reference that
    claims a mod's track.

### How a Mod Author Protects Players

1. **Choose sources that are not, and cannot become, Content ID
   references:** code synthesis; CC0 samples; owned AI generations
   dedicated to CC0. Avoid safelisting libraries, AudioJungle, Pond5 and
   Pixabay Music.
2. **Never register the soundtrack.** If it is ever released on streaming
   services, use a distributor with YouTube Content ID switched off; many
   enable it by default (unverified per distributor).
3. **Publish first, with evidence.** Put the masters in a dated GitHub
   release with SHA-256 hashes before or at the Workshop release, so a
   dispute can show prior publication and a CC0 dedication. CC0 material
   is ineligible for Content ID by YouTube's own rule.
4. **State the permission.** In the README and Workshop description: all
   mod audio is CC0, not registered with any content-identification
   system, and free to use in monetized videos and streams; any claim on
   it is invalid and should be disputed citing the page. This "whitelist
   statement" costs nothing.
5. **Prefer effects and stings over long loops for AI material.** Sound
   effects and short loops are themselves ineligible references; the
   60 to 90 s disco loop is the asset with real claim risk.

## 6. The Label Settlements With Suno and Udio

### Udio

- **Universal** settled with Udio on 29 to 30 October 2025, announcing a
  licensed platform for 2026. Udio disabled downloads of audio, video and
  stems without warning on 30 October 2025, then opened a 48-hour window
  from 3 November for existing songs (secondary). **Warner** settled in
  November 2025. **Sony's** case remained active in April 2026, with
  a judge refusing to dismiss its DMCA claim (secondary).
- **Udio's terms** ("Last Revised on November 12, 2025"): "You may not
  download copies of any Output"; the user covenants "not to download,
  reproduce, transmit, display, publish or otherwise distribute" any
  output; "the Company or its affiliates or their respective licensors own
  and will continue to own the Services, the Output"; outputs may be used
  "solely for your personal and non-commercial purposes".
- The walled-garden platform had not launched, and downloads had not
  returned, by late September 2026 (secondary).
- **Older downloads.** No transitional clause preserving rights in pre-
  October 2025 downloads was found in the current terms; the old terms'
  grant may still govern those files, but the question is unresolved.
  **Treat Udio, old or new, as unsafe.**

### Suno

- **Warner** settled on 25 November 2025: downloads for paid accounts
  only, monthly download caps with paid top-ups, "new, more advanced, and
  fully licensed models" in 2026 and retirement of the old ones.
- **v6** launched 9 September 2026 with models built with WMG, BMG and
  Believe; every earlier model was retired that day; existing songs stay
  in users' libraries; free-tier songs can be played and shared but not
  downloaded; Suno committed to watermarking and fingerprinting.
- **Universal and Sony** were still suing Suno in 2026; settlement talks
  hit an impasse in April 2026 over the "walled garden", that is, sharing
  and distributing tracks off the service (Digital Music News). A
  settlement could narrow export rights for new generations.
- **Current terms** (effective 3 September 2026):
  - Pro and Premier: "Suno hereby assigns to you all of its right, title
    and interest in and to any Output owned by Suno", with "no
    representation or warranty to you that any copyright will vest".
  - Free or Basic: "you will only use such Outputs for your lawful,
    personal and non-commercial purposes".
  - Commercial use requires a permitted Download through Suno's channel;
    stream ripping is prohibited; "You may edit, process, or convert the
    format of an Output ... to the extent such use is incidental";
    remixes are non-commercial whatever the tier.
  - Survival: the assignment and "the commercial use rights ... with
    respect to a Download you have obtained, are perpetual and are not
    affected by ... the expiry, cancellation, downgrade or suspension of
    your subscription". This meets the brief's "after the subscription
    ends" test for files downloaded while subscribed.
  - Older free-tier songs gain no commercial rights by upgrading later
    (Suno help article, secondary); regenerate while subscribed.

**What this means.** Download everything needed within the one paid month,
keep the receipts and the account's plan history, and expect the files to
carry a Suno watermark. Suno remains an AI service under active label
litigation; its output is assigned but not guaranteed free of others'
revenue claims.

## 7. Attribution, Free Tiers and "Commercial"

- **Creative Commons' definition:** NonCommercial means "not primarily
  intended for or directed towards commercial advantage or monetary
  compensation". It "turns on the use, not the identity of the reuser";
  CC "cannot advise you on what is and is not commercial use".
- **CC's own example settles the video question:** "an NC song may be used
  as the basis for a video ... When the music video is distributed as a
  whole, it may not be used commercially because of the NC license of the
  song." A player's monetized Let's Play containing NC mod music is that
  case.
- **Is a free Workshop mod commercial?** The mod's own distribution is
  plausibly non-commercial: the EULA forbids monetizing it, and NC does not
  turn on the platform being a store. But the chain around it is not:
  Ludeon's licence (any purpose, sell or sublicense), Valve's (promotion of
  Steam and Steam games) and players' monetized videos. So **NC material is
  unsafe**, whatever view one takes of the mod itself.
- **Service free tiers are not CC licences.** Their own words govern, and
  they are usually stricter: Suno's free tier and all of Udio say
  "personal and non-commercial". "Personal" excludes public distribution
  in a mod even where nobody earns a cent.
- **Attribution can live in a credits file.** CC attribution "may be
  implemented in any reasonable manner", including "a link to a place where
  the attribution information may be found". `Sounds/CREDITS.txt`, plus a
  link from the Workshop description and README, satisfies CC BY. Two
  wrinkles: Ludeon may reuse without credit (it would then owe CC BY
  credit itself), and CC BY material cannot be relicensed under CC0, so
  keep it listed separately.
- **"Royalty-free" is not "redistributable as loose files".** The Sonniss
  GDC bundle licence (version 2.0, effective 27 August 2026) allows use in
  unlimited projects with no attribution, but "Licensee may not distribute,
  publish, sub-license or otherwise supply the sound effects as sound
  effects to any other person", whether "unmodified, modified or
  re-designed ... or as part of a sound effects library, sample pack,
  asset pack, project template". Shipping them inside the mod is probably
  "incorporated into licensee project"; publishing them as loose files in
  a public MIT repository invites the opposite reading. If used at all,
  exclude them from the repository's licence and say so per file.

## 8. YouTube Disclosure for Players

YouTube asks creators to disclose AI music only when it "Creates music
that's the main focus of the video"; background music in gameplay does not
qualify. Players need do nothing.

## Recommendations

1. Rank sources: (1) code synthesis; (2) CC0 recordings, such as Freesound
   filtered to CC0; (3) owned AI generations from a paid tier whose terms
   assign the output and survive cancellation (Suno Pro or Premier
   qualifies, with the section 5 caveat); (4) CC BY only where nothing
   else serves, credited per file.
2. Exclude: Udio; every free or personal-use tier; CC BY-NC and CC BY-ND;
   safelisting libraries; AudioJungle, Pond5 and Pixabay Music; anything
   whose licence forbids supplying the files as sounds, unless carved out.
3. Licence: `Sounds/` under CC0 1.0 with a per-file `CREDITS.txt`
   (source, licence, provenance, edits); code stays MIT; the root README
   says so. Resolve SOUND-PIPELINE open question 12 this way.
4. Disclose AI use in one plain sentence on the Workshop page and in the
   README; keep any donation link off the Workshop page if a release post
   is planned for r/RimWorld.
5. Publish masters with hashes in a dated GitHub release; add the
   stream-safety statement; never register any track for Content ID.
6. Because music has the higher claim risk, prefer synthesis or a
   hand-built arrangement for the long disco loop, and reserve AI services
   for short stings and organic effects, which are ineligible references
   anyway.

## Sources

All read 2026-10-15.

### Copyright Law and Policy

- US Copyright Office, "Copyright Office Releases Part 2 of Artificial
  Intelligence Report", NewsNet 1060, 29 January 2025:
  https://copyright.gov/newsnet/2025/1060.html
- US Copyright Office, AI initiative page: https://www.copyright.gov/ai/
- Lowenstein Sandler, summary of Part 2:
  https://www.lowenstein.com/news-insights/publications/client-alerts/us-copyright-office-issues-report-on-copyrightability-of-generative-ai-outputs-ai
- SCOTUSblog, Thaler v. Perlmutter (cert. denied 2 March 2026):
  https://www.scotusblog.com/cases/case-files/thaler-v-perlmutter
- Baker Donelson, "Supreme Court Denies Certiorari in Thaler v.
  Perlmutter":
  https://www.bakerdonelson.com/supreme-court-denies-certiorari-in-thaler-v-perlmutter-ai-cannot-be-an-author-under-the-copyright-act
- US reply brief and cross-motion, Allen v. Perlmutter:
  https://www.copyright.gov/ai/docs/us-reply-brief-and-cross-motion-for-summary-judgment.pdf
- Previs Office, Allen case status (secondary):
  https://previsoffice.com/archive/the-allen-case-and-ai-authorship-summary-judgment-2026/
- Novagraaf, Czech AI-image ruling:
  https://www.novagraaf.com/en/insights/ai-and-copyright-first-ruling-european-court/
- IRIS Merlin, European Parliament resolution on copyright and generative
  AI: https://merlin.obs.coe.int/article/10499
- European Parliament adopted text TA-10-2026-0066:
  https://data.europarl.europa.eu/distribution/doc/SP-2026-07-27-TA-10-2026-0066_en.pdf
- UK Government, Report on Copyright and Artificial Intelligence,
  18 March 2026:
  https://www.gov.uk/government/publications/report-and-impact-assessment-on-copyright-and-artificial-intelligence/report-on-copyright-and-artificial-intelligence
- Kluwer Copyright Blog, Beijing Internet Court (Li v. Liu):
  https://legalblogs.wolterskluwer.com/copyright-blog/beijing-internet-court-grants-copyright-to-ai-generated-image-for-the-first-time/
- Baker Botts, EU AI Act Article 50 go-live (September 2026):
  https://www.bakerbotts.com/thought-leadership/publications/2026/september/eu-ai-act-article-50-transparency-obligations-go-live

### Creative Commons

- CC FAQ (NC, attribution, public domain, platforms):
  https://creativecommons.org/faq/
- CC wiki, NonCommercial interpretation:
  https://wiki.creativecommons.org/wiki/NonCommercial_interpretation
- CC, "Understanding CC Licenses and Generative AI", 18 August 2023:
  https://creativecommons.org/2023/08/18/understanding-cc-licenses-and-generative-ai/

### Steam and Valve

- Steam Subscriber Agreement, Section 6 (updated 10 September 2026):
  https://store.steampowered.com/subscriber_agreement/
- Steam Online Conduct: https://store.steampowered.com/online_conduct/
- Steamworks, Content Survey (AI section):
  https://partner.steamgames.com/doc/gettingstarted/contentsurvey
- Steamworks Development, "AI Content on Steam", 9 January 2024:
  https://store.steampowered.com/news/group/4145017/view/3862463747997849618
- VGC, Valve's rewritten AI disclosure rules, 17 January 2026:
  https://www.videogameschronicle.com/news/valve-has-significantly-rewritten-steams-rules-for-how-developers-much-disclose-ai-use
- Steam store, My Winter Car (AI disclosure, reviews):
  https://store.steampowered.com/app/4164420/My_Winter_Car/

### RimWorld and Its Community

- RimWorld EULA (updated 7 April 2022): https://rimworldgame.com/eula/
- Ludeon FAQ: https://ludeon.com/blog/faq/
- Workshop, "Mods made with AI" collection:
  https://steamcommunity.com/sharedfiles/filedetails/?id=3463211554
- Workshop, "Mods that incorporate AI" collection:
  https://steamcommunity.com/sharedfiles/filedetails/?id=3463214912
- Workshop, Rimworld Music Expanded:
  https://steamcommunity.com/sharedfiles/filedetails/?id=3692422386
- Workshop, Rimforged Music, Volume 1:
  https://steamcommunity.com/sharedfiles/filedetails/?id=3767902853
- Workshop, Rimforged Music, Volume 2:
  https://steamcommunity.com/sharedfiles/filedetails/?id=3770902269
- Workshop, Relic Blessings:
  https://steamcommunity.com/sharedfiles/filedetails/?id=3809039390
- Workshop, AI Upscaled Textures - Core:
  https://steamcommunity.com/sharedfiles/filedetails/?id=2307494117
- Steam forum, "Midjourney AI create art for events" (2023):
  https://steamcommunity.com/app/294100/discussions/0/3818529563395865496
- Steam forum, "Please include GPT 4 into the RimWorld game" (2024):
  https://steamcommunity.com/app/294100/discussions/0/4349995545101255982
- r/RimWorld Rule 8 (search-index snippet of a Reddit mirror; secondary):
  https://red.applefritter.com/r/RimWorld/comments/1kpebdd/comment/msx7nxg
- Game*Spark, RimTalk (25 August 2025):
  https://www.gamespark.jp/article/2025/08/25/156429.html

### Wider Scene

- Shacknews, Nexus Mods generative AI tagging, 31 July 2026:
  https://www.shacknews.com/article/150216/nexus-mods-gen-ai-tagging-moderation
- GamingOnLinux, itch.io AI disclosure for assets, November 2024:
  https://gamingonlinux.com/2024/11/itchio-store-now-requires-ai-generated-content-disclosures-for-assets
- GamesMarket, itch.io AI tagging, 21 November 2024:
  https://www.gamesmarket.global/itchio-now-requires-gen-ai-content-to-be-tagged-in-all-games-and-asset-packs-711e824843d410eab51fcd9436836294
- Malay Mail, Indie Game Awards rescind Clair Obscur honours, 21 December
  2025:
  https://www.malaymail.com/news/tech-gadgets/2025/12/21/indie-game-awards-rescinds-honours-from-clair-obscur-expedition-33-for-genai-use/202718
- PC Gamer, Vapor World backlash (via search summary; page truncated):
  https://www.pcgamer.com/gaming-industry/steam-week-in-review-a-touch-of-ai-is-all-it-takes-to-trigger-backlash-as-a-promising-new-indie-falls-afoul-of-slop-skeptics/

### Content ID, Streaming and Libraries

- YouTube Help, "Content eligible for Content ID":
  https://support.google.com/youtube/answer/2605065?hl=en
- YouTube Help, "Disclose Gen AI usage for music content":
  https://support.google.com/youtube/answer/17124251?hl=en
- YouTube Help, "Disclosing use of GenAI content":
  https://support.google.com/youtube/answer/14328491?hl=en
- Twitch, "Important: Changes To Audio In VODs" (Audible Magic):
  https://blog.twitch.tv/en/2014/08/06/important-changes-to-audio-in-vods-35939b33ee2a/
- MCV/Develop, YouTube on game music claims, 19 December 2013:
  https://www.mcvuk.com/development-news/youtube-suggests-users-turn-off-game-music-to-avoid-copyright-claims/
- Steam forum, Trails of Cold Steel stream claim, 11 February 2018:
  https://steamcommunity.com/app/538680/discussions/0/1700542332334029201
- Music Business Worldwide, Murphy Campbell and Vydia claims, April 2026:
  https://musicbusinessworldwide.com/a-folk-musician-had-her-voice-cloned-by-ai-and-her-recordings-claimed-by-a-copyright-troll-welcome-to-2026
- Cyberleaf Studio, itch.io comments on Content ID non-registration:
  https://cyberleaf.itch.io/retro-future/comments
- Envato Elements, Claim Clear: https://elements.envato.com/claim-clear
- Epidemic Sound, safelisting:
  https://www.epidemicsound.com/how-it-works/music-licensing/what-is-safelisting/
- Epidemic Sound Help, "What happens when a subscription is canceled?":
  https://help.epidemicsound.com/hc/en-us/articles/26254650213266-What-happens-when-a-subscription-is-canceled
- Thematic, Thematic vs Pixabay (Pixabay claim risk; secondary):
  https://hellothematic.com/thematic-vs-pixabay/
- Sonniss, GDC Game Audio Bundle licence v2.0 (effective 27 August 2026):
  https://sonniss.com/gdc-bundle-license/
- Music Business Worldwide, labels push Content ID-style fingerprinting:
  https://www.musicbusinessworldwide.com/suno-upgrades-ai-song-editor-as-labels-reportedly-push-for-content-id-style-fingerprinting-in-licensing-talks-with-ai-music-platforms
- RouteNote, distributing Suno music (distributor rules; secondary):
  https://routenote.com/radar/can-you-distribute-suno-music-to-spotify-and-other-streaming-services/

### Suno and Udio

- Suno Terms of Service (effective 3 September 2026):
  https://suno.com/terms
- Music Business Worldwide, Suno v6 launch, 9 September 2026:
  https://musicbusinessworldwide.com/suno-v6-ai-music-models-launch-in-partnership-with-wmg-bmg-and-believe
- SiliconANGLE, Warner and Suno settlement, 25 November 2025:
  https://siliconangle.com/2025/11/25/warner-music-inks-suno-deal-amid-cooling-tensions-ai-generated-music/
- Digital Music News, Suno, UMG and Sony settlement impasse, 9 April 2026:
  https://www.digitalmusicnews.com/2026/04/09/suno-universal-music-lawsuit-settlement-impasse/
- Udio Terms of Service (last revised 12 November 2025):
  https://www.udio.com/terms-of-service
- PPC Land, Udio settles with UMG and disables downloads, 30 October
  2025: https://ppc.land/udio-settles-copyright-lawsuit-partners-with-universal-music-group/
- Music Business Worldwide, WMG settles with Udio:
  https://www.musicbusinessworldwide.com/warner-music-settles-udio-lawsuit-strikes-licensing-deal-with-ai-platform/
- AITools Directory, Udio review 2026 (downloads still off; secondary):
  https://www.aitools-directory.com/blog/udio-ai-review-2026/
- Freeacademy, Suno free-tier songs and retroactive rights (secondary):
  https://freeacademy.ai/lessons/suno-copyright-and-commercial-use
