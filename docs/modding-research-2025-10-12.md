# Modding risk review — 2025-10-12

## Research highlights
- The RimWorld wiki's Harmony guide warns mod authors not to ship bundled
  copies of Harmony, to depend on the standalone mod instead, and to avoid
  heavy-handed prefixes that skip the original method because that can break
  other mods using the same hook.【1】
- The Harmony guide also reiterates that RimWorld mods should target the
  official .NET Framework 4.7.2 toolchain so builds match the game's runtime
  environment.【2】
- HugsLib's README explains that the library must be installed as a separate
  mod, should be declared as a dependency rather than redistributed, and can
  broadcast update news so players learn about changes without manual
  changelog edits.【3】

## Repository findings
- `RandomReplicator.csproj` already targets .NET Framework 4.7.2 and links the
  managed RimWorld assemblies exactly as the wiki prescribes, so no build
  adjustments are required.【2】【4】
- The repository does not bundle Harmony or HugsLib binaries, nor does the
  mod list unnecessary dependencies, so we are compliant with the Harmony and
  HugsLib guidance.【1】【3】
- Because the project avoids Harmony prefixes that short-circuit original
  methods, there are no compatibility hazards identified during this pass.
  We will continue to prefer postfix-style integrations if a future feature
  requires Harmony patches.【1】

## Next steps
- Keep the RimWorld version metadata current in `About/About.xml` whenever the
  game updates.
- Capture any future Harmony usage in unit tests so we can spot incompatibility
  regressions before shipping updates.

[1]: https://r.jina.ai/https://rimworldwiki.com/wiki/Modding_Tutorials/Harmony
[2]: https://r.jina.ai/https://rimworldwiki.com/wiki/Modding_Tutorials/Setting_up_a_solution
[3]: https://raw.githubusercontent.com/UnlimitedHugs/RimworldHugsLib/master/README.md
[4]: Source/RandomReplicator.csproj
