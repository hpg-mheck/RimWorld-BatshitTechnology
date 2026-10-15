# The Showcase and AnyRim

This document is the plan for the in-game showcase and the design of
AnyRim, the small test town the showcase builds in the map's north-west.
Matt "Breakpoint" Heck asked for AnyRim on 2026-10-13: "somewhere to go
test stuff". Where this document and the code disagree, fix whichever is
wrong in the same change.

## Running It

```powershell
.\Tools\smoke-test.ps1 -Showcase
```

The showcase is `BT_Showcase`, the first client of the Showcase API
(`docs/SHOWCASE-API.md`). Players start it from the main menu: Showcases
(at the bottom of the menu, below Quit to OS; the Harmony mod must be
active), then Batshit Technology, then "Living better with
batshittium", then Start. The script builds nothing itself: it starts
RimWorld with `-showcase=BT_Showcase`, the main menu acts on that by
generating a fresh game for the showcase, and the def's worker,
`BatshitTourDirector` (`Source/Tour/`), builds the stage through
`Source/Showcase.cs` and directs the tour, leaving the game running. The
script exits at once with an error if the showcase is refused or fails
to set up, or if the mod logs an error before `READY`. Windows shown to
the operator open maximized at 2x UI scale. The game log carries cues
for screen-capture tooling, so it never has to guess at timings, all
prefixed `[BT-SHOWCASE]`: `READY` (once the stage is built), `ANYRIM` or
"AnyRim skipped", then for each scene `SCENE <key>`, `BEAT <key>
<moment>` for each beat and `END <key> <outcome>`, or `SKIPPED <key>:
<reason>` for a scene this map cannot stage, and last `FINISHED
BT_Showcase` (the API's full cue list is in its specification).

`.\Tools\smoke-test.ps1 -Showcase -Verify` runs the whole tour as a test
(`docs/AI-TESTING-STRATEGY.txt`, section 4): small, muted, at Superfast,
with AnyRim forced to decline so that declining is exercised on every
map; the Raccoon Incident needs the town shown first, so it declines
with it (`-TourSkip ''` runs all ten).

A showcase game never autosaves, so the player's autosave slots are
safe. The devices' DEV buttons show only in dev mode: the showcase
borrows god mode for them through the API, which gives it back when the
showcase stops or finishes, or the viewer leaves the game, so it never
follows anyone into their own colonies. `-quicktest -showcase=BT_Showcase`
also works, for developer runs: the quick-test game adopts the showcase
on its own 250-cell map.

## The Map

The quick test always generates a 250 by 250 map. The tour's seed picks
its tile, biome and terrain (below); a different seed gives a different
map. Its centre is (125,125). RimWorld's x grows east and z grows north.

| Area | Where | Contents |
|------|-------|----------|
| Demo base | Centre, within 16 tiles | Every device, working |
| Ballista range | East of the base | Volleys at (185,125) |
| Stampede | East edge, level with the base | 200 boomalopes enter |
| Grenade range | Clearing at (100,125) | Junk pile, two colonists |
| Self-destruct pads | Two corners at centre ±75 | A replicator, a reactor |
| AnyRim | North-west, about (55,163) | The test town |

### The Tour's Seed

`BT_Showcase` pins the seed `tour-04` (`Defs/Misc/BT_Showcases.xml`), so
every launch builds the same world, tile, colonists, map and stage
(`docs/SHOWCASE-API.md`, "Seeds"). A `-showcaseseed=<text>` command line
overrides it, and `Tools/tour-trials.ps1` tries the tour on others.

It was chosen on 2026-10-14 and 2026-10-15 (slice 18000), at Matt
"Breakpoint" Heck's request to "try 32 variations of the demo scenario"
and "select the best one":

1. **Trials.** The full tour ran once on each of `tour-01` to `tour-32`.
   Nine seeds passed every hard requirement: all ten scenes completed,
   nobody harmed, AnyRim placed, the grenade thrown, Ned's throw and the
   tractor's protest. The rest failed on hostiles the map started with
   (dark entities, mechs) or on faults in the tour's own script.
2. **Repeats.** Six candidates ran three more times each. Every one
   repeated its map and stage checksums (`MAPHASH`, `STAGEHASH`) in
   every process. tour-04 and tour-17 were clean in four of four runs;
   tour-05 and tour-27 in two.
3. **A close look.** Four finalists (tour-04, tour-17, tour-05,
   tour-27) were judged scene by scene from 51 frames a run and their
   logs. The map was weighed above the script, which will change.

Why tour-04, an arid shrubland:

- The stampede's band on the east edge is open (326 of 328 cells), and
  the 256-bolt volley killed all 200 boomalopes at once in every run,
  with no fire near the base.
- Both self-destruct pads stand in open ground with readable rings. On
  tour-17 a mountain overhang covers a quarter of the reactor's ring;
  on tour-27 an outcrop covers half of it and splits the stampede.
- AnyRim stands on flat ground at (51,166), with every sign, house and
  car legible.
- The tractor yard is roomy, and its single pull is the busiest of the
  four (about 415 item moves against tour-17's 187).
- No anima tree stands in the volley zone. On tour-17 and tour-27 the
  stampede's volley kills one every run.
- It has the fewest map-made hostiles of the four (three).

Its known weaknesses:

- An ancient walled structure stands 20 to 25 tiles north-east of the
  base, beside the defence volley's craters.
- Three "Sorne Geneline" hostiles appear during the first scene. Their
  source is untraced (`docs/AI-TODO.txt`). In two of its eight runs,
  colonists were hurt while they were still about.
- Rock chunks lie piled beside the Transmogrifier.
- Grass fires start around the pads.

The runner-up is `tour-17`, a grassland.

## The Scenes

Built in slice 12000 (2026-10-14). There is no timer: each scene waits on
the world (a burst ending, an aftermath going idle, an implosion closing)
with its def's `timeoutTicks` behind it, and every scene declines, with a
stated reason, a map that lacks what it needs. Ticks run at 60 a second
at normal speed; the reactor's barrage slows the game below real time,
which is why capture tools wait for log cues.

| # | Key | Title | Beats | Declines when |
|---|-----|-------|-------|---------------|
| 1 | `home` | The Home of Tomorrow | each device | no device stands |
| 2 | `defence` | Home Defence | artificer, volley | no Ballista |
| 3 | `stampede` | Uninvited Guests | herd, volley, aftermath† | see below |
| 4 | `retire-replicator` | When Devices Retire | blast | no pad 0 |
| 5 | `retire-reactor` | When Reactors Retire | blast | no pad 1 |
| 6 | `anyrim` | A Typical Rim Community | sign, street, homes | no AnyRim |
| 7 | `grenade` | Comfort Is a Weapon | thrown* | no able colonist |
| 8 | `raccoon` | The Raccoon Incident | guard, pest, thrown, census‡ | see Reel 9 |
| 9 | `tractor` | The Tractor of Tomorrow | barn to blast | no yard or foe |
| 10 | `twins` | Two Tractors | planted, pull, blast | no yard or foe |

\* Or `thrownAlone` when no colonist is left to volunteer.

‡ The census beat's words are computed: AnyRim's own census line, with
the real count of surviving residents (`TourSceneScript.Computed`).

† And `stragglers` before the aftermath when a cleanup volley fires. The
stampede declines without a Ballista, with fewer than 256 explosive bolts
in its chain, without the boomalope in the game, or when no open ground on
the east edge reaches the base.

`TourScript` (Core) holds the order and the beats; the def's XML, the
scene classes and the tests all read it. The Ballista picks no target
of its own during the tour (`AutoTargetHeld`): it fires only the
volleys it is ordered to.

**Colonists are never the casualties** (the operator, 2026-10-14;
review step of slices 13000 to 16000). From a scene's set-up until it
ends, the self-destructs, the grenade and both tractor scenes hold
the colonists clear of everything the scene can reach
(`TourScene.HoldColonistsClear`): any colonist in reach is moved back
to the base, every other is drafted with Fire At Will off, and every
30 ticks any who came back in (one that could not be drafted, say)
is moved out again and logged. The reach is 87.6 tiles round a pad
(the self-destruct's blast, fires, secondaries and barrage, with the
planner's margin), 16 tiles round the grenade's pile, and the yard
grown by 31 tiles for the tractors. The grenade's grenadier and
volunteer and the tractor's stray are placed by their scenes and
spared. They are let go when the scene ends, however it ends. A
hauler had walked back into the reactor's barrage in one verify run
of slice 14000; the self-destructs had cleared colonists only at
set-up and arming. The verifier now fails any colonist hurt, downed
or killed in any scene, but the volunteer during the grenade; harm
done beside one of the map's starting hostiles (a mechhive's, dark
entities) is noted instead, since the tour did not cause it. The
plan's tourists, a party of visitors who would be the casualties a
scene needs, were not built in this step (`docs/AI-TODO.txt`).

**Pacing on the overlay.** Each line is read in full and gone two
seconds before the next is due (Core's `TourPacing`: its reading
time, the 1-second fade and the 2-second lead, at 60 ticks a
second). A scene's start line waits until the previous scene's
outcome lines have cleared (460 to 520 ticks; 940 after the raccoon's
two-line verdict); a cue's second line waits for its first (the
home scene's tagline at 660 ticks, the raccoon's thanks at 460, the
byline at 430); and each scene holds its action for an intro until
its start lines have cleared (`TourScene.IntroTicks`, 710 to 1,294
ticks), so its first beat comes on a clear screen. Everything a
scene does is timed on its own clock, which starts after the intro,
so the tuned timings inside each scene are unchanged; the stampede's
herd and the twins' robots arrive after it. Each scene's timeout
grew by its intro. The scenes now run about 23,700 ticks in all (6.6
minutes at normal speed, against about 4.2 before), and the menu says
8 minutes (it said 7); `TourXmlTests` hold the def to these rules.

The tour's tractors run one setting up from the defaults: Popular (a
22.5-tile pull) and Block Party (a 12-tile blast), as the operator asked on
2026-10-14 ("bump up to the next setting"; slice 16000, Core's
`TourStagePlanner.TourPull` and `TourBlast`). The barn in the Tractor of
Tomorrow is set the same, and says so on its pane. That robot is Prudent,
so it stops 15 tiles short of the party (its emplacing range is nearer
than 21.5); the barn stands 30 tiles west of the yard's centre and the
party 12 east, so the pull, reaching 22.5 tiles from where the robot
plants, falls more than 2 tiles short of the barn. The stray it protests
about is a colonist with nothing to do, else the one in the Brain
Squeezer, else the grenadier (drafted and holding fire); the scene logs
whom it chose. The twins stay Nosy and plant 14 tiles apart, each outside
the other's blast.

