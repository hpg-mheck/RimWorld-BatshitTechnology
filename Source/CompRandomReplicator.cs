using System;
using System.Collections.Generic;
using System.Linq;
using RandomReplicator.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace RandomReplicator;

/// <summary>
/// Defines the configurable parameters that drive the random replicator.
/// </summary>
public sealed class CompProperties_RandomReplicator : CompProperties
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompProperties_RandomReplicator"/> class.
    /// </summary>
    public CompProperties_RandomReplicator()
    {
        compClass = typeof(CompRandomReplicator);
    }

    /// <summary>
    /// Gets or sets the number of ticks between production attempts.
    /// </summary>
    public int ticksPerSpawn = 15000;

    /// <summary>
    /// Gets or sets the internal storage capacity. The name remains from an
    /// earlier iteration that counted nearby stacks instead of held items.
    /// </summary>
    public int maxNearbyStacks = 10;

    /// <summary>
    /// Gets or sets the radius used when ejecting stored items.
    /// </summary>
    public float searchRadius = 1.9f;

    /// <summary>
    /// Gets or sets the ThingCategoryDef names that may be replicated.
    /// </summary>
    public List<string> allowedThingCategoryDefs = new();

    /// <summary>
    /// Gets or sets a value indicating whether power is required to operate.
    /// </summary>
    public bool requirePower = true;

    /// <summary>
    /// Gets or sets a value indicating whether the countdown should appear in
    /// the inspection pane.
    /// </summary>
    public bool writeTimeLeftToSpawn = true;

    /// <summary>
    /// Gets or sets a value indicating whether the interaction cell is the
    /// preferred ejection location.
    /// </summary>
    public bool preferInteractionCell = true;
}

/// <summary>
/// Provides random food fabrication with internal storage and self-destruct
/// integration.
/// </summary>
public sealed class CompRandomReplicator : ThingComp, IThingHolder
{
    private const int TicksPerLetterCheck = 2500;
    private const float ExplosionRadius = 6.5f;
    private const int SelfDestructWarmupTicks = 60;

    private ThingOwner<Thing> _internalStorage = null!;
    private readonly WeightedSelector<ThingDef> _selector = new(() => Rand.
        Value);

    private List<ThingDef>? _cachedAllowedThings;
    private List<WeightedOption<ThingDef>>? _cachedWeightedOptions;
    private bool _hasSentPowerLossLetter;
    private int _ticksUntilNextSpawn;
    private int _selfDestructTicksRemaining;
    private Pawn? _armedBy;

    /// <summary>
    /// Gets the strongly typed properties instance.
    /// </summary>
    public CompProperties_RandomReplicator Props => (CompProperties_
        RandomReplicator)props;

    /// <summary>
    /// Gets a snapshot of stored items for UI display.
    /// </summary>
    public IReadOnlyList<Thing> StoredItems => _internalStorage.InnerListFor
        Reading;

    /// <inheritdoc />
    public override void Initialize(CompProperties props)
    {
        base.Initialize(props);
        _internalStorage = new ThingOwner<Thing>(this);
    }

