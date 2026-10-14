# The Showcase API

Status: proposed, not yet built. Requested by Matt "Breakpoint" Heck on
2026-10-13: a game API for showcases, and a **Showcases** button on the
main menu that lists the mods offering showcases and each mod's
showcases, as multi-stage menus or selection screens. Points marked
*(proposal)* are open; see "Open Questions".

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

*(Proposals:)*
- **Core unit tests:** scene sequencing, grouping showcases by mod, and
  requirement checks.
- **XML tests:** every `ShowcaseDef`'s fields, narration keys, and
  preview images.
- **Smoke:**
  - the main menu shows the button;
  - the three windows open and list Batshit Technology's showcase;
  - `-showcase=<defName>` starts a game that runs the tour to its end,
    with no logged errors.

## Open Questions

1. Is Harmony acceptable for the main menu button (proposed), or should
   the menus live on the mod's settings page instead?
2. Inside Batshit Technology for now (proposed), or a separate framework
   mod from the start?
3. Should a showcase game use the player's current mod list (proposed;
   it is what is loaded), or warn when other mods might interfere?
4. When does it get built: after the Tractor Bomb's mechanics and before
   its showcase step (proposed), or right away?