### Scene 3: Uninvited Guests (the Stampede)

Requested by Matt "Breakpoint" Heck on 2026-10-14 ("a 256 bolt volley
taking out hundreds of incoming boomalopes from the east") and built in
slice 14000. Core's `Stampede` holds the rules; the scene is
`Source/Tour/StampedeScene.cs`.

- **The herd.** 200 manhunting boomalopes (adults, so each bursts at 4.9
  tiles) enter on the east edge, 25 a tick, in a dense front 8 tiles deep and
  41 tall, level with the base and 2 tiles in from the edge: about 0.6 a cell.
  Only standable cells that reach the base count; with too few, the band
  widens twice (12 by 61, then 16 by 91), then each size is tried again 35 and
  70 tiles further north along the edge (never south, where the tractor yard's
  candidates lie), before the scene declines, naming the best band's counts.
  They go manhunting silently, so no letter or message interrupts, and make
  for the colonists. Measured in the verify runs: 2.8 tiles a second in the
  rain, and 220 game ticks a second at Superfast against about 288 in the
  other scenes (4.5 ms a tick against 3.5), so the herd stays at 200.
- **The volley.** The Ballista loads explosive (Really Painful) bolts: each
  lands as a 3.9-tile bomb, and every boomalope it kills bursts in turn. Its
  first volley is 256 bolts, led to where the herd's centre will be when they
  land (the herd's own drift over the last second, and the bolt's 0.45 tiles a
  tick). The grid sets the range: the demo base has one 250 kW reactor and no
  batteries, so the Ballista fires when the lead point is within the longest
  range whose firing draw the reactor covers with the magazines' 1.5 kW
  feeding and a 10 kW margin spare (`BallistaPower.LongestRangeWithin`): 76
  tiles, about 60 tiles out from the base's edge. The three magazines hold
  1,200 explosive bolts. In the verify runs the volley left between scene
  ticks 761 and 1,040 (13 to 17 s in at normal speed; usually about 800), with
  the herd's centre about 84 tiles off, and killed all 200 in all but one run,
  where one 128-bolt cleanup volley finished 51 stragglers. At most two such
  volleys may follow, at the thickest group of stragglers
  (`Stampede.BestCover`), each after the Ballista's 5-second cooldown and once
  the last bolt has landed. While the first volley waits, the scene logs where
  the herd is every 600 ticks; it gives up 1,800 ticks after tick 360 and
  sends the herd home.
- **Fire.** It rains for the scene: the rain starts at full strength as the
  scene opens, so the boomalopes' fires cannot take the grassland, and the
  bolts' bomb waves put out any fire they touch. During the aftermath's hold
  the scene puts out a share of whatever still burns each tick, so the last is
  out as it ends; at the end it puts out the rest and gives back the weather
  from before, at once. In the verify runs 0 to 13 fires were burning at the
  aftermath, all out by the end. No batteries were added: a vanilla battery
  under open sky can short-circuit in rain. The verifier counts fires near
  the base while the scene runs (none may start within the 25-tile
  keep-away); its count after the scene shows only that the clean-up ran,
  since the scene's end puts every fire out.
- **Safety.** The colonists are moved inside the base's square and drafted
  with Fire At Will off, so none wanders east and none fires at a
  boomalope near the base (the grenadier, holding the singularity
  grenade, would otherwise throw it at the first one in range); they
  are undrafted at the end. No volley is ordered whose footprint (its
  scatter, a bolt's 3.9-tile blast and a boomalope's 4.9-tile burst)
  reaches the base's square or a colonist (Core's `Stampede.SafeAim`):
  a refused first volley sends the herd home at once, and a refused
  cleanup ends the volleys. A boomalope counts as
  having reached the base inside 25 tiles of its square, five times its own
  blast. Any boomalope still on its feet at the aftermath stops manhunting and
  walks off the east edge (a downed one stays down); any still alive at the
  end is removed, however the scene ended (Skip and Stop included), so no herd
  outlives the scene. The Ballista holds its fire again, at its old count of
  16.
