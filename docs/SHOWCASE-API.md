# The Showcase API

Status: built on `feature/showcase-api` (phase 4000, slices 1000 to 7000,
with the adversarial review's fixes), with `BT_Showcase` as its first
client; awaiting the merge into `trunk` (slice 8000). "As Built" below is
the contract as it stands. Requested by Matt "Breakpoint" Heck on
2026-10-13: a game API for showcases, and a **Showcases** button on the
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
  and "The Story") becomes the first director-driven showcase built on
  it; `BT_Showcase` runs today's demo base through it meanwhile.

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
  by tests.
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
the mod's first and only Harmony dependency. Everything else in the API
needs no patches. The postfix sits on
`OptionListingUtility.DrawOptionListing`, the method that list is handed
to, and acts only on the main menu's own column; see "As Built". (The
fallback considered if Harmony had been ruled out, opening the menus
from the mod's settings page, was not needed.)

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
  in-game Quit to OS is). It is the mod's only Harmony patch, under the
  id `breakpoint8088.BatshitTechnology.Showcases`. Harmony comes from the
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
cover the worker class, the map size and running time, malformed
package ids, scene keys and timings, narration naming a missing scene,
and delayed `stopped` lines.

A scene (`ShowcaseScene`, or a subclass named with `Class="..."`) has a
`key`, a caption `title`, `minTicks` (120), `timeoutTicks` (3600),
`settleTicks` (0), a `focusOffset` from the map's centre, `selectSubject`
(true), and `shots`. A shot has `atTick`, an `offset` (from the scene's
focus, or the map's centre with `fromMapCenter`), a `zoom` (8 to 80,
default 24), and either a glide (`pan`, the default, over `panSeconds`,
0.6) or a cut. Subclasses override `Subject`, `Setup`, `Tick`, `IsDone`,
`End` and `OutcomeKey`; a scene holding mutable state overrides
`CloneForRun`.

### The Worker, the Director and the Session

- **A worker** overrides `Setup(Map)`, `Tick()` and `Finished`, and may
  offer `Skip()` (with `CanSkip`) and a `Caption`. `Narrate(scene, when,
  targets)` queues the def's lines; `Say(scene, text, targets)` says a
  computed one. `EnableDevButtons()` turns on the developer buttons of
  the devices it stages; see "God Mode" below.
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
    and narration ends. The game keeps running as a showcase game (it
    still never autosaves); the camera and the colony are the viewer's,
    and a device already counting down still goes off.
- **The session** (`GameComponent_ShowcaseSession`) adopts the
  launcher's request for its own new game only, waits 120 frames for the
  map to settle, sets the worker up, ticks it, suppresses autosave,
  draws the caption bar, and shows the closing message once any queued
  narration has been said.

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

### The Command Line

- `-showcase=<defName>` acts once, the first time the main menu is
  shown: the menu starts the showcase through the same launcher as the
  Start button, so the def's map size and biome apply. An unknown,
  unavailable or refused showcase logs a `REFUSED` cue and an error
  saying why.
- `-showcaseforce` also lets `-showcase=` start a showcase flagged
  fatal, as Force start does. Missing requirements can never be forced.
- With `-quicktest` as well, vanilla generates its own quick-test game
  (250 cells, a random tile) and draws the main menu for a single frame
  on the way; the menu then leaves the command line alone, and the
  quick-test game adopts the showcase on its map as it is. The def's
  `mapSize` and `biome` are not applied on this path (that would take a
  second Harmony patch); `MAPSIZE` and `BIOME` cues say so. It is meant
  for developer runs. (Starting a second generation from that one menu
  frame collided with vanilla's: "Could not generate starting map
  because there is no any player faction base", then an exception in
  `Game.InitNewGame`, observed on RimWorld 1.6.4871.)

### Log Cues

