# The Showcase API

Status: built on `feature/showcase-api` (phase 4000, slices 1000 to 7000,
with the adversarial review's fixes), with `BT_Showcase` as its first
client; awaiting the merge into `trunk` (slice 8000). "As Built" below is
the contract as it stands. The narration overlay ("The Narration
Overlay") was added on `feature/tractor-bomb` (phase 3000, slice
13000); seeds, fingerprints and the quiet storyteller ("Seeds",
"Fingerprints" and "The Quiet Storyteller") in slice 18000. Requested by
Matt "Breakpoint" Heck on 2026-10-13: a game API
for showcases, and a **Showcases** button on the
main menu that lists the mods offering showcases and each mod's
showcases, as multi-stage menus or selection screens. "Because these are
amazing, and I think it would be a hell of a lot of fun to let players
use them normally." His answers to the open questions are under
"Decisions"; the proposals made while building stood, and are recorded
there too.

## What It Is

Before the API, the showcase was a dev tool: `Tools/smoke-test.ps1
-Showcase` started RimWorld with command-line flags, and
`Source/Showcase.cs` did the rest. The API turns that into something any
player can start from the main menu, and any mod can contribute to.

- **For players:** Main menu, then **Showcases**, then a mod, then one of
  its showcases, then **Start**. A fresh, throwaway game is generated and
  the showcase plays out, a directed tour with its own camera and
  commentary. Nothing touches the player's saves unless they choose to
  save.
- **For mod authors:** declare a showcase in XML (a `ShowcaseDef`), point
  it at a worker class, and optionally build it from the API's director:
  scenes, camera moves, captions and narration lines, all data-driven.
  Batshit Technology's own tour (`docs/SHOWCASE.md`, "The Directed Tour"
  and "The Story") is the first director-driven showcase built on it:
  `BT_Showcase`'s worker is `BatshitTourDirector`, whose ten scenes
  use the two hooks the tour added, a scene declining a map (G1) and a
  scene playing beats (G2).

## The Menus

1. **Showcases** (main menu button): a window listing every mod that has
   at least one showcase, with its preview image, name and number of
   showcases. Mods are found automatically from the `ShowcaseDef`s
   loaded, grouped by each def's mod.
2. **A mod's showcases:** its showcases, each with a title, a short
   description, an estimated running time, and anything it requires (for
   example a DLC). Showcases whose requirements are not met are listed
   but greyed out, with the reason.
3. **The showcase:** a details page (a longer description and a preview
   image) with **Start** and **Back**.

Back returns one level; Escape closes the whole thing. The windows use
vanilla's dialog style, so they look like part of the game. Each level
replaces the one before, so only one window is open at a time.

## The API

- **`ShowcaseDef`** (a RimWorld Def, so any mod adds one in XML; the
  fields are listed under "As Built"):
  - `label`, `description` and a short `summary`;
  - `previewTexPath` and `estimatedMinutes`;
  - `requiredMods` (package ids, such as DLCs);
  - `compatibility`: known issues with other mods, by package id, each
    with a severity (see "Other Mods") and a short note;
  - `mapSize` (default 250) and an optional `biome` preference;
  - `workerClass`, a subclass of `ShowcaseWorker`;
  - for director-driven showcases, a list of `scenes` and `narration`.
- **`ShowcaseWorker`**: the code side. `Setup(Map)` builds what the
  showcase needs once the map exists, `Tick()` runs every game tick, and
  `Finished` says when it is done. Mods with simple needs subclass it
  directly.
- **`ShowcaseDirector`**, a worker that runs scenes in order. Each scene
  (a `ShowcaseScene` subclass, configured in XML) does four things:
  1. sets up;
  2. frames the camera;
  3. shows its caption and narration;
  4. runs, and reports when it has finished.

  The director also provides the caption bar, the Skip and Stop buttons,
  the log cues for capture tooling, and letting go of the camera at the
  end.
- **Narration** lives in the def's XML as lines keyed by scene and
  outcome, so it can be translated, and the motif's voice can be checked
  by tests. It is shown on the API's own narration overlay, one line at a
  time, never as vanilla messages.
- **Starting a showcase:**
  1. Generate a throwaway game the way vanilla's dev Quick Test does
     (`Root_Play.SetupForQuickTestPlay`, then `PageUtility.InitGameStart`),
     with the def's map size.
  2. Remember which showcase was asked for.
  3. Once the map is up, start its worker from a game component.

  Quick Test itself is a dev-mode feature, but the setup it uses works
  without dev mode. A showcase game never autosaves.
- **The command line still works.** `-showcase=<defName>` starts a
  showcase directly, so `Tools/smoke-test.ps1 -Showcase` and the
  capture tooling keep working. Details under "As Built".

## Other Mods

A showcase game runs with the player's current mod list. The showcase
itself declares what it knows about other mods, and the menu acts on it
(specified 2026-10-13):

- **No warning by default.** The details page warns only when a loaded
  mod matches an entry in the showcase's `compatibility` list.
- **Concern, warning, or known incompatibility:** the details page shows
  each matching mod, its severity and the showcase's note, and Start
  still works.
- **Fatal incompatibility:** Start is refused, with the reason. A
  separate **Force start** button (behind a confirmation) launches it
  anyway, for testing and for reproducing the crash. It is offered to
  every player, in dev mode or not. A missing required mod can never be
  forced.

## The Main Menu Button Needs Harmony

Vanilla builds the main menu's buttons as a local list inside
`MainMenuDrawer.DoMainMenuControls`, with no extension point, so adding a
button takes a Harmony patch: a small postfix that adds one option. That
is the case the Harmony policy allows ("acceptable if necessary"); it is
the mod's first Harmony patch and its Harmony dependency. The postfix
sits on `OptionListingUtility.DrawOptionListing`, the method that list
is handed to, and acts only on the main menu's own column; see "As
Built". (The fallback considered if Harmony had been ruled out, opening
the menus from the mod's settings page, was not needed.) The API's one
other patch, approved on 2026-10-14, pins the seeds a showcase's map is
generated from, and only while it is generated ("Seeds"); everything
else in the API needs no patches.