- **Camera and beats.** The opening shot looks at the band (zoom 40). `herd`
  at tick 150 frames the herd, a little west of its centre; `volley` frames
  the Ballista and the impact together (zoom 56) and selects the Ballista,
  whose inspect pane reads "Volley: 256 x really painful bolt"; `stragglers`
  plays with a cleanup volley, but no sooner than 510 ticks after the
  first, so the operator's volley line gets its full reading time (the
  cleanup itself fires at once); `aftermath` frames the impact (zoom 36)
  and holds 420 ticks. The aftermath beat waits at least 510 ticks after
  the last volley, for the same reason.
- **Narration.** The volley line is the operator's own (2026-10-14), word for
  word, with his spelling and em dashes: "Two hundred fifty-six largely
  inaccurate—but highly explosive—Ballista bolts make for a firm, but
  neighborly, "No."" At 115 characters it raised the narration limit from 96
  to 120 (`docs/ARTISTIC-MOTIF.md`, "Narration"). The smoke runner checks that
  every line fits the overlay in two lines on a 3840 by 2160 screen at 2x UI
  scale, with a glyph for each character.

## The Directed Tour

Requested by Matt "Breakpoint" Heck on 2026-10-13 and built with the
Tractor Bomb's showcase work in slice 12000 (`ROADMAP.md`), on the
Showcase API's director. The tour takes ownership of the camera, directs
the action, and shows one major feature at a time, explaining as it
goes. The bullets below were the plan; the notes in brackets say how it
was built.

- **A director.** A small state machine runs scenes in order. Each scene
  sets up, frames the camera, says what is happening, runs, and waits for
  the action to finish (an aftermath ending, a pull completing, the smoke
  settling) rather than a fixed delay. Then the next begins.