    /// <inheritdoc />
    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Deep.Look(ref _internalStorage, "internalStorage", this);
        Scribe_Values.Look(ref _ticksUntilNextSpawn,
            nameof(_ticksUntilNextSpawn));
        Scribe_Values.Look(ref _selfDestructTicksRemaining,
            nameof(_selfDestructTicksRemaining));
        Scribe_References.Look(ref _armedBy, nameof(_armedBy));
        Scribe_Values.Look(ref _hasSentPowerLossLetter,
            nameof(_hasSentPowerLossLetter));
    }

    /// <inheritdoc />
    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        _internalStorage.Notify_AddedToWorld(parent);
        if (!respawningAfterLoad)
        {
            ResetProductionTimer();
        }
    }

    /// <inheritdoc />
    public override void PostDeSpawn(Map map)
    {
        base.PostDeSpawn(map);
        _internalStorage.TryDropAll(parent.Position, map, ThingPlaceMode.Near);
        _cachedAllowedThings = null;
        _cachedWeightedOptions = null;
    }

    /// <inheritdoc />
    public override void CompTick()
    {
        base.CompTick();
        if (!parent.Spawned)
        {
            return;
        }

        if (_selfDestructTicksRemaining > 0)
        {
            TickSelfDestructCountdown();
            return;
        }

        if (!IsOperational())
        {
            MaybeNotifyPowerLoss();
            return;
        }

        _hasSentPowerLossLetter = false;

        if (_internalStorage.Count >= Props.maxNearbyStacks)
        {
            return;
        }

        if (--_ticksUntilNextSpawn <= 0)
        {
            if (TryProduceFood(out var producedThing))
            {
                if (!_internalStorage.TryAdd(producedThing))
                {
                    DropThing(producedThing);
                }
            }

            ResetProductionTimer();
        }
    }

    /// <inheritdoc />
    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (var gizmo in base.CompGetGizmosExtra())
        {
            yield return gizmo;
        }

        yield return BuildEjectSingleGizmo();
        yield return BuildEjectAllGizmo();
        yield return BuildSelfDestructToggle();
    }

    /// <inheritdoc />
    public override string CompInspectStringExtra()
    {
        var text = base.CompInspectStringExtra();
        text = AppendLine(text, "Stored items", _internalStorage.Count.ToString
            ());

        if (Props.writeTimeLeftToSpawn && _selfDestructTicksRemaining == 0 &&
            IsOperational() && _internalStorage.Count < Props.maxNearbyStacks)
        {
            text = AppendLine(text,
                "Time until next fabrication",
                _ticksUntilNextSpawn.ToStringTicksToPeriod());
        }

        if (_selfDestructTicksRemaining > 0)
        {
            text = AppendLine(text,
                "Self-destruct",
                _selfDestructTicksRemaining.ToStringTicksToPeriod());
        }

        return text.TrimEndNewlines();
    }

    /// <inheritdoc />
    public override void PostDrawExtraSelectionOverlays()
    {
        base.PostDrawExtraSelectionOverlays();
        GenDraw.DrawRadiusRing(parent.Position, Props.searchRadius);
        if (_selfDestructTicksRemaining > 0 || HasDesignation())
        {
            GenDraw.DrawRadiusRing(parent.Position, ExplosionRadius, Color.red);
        }
    }

    /// <inheritdoc />
    public void GetChildHolders(List<IThingHolder> outChildren)
    {
        if (outChildren == null)
        {
            throw new ArgumentNullException(nameof(outChildren));
        }

        ThingOwnerUtility.AppendThingHoldersFromThings(outChildren,
            _internalStorage.InnerListForReading);
    }

    /// <inheritdoc />
    public ThingOwner GetDirectlyHeldThings() => _internalStorage;

    /// <summary>
    /// Triggers the self-destruct warmup sequence when a pawn arms the device.
    /// </summary>
    /// <param name="armedBy">The pawn responsible for arming the device.</param>
    public void ArmSelfDestruct(Pawn armedBy)
    {
        _armedBy = armedBy;
        _selfDestructTicksRemaining = SelfDestructWarmupTicks;
        RemoveDesignation();
        Messages.Message("Random Replicator self-destruct armed.", parent,
            MessageTypeDefOf.ThreatSmall);
    }

    /// <summary>
    /// Determines whether the device is currently counting down to detonation.
    /// </summary>
    public bool IsSelfDestructArmed => _selfDestructTicksRemaining > 0;

    /// <summary>
    /// Attempts to add or remove the self-destruct designation.
    /// </summary>
    public void ToggleSelfDestructDesignation()
    {
        if (HasDesignation())
        {
            RemoveDesignation();
            Messages.Message("Self-destruct designation cleared.", parent,
                MessageTypeDefOf.TaskCompletion);
        }
        else
        {
            parent.Map.designationManager.AddDesignation(new Designation(
                parent, RRDefOf.RR_SelfDestruct));
            Messages.Message("Self-destruct designation added.", parent,
                MessageTypeDefOf.TaskCompletion);
        }
    }

    /// <summary>
    /// Indicates whether the map currently contains a self-destruct
    /// designation for this replicator.
    /// </summary>
    public bool HasDesignation()
    {
        return parent.Map.designationManager.DesignationOn(parent,
            RRDefOf.RR_SelfDestruct) != null;
    }

    /// <summary>
    /// Appends a labelled value to the inspection string when the value is
    /// non-empty.
    /// </summary>
    /// <param name="text">The existing inspection string.</param>
    /// <param name="label">The label describing the value.</param>
    /// <param name="value">The value to append when available.</param>
    /// <returns>The composed inspection string.</returns>
    private static string AppendLine(string text, string label, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return text;
        }

        var fragment = $"{label}: {value}";
        if (string.IsNullOrEmpty(text))
        {
            return fragment;
        }

        return text + "\n" + fragment;
    }

    /// <summary>
    /// Builds the gizmo that ejects a single stored item.
    /// </summary>
    /// <returns>The configured command.</returns>
    private Gizmo BuildEjectSingleGizmo()
    {
        var command = new Command_Action
        {
            defaultLabel = "Eject meal",
            defaultDesc = "Drops the oldest replicated item on the ground.",
            icon = TexCommand.DropCarried,
            action = () => EjectItems(limit: 1)
        };

        if (_internalStorage.Count == 0)
        {
            command.Disable("No items to eject.");
        }

        return command;
    }

    /// <summary>
    /// Builds the gizmo that ejects every stored item.
    /// </summary>
    /// <returns>The configured command.</returns>
    private Gizmo BuildEjectAllGizmo()
    {
        var command = new Command_Action
        {
            defaultLabel = "Eject all",
            defaultDesc = "Drops every stored item around the replicator.",
            icon = TexCommand.DesirePower,
            action = () => EjectItems(limit: null)
        };

        if (_internalStorage.Count == 0)
        {
            command.Disable("No items to eject.");
        }

        return command;
    }

    /// <summary>
    /// Builds the gizmo that toggles the self-destruct designation.
    /// </summary>
    /// <returns>The configured command.</returns>
    private Gizmo BuildSelfDestructToggle()
    {
        var command = new Command_Action
        {
            defaultLabel = HasDesignation() ? "Cancel self-destruct" :
                "Arm self-destruct",
            defaultDesc = "Requests a colonist to arm the explosive charge.",
            icon = TexCommand.TogglePower,
            action = ToggleSelfDestructDesignation
        };

        if (IsSelfDestructArmed)
        {
            command.Disable("Countdown already in progress.");
        }

        return command;
    }

    /// <summary>
    /// Ejects stored items, optionally constrained by a maximum count.
    /// </summary>
    /// <param name="limit">
    /// The maximum number of items to eject; <see langword="null"/> ejects all.
    /// </param>
    private void EjectItems(int? limit)
    {
        var ejected = 0;
        for (var index = _internalStorage.Count - 1; index >= 0; index--)
        {
            var thing = _internalStorage[index];
            _internalStorage.Remove(thing);
            DropThing(thing);
            ejected++;
            if (limit.HasValue && ejected >= limit.Value)
            {
                break;
            }
        }

        if (limit.HasValue)
        {
            Messages.Message($"Ejected {ejected} item(s).", parent,
                MessageTypeDefOf.TaskCompletion);
        }
        else
        {
            Messages.Message("All replicated items ejected.", parent,
                MessageTypeDefOf.TaskCompletion);
        }
    }

    /// <summary>
    /// Drops a single item near the replicator while respecting the configured
    /// search radius.
    /// </summary>
    /// <param name="thing">The item to drop.</param>
    private void DropThing(Thing thing)
    {
        var map = parent.Map;
        var dropCenter = Props.preferInteractionCell
            ? parent.InteractionCell
            : parent.Position;

        GenPlace.TryPlaceThing(thing, dropCenter, map, ThingPlaceMode.Near,
            out _,
            nearPlaceValidator: cell => cell.InHorDistOf(parent.Position,
                Props.searchRadius));
    }

    /// <summary>
    /// Attempts to fabricate a new item based on the weighted options.
    /// </summary>
    /// <param name="thing">When successful, receives the produced item.</param>
    /// <returns><see langword="true"/> if a valid item was produced.</returns>
    private bool TryProduceFood(out Thing thing)
    {
        var options = BuildWeightedOptions();
        if (options.Count == 0)
        {
            thing = null!;
            return false;
        }

        var chosen = _selector.Pick(options);
        thing = ThingMaker.MakeThing(chosen);
        thing.stackCount = Math.Min(thing.def.stackLimit, 1);
        return true;
    }

    /// <summary>
    /// Builds the cached list of weighted options derived from allowed
    /// definitions.
    /// </summary>
    /// <returns>The cached list of weighted options.</returns>
    private List<WeightedOption<ThingDef>> BuildWeightedOptions()
    {
        if (_cachedAllowedThings == null)
        {
            _cachedAllowedThings = ResolveAllowedThingDefs();
        }

        _cachedWeightedOptions ??= new List<WeightedOption<ThingDef>>();
        _cachedWeightedOptions.Clear();

        foreach (var def in _cachedAllowedThings)
        {
            var weight = Math.Max(def.ingestible?.preferability switch
            {
                null => 1,
                FoodPreferability.DesperateOnlyForHumanlikes => 0.25,
                FoodPreferability.DesperateOnly => 0.5,
                FoodPreferability.RawBad => 0.75,
                FoodPreferability.RawTasty => 1,
                FoodPreferability.MealAwful => 1.25,
                FoodPreferability.MealSimple => 1.5,
                FoodPreferability.MealFine => 2,
                FoodPreferability.MealLavish => 2.5,
                FoodPreferability.MealBest => 3,
                _ => 1
            }, 0.1);

            _cachedWeightedOptions.Add(new WeightedOption<ThingDef>(def,
                weight));
        }

        return _cachedWeightedOptions;
    }

    /// <summary>
    /// Resolves the set of ingestible <see cref="ThingDef"/> instances matching
    /// the configured categories.
    /// </summary>
    /// <returns>The resolved ingestible definitions.</returns>
    private List<ThingDef> ResolveAllowedThingDefs()
    {
        var categories = Props.allowedThingCategoryDefs
            .Select(DefDatabase<ThingCategoryDef>.GetNamedSilentFail)
            .Where(def => def != null)
            .ToHashSet();

        if (categories.Count == 0)
        {
            Log.Warning($"{parent.LabelCap} has no allowed categories.");
            return new List<ThingDef>();
        }

        var defs = DefDatabase<ThingDef>.AllDefsListForReading
            .Where(def => def.ingestible != null && def.tradeability !=
                Tradeability.Never)
            .Where(def => def.thingCategories?.Any(categories.Contains) ?? false)
            .ToList();

        return defs;
    }

    /// <summary>
    /// Determines whether the replicator meets all prerequisites to operate.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the replicator is powered, switched on, and
    /// spawned.
    /// </returns>
    private bool IsOperational()
    {
        if (!parent.Spawned)
        {
            return false;
        }

        if (Props.requirePower)
        {
            var power = parent.TryGetComp<CompPowerTrader>();
            if (power == null || !power.PowerOn)
            {
                return false;
            }
        }

        var flick = parent.TryGetComp<CompFlickable>();
        return flick == null || flick.SwitchIsOn;
    }

    /// <summary>
    /// Sends a letter the first time power is lost during the current outage.
    /// </summary>
    private void MaybeNotifyPowerLoss()
    {
        if (_hasSentPowerLossLetter)
        {
            return;
        }

        if (parent.IsHashIntervalTick(TicksPerLetterCheck))
        {
            Find.LetterStack.ReceiveLetter("Random Replicator offline",
                "The Random Replicator lost power and has paused fabrication.",
                LetterDefOf.NegativeEvent,
                parent);
            _hasSentPowerLossLetter = true;
        }
    }

    /// <summary>
    /// Resets the fabrication countdown to the configured production interval.
    /// </summary>
    private void ResetProductionTimer()
    {
        _ticksUntilNextSpawn = Props.ticksPerSpawn;
    }

    /// <summary>
    /// Advances the self-destruct countdown and triggers detonation when
    /// completed.
    /// </summary>
    private void TickSelfDestructCountdown()
    {
        _selfDestructTicksRemaining--;
        if (_selfDestructTicksRemaining <= 0)
        {
            Detonate();
        }
        else if (_selfDestructTicksRemaining % 10 == 0)
        {
            FleckMaker.ThrowMicroSparks(parent.TrueCenter(), parent.Map);
        }
    }

    /// <summary>
    /// Destroys the replicator via an explosion and records the tale when
    /// applicable.
    /// </summary>
    private void Detonate()
    {
        var map = parent.MapHeld;
        if (map == null)
        {
            return;
        }

        GenExplosion.DoExplosion(parent.Position, map, ExplosionRadius,
            DamageDefOf.Bomb, parent, postExplosionSpawnThingDef: ThingDefOf.
            Filth_Ash);
        if (_armedBy != null)
        {
            TaleRecorder.RecordTale(TaleDefOf.DestroyedBuilding,
                _armedBy, parent);
        }

        parent.Destroy(DestroyMode.KillFinalize);
    }

    /// <summary>
    /// Removes the self-destruct designation if one is present.
    /// </summary>
    private void RemoveDesignation()
    {
        var designation = parent.Map.designationManager.DesignationOn(parent,
            RRDefOf.RR_SelfDestruct);
        designation?.Delete();
    }
}
