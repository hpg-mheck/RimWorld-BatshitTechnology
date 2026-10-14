# The Showcase API

Status: specified, not yet built; it follows the Tractor Bomb's
mechanics (see "Order of Work"). Requested by Matt "Breakpoint" Heck on
2026-10-13: a game API for showcases, and a **Showcases** button on the
main menu that lists the mods offering showcases and each mod's
showcases, as multi-stage menus or selection screens. "Because these are
amazing, and I think it would be a hell of a lot of fun to let players
use them normally." His answers to the open questions are under
"Decisions"; smaller details still marked *(proposal)* stand unless he
says otherwise.

## What It Is

Today the showcase is a dev tool: `Tools/smoke-test.ps1 -Showcase`
starts RimWorld with command-line flags, and `Source/Showcase.cs` does
the rest. The API turns that into something any player can start from
the main menu, and any mod can contribute to.

- **For players:** Main menu, then **Showcases**, then a mod, then one of
  its showcases, then **Start**. A fresh, throwaway game is generated and
  the showcase plays out, a directed tour with its own camera and
  commentary. Nothing touches the player's saves unless they choose to
  save.
- **For mod authors:** declare a showcase in XML (a `ShowcaseDef`), point
  it at a worker class, and optionally build it from the API's director:
  scenes, camera moves, captions and narration lines, all data-driven.
  Batshit Technology's own tour (`docs/SHOWCASE.md`, "The Directed Tour"
  and "The Story") becomes the first showcase built on it.

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

Back returns one level; Escape closes the whole thing. *(Proposal:)* the
windows use vanilla's dialog style, so they look like part of the game.

## The API

*(Proposals throughout.)*

- **`ShowcaseDef`** (a RimWorld Def, so any mod adds one in XML):
  - `label`, `description`;
  - `previewTexPath` and `estimatedMinutes`;
  - `requiredMods` (package ids, such as DLCs);
  - `compatibility`: known issues with other mods, by package id, each
    with a severity (see "Other Mods") and a short note;
  - `mapSize` (default 250) and an optional biome preference;
  - `workerClass`, a subclass of `ShowcaseWorker`;
  - for director-driven showcases, a list of scenes.
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

  The director also provides the caption bar, the Skip button, the log
  cues for capture tooling, and letting go of the camera at the end.
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
  without dev mode.
- **The command line still works.** `-showcase=<defName>` starts a
  showcase directly, so `Tools/smoke-test.ps1 -Showcase` and the
  capture tooling keep working.

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
  anyway, for testing and for reproducing the crash.

## The Main Menu Button Needs Harmony

Vanilla builds the main menu's buttons as a local list inside
`MainMenuDrawer.DoMainMenuControls`, with no extension point, so adding a
button takes a Harmony patch: a small postfix that adds one option. That
is the case the Harmony policy allows ("acceptable if necessary"); it
would be the mod's first Harmony dependency. Everything else in the API
needs no patches. *(Fallback, if Harmony is ruled out:)* the same menus
open from the mod's settings page (Options, then Mod Settings), which
needs no patch but is harder to find.

## Packaging

The API can live in one of two places:

- **Inside Batshit Technology.** It is quickest to build, but other mods
  that want showcases would have to depend on Batshit Technology.
- **As its own small framework mod** (for example "Showcase Framework",
  packageId `breakpoint8088.Showcases`), which Batshit Technology and any
  other mod depend on. This is the conventional shape for an API many
  mods share. It needs its own folder and About.xml, and a load-order
  rule.

*(Proposal:)* build it inside Batshit Technology under its own namespace
(`BatshitTechnology.Showcases`), keeping it free of Batshit-specific
code, and split it out into a framework mod once a second mod wants it.

## Order of Work

The directed tour (Tractor Bomb roadmap step 12) should be built on this
API rather than before it. *(Proposal:)* build the API on its own branch,
`feature/showcase-api`, after the Tractor Bomb's mechanics (steps 2 to
11). Then rebuild the showcase on it as the tour, as the Tractor Bomb's
step 12.

## Tests

- **Core unit tests:** scene sequencing, grouping showcases by mod,
  requirement and compatibility checks, the launch decision, the caption
  and its bar's layout, and the Force-start and command-line rules.
- **XML tests:** every `ShowcaseDef`'s fields, the keyed text and the
  keys the code uses, preview images, the Harmony dependency, and that
  the smoke runner's test showcases are never shipped.
- **Smoke** (`Source/SmokeTestShowcases.cs`, with test showcases made in
  code; details in `docs/AI-TESTING-STRATEGY.txt`):
  - the button is the mod's only Harmony patch, and vanilla's main menu
    gets exactly one Showcases option while the in-game menu gets none;
  - the three windows open, list Batshit Technology and its showcases,
    grey out one missing a mod with the reason, refuse a fatal flag while
    offering Force start, and honour Back and Escape;
  - verdicts in game match Core's;
  - a directed test showcase runs through its scenes, Skip, a timeout
    and Stop, with its cues and narration in order;
  - a showcase game never autosaves, an ordinary one does, and the save
    folder gains no files.
- **Showcase run:** `Tools/smoke-test.ps1 -Showcase` starts BT_Showcase
  through the main menu with `-showcase=BT_Showcase` and waits for
  `[BT-SHOWCASE] READY`.

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