- **Commentary.** Pop-up comments as game events: vanilla messages, with
  look targets so a click jumps to the subject. [Replaced in slice
  13000, at the operator's request, by the Showcase API's narration
  overlay: one large, silent line at a time in the lower third,
  `docs/SHOWCASE-API.md`, "The Narration Overlay".] They are written in the
  motif's deadpan voice (`docs/ARTISTIC-MOTIF.md`). Each chapter also
  gets a short caption bar, drawn by the Showcase API as a title card at
  the top centre, just below the colonist bar ("2 of 8: The Batshittium
  Reactor"). It was first proposed for the bottom of the screen, but the
  bottom holds the selected subject's gizmos, the inspect pane and the
  main buttons, and the bar draws before them (`GameComponentOnGUI` runs
  ahead of the map interface and the window stack), so there it would
  cover them and take their clicks (`docs/AI-DECISIONS.txt`,
  2026-10-14).
- **The camera.** The tour moves and zooms it for every beat, framing the
  subject, and pulls back for anything with a big radius.
- **Scenes** (as built; see "The Scenes" above): the demo base's
  devices; the Ballista's volley (the Artificer appears there); the
  boomalope stampede (added in slice 14000); the two self-destructs;
  AnyRim; the grenade at the junk pile west of the base; the Raccoon
  Incident in AnyRim (added in slice 15000); the Tractor Bomb against a
  staged raider party, with a colonist's protest; and two Tractor Bombs
  in a junkyard as the finale. Changes from the plan: AnyRim and the
  grenade come before the Tractor Bomb, so the film ends on its biggest
  bang; the grenade stays at the junk pile, and the Raccoon Incident
  (Reel 9) follows it as its own scene (the operator's "test both",
  2026-10-14); the barn does not build the robot on screen ("This one
  was made earlier"; `docs/AI-TODO.txt`). Both proposals are in
  `docs/AI-requests-for-humans.txt`, items 17 and 18.
- **Log cues.** Each scene logs `SCENE`, `BEAT` and `END` cues (or
  `SKIPPED`), replacing the old `THROW` and `TOUR` cues, which are gone.
- **Letting go.** The caption bar's buttons, provided by the API: Skip
  ends the current scene, and the next begins at once; Stop ends the
  tour, after which the director stops moving the camera and narrating,
  the game keeps running (still never autosaving), and a device already
  counting down still goes off. At the end of the last scene the
  director lets go of the camera on its own.

### The Story *(draft)*

Requested by Matt "Breakpoint" Heck on 2026-10-13: a narrative to tie the
showcase together. The tour is a 1950s industrial film, the kind that
gave the world "Duck and Cover" and "The House in the Middle", in which a
cheerful narrator presents the future, in reels.

**Living Better with Batshittium.** *A Batshit Technology Public
Information Film.* The caption bar reads like a title card throughout.

- **The narrator** is warm, confident and on message. He is the motif's
  deadpan, spoken aloud. As the reels go by his reassurances grow
  shorter, and in the final reels they are mostly about remaining calm.
  He never breaks the fourth wall, and he never admits anything is wrong.
- **The cast** is the colony, introduced as "our volunteer family". The
  grenade's volunteer is "a valued member of the family". AnyRim's
  residents are "our neighbours". Captions name colonists by their real
  names.
- **The premise** is the arc. The film opens on the byline's first three
  promises (sooner, cheaper, funnier) and closes, once the evidence is in,
  on the fourth (and ludicrously dangerous).

Reels and sample narration (final lines are written with the scenes):

1. **Reel 1: Power for the Modern Colony.** The reactor. "Two hundred and
   fifty thousand watts from a single unit of batshittium. Clean,
   abundant, and only occasionally hot enough to cook the kitchen."
2. **Reel 2: The Kitchen of Tomorrow.** The Random Replicator. "A
   different dinner every time. Your family will never know what's for
   supper, and neither will you."
3. **Reel 3: Rocks Into Riches.** The Transmogrifier. "Every rock on
   your land is a fortune waiting to be transmogrified."
4. **Reel 4: Leisure, Scientifically Applied.** The Brain Squeezer. "A
   firm, reassuring squeeze for the overworked mind."
5. **Reel 5: Home Defence.** The Artificer and the Batshit Ballista.
   "Sixteen bolts, one volley, and a neighbourhood with very few
   complaints."
6. **Reel 6: When Devices Retire.** The replicator's and the reactor's
   self-destructs. "Every Batshit device knows when to step aside.
   Please step aside also."
7. **Reel 7: The Tractor of Tomorrow.** The Tractor Bomb. "The Tractor
   Barn builds it. The tractor finds the problem. The problem is
   resolved." Those too close are "making peace with their impending
   doom", and the narrator explains that this is normal.
8. **Reel 8: A Typical Rim Community.** AnyRim, street by street, and the
   Survival Town history it comes from. "Every home in AnyRim is a
   proving ground. So is every neighbour."
9. **Reel 9: Comfort Is a Weapon.** The Ergonomic Singularity Grenade,
   shown in AnyRim (specified by Matt "Breakpoint" Heck on 2026-10-13):
   an over-enthusiastic security guard deals with a rabid raccoon...
   unfortunately. The incident is described below.
10. **The End.** The sign-off card: "The Future: Sooner. Cheaper. Funnier.
    And ludicrously dangerous." Then: "This concludes our presentation.
    The colony is yours. Please remain calm." The director lets go of
    the camera.

As built, the reels run in the scenes' order: Reels 1 to 4 are the
first scene, Reel 5 the second and third (the volley, then the
stampede), Reel 6 the fourth and fifth, then AnyRim
(Reel 8), the grenade at the junk pile (Reel 9's title) and the Raccoon
Incident (Reel 9's story), then the Tractor of Tomorrow (Reel 7) and the
twins, which
close on the byline. The film now opens on the first three promises
("The future, delivered sooner, cheaper and funnier."). The narration's
rules are in `docs/ARTISTIC-MOTIF.md`, "Narration".

### Reel 9: The Raccoon Incident

Specified by Matt "Breakpoint" Heck on 2026-10-13 and built in slice
15000 (2026-10-14) as scene `raccoon`, after the grenade. AnyRim's ninth
resident is the security guard of AnyRim Savings & Loan, which until
then had nobody in it: **Ned "Overkill" Lessly** (read it aloud). He
carries an Ergonomic Singularity Grenade "for emergencies", stands at
the bank door, and takes pests seriously.

1. **The setup.** The camera finds Ned at the bank. A raccoon wanders onto
   Main Street, foaming slightly: it is in vanilla's manhunter state,
   which is RimWorld's rabid. "Every community has its pests. In AnyRim,
   pest control is in the capable hands of Ned Lessly, AnyRim Savings &
   Loan Security."
2. **The incident.** The raccoon goes for a neighbour, most likely
   Constance in her lawn chair. Ned throws the grenade at it, in the
   middle of Main Street. "Ned believes in a proportionate response."
3. **The consequences.** The singularity takes the raccoon, the lawn
   chairs, the neighbours in reach, the street furniture and, very
   possibly, Ned. Survivors get the grenade's comfort and its quips
   ("Wow, that was so ergonomic!"), and the census does the rest: "AnyRim
   census revised to 6. The sign has been updated. Please remain calm."
4. **The verdict.** The narrator reads the result rather than a script:
   - If the raccoon survived: "The raccoon has been reassured."
   - If it died: "The raccoon will not be troubling anyone."
   - Either way: "AnyRim thanks Ned for his service. Unfortunately."

As built (`Source/Tour/RaccoonScene.cs`; the places are Core's
`RaccoonIncident`, in town-local cells):

- **The cast.** Ned is a regular resident (the Ancients faction, his
  household's own lord) generated capable of violence and holding the
  grenade; his duty has no combat in it, so he never throws it on his
  own (the smoke test watches him for 600 ticks). Before the camera
  arrives the scene puts him on the bank's front walk, (+5,+39), and
  Constance in her middle lawn chair, (+5,+28), facing the street, on a
  wait the scene ends.
- **Beats.** `guard` at tick 330 frames Ned close and selects him;
  `pest` at tick 780 brings the raccoon in at Main Street's south end,
  (0,−21), in the manhunter state (silently, so no message), sends it
  running up the street to the sidewalk at the Damages' gate, (+3,+25),
  and then sets it on Constance (left to path to her directly, it went
  through the front yards chewing their fences), and frames the whole
  street (zoom 30). Ned sprints to his
  post in the west lane beside the car at the bank's kerb, (−1,+33), and
  throws once, from there, as soon as the raccoon is in range: on the
  centre line that is row +21, still on the road and 8.6 tiles from
  Constance's chair, so the singularity's 10-tile pull reaches her.
  During the throw's 1.5-second wind-up the raccoon keeps coming, so the
  grenade usually lands within 10 tiles of Ned as well. If the raccoon
  is not in his range and sight 900 ticks after it came, Ned goes after
  it, the throw queued behind each sprint; while he waits, the scene logs
  where both are every 600 ticks. `thrown` plays as
  the grenade leaves his hand; `census` plays once the singularity is
  over, on the welcome sign, with the census line said by the scene.
  The scene is done 390 ticks later; Constance's and Ned's fates are the
  world's.
- **The verdict.** The outcome is `raccoonLost` if the raccoon is dead,
  `raccoonSurvived` otherwise, and either way "AnyRim thanks Ned for his
  service. Unfortunately." follows 460 ticks later, once the fate has
  been read, before the next scene's start line. The census reads
  "AnyRim census revised to N..." when the count fell, or "AnyRim
  census holds at 9. Please remain calm." when no one was lost (`AnyRimText.CensusReport`). While a showcase
  narrates on the map, AnyRim's own census message stays out of the
  message stack, so the line is not said twice.
- **Afterwards.** However the scene ends, a living raccoon is calmed,
  Ned is stood down, a grenade he dropped is taken back, and Constance
  leaves her chair.
- **Declining.** It declines when AnyRim is not on the map, when the
  AnyRim scene was skipped (the tour never shows the town's incident
  without the town; the verifier's default `-TourSkip anyrim` therefore
  skips it too), when Ned is not standing with his grenade, when the
  game has no raccoons, or when Main Street's south end is blocked. The
  tour asks only before it starts; if Ned has been lost by the time the
  scene begins, no raccoon comes and the scene closes after its least
  time on its "completed" line, which thanks him regardless (the
  verifier then reports its missing beats and facts).
- **Verifier facts.** Ned threw the grenade (the projectile's thrower,
  seen in the air); one more implosion; the END cue's outcome matches the
  raccoon's fate; the census line matches `AnyRimText.CensusReport` of
  the counts before the scene and at the tick it was said; no census
  message entered vanilla's stack; no colonist was hurt within the
  incident's reach (Main Street from the entry to the bank, grown by the
  throw and the pull, or within 5 tiles of the raccoon). The tour-wide
  fact that no colonist is harmed in any scene covers the rest of the
  map, and both the grenade and the raccoon scenes must end with no
  singularity field on the map and no implosion still ticking, so the
  grenade's explosion can never land in the next scene again.

*(Proposals:)* the reels map onto the scenes above (Reels 1 to 5 are the
demo base and the Ballista); the narrator's lines live in XML so they
can be translated and tested, as the Brain Squeezer's monologues do; and
a future Colony Bomb reel ends the film with "Holy shit!" and "I know,
right?".

## Safety Rules

- **Demo corners keep away from AnyRim.** The replicator's reach,
  counting the fires and secondary blasts it starts, is 83.6 tiles; the
  reactor's barrage reaches 74.6. A corner is a candidate only if it is
  more than 87.6 tiles (the larger reach plus a 4-tile margin) from every
  part of AnyRim, tapering roads included. The site search keeps the
  north-east and south-east corners clear by the same rule, measured the
  same way, so both demos run; the most open of the two gets the
  replicator. If ever only one corner qualifies, the showcase logs it,
  the reactor's scene declines (`SKIPPED retire-reactor`), and the
  captions' "i of N" count only the scenes that run.
- **AnyRim keeps away from the base and the grenade range:** at least 10
  tiles from the base's cleared square and 15 from the grenade clearing.
- **The stampede stays east of the base.** The herd must be 90 per cent
  dead before any boomalope comes within 25 tiles of the base's square,
  the colonists wait drafted inside it with Fire At Will off, no volley
  may land within its footprint of the square, rain keeps the fires
  from spreading, and no live boomalope outlasts the scene (see "Scene
  3" above).
- **Colonists are held clear until it is over.** Every dangerous scene
  keeps colonists out of its reach from set-up to its end, not only as
  it arms ("The Scenes" above).
- **The tractor yard and the pads are planned together** (Core's
  `TourStagePlanner`, slice 12000). The yard, 65 by 29 tiles, keeps 31
  tiles from the base square, the grenade clearing and every part of
  AnyRim: as far as anyone runs from one of the tour's Popular tractors
  (its 22.5-tile pull plus 8), rounded up (18 before slice 16000). Its
  candidates lie south-west, south and south-east of the base, 62 tiles
  south of it, so the yard's top edge is 32 tiles below the square's.
  Each pad keeps 87.6 tiles from AnyRim and from the yard, measured
  from the device's own cell. The first yard that
  leaves two pads wins; failing that, the one that leaves the most. Every
  keep-away goes through one rule, `Clears(Box, KeepAway)`, and AnyRim
  through `TownSite.Clears`.
- **Fire cannot walk in.** A fire spreads to adjacent cells or throws a
  spark two cells. AnyRim is ringed by a 4-tile firebreak of bare ground,
  and its roads are non-flammable. Its own lawns and wooden houses burn
  normally once something inside the ring sets them alight.

## AnyRim

### The Idea

Cold War legend had the Soviets training spies in replica American towns:
TIME reported "Iowa in the Ukraine" in 1959, and Nelson DeMille's *The
Charm School* made it a novel. The real thing was in Nevada:

- **Operation Doorstep, 1953, "Doom Town".** Two furnished colonial
  houses stood 3,500 and 7,500 feet from the tower.
  - Mannequin families were dressed by JCPenney: Dad reading by the
    window, Mom in the kitchen.
  - Fresh food was flown in for the dinner tables.
  - The near house was destroyed in 2⅓ seconds on camera.
- **Operation Cue, 1955, "Survival Town".** Ten houses were built in
  matched pairs of wood frame, brick and concrete, at a near and a far
  distance. A utility substation, a radio station, cars and a bank vault
  stood among them.
  - The canned food and the beer survived and were taste-tested.
  - The brick house became "little more than a heap of rubble"; a
    basement shelter stood intact under it.

AnyRim is that town, transplanted to the Rim and shrunk to one street: an
earnest little community that knows exactly what it is for. Its jokes are
in what is placed where, in the deadpan voice of
`docs/ARTISTIC-MOTIF.md`, and every gag doubles as a test fixture.

### The Plan

Coordinates below are local: (0,0) is the crossroads, which is also
Ground Zero. The site search prefers map cell (55,163); on open ground
it settles a few tiles away, near (51,166), because candidates lie on a
2-tile grid and the Ballista yard must stay 15 tiles from the grenade
clearing.

```
                        N
            +-----------+-----------+
            |  AnyRim Memorial      |        z +56..+66
            |  Gardens (10 graves)  |
            +-----------+-----------+
                        | Main Street dead-ends at the cemetery
   [W4 Graves & Sons    |   [E4 AnyRim Savings
    Funeral Home]       |    & Loan, vault]           z +35..+43
                        |
   [W3 Knott house]     |   [E3 Damage house]         z +21..+29
   shelter, Daisy's pen |   lawn chairs, Priscilla's pen
                        |
   [W2 Now house]       |   [E2 Town Hall & Fire]     z  +7..+15
 Substation ====== Doorstep Lane ==(X)============= east arm ...
 No. 1                  | Ground Zero                 z 0
   [W1 Five-and-Dime    |   [E1 Public Works] [Ballista yard]
    & Lunch Counter]    |                             z -15..-7
                        |
                 Welcome sign (east verge)
                        :  road tapers to dirt
```

### Streets

- **Main Street:** carriageway x −2..+2, z −21..+47, concrete painted
  dark grey, with a pastel yellow centre line on x 0 (2 cells painted,
  2 not).
- **Doorstep Lane,** the cross street: carriageway z −2..+2,
  x −20..+20, painted the same, centre line on z 0.
- **Sidewalks:** paved tile on x ±3 and z ±3.
- **Ground Zero roundel** at the crossroads: gold tile within radius
  1.5, marble flagstone within 3, slate flagstone within 4.5.

- **The roundel** is laid only on street and sidewalk cells. It doubles
  as a range card: every distance in town is measured from it, and the
  centre lines stop short of it.
- **Doorstep Lane** is named for Operation Doorstep. Its verges stay
  bare, a firebreak between the south pair and the rest of town.
- **Tapers.** The roads run on a little past the town, then taper off:
  - South: broken asphalt z −26..−22 (5 wide narrowing to 3); gravel
    z −31..−27 (3, then 2); packed dirt z −36..−32, one wide, wandering a
    tile left or right, with every third cell left natural; then
    nothing.
  - North: broken asphalt z +48..+50, gravel +51..+53, packed dirt
    +54..+55, ending at the cemetery gate at (0,+56).
  - West arm: broken asphalt x −24..−21, gravel x −26..−25, ending at the
    substation gate at (−27,0).
  - East arm: broken asphalt x +21..+26, gravel x +27..+32, packed dirt
    x +33..+40, wandering, gaps; then nothing.
  - The wander and gaps come from a seeded random stream, so the same
    seed always gives the same town.

### Lots

Every building is 9 tiles along the street. The street-facing wall (the
facade) stands 7 tiles from the street's centre line: x −7 on the west,
x +7 on the east. In front of it lie a 3-deep front yard (lawn, a wooden
picket fence on the sidewalk edge, a gate, a paved walk to the front
door) and the sidewalk. Facing fronts are 13 tiles apart.

| Lot | Outer footprint | Building |
|-----|-----------------|----------|
| W1 | x −15..−7, z −15..−7 | AnyRim Five-and-Dime & Lunch Counter |
| E1 | x +7..+17, z −15..−7 | AnyRim Light, Power & Public Works |
| W2 | x −15..−7, z +7..+15 | The Now residence (wood) |
| E2 | x +7..+17, z +7..+15 | Town Hall & Volunteer Fire Department |
| W3 | x −15..−7, z +21..+29 | The Knott residence (brick) |
| E3 | x +7..+15, z +21..+29 | The Damage residence (steel) |
| W4 | x −17..−7, z +35..+43 | Graves & Sons Funeral Home |
| E4 | x +7..+15, z +35..+43 | AnyRim Savings & Loan |

Side yards between lots are 3 to 5 tiles and hold gravel driveways and
pens. Outbuildings:

- **Substation No. 1:** an unroofed 11 by 11 steel fence ring at
  x −37..−27, z −5..+5, gate at (−27,0), with the reactor at (−32,0).
- **Ballista yard:** a fenced, unroofed 9 by 11 yard behind Public
  Works, x +19..+27, z −16..−6, gate on its west side, ringed with
  sandbags.
- **The Knott shelter:** a 5 by 5 granite-block shelter behind the Knott
  house, x −23..−19, z +23..+27. Its steel door faces west, toward the
  reactor.
- **AnyRim Memorial Gardens:** a wooden-fenced cemetery at x −6..+6,
  z +56..+66, gate at (0,+56).
- **News Nob:** eight stools behind a row of sandbags east of town,
  about (+45..+52, +12), named for the Nevada press point. Colonists can
  watch from there.

### Buildings

The three residences share one floor plan (Plan A), mirrored for the east
side, so that they compare directly as test pieces:

- **Living room** (street side, 3 by 7 inside): a 2 by 2 table with two
  dining chairs, two armchairs facing a tube television (switched off:
  nothing good is on), an end table, a standing lamp, and a potted rose.
- **Bedroom** and **kitchen** (3 by 3 each) behind a partition with two
  doors. The kitchen has an ancient refrigerator, sink and stove, a small
  shelf of ten survival meals (Operation Cue's pantry test) and a six-pack
  of beer.

Each building, with its walls and floor:

- **W1, AnyRim Five-and-Dime & Lunch Counter** (limestone blocks,
  limestone tile):
  - three aisles of shelves: chocolate, survival meals and pemmican;
    beer, kibble and herbal medicine; cloth;
  - six Ergonomic Singularity Grenades on a small shelf by the till,
    where impulse purchases belong;
  - a lunch counter (two 1 by 2 tables, four stools, an electric stove);
  - Bea's bed in the stockroom;
  - a vending machine at the door, a shopping cart in the gravel lot.
- **E1, AnyRim Light, Power & Public Works** (steel, concrete; an
  autodoor):
  - the Transmogrifier, its bill suspended, with granite chunks by its
    hopper;
  - the Artificer, fuelled and switched off (left running, its bolts
    would be hauled 100 tiles to the base's magazines);
  - a 3 by 3 "employee wellness" room with the Brain Squeezer (switched
    off) and a potted plant;
  - the Random Replicator, still in its crate;
  - a shelf of 30 batshittium;
  - Morty's bed, a desk and two lamps.
- **Ballista yard** (steel fence, sandbags): the Batshit Ballista,
  switched off, loaded with 16 painful bolts, and two chained magazines
  (200 painful, 100 really painful).
- **Substation No. 1** (steel fence): the Batshittium Reactor, fuelled,
  auto-refuel off, and a small shelf of 10 batshittium. No batteries.
- **W2, the Now house** (wood, wood plank):
  - Plan A with two single beds, for Wendy and her lodger;
  - dinner for two (fine meals) on the table;
  - one armchair turned to the window, with a novel on the end table
    beside it: the evening paper;
  - a rusted car in the driveway, a postbox and a porch light.
- **E2, Town Hall & Volunteer Fire Department** (wood, wood plank):
  - a council chamber with a 2 by 4 table and eight dining chairs, one
    per resident;
  - the mayor's office: desk, armchair, bed and lamp;
  - a fire bay with three firefoam poppers;
  - Engine No. 1 (a rusted truck) in the side yard, and a hydrant on the
    corner.
- **W3, the Knott house** (sandstone blocks, which read as brick;
  mustard carpet):
  - Plan A with a double bed;
  - the shelter out back: a bed, a shelf of 20 survival meals, beer, a
    lamp, and a thick rock roof;
  - Daisy the Boomalope in a fenced pen with hay;
  - a potato patch.
- **E3, the Damage house** (steel, metal tile):
  - Plan A with a double bed;
  - three armchairs on the front lawn, facing the street, beside an end
    table with a six-pack;
  - a rusted car frame in the driveway;
  - Priscilla the Pig in a fenced pen.
- **W4, Graves & Sons Funeral Home** (marble blocks, burgundy carpet; an
  ornate front door):
  - a showroom of three sarcophagi (marble, steel and wood);
  - a viewing room: two rows of chairs facing an empty sarcophagus,
    between potted daylilies;
  - Barry's bed in the back office.
- **E4, AnyRim Savings & Loan** (granite blocks, marble tile):
  - two marble columns by the door, and a teller counter;
  - a plasteel vault, with a plasteel door, holding 2,000 silver and 50
    gold;
  - an ATM on the front path;
  - Ned's cot behind the north counter, and a heater at the far end of
    the south one (until slice 15000 nobody lived here).

Around town:

- Lawns of grass, one grown oak per house, potted roses at front doors,
  picket fences and gates, and postboxes.
- Powered standing lamps at the lot lines on alternating sides of Main
  Street, and a dead ancient lamppost at each end of town.
- Hydrants on two corners of the crossroads.
- Cars stand at measured distances from Ground Zero (about 12, 12, 21
  and 33 tiles), as Doorstep's cars did.
- Ten graves in the cemetery: nine reserved, one for each resident, and
  one unreserved; two oak trees.

### Residents

Nine residents, each with a home and a reserved plot. The nickname is
what shows above a pawn on the map.

| Name | Age | Home |
|------|-----|------|
| Hugh "Lame Duck" Moribund | 63 | Town Hall |
| Wendy "Any Minute" Now | 34 | Now house |
| Ernest "Next" Inline | 41 | Now house |
| Shirley "Probably Fine" Knott | 52 | Knott house |
| Constance "Collateral" Damage | 46 | Damage house |
| Barry "Six Feet" Graves | 58 | Funeral home |
| Bea "Right Back" Shortly | 29 | Five-and-Dime |
| Morty "Two Weeks" Tallis | 64 | Public Works |
| Ned "Overkill" Lessly | 44 | Savings & Loan |

- **Hugh** is mayor, fire chief and notary, in a bowler hat.
- **Wendy** keeps dinner warm. **Ernest** is her lodger, reading by the
  window in a cowboy hat.
- **Shirley** wears a foil helmet and owns a shelter rated for one.
- **Constance** watches every test from her lawn chair.
- **Barry** has reserved a plot for everyone, himself included; duster
  and bowler.
- **Bea** runs the till beside the grenades.
- **Morty** is two weeks from retirement, in a hard hat.
- **Ned** guards the Savings & Loan in a flak vest, with an Ergonomic
  Singularity Grenade "for emergencies" (Reel 9).

How they behave:

- **Faction:** RimWorld's hidden, neutral Ancients faction. Harming its
  members costs no goodwill and sends no letters, so the town can be
  blown up freely. Residents never turn hostile.
- **Daily life:** each household gets its own lord,
  `LordJob_AnyRimResident`. It assigns a custom duty,
  `BT_AnyRimResident`, which has no combat in it. Residents:
  - wander within 8 tiles of their front door;
  - never get hungry (civic composure sees to it; the kitchen meals are
    set dressing, as the Doom Town food was);
  - sleep in their own beds;
  - seek shelter from dangerous temperatures within 16 tiles of home,
    and no further.
  
  The lord keeps downed residents, so nobody wanders off the map.
- **Composure:** a hediff, `BT_AnyRimComposure` ("civic composure"),
  blocks mental breaks, holds mood steady and stops hunger, and residents
  never panic and flee. They do still dodge a grenade they can see.
- **Generation:** from a custom kind, `BT_AnyRimResident`:
  - adults with the fixed names, genders and ages above;
  - industrial clothes plus the hats named, and no weapons but Ned's
    grenade (he is generated capable of violence, so he can use it);
  - no royal titles, and baseliners only.
- **Census:** when a resident dies, a quiet message (not a letter) reads
  "AnyRim census revised to 8. The sign has been updated. Please remain
  calm." While a showcase narrates on the map the message is left out,
  since the narrator reads the census (Reel 9).

### Signs and Labels

There is no sign building in RimWorld, so signs are a small structure (a
wall between two columns) with text drawn above it by
`MapComponent_AnyRim`. Text shows at middle zoom or closer, in neutral
colours, since magenta is reserved for batshittium.

- **Welcome sign,** at the south entrance on the east verge, (+5,−24):
  WELCOME TO ANYRIM / A Typical Rim Community / Pop. 9, subject to change
  / Est. this morning. Re-est. as needed. At zero the population line
  reads "Pop. 0. Thank you for your participation."
- **Exit sign,** on the east arm, (+31,−4): NOW LEAVING ANYRIM / Come
  back soon. We will try to be here.
- **Cemetery gate:** ANYRIM MEMORIAL GARDENS / Reservations only.
- **Substation gate:** SUBSTATION No. 1 / Capacity 250 kW. Demand: a few.
- **Main Street,** just north of the crossing: EVACUATION ROUTE: NORTH
  (north is the cemetery).
- **Public Works door:** DAYS WITHOUT INCIDENT: n, reset whenever a
  resident dies.
- **Ground Zero,** on the roundel: GROUND ZERO.

### Ownership and Housekeeping

Colonists must not commute 100 tiles to use, haul from or fight fires in
the town, so:

- **Owned by the Ancients:** town structures and furniture. Residents use
  only beds of their own faction, colonists only theirs, and Ancients
  doors open for anyone who isn't hostile.
- **Owned by the player,** so their buttons work: the Batshit devices,
  the Ballista and its magazines. The Ballista and magazines accept no
  hauled bolts, so colony haulers never carry ammunition to town; they
  hold what they were built with.
- **Power:** RimWorld's power nets do not check faction, so the
  player's reactor lights the Ancients' lamps. Conduits run from the
  substation along Doorstep Lane's north sidewalk, up both Main Street
  sidewalks, and into every building and the Ballista yard.
- **Things left off,** so that nothing draws colonists over: every TV,
  the Brain Squeezer, the Artificer and the Ballista are switched off.
  The reactor and Artificer have auto-refuel off, and the
  Transmogrifier's bill is suspended.
- **Forbidden:** every loose item in town. Colonists and haulers ignore
  forbidden things. Unforbid what you want to take (the grenades, for
  instance).
- **Home area:** player buildings extend it as they spawn, so the town
  is removed from it afterwards. Colonists do not fight fires, repair or
  clean in AnyRim.
- **Cold climates:** each house gets a heater on the town grid if the
  outdoor temperature is below 5 °C when the town is built.

### Heat, Fire and Cook-Off Rules

These are checked by unit tests on the plan:

- **Reactor heat:** no roofed cell within 13 tiles of the reactor. It
  heats every enclosed room within 12 tiles to 200 °C. Its yard is
  unroofed, and RimWorld treats a room 25% or more unroofed as outdoors.
- **Cook-off reach:** the Ballista yard is more than 30 tiles from the
  reactor. A magazine's death blast throws bolts up to 25 to 30 tiles, so
  one test cannot chain into a town-wide barrage by accident.
- **The crated replicator:** it stays in its crate because, installed, a
  stray hit could set off its 75-tile blast, which reaches the base. To
  try it, install it at the north end of town.
- **The barrage control:** the Knott shelter's thick rock roof swallows
  overhead bolts, while constructed roofs cave in under them. A lone
  5 by 5 thick roof is far too small to attract an infestation.

### What to Test Where

- **Five-and-Dime:** the singularity grenade (shelves of small stacks
  scatter well), and the grenades themselves.
- **The three houses:** the same plan in wood, brick and steel at
  matched distances, for the grenade, the Ballista and the barrage.
- **Town Hall:** fire, with wooden walls and floors and real firefoam
  poppers.
- **Funeral home:** marble, the weakest stone, and three sarcophagi in
  three materials.
- **Savings & Loan:** a nested plasteel vault. Does the singularity rip a
  plasteel door loose?
- **Knott shelter:** the barrage control, under its thick roof.
- **Substation:** the reactor's self-destruct, 768 bolts over the whole
  town.
- **Public Works:** three death blasts in one shed (rock spew, brain
  squeeze, molten spray), with the Ballista yard next door.
- **Daisy the Boomalope:** chain reactions.
- **News Nob:** watching from a safe distance.

### Site Selection and Ground Work

The quick-test map is random, so the town finds its own ground:

- **Candidates:** crossroads positions on a 2-tile grid near the preferred
  spot (55,163), within x 45..60 and z 150..177 on a 250 map. They must
  keep the whole town inside the map with a 6-tile margin and obey the
  keep-away rules in "Safety Rules".
- **Rejected outright,** measured from the cells the town actually
  occupies (its parts), not from a rectangle around it:
  - anything indestructible (steam geysers, craters) within one cell of
    a building, which would otherwise lose that building;
  - lava within 6 tiles;
  - hostile pawns within 15 tiles, hostile buildings within 35, ancient
    vents within 20.

  An indestructible feature elsewhere (in a lawn, on a road) simply
  stays, and the town builds around it. Profoundly indestructible terrain
  features may even add character (Matt "Breakpoint" Heck, 2026-10-13).
- **Score:** the rest are scored by the work needed to level them:
  - deep water 6, shallow water and marsh 3, soft sand 2;
  - natural rock 4, thick rock roof 2, thin roof 1, other ruins 1;
  - 10 per tile of distance from the preferred spot.
  
  The cheapest wins. If none qualifies (one map had an insect hive
  close enough to rule out the whole window), the search widens to the
  north-west half of the map, x 45..100 and z 120..177, with the same
  rules. If that fails too, the showcase logs `[BT-SHOWCASE] AnyRim
  skipped` and carries on without it. The smoke test then tries the
  whole map, since it tests the town rather than the showcase's site.
- **Levelling the town and its 4-tile firebreak.** Only the cells the
  town occupies are touched: each part grown by the firebreak, and the
  taper cells actually laid. The empty land inside the town's bounding
  rectangle is left alone.
  1. Unfog the area first, so that breaking into a fogged cave sends no
     "area revealed" letter.
  2. Move pawns out.
  3. Remove roofs before rock, so that nothing collapses.
  4. Destroy everything destroyable, with no leavings or blasts.
  5. Clear any pending roof collapses.
  6. Remove player zone cells from the cleared ground.
  7. Turn ground without heavy affordance (water, marsh, mud, soft sand)
     into soil; other natural ground stays, so each run gives a desert,
     tundra or forest AnyRim.
  8. Lay the planned surfaces, then build.

## Implementation

The town is data first, so that its geometry can be unit tested without
RimWorld:

- **`Core/TownGeometry.cs`:** cells, boxes, facings, and RimWorld's
  footprint rule for multi-cell things. The rule is mirrored from
  `GenAdj.OccupiedRect` and checked against known cases.
- **`Core/TownPlan.cs`:** the whole town as declarative lists in local
  coordinates:
  - terrain (def and paint), roofs and placements (def, stuff, cell,
    facing, owner, forbidden, count or growth);
  - buildings, residents, labels and key points;
  - `TownPlan.AnyRim(seed, palette)` builds AnyRim;
  - `Avoiding(obstacles)` drops whatever an obstacle would overlap.
  
  The Colony Bomb will reuse the plan at other sizes and quality tiers.
- **`Core/TownPalette.cs`:** street surfaces, walls and furniture
  quality for the six quality tiers of `docs/COLONY-BOMB.md`. AnyRim is
  Medium.
- **`Core/TownSite.cs`:** the site scorer, over a cost grid the game
  fills in.
- **`Core/AnyRimText.cs`:** the sign, census and message texts.
- **`Source/AnyRim.cs`:**
  - finds a site, levels it, and executes the plan in order (terrain,
    roofs, buildings, furniture, items, plants, animals, residents);
  - wires power, clears the home area, and registers the town, saving
    the obstacles it built around so the exact town (signs included) is
    known after a load;
  - `AnyRim.Demolish` takes it down again, over the same cells. It
    removes only the town's own residents and animals: a resident the
    player recruited or an animal the player tamed stays.
- **`Source/AnyRimResidents.cs`:** the lord job, its toil, and resident
  generation.
- **`Source/MapComponent_AnyRim.cs`:** the saved town record, the census,
  and the sign and label text.
- **`Defs/Misc/BT_AnyRim.xml`:** the duty, pawn kind and composure
  hediff.
- **Debug actions,** under "Mods" in the dev-mode Actions menu: "Build
  AnyRim here" (click a crossroads; it levels whatever stands there),
  "Build AnyRim (best site)" and "Remove AnyRim". The operator can use
  them in his own dev-mode games. The best site keeps the town 10 tiles
  from the colony's buildings, and its reactor more than 78.6 tiles away
  (its barrage's 74.6 plus a margin).

### Tests

- **Unit tests** (Core) of the plan:
  - nothing overlaps that shouldn't; every placement lies inside its
    room;
  - doors and the cells in front of them are clear; every room is
    reachable from the street;
  - the cross street runs north of the first pair; each side has 3 to 5
    lots;
  - the tapers narrow and end; everything lies inside the bounds;
  - the heat and cook-off rules hold;
  - the same seed gives the same town;
  - nine residents with nicknames of 16 characters or fewer, only Ned
    armed;
  - the footprint rule; `Avoiding`; the site scorer; the texts.
- **XML tests:** the new defs follow the house rules, and every def name
  the plan uses exists in RimWorld's Core data (when the game is
  installed) or in the mod.
- **Smoke test** (`TestAnyRim`, run after the non-destructive tests and
  torn down before the destructive ones):
  - the town builds at a scored site;
  - every placement spawned where planned;
  - houses are enclosed, roofed rooms reachable from the street;
  - no roof collapse pending before the first tick, and every roofed
    cell still roofed after it; the firebreak holds no plants;
  - the devices are powered; the reactor's heat ring holds no room;
  - nine named, non-colonist residents, each in a lord; Ned holds his
    grenade, nobody else is armed, and he has not thrown it 600 ticks
    later;
  - after 600 ticks each is within 20 tiles of its own home;
  - the census drops by one when a resident dies;
  - demolition leaves nothing behind;
  - nothing logs an error.
- **Showcase:** a screen capture at each `SCENE` cue, or at the `END`
  cue of a named scene; `-Showcase -Verify` checks the whole tour.

## Known Limits

- Residents in period clothes may suffer heatstroke in a desert map; the
  houses have heaters, not coolers.
- Raiders are hostile to the Ancients and will shoot residents. That is
  acceptable on a test range.
- Daisy the Boomalope can be killed by a predator or lightning, and will
  explode, before anyone tests anything.
- The firefoam poppers in the fire hall will douse nearby test fires.
  That is intended, but bear it in mind when testing fire on the Now
  house opposite.