## Packaging

The API can live in one of two places:

- **Inside Batshit Technology.** It is quickest to build, but other mods
  that want showcases would have to depend on Batshit Technology.
- **As its own small framework mod** (for example "Showcase Framework",
  packageId `breakpoint8088.Showcases`), which Batshit Technology and any
  other mod depend on. This is the conventional shape for an API many
  mods share. It needs its own folder and About.xml, and a load-order
  rule.

As decided (Decision 2), it is built inside Batshit Technology under its
own namespace (`BatshitTechnology.Showcases`), free of Batshit-specific
code, to be split out into a framework mod once a second mod wants it.

## Order of Work

The directed tour (Tractor Bomb slice 12000) is to be built on this API
rather than before it. The API was built on its own branch,
`feature/showcase-api`, after the Tractor Bomb's mechanics (its slices
2000 to 11000). The showcase is then rebuilt on it as the tour, as the
Tractor Bomb's slice 12000.

## As Built

### Settled While Building

Three proposals were made while building and stand as settled (they were
listed in `ROADMAP.md` for confirmation):

- **The button** sits at the bottom of the main menu's column, below
  Quit to OS. A postfix on `OptionListingUtility.DrawOptionListing`
  draws it only for a list drawn in the Entry scene (never the in-game
  menu), holding no web links and no empty entries, whose last option's
  action is vanilla's `Root.Shutdown` itself (not a closure, as the
  in-game Quit to OS is). It is one of the mod's two Harmony patches
  (the other is the map seed's prefix, "Seeds"), both installed by
  `ShowcaseMenuButton` through one Harmony instance under the id
  `breakpoint8088.BatshitTechnology.Showcases`. Harmony comes from the
  Harmony mod (`brrainz.harmony`, a declared dependency); the mod never
  ships `0Harmony.dll`.
- **No autosave in showcase games.** The session resets the autosaver's
  private tick counter (`Autosaver.ticksSinceSave`, RimWorld 1.6.4871)
  every tick, without Harmony and without touching the player's
  preferences, so a showcase never overwrites the player's autosave
  slots. Nothing about the session is saved, so a showcase game the
  player chooses to save loads as an ordinary game and autosaves as
  usual.
- **One command-line flag:** `-btshowcase` is retired in favour of
  `-showcase=<defName>`; `Tools/smoke-test.ps1 -Showcase` passes
  `-showcase=BT_Showcase`.

### The Def

A `ShowcaseDef` (written in XML as
`<BatshitTechnology.Showcases.ShowcaseDef>`) has these fields:

| Field | Meaning |
|---|---|
| `label`, `description` | The title and the details page's text |
| `summary` | The list's line; blank uses the description's first paragraph |
| `previewTexPath` | A 16:9 texture path; blank shows no picture |
| `estimatedMinutes` | Running time, game generation included (default 5) |
| `order` | Place in its mod's list, lowest first |
| `requiredMods` | Package ids that must be active, DLCs included |
| `compatibility` | Entries of `packageId`, `severity` and `note` |
| `mapSize` | Width and height in cells (default 250, limits 75 to 400) |
| `biome` | A start in a random valid tile of that biome, if any exists |
| `seed` | The seed the game is generated from; blank is random ("Seeds") |
| `holdWeather` | A weather held while it runs ("The Held Weather") |
| `quietScreen` | Clutter kept off the film ("The Quiet Screen") |
| `workerClass` | A `ShowcaseWorker` subclass (default `ShowcaseDirector`) |
| `scenes` | For a director: scenes in order, keys unique |
| `narration` | Lines of `scene`, `when`, `delayTicks` and `text` |

Severities are `Concern`, `Warning`, `KnownIncompatibility` and `Fatal`.
A narration line's `when` is `start` (the default), an outcome
(`completed`, `timedOut`, `skipped`, `stopped` or `failed`), or a
worker's own beat name. A `stopped` line is said only when its delay is
0: Stop ends all narration, so the configuration check reports a delayed
one. Setup code should read `map.Size` rather than assume `mapSize` (see
the command line below). Configuration errors (dev mode, at start-up)
cover the worker class, the map size and running time, a malformed
seed, malformed package ids, scene keys and timings, narration naming a
missing scene, and delayed `stopped` lines.

A scene (`ShowcaseScene`, or a subclass named with `Class="..."`) has a
`key`, a caption `title`, `minTicks` (120), `timeoutTicks` (3600),
`settleTicks` (0), a `focusOffset` from the map's centre, `selectSubject`
(true), and `shots`. A shot has `atTick`, an `offset` (from the scene's
focus, or the map's centre with `fromMapCenter`), a `zoom` (8 to 80,
default 24), either a glide (`pan`, the default, over `panSeconds`,
0.6) or a cut, and `keepOnMap` (off by default). A shot that keeps on
the map is moved in from an edge until its view no longer shows the
black beyond the map, and a view bigger than the map is centred on it
(Core's `ShotFraming`; the view is twice the zoom tall and that times
the screen's aspect ratio wide). A scene with no shots opens with a cut
to its focus at zoom 24, so its caption never shows the last scene's
place; give a scene's own opening shot `<pan>false</pan>` for the same
(slice 19000d). The director's last camera move is
`ShowcaseDirector.LastFraming` (scene, beat, centre, zoom, cut or glide,
tick), for tests and capture tooling to compare with where the camera
is. Subclasses override `Subject`, `UnavailableReason`, `Setup`, `Tick`,
`IsDone`, `End` and `OutcomeKey`; a scene holding mutable state
overrides `CloneForRun`.

Two hooks were added for the directed tour (phase 3000, slice 12000):

- **Declining a scene (gap G1).** `UnavailableReason` is null by
  default. A scene that cannot run on this map (no hostile faction, no
  pad) returns why. The director asks each scene once, after attaching
  it and before building the sequencer, and drops the ones that decline,
  logging `SKIPPED <key>: <reason>` as a message, not an error. The
  caption counts only the scenes that run ("2 of 7", not "2 of 8"). A
  check that throws is logged as an error and the scene is dropped.
- **Beats (gap G2).** Shots are timed from a scene's start; an event
  (the bomb is planted, the volley begins) is not. A scene calls the
  protected `Beat(moment, shot, select)`: the director logs `BEAT <key>
  <moment>`, selects the subject (unless `select` is false), frames the
  shot from the scene's focus as it is now (its `atTick` is ignored),
  and narrates that moment, so a scene's lines can name its beats. A
  beat does nothing once the tour has stopped, or from a scene that is
  not the one running.

### The Worker, the Director and the Session

- **A worker** overrides `Setup(Map)`, `Tick()` and `Finished`, and may
  offer `Skip()` (with `CanSkip`) and a `Caption`. `Narrate(scene, when,
  targets)` queues the def's lines; `Say(scene, text, targets)` says a
  computed one. Both show on the narration overlay (below) and are
  recorded in `NarrationLog`. `EnableDevButtons()` turns on the
  developer buttons of the devices it stages; see "God Mode" below.
- **The director** runs the scenes one at a time with Core's
  `SceneSequencer`: a scene ends when its `IsDone()` has held, it has run
  `minTicks`, and it has settled; or at `timeoutTicks`. Its shots come
  due by tick (Core's `BeatSchedule`). A cut also cancels any glide still
  running, which CameraDriver would otherwise keep re-applying.
- **A scene that throws** is logged once (an error) and the tour goes
  on. A scene whose `Setup` throws never begins: a `SKIPPED` cue gives
  the reason, and it gets no `SCENE` or `END` cue and no narration. One
  whose `Tick` or `IsDone` throws ends `Failed` on that tick, says its
  `failed` lines, and the next begins. One whose `End` throws still ends
  as it ended.
- **The caption bar** ("2 of 8: The Batshittium Reactor", from a
  translatable pattern) is a title card at the top centre, just below
  the colonist bar. It was first proposed for the bottom of the screen,
  but the bottom holds the selected subject's gizmos, the inspect pane
  and the main buttons, and the bar draws before them
  (`GameComponentOnGUI` runs ahead of the map interface and the window
  stack), so there it would cover them and take their clicks. It hides
  on the world map and in screenshot mode, and offers:
  - **Skip** (when the worker offers it): the running scene ends as
    skipped, and the next begins at once;
  - **Stop**: the running scene ends as stopped, no later scene runs,
    and narration ends: the overlay is cleared at once, a scene's own
    `stopped` line then shows on it, and a silent vanilla message
    confirms the Stop. The game keeps running as a showcase game (it
    still never autosaves); the camera and the colony are the viewer's,
    and a device already counting down still goes off.
- **The session** (`GameComponent_ShowcaseSession`) adopts the
  launcher's request for its own new game only, logs the new map's
  fingerprint, quiets the storyteller, holds the game paused for the 120
  frames it waits for the map to settle, reseeds the global random state
  from the launch's seed, sets the worker up, logs the stage's
  fingerprint, ticks the worker, suppresses autosave, draws the caption
  bar and the narration overlay, hands the overlay the closing message
  once any queued narration has been said, and gives the storyteller
  back when the showcase finishes or stops.

### The Narration Overlay

Requested by Matt "Breakpoint" Heck on 2026-10-14, after watching the
tour: narration shown as vanilla messages, in the stack at the top left
with a sound for each, was "kind of obnoxious"; he asked for "a single
oversized overlay with a dropshadow that shows the current line, fading
in at the start of a segment, out a few seconds before the next one
starts, with no audio blat when it appears." Every showcase gets it;
nothing a worker says goes to `Messages`.

- **Where and how large.** One line at a time, centred in the lower
  third: its bottom 230 UI units above the screen's bottom, clear of the
  main buttons, the inspect pane and two rows of gizmos, and never above
  the caption bar. White, about twice the Medium font (sized in UI
  units, so it follows the UI scale), wrapping at 1,100 units to at most
  two lines; a longer line steps the font down, never below Medium.
- **Drop shadow.** Eleven light black passes around the text, heavier
  below it, make a soft shadow that keeps white lettering legible over
  snow, sand and explosions.
- **Silent, and not clickable.** It plays no sound and takes no click;
  the director already frames what a line is about.
  `NarrationRecord.Targets` still records it, for tooling.
- **Timing, in real seconds,** because people read in real time,
  whatever the game's speed (Core's `NarrationTiming` and
  `NarrationOverlay`, unit tested):
  - a line fades in over 0.5 s;
  - its reading time is 2.5 s plus 50 ms a character, at most 9 s;
  - with nothing queued, it holds for its reading time and then fades
    out over 1 s;
  - with the next line already queued (a delayed cue), it holds until it
    can be gone 2 s before that line is due, but never for less than its
    reading time; ticks become real seconds at the game's nominal speed
    (a slower game only makes the gap longer), and while the game is
    paused the line holds;
  - a line said while another shows waits until that one has been up
    for 2.5 s, then replaces it with a 0.3 s cross-fade; lines said
    together show in turn, each for at least 2.5 s.
- **Pacing is the showcase's.** The overlay never delays a line beyond
  the 2.5 s rule; a showcase that wants each line read in full spaces
  its own cues. Batshit Technology's tour does (Core's `TourPacing`:
  each line's reading time, the 1 s fade and the 2 s lead), and its
  scenes hold their action until their start lines have cleared
  (`docs/SHOWCASE.md`, "The Scenes").
- **The closing message** ("This concludes our presentation. ...")
  speaks in the narrator's voice, so it is narration and goes to the
  overlay too; it keeps the narration rules (no fourth-wall words, at
  most 120 characters), which a unit test holds it to. It is patient:
  it waits for the last line's full reading time, then fades like any
  other line, leaving the overlay empty. The Stop
  confirmation stays a vanilla message, now silent (`SilentInput`): it
  answers the viewer's click, as the details page's refusals do.
- **Clearing.** Stop clears the overlay at once, before the worker
  stops, so a scene's own `stopped` line still shows; a smoke-test reset
  clears it too. The overlay belongs to the game's session, so leaving
  the game takes it along. After a showcase finishes it empties itself
  once the closing message has faded.
- **Failure.** Updating and drawing are guarded as the caption bar is:
  one failure is logged as an error and hides the overlay for that game;
  lines are still recorded.
- **Unchanged for tooling:** `NarrationLog` and the `SCENE`, `BEAT` and
  `END` cues. The session adds `NarrationFrames` (frames the overlay drew
  text in) and `NarrationFailed`.

### God Mode

Vanilla shows developer buttons only with dev mode and god mode both on
(`DebugSettings.ShowDevGizmos`). God mode is a static flag that outlives
the game that set it, nothing in vanilla resets it when a game ends, and
outside dev mode the player cannot even see it; left on, it builds
everything at once and for free. So `EnableDevButtons()` borrows it only
in dev mode, remembering what it was, and the API gives it back when the
showcase stops or finishes, when its set-up fails, at the main menu, and
when any game that is not a showcase starts or loads. A developer's own
god mode stays on; a player outside dev mode never gets it. The mod's
own DEV buttons check `DebugSettings.ShowDevGizmos`, as vanilla's do.
The directed tour asks for none (slice 19000b): it is a film, and a
viewer in dev mode should see no developer buttons in it.

### The Command Line

- `-showcase=<defName>` acts once, the first time the main menu is
  shown: the menu starts the showcase through the same launcher as the
  Start button, so the def's map size and biome apply. An unknown,
  unavailable or refused showcase logs a `REFUSED` cue and an error
  saying why.
- `-showcaseforce` also lets `-showcase=` start a showcase flagged
  fatal, as Force start does. Missing requirements can never be forced.
- `-showcaseseed=<text>` gives the seed for any showcase launched in that
  run, from the menu or the command line, over the def's own ("Seeds").
  `Tools/smoke-test.ps1 -Seed <text>` passes it.
- With `-quicktest` as well, vanilla generates its own quick-test game
  (250 cells, a random tile) and draws the main menu for a single frame
  on the way; the menu then leaves the command line alone, and the
  quick-test game adopts the showcase on its map as it is. The def's
  `mapSize` and `biome` are not applied on this path (that would take
  another Harmony patch); `MAPSIZE` and `BIOME` cues say so. It is meant
  for developer runs. (Starting a second generation from that one menu
  frame collided with vanilla's: "Could not generate starting map
  because there is no any player faction base", then an exception in
  `Game.InitNewGame`, observed on RimWorld 1.6.4871.)

### Seeds

Requested by Matt "Breakpoint" Heck on 2026-10-14: "Showcases should be
able to specify the random number seed used for map generation, to get
repeatable results." Built in slice 18000.

- **The seed.** Every launch has one, a short text like a world seed: 1
  to 64 ASCII letters, digits, `-`, `_` or `.` (so it survives
  RimWorld's command line, which splits at `=`, the cue, and a folder
  name). In order: `-showcaseseed=<text>` on the command line (for
  trials), else the def's `seed`, else a fresh random one made at launch
  from the game's own seed words (`GenText.RandomSeedString`). A
  malformed seed offered is refused with an error naming why, and the
  next in order is used; a malformed def seed is also a configuration
  error. Core's `ShowcaseSeed` holds the rules, unit tested.
- **The cue.** `SEED <text> (<source>)`, the source being `command
  line`, `def` or `random`, is logged as the game is queued, so any run,
  a random one included, can be pinned later by copying its seed into
  the def or the command line.
- **What it seeds.** Each stage of a launch draws under its own Rand
  seed, the 32-bit FNV-1a hash of `<seed>/<purpose>` (never
  `string.GetHashCode`, which a runtime may vary per process):
  1. `world`: the quick-test set-up, pushed and popped around it on the
     long event's thread as vanilla's world generation is. It draws the
     world's seed string (so the world follows: vanilla seeds each world
     step from that string), the starting tile, the starting colonists,
     and then the def's biome's tile.
  2. `prep`: the map's preparation. The launcher queues vanilla's
     `PageUtility.InitGameStart` itself, with its same two calls under
     this seed: the colonists' work priorities (which break ties at
     random) and the player's settlement (its name is random).
  3. The map: seeded from the world's seed and the starting tile, so it
     follows from the two above, once the map seed's prefix has made
     those seeds the same in every process ("The map's seeds", below),
     all but the rooms of a layout ("What it cannot pin").
  4. `start`: the global random state through the rest of the game's
     start, set afresh (as for `stage`) in the session's
     `FinalizeInit`, once the map is generated. The scenario, the
     history and every game component's `StartedNewGame` draw on it
     before the first tick and keep what they draw: Anomaly's first
     harbinger tree check, for one, which unseeded came at a tick
     random per process, then drew again and queued an incident in the
     middle of the tour. Nothing is pushed then, and the main thread
     draws nothing during the long event. The session records the
     state the start left (`StartState`, the SUMMARY's `startState`),
     the same for a seed in every process.
  5. `stage`: the global random state, set afresh (as `Rand.Seed`
     would, through its private state, since the public setter logs an
     error unless something is pushed) just before the worker is set
     up.
- **Before the stage, no tick.** The session holds the game paused while
  the map settles (120 frames), and sets the worker up before the
  game's first tick (tick 0); before, the game ran as many ticks in
  those frames as the frame rate allowed, and pawns, plants and the
  random state moved on by a different amount every run. The speed is
  put back as the worker is set up (the tour's stage sets Normal
  anyway). RimWorld silently refuses a change of speed while the player
  has no control: while the screen fades in after the map loads
  (`Root_Play.Start` queues a half-second fade) or a cutscene plays. So
  the settling frames count only while the player has control, and if
  the unpause is refused anyway, the session keeps the hold and tries
  again every frame until it takes (`SettleReleaseRefused` counts the
  tries; the SUMMARY's `settleRetries`). Before, a set-up that landed in
  the fade (at about 240 frames a second or more) left the showcase
  paused at `READY`.
- **The loading screen.** Rand is one global state, shared by every
  thread. While the game generates on the long event's thread, the main
  thread draws the loading screen, and the first time it shows the
  gameplay tips it shuffles them with Rand. Done mid-generation, that
  shuffle would steal draws from the seeded sequence at a moment that
  depends on thread timing. The launcher shuffles them first, on the
  main thread, as vanilla would (`LoadingTips.Prime`); nothing else on
  the loading screen draws on Rand (RimWorld 1.6.4871).
- **What a seed pins.** In any process, on the same game and mod list: the
  world, the starting tile, the colonists (names, traits, gear, work
  priorities), the settlement, the rest of the game's start, the whole map but
  the rooms of any layout on it, and the stage the worker builds on it, so
  `MAPHASH` and `STAGEHASH` repeat for a map without layouts. A seed given in
  the def and the same seed given on the command line give the same map.
  Checked in slice 18000 (`docs/AI-DECISIONS.txt`): two processes with the
  same seed drew the same world seed string, tile and colonists, every Rand
  checkpoint matched through the preparation and every gen step's draws, and,
  with the map seed's prefix, every gen step's result, `MAPHASH` and
  `STAGEHASH` matched too.
- **The map's seeds: the second Harmony patch.** RimWorld 1.6.4871 alone
  cannot generate the same map twice in two processes. `Map.NextGenSeed` is
  `System.HashCode.Combine(tile, counter, world seed)`, and `System.HashCode`
  mixes in a random seed chosen once per process. BaseGen (every symbol:
  ancient shrines, ruins), the scatter groups (mechanoid remains, junk
  clusters), prefabs, layouts' sketches (not their rooms) and unique weapons
  seed their Rand blocks from it, so those structures differed from process to
  process, and every later gen step that walks the map (plants, rock chunks,
  animals) shifted with them. Matt "Breakpoint" Heck approved a second Harmony
  patch for it on 2026-10-14 (requests item 22; Decision 1): a prefix on the
  `NextGenSeed` getter (`ShowcaseMapSeed.Prefix`) that reads the same three
  inputs in vanilla's order (the tile's hash, or the map's id off the planet;
  the counter, advanced as vanilla advances it; the world's seed) and combines
  them with `Gen.HashCombineInt`, which is the same in every process. It acts
  only inside a window:
  - the launcher opens it for the showcase's new `Game` on the long
    event's thread, once the world and the starting tile are drawn and
    before the map is generated;
  - the session closes it at the start of `StartedNewGame` (the end of
    `Game.InitNewGame`, once the map exists), for every new game,
    showcase or not, and logs how many seeds it pinned (`MAPSEED`);
  - it is closed, with a warning if it was found open, if the set-up or the
    map's preparation throws (and the launch request is dropped, with a
    warning: vanilla only logs a failed preparation and starts the
    half-prepared game anyway, which then starts as an ordinary game, never as
    a showcase on a map its seed did not pin), whenever the main menu is shown
    (which vanilla never draws while a long event runs), when a game is
    loaded, and again, harmlessly, at the showcase's set-up, Stop and end;
  - the window is bound to that `Game` object (Core's `GenerationPin`),
    so a window some failure left open can never pin another game's
    seeds: the player's next real game is another object.

  Outside the window the prefix returns at once and vanilla's getter runs
  untouched, so every other map, in a showcase's game after its start or in
  any other game, is vanilla's. A quick-test map (`-quicktest -showcase=`) is
  generated by vanilla before the showcase adopts it, so it is not pinned
  (`MAPSEED not pinned (quicktest map)`); any other showcase game whose window
  was not open says `not pinned (window not open)`. If Harmony is missing, or
  a later RimWorld renames the getter or its private counter
  (`Map.generatedId`), the patch is not installed, an error says so, and maps
  follow vanilla (`MAPSEED not pinned (no patch)`); the menu button is
  installed on its own and still works.
- **What it cannot pin: layouts' rooms.** RimWorld 1.6.4871 fills the
  rooms of every layout (`LayoutWorker.FillAllRooms`) inside `new
  RandBlock(HashCode.Combine(sketch id, room))`; the sketch's id is
  never set, and `System.HashCode` is random per process again. The
  prefix pins the sketch, drawn from `NextGenSeed`, but not the rooms:
  their loot, furniture and hostiles (a garrison's, an infested
  settlement's), and every gen step placed around them afterwards,
  differ from process to process. Layouts reach a starting map through
  Odyssey's landmarks (the ancient garrison, warehouse, chemfuel
  refinery, infested settlement and launch site, each a 0.2 chance on
  its tile) and through ruins; a showcase's random tile does not avoid
  them. Pinning the rooms would take a third Harmony patch, which is
  not approved. Instead the session reads the tile's landmark and
  mutators and counts the map's layouts with rooms
  (`UnpinnedLayouts`), the `MAPSEED` cue names them (`pinned (653
  seeds); rooms of 1 layout not pinned; landmark AncientGarrison`), the
  verifier notes them and the SUMMARY carries them (`landmark`,
  `mutators`, `unpinnedLayouts`), and `Tools/tour-trials.ps1` lists
  such runs. The guard is its repeat check: run a candidate seed in two
  processes (`-Repeat 2`) and require `MAPHASH` and `STAGEHASH` to
  match. A map with no layout is pinned whole.
- **What it cannot pin: after READY.** After `READY` the game draws on the
  same global random state from code that runs per frame rather than per tick
  (effects, motes, sounds and the interface draw on Rand), and how many ticks
  fall in each frame depends on the machine's load and the speed: Superfast in
  a verify run, Normal for a viewer, whose own clicks and camera add more. So
  the run after `READY` repeats only as far as those draws happen not to land
  between ticks that matter; how far that is for the tour is measured by
  `Tools/tour-trials.ps1` and recorded in `docs/AI-TESTING-STRATEGY.txt`
  ("Tour trials"). A seed also does not cover a `-quicktest` game: vanilla
  generated that map unseeded, so its cue adds `quicktest map, not pinned`
  (the stage is still seeded).
- **Viewing and verify runs.** Nothing in generation or staging depends
  on the window, the UI scale or the speed, so a viewing run
  (`-Showcase`: Normal speed, a maximized window at 2x UI) and a verify
  run (`-Showcase -Verify`: Superfast, a small muted window) of one seed
  pin the same things. The verifier's forced declines (`-TourSkip`)
  change which scenes run, and so everything after the first one
  skipped.

### Fingerprints

The session logs a map's fingerprint twice: `MAPHASH` as the new game
starts (at the end of `Game.InitNewGame`, before any tick) and
`STAGEHASH` just before `READY`, once the worker is set up. Each is
Core's `MapFingerprint`, a 64-bit FNV-1a hash, written as sixteen hex
digits and followed by what it covered, as in `f70e1a4a4518bc51 (62500
cells, 35221 things, 53 pawns)`. It covers:

- the map's size, and every cell's terrain and roof, in cell order;
- every spawned building, item, plant and filth: its def, cell,
  rotation, stuff and stack count;
- every pawn on the map, spawned or held (in a bed, a Brain Squeezer):
  its full name, kind and faction (def and name).

Things and pawns count as a multiset, whatever order the map lists
them in. Motes, projectiles, fire and other passing effects are left
out, as are thing IDs (a mote drawn in a frame takes an ID) and pawns'
cells (pawns walk). Reading a map draws nothing from Rand, so logging a
fingerprint never changes the run. The session's `MapHash` and
`StageHash` hold the same hex. Core's tests pin the hash against an
independent reference; the smoke run checks that it is stable, sees a
stack of steel come and go, and covers every cell and pawn.

### The Quiet Storyteller

Random incidents are off while a showcase runs, for every showcase
(requests item 19, part one; the operator's default answer, "yes"). From
the session's start (the new game, or a smoke test's adoption) until the
showcase finishes or stops, the session holds the storyteller's parts
(`Storyteller.storytellerComps`) and drops any incident it had queued,
so no raid, manhunter pack or dark entity is sent mid-showcase. When it
finishes or stops, the parts are given back, since the closing line
hands the colony to the viewer; a `STORYTELLER` cue marks each change.
If a set-up fails, they are given back at once. The parts belong to the
showcase's game, so leaving it mid-showcase takes them along, and a
showcase game the viewer saves rebuilds them on loading. Not covered:
incidents that quests make (a fresh game has none), and the hostiles a
map starts with (dark entities, a mechhive, an ancient temple's hives).
The API leaves those to the showcase: the tour's stage removes them
before it clears the fog (item 19, part two, built in slice 19000a;
docs/SHOWCASE.md, "A Clean Stage").

### The Held Weather

A def may name a weather to hold, `holdWeather` (slice 19000a; the tour
holds `Clear`, since rain and fog greyed out its middle scenes). From the
set-up until the showcase finishes or stops, the session keeps that
weather showing, without Harmony and without drawing a random number:

- **At the set-up**, before the worker, it notes the weather as it is
  (current, last and age), makes the held weather both the current and
  the last one at once, and logs `WEATHER held <def> (was <def>)`.
- **Every tick** after the map's weather has ticked, Core's
  `WeatherHold.Next` decides: another current weather is replaced at once
  (counted as a restart); a transition back to the held weather is
  settled, the last weather made the held one; and the held weather's
  age goes back to 0 once past 1,000 ticks. RimWorld 1.6.4871's weather
  decider starts the next weather when the age passes the current turn:
  10,000 ticks for a new game's first weather, else the weather's own
  duration (15,000 at the least), cut to a quarter while a large fire
  burns, so never under 2,500, and it never fires. A vanilla
  `GameCondition_ForceWeather` would do the same, but a condition is
  saved with the map, and nothing about a session may be saved.
- **A scene may borrow the weather** for weather of its own
  (`YieldWeather(scene)`, then `ReclaimWeather(scene)`; the tour's
  stampede brings rain). The hold stands aside while it is lent, and
  takes it back by itself once the borrower is no longer its director's
  current scene, so a scene that fails in its end cannot keep it.
- **When it finishes or stops**, or its set-up fails, the weather from
  before is given back, and `WEATHER given back (<def>)` is logged after
  the `STORYTELLER` cue; the decider carries on from there.

The weather's state belongs to the map and is saved as usual, so a
showcase game the viewer saves keeps the weather it had, and the hold,
being the session's, does not survive the load.

### The Quiet Screen

A def may ask for a quiet screen, `quietScreen` (slice 19000b; the tour
does, since "Need colonist beds", "Pen needed", "Ancient mech", "Roof
collapsed", top-left messages and the Learning helper's lessons piled up
over its film). From the set-up until the showcase finishes or stops,
the session keeps vanilla's clutter off the screen, without Harmony and
without changing anything the player set (`ShowcaseScreen`):

- **Alerts and the Learning helper.** The current storyteller def's
  `disableAlerts` and `disableAdaptiveTraining` are raised, as vanilla's
  hidden Tutor storyteller has them. In RimWorld 1.6.4871,
  `AlertsReadout.AlertsReadoutUpdate` clears every active alert while
  the first is set, and `TutorSystem.AdaptiveTrainingEnabled` is false
  while the second is, which hides the Learning helper and stops new
  lessons. A def is shared by every game until RimWorld restarts, so the
  values from before are lent, not set (Core's `OwnedFlagsLoan`), and
  given back when the showcase finishes or stops, when its set-up fails,
  at the main menu, and when any game starts or loads, as god mode is.
  If the viewer picks another storyteller mid-showcase, the old def's
  flags are given back and the new def's lent, at the next frame.
  Prefs are never touched: `Prefs.Save` runs from the options, the main
  menu and the key bindings, and would keep a value changed for the
  showcase.
- **Letters.** Each frame, after the ticks and before the frame is
  drawn, every letter that arrived since the set-up is taken off the
  stack (`LetterStack.RemoveLetter`); one from before stays. The letter
  stays in the History tab's archive. Its arrival sound has played by
  then, and a big threat's letter pauses the game under the default
  preference (pause on major threats), so the mod's own letters ("Self-
  destruct armed", "Replicator power cut") are not sent at all while the
  screen is quiet (`ShowcaseScreen.IsQuiet`).
- **Messages.** Each frame, vanilla's live messages are cleared
  (`Messages.Clear`); historical ones stay archived. Their arrival
  sounds still play.

The session's `ScreenQuiet` says whether it is quiet now, and
`ShowcaseScreen.LettersTakenOff` how many letters it took off. A
showcase that does not ask leaves the screen alone. Viewing runs of
`Tools/smoke-test.ps1` also turn dev mode and the Learning helper off in
their own prefs (docs/AI-TESTING-STRATEGY.txt, section 4).

### Log Cues

Every API cue is one log message, `[BT-SHOWCASE] <CUE> <detail>`; the
prefix carries the host mod's tag while the API lives in this mod. The
API's errors and warnings carry the same prefix, but are not cues. Six
are record cues (`ShowcaseLog.RecordCues`): they say what a run was
(its seed, whether its map's seeds were pinned, its fingerprints, the
storyteller's state, the held weather) rather than how it went, so
tooling comparing a run's flow of cues can leave them out. A
`ShowcaseLog.Cued` event lets capture tooling in the assembly listen
for cues (the tour verifier's shots do).

- `LAUNCH <def>`: a game is being generated for it; with `forced`, or
  `quicktest` when it is adopted on a quick-test map.
- `SEED <text> (<source>)` (record): the launch's seed and where it came
  from (`command line`, `def` or `random`); with `; quicktest map, not
  pinned` on a quick-test map.
- `BIOME <biome>`: a tile of the biome was chosen; or `... not found;
  random start`; or `... ignored; quicktest map`.
- `MAPSIZE <n> ignored; quicktest map is <m>`: the quick-test map cannot
  have the def's size.
- `GAME <def>`: the new game's session adopted the showcase.
- `MAPSEED pinned (<n> seeds)` (record): the map seed's window was open
  while the map was generated, and pinned that many of its seeds; it
  is closed by now. With `; rooms of <n> layouts not pinned` when the
  map holds layouts, and `; landmark <defName>` when its tile has a
  landmark. Or `MAPSEED not pinned (quicktest map)`, `... (window not
  open)` or `... (no patch)`.
- `MAPHASH <hex> (<n> cells, <n> things, <n> pawns)` (record): the new
  map's fingerprint, before any tick.
- `STORYTELLER quiet (<n> parts held)` (record): random incidents are
  off; with `, <n> queued incidents dropped` if any were queued. And
  `STORYTELLER restored (<n> parts)` after `FINISHED` or `STOPPED`, or
  a failed set-up.
- `START <def>`: the map has settled and the worker is being set up
  (with `forced` if it was).
- `WEATHER held <def> (was <def>)` (record): the def's weather is held
  from here. And `WEATHER given back (<def>)` after `FINISHED` or
  `STOPPED` (following the `STORYTELLER` cue), or a failed set-up.
- `STAGEHASH <hex> (...)` (record): the map's fingerprint once the
  worker is set up, just before `READY`.
- `READY`: the worker is set up; capture tools maximize and start here.
- `SCENE <key>`: a director scene began.
- `END <key> <outcome>`: it ended: `completed`, `timedOut`, `skipped`,
  `stopped`, `failed`, or the scene's own outcome key.
- `SKIPPED <key>: set-up failed`: a scene's set-up threw; it never
  began.
- `SKIPPED <key>: <reason>`: before `READY`, a scene declined the map
  (`UnavailableReason`); it is not counted.
- `BEAT <key> <moment>`: a scene played a beat.
- `SKIP <def>`: the viewer pressed Skip.
- `FINISHED <def>`: the showcase played out.
- `STOPPED <def>`: the viewer pressed Stop, or the worker failed while
  running (with an error).
- `FAILED <def>`: the worker could not be set up.
- `REFUSED <name>`: `-showcase=` will not start (with an error saying
  why).

Capture tooling should treat `REFUSED` and `FAILED` as ending the run:
`Tools/smoke-test.ps1 -Showcase` does. `BT_Showcase` adds its own cues
(`ANYRIM` or "AnyRim skipped", and "stage cleared: ..."); its scenes log
the director's `SCENE`, `BEAT`, `END` and `SKIPPED` cues above.

## Tests

- **Core unit tests:** scene sequencing (including a failed scene),
  grouping showcases by mod, requirement and compatibility checks, the
  launch decision, the caption and its bar's layout, narration (including
  delayed `stopped` lines), the narration overlay's timing and layout,
  the god-mode loan, and the Force-start and command-line rules; from
  slice 18000, the seed's precedence, characters and refusals, the
  derived Rand seeds and FNV-1a (pinned to the published vectors), the
  map fingerprint (pinned to an independent reference, blind to the
  order of things and pawns, changed by every field it covers), and the
  `MAPSEED` cue's detail (`MapSeedCue`: each reason for not pinning,
  and the layouts and landmark of a pinned map).
- **XML tests:** every `ShowcaseDef`'s fields (worker classes matched by
  exact namespace), the keyed text and the keys the code uses, preview
  images, the Harmony dependency, that no file but the button's uses
  Harmony and that it makes exactly two patches (the button's postfix
  and the map seed's prefix, on their targets, under one Harmony id),
  that the map seed's prefix opens with its pass-through test, that
  only the god-mode loan sets god mode and every DEV
  button needs dev mode, and that the smoke runner's test showcases are
  never shipped. Narration scene keys are checked by the def's
  configuration check (no shipped def has narration yet). A def's
  `seed`, if any, must be well formed.
- **Smoke** (`Source/SmokeTestShowcases.cs`, with test showcases made in
  code; details in `docs/AI-TESTING-STRATEGY.txt`):
  - the button's postfix and the map seed's prefix are the mod's only
    Harmony patches, both under the button's Harmony id; vanilla's main
    menu gets exactly one Showcases option while the in-game menu gets
    none, and each of the column's gates works on its own;
  - the three windows open, list Batshit Technology and its showcases,
    grey out one missing a mod with the reason, refuse a fatal flag while
    offering Force start (in dev mode and outside it), and honour Back
    and Escape;
  - verdicts in game match Core's, and the launcher refuses what it must
    and hands a request only to its own game;
  - a directed test showcase runs through its scenes, Skip, a timeout
    and Stop, with its cues and narration in order and no API warning;
    its fifth scene declines (`SKIPPED unavailable: smoke` before
    `READY`, and the caption still reads "2 of 4"), and its waiting
    scene plays a beat (`BEAT waiting poke`, with that moment's line);
    scenes that throw are logged once each and the tour goes on; a cut
    right after a glide holds;
  - narration and the closing message go to the overlay, never to
    vanilla's live messages or the archive; in real frames, the
    overlay's current line is the last line said, replacing the line
    before after its base time and painted; Stop clears it, leaving the
    stopped scene's own line alone;
  - god mode is lent only in dev mode and given back on every way out;
  - a showcase game never autosaves, an ordinary one does, the session
    saves nothing, and the save folder gains no files;
  - from slice 18000 (`Source/SmokeTestShowcases.Seeds.cs`): a launch's seed
    is the def's, else a fresh random one; a malformed def seed is a
    configuration error; the global reseed repeats a seed's draws; the
    fingerprint is stable, draws no Rand and sees a stack come and go; the
    storyteller is held from a run's start and given back after `FINISHED` and
    after Stop; `STAGEHASH` comes just before `READY`; a failed launch drops
    its request and closes the window, saying so; the settle hold outlasts a
    screen fade (a refused unpause keeps the hold, and the next try takes); in
    the smoke run's ordinary game the map seed's window is closed and
    `Map.NextGenSeed` gives vanilla's own value (computed through
    `System.HashCode.Combine` in the same process), even with the window open
    for another game, while with the window open for its own game it gives the
    pinned value, and is vanilla's again once closed;
  - from slice 19000a (`Source/SmokeTestShowcases.Weather.cs`): the directed
    test showcase holds Clear over the smoke game's fog from its start,
    undoes another weather on the next tick with its age under the
    decider's turn, stands aside while a scene has borrowed the weather and
    takes it back once that scene is skipped, and gives the fog back when
    it finishes and when it is stopped.
- **Showcase run:** `Tools/smoke-test.ps1 -Showcase` starts BT_Showcase
  through the main menu with `-showcase=BT_Showcase` and waits for
  `[BT-SHOWCASE] READY`, failing on a `REFUSED` or `FAILED` cue or a mod
  error before it. The run on to `FINISHED` is checked by hand in the
  verification runs, since the game is left open for the viewer.
- **Tour verify run:** `Tools/smoke-test.ps1 -Showcase -Verify` checks Batshit
  Technology's tour to `FINISHED`, and, for the API, that the launch logged
  its seed from the source its precedence picks, `MAPSEED` said the map's
  seeds were pinned (as many as the session holds, and every seed pinned in
  the process, with its layouts and landmark), with the window closed at
  `READY` and still closed at the end, `MAPHASH` came before `START` and
  `STAGEHASH` just before `READY`, the rest of the game's start was drawn from
  the seed, the stage was set up at tick 0, and the storyteller was quiet
  until `FINISHED` and given back after it (`docs/AI-TESTING-STRATEGY.txt`,
  section 4). `Tools/tour-trials.ps1` runs it over many seeds.

## Decisions

Answered by Matt "Breakpoint" Heck on 2026-10-13:

1. **Harmony** is approved for the main-menu button, the mod's first
   patch. Everything else stays patch-free. Amended on 2026-10-14
   (requests item 22, "Approve the patch"): a second patch, the prefix
   on `Map.NextGenSeed` that pins a showcase map's seeds while it is
   generated ("Seeds"). These two are the mod's only patches; the XML
   tests and the smoke run hold them to that.
2. **Packaging:** inside Batshit Technology for now, under its own
   namespace and free of Batshit-specific code. It will "absolutely" be
   split out into a framework mod later.
3. **Other mods:** warn only about mods the showcase itself flags (a
   concern, a warning, or a known incompatibility). A fatal
   incompatibility refuses to launch unless forced, for testing and
   reproducing the crash.
4. **Timing:** after the Tractor Bomb's mechanics (roadmap steps 2 to
   11), on `feature/showcase-api`, with the directed tour then built on
   it as the Tractor Bomb's step 12.

Proposed while building and standing as settled (see "As Built"):

5. **The button** goes at the bottom of the main menu's column, through
   a postfix on `OptionListingUtility.DrawOptionListing`.
6. **Showcase games never autosave**, so the player's autosave slots are
   never overwritten; nothing touches the player's saves unless they
   save.
7. **`-btshowcase` is retired** in favour of `-showcase=BT_Showcase`.

Asked for by Matt "Breakpoint" Heck on 2026-10-14, after watching the
tour, with the details settled while building (see "The Narration
Overlay"):

8. **Narration on one overlay,** oversized, with a drop shadow, fading
   in and out, silent, in place of vanilla's messages.
9. **A line waits 2.5 s before it replaces another,** rather than
   replacing it at once: the tour says each scene's start line 60 ticks
   before its first beat (and its two opening lines 30 ticks apart), so
   instant replacement would flash lines past unread.
10. **The closing message is narration** and goes to the overlay; the
    Stop confirmation stays a vanilla message, made silent.

Asked for by Matt "Breakpoint" Heck on 2026-10-14 (slice 18000), with
the details settled while building (see "Seeds", "Fingerprints" and "The
Quiet Storyteller"):

11. **Every showcase has a seed:** the command line's, the def's, or a
    fresh random one, logged so any run can be pinned. It pins the
    world, tile, colonists and settlement, and, through the second
    Harmony patch (approved 2026-10-14, requests item 22), the map and
    the stage, all but the rooms of a layout, which would need a third
    patch; the MAPSEED cue names a map's layouts, and a trial's repeat
    check catches them.
12. **No random incidents during a showcase** (requests item 19, part
    one, the operator's default answer): the storyteller is held from
    the session's start and given back when the showcase finishes or
    stops.
