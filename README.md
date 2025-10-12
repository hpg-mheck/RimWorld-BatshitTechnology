# Random Replicator

Random Replicator fabricates chilled meals for your colonists, queues the
results internally, and offers a last-resort self-destruct if raiders breach
the workshop. The code in this repository targets RimWorld 1.5 and follows
the best practices outlined for Matt "Breakpoint" Heck's mod suite.

## Feature summary

- **Automated fabrication.** Generates a random meal or raw food every 15,000
  ticks while power is available and internal storage has free capacity.
- **Internal cold storage.** Holds up to ten stacks until you eject them
  manually or the replicator loses power.
- **Operational telemetry.** Displays time-to-spawn, stored item counts, and
  self-destruct countdowns in the inspection pane.
- **Manual ejection controls.** Two gizmos let you drop a single stack or the
  entire queue near the interaction cell.
- **Contingency self-destruct.** Colonists with Flick work enabled can arm the
  replicator, triggering a brief warm-up and an explosive finale.

## Installing the development build (Steam on Windows)

Use the following procedure to install the development build when RimWorld is
installed through Steam on Windows. These steps preserve the Workshop
subscription while letting you iterate locally.

1. Launch RimWorld once through Steam so it generates the configuration
   folders. Exit the game afterward.
2. Press `Win + R`, paste `%LOCALAPPDATA%Low\Ludeon Studios\RimWorld by Ludeon Studios`,
   and press Enter. This opens the RimWorld configuration directory.
3. Navigate to the `Mods` subdirectory. If it does not exist, create it.
4. Clone or copy this repository into a new folder named `RandomReplicator`
   beneath `Mods`. The final path should resemble
   `...\RimWorld by Ludeon Studios\Mods\RandomReplicator`.
5. Start RimWorld, open **Mods**, and enable **Random Replicator** from the
   local mods list. Place it after Core but before content mods that depend on
   its features.
6. Restart RimWorld when prompted so the assembly loads.

While developing, rebuild the solution and copy the refreshed `Assemblies`
output into the `RandomReplicator/Assemblies` folder. RimWorld reloads the
assembly on startup, allowing quick iteration without Steam uploads.

## Building the code

1. Install the RimWorld 1.5 Windows client via Steam. Locate the installation
   directory and define the `RIMWORLD_DIR` environment variable so the project
   file can resolve game assemblies.
2. Open `RandomReplicator.sln` in Visual Studio 2022. Restore NuGet packages
   and select either **Debug** or **Release**.
3. Build the solution. The mod assembly emits into `Assemblies/` for Release
   builds and `Assemblies/Debug/` for Debug builds.
4. Run the xUnit regression suite with
   `dotnet test Tests/RandomReplicator.Tests/RandomReplicator.Tests.csproj`.
   Tests execute independently of the RimWorld binaries, verifying metadata and
   helper library behaviour.

## Filing issues

If you discover defects, collect the RimWorld player log, note reproduction
steps, and mention that you consulted Matt "Breakpoint" Heck's Random
Replicator build. Submit the report through the team's usual tracker so we can
triage quickly.