Every API cue is one log message, `[BT-SHOWCASE] <CUE> <detail>`; the
prefix carries the host mod's tag while the API lives in this mod. The
API's errors and warnings carry the same prefix, but are not cues.

- `LAUNCH <def>`: a game is being generated for it; with `forced`, or
  `quicktest` when it is adopted on a quick-test map.
- `BIOME <biome>`: a tile of the biome was chosen; or `... not found;
  random start`; or `... ignored; quicktest map`.
- `MAPSIZE <n> ignored; quicktest map is <m>`: the quick-test map cannot
  have the def's size.
- `GAME <def>`: the new game's session adopted the showcase.
- `START <def>`: the map has settled and the worker is being set up
  (with `forced` if it was).
- `READY`: the worker is set up; capture tools maximize and start here.
- `SCENE <key>`: a director scene began.
- `END <key> <outcome>`: it ended: `completed`, `timedOut`, `skipped`,
  `stopped`, `failed`, or the scene's own outcome key.
- `SKIPPED <key>: set-up failed`: a scene's set-up threw; it never
  began.
- `SKIP <def>`: the viewer pressed Skip.
- `FINISHED <def>`: the showcase played out.
- `STOPPED <def>`: the viewer pressed Stop, or the worker failed while
  running (with an error).
- `FAILED <def>`: the worker could not be set up.
- `REFUSED <name>`: `-showcase=` will not start (with an error saying
  why).

Capture tooling should treat `REFUSED` and `FAILED` as ending the run:
`Tools/smoke-test.ps1 -Showcase` does. `BT_Showcase` adds its own cues
(`ANYRIM` or "AnyRim skipped", `THROW`, `TOUR`).

## Tests

- **Core unit tests:** scene sequencing (including a failed scene),
  grouping showcases by mod, requirement and compatibility checks, the
  launch decision, the caption and its bar's layout, narration (including
  delayed `stopped` lines), the god-mode loan, and the Force-start and
  command-line rules.
- **XML tests:** every `ShowcaseDef`'s fields (worker classes matched by
  exact namespace), the keyed text and the keys the code uses, preview
  images, the Harmony dependency, that no file but the button's uses
  Harmony, that only the god-mode loan sets god mode and every DEV
  button needs dev mode, and that the smoke runner's test showcases are
  never shipped. Narration scene keys are checked by the def's
  configuration check (no shipped def has narration yet).
- **Smoke** (`Source/SmokeTestShowcases.cs`, with test showcases made in
  code; details in `docs/AI-TESTING-STRATEGY.txt`):
  - the button is the mod's only Harmony patch, vanilla's main menu gets
    exactly one Showcases option while the in-game menu gets none, and
    each of the column's gates works on its own;
  - the three windows open, list Batshit Technology and its showcases,
    grey out one missing a mod with the reason, refuse a fatal flag while
    offering Force start (in dev mode and outside it), and honour Back
    and Escape;
  - verdicts in game match Core's, and the launcher refuses what it must
    and hands a request only to its own game;
  - a directed test showcase runs through its scenes, Skip, a timeout
    and Stop, with its cues and narration in order and no API warning;
    scenes that throw are logged once each and the tour goes on; a cut
    right after a glide holds;
  - god mode is lent only in dev mode and given back on every way out;
  - a showcase game never autosaves, an ordinary one does, the session
    saves nothing, and the save folder gains no files.
- **Showcase run:** `Tools/smoke-test.ps1 -Showcase` starts BT_Showcase
  through the main menu with `-showcase=BT_Showcase` and waits for
  `[BT-SHOWCASE] READY`, failing on a `REFUSED` or `FAILED` cue or a mod
  error before it. The run on to `FINISHED` is checked by hand in the
  verification runs, since the game is left open for the viewer.

## Decisions

Answered by Matt "Breakpoint" Heck on 2026-10-13:

1. **Harmony** is approved for the main-menu button, the mod's first
   patch. Everything else stays patch-free.
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
