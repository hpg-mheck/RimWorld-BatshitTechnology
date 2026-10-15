using System;

namespace BatshitTechnology.Showcases;

/// <summary>
/// The window in which a showcase's map generation draws pinned seeds:
/// opened (armed) for one owner, the game being generated, and closed
/// (disarmed) once its map is made or anything goes wrong. A call made
/// for that very owner while the window is open is pinned; every other
/// call, for another owner, for none, or with the window closed, passes
/// through to vanilla. It holds that rule without RimWorld, so it is unit
/// tested; the game's <c>ShowcaseMapSeed</c> applies it to
/// <c>Map.NextGenSeed</c> (docs/SHOWCASE-API.md, "Seeds").
/// </summary>
/// <remarks>
/// Binding the window to its owner by reference, rather than keeping a
/// bare flag, means a window that some failure leaves open can never pin
/// another game's seeds: the player's next real game is another object.
/// Arming, disarming and taking a call may come from different threads
/// (a long event's worker generates the map; the main menu disarms), so
/// every member holds one lock.
/// </remarks>
public sealed class GenerationPin
{
    /// <summary>Guards every field.</summary>
    private readonly object gate = new object();

    /// <summary>The owner the window is open for, or null when closed.</summary>
    private object? owner;

    /// <summary>Calls pinned since the window last opened.</summary>
    private int pinnedSinceArmed;

    /// <summary>Calls pinned since the process started.</summary>
    private long pinnedTotal;

    /// <summary>Calls passed through since the process started.</summary>
    private long passedTotal;

    /// <summary>
    /// Gets a value indicating whether the window is open, for any owner.
    /// </summary>
    public bool Armed
    {
        get
        {
            lock (gate)
            {
                return owner != null;
            }
        }
    }

    /// <summary>Gets how many calls have been pinned, ever.</summary>
    public long PinnedTotal
    {
        get
        {
            lock (gate)
            {
                return pinnedTotal;
            }
        }
    }

    /// <summary>
    /// Gets how many calls have passed through to vanilla, ever.
    /// </summary>
    public long PassedTotal
    {
        get
        {
            lock (gate)
            {
                return passedTotal;
            }
        }
    }

    /// <summary>
    /// Opens the window for an owner. An earlier owner's window, if one
    /// was left open, is closed first and its count dropped: only one
    /// game generates at a time.
    /// </summary>
    /// <param name="newOwner">The owner: the game being generated.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="newOwner"/> is null.
    /// </exception>
    public void Arm(object newOwner)
    {
        if (newOwner == null)
        {
            throw new ArgumentNullException(nameof(newOwner));
        }

        lock (gate)
        {
            owner = newOwner;
            pinnedSinceArmed = 0;
        }
    }

    /// <summary>
    /// Whether a call made for an owner would be pinned now: only while
    /// the window is open for that very object. Counts nothing.
    /// </summary>
    /// <param name="current">The call's owner (the current game), or null.</param>
    /// <returns>True if it would be pinned.</returns>
    public bool Pins(object? current)
    {
        lock (gate)
        {
            return PinsLocked(current);
        }
    }

    /// <summary>
    /// Decides one call and counts it: pinned (true) only while the
    /// window is open for <paramref name="current"/>, otherwise passed
    /// through (false).
    /// </summary>
    /// <param name="current">The call's owner (the current game), or null.</param>
    /// <returns>True if the call is to be pinned.</returns>
    public bool Take(object? current)
    {
        lock (gate)
        {
            if (PinsLocked(current))
            {
                pinnedSinceArmed++;
                pinnedTotal++;
                return true;
            }

            passedTotal++;
            return false;
        }
    }

    /// <summary>
    /// Closes the window. Safe to call on every way out, any number of
    /// times.
    /// </summary>
    /// <returns>
    /// How many calls were pinned while it was open, or -1 if it was
    /// already closed.
    /// </returns>
    public int Disarm()
    {
        lock (gate)
        {
            if (owner == null)
            {
                return -1;
            }

            owner = null;
            int pinned = pinnedSinceArmed;
            pinnedSinceArmed = 0;
            return pinned;
        }
    }

    /// <summary>
    /// The rule itself, with the lock held: open, and for this very
    /// object.
    /// </summary>
    /// <param name="current">The call's owner, or null.</param>
    /// <returns>True if a call for it is pinned.</returns>
    private bool PinsLocked(object? current) =>
        current != null && owner != null && ReferenceEquals(current, owner);
}
