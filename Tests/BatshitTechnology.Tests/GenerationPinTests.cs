using System;
using System.Linq;
using System.Threading.Tasks;
using BatshitTechnology.Showcases;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the window in which a showcase's map generation draws pinned
/// seeds (Core's GenerationPin; docs/SHOWCASE-API.md, "Seeds"): closed
/// until armed, open only for the very object it was armed for, closed
/// again on any disarm, and counting every call either way.
/// </summary>
public sealed class GenerationPinTests
{
    /// <summary>
    /// A fresh window is closed: nothing is pinned, every call passes
    /// through and is counted as such, and disarming says it was closed.
    /// </summary>
    [Fact(DisplayName = "A fresh window is closed and passes every call through")]
    public void Fresh_window_should_be_closed()
    {
        var pin = new GenerationPin();
        var game = new object();

        Assert.False(pin.Armed);
        Assert.False(pin.Pins(game));
        Assert.False(pin.Pins(null));
        Assert.False(pin.Take(game));
        Assert.False(pin.Take(null));
        Assert.Equal(0, pin.PinnedTotal);
        Assert.Equal(2, pin.PassedTotal);
        Assert.Equal(-1, pin.Disarm());
    }

    /// <summary>
    /// Armed for a game, the window pins that game's calls only: another
    /// game's and an ownerless call pass through. Disarming reports the
    /// calls pinned and closes it, after which the same game's calls pass
    /// through; a second disarm says it was already closed.
    /// </summary>
    [Fact(DisplayName = "An armed window pins only its own game's calls until disarmed")]
    public void Armed_window_should_pin_only_its_owner()
    {
        var pin = new GenerationPin();
        var game = new object();
        var other = new object();

        pin.Arm(game);

        Assert.True(pin.Armed);
        Assert.True(pin.Pins(game));
        Assert.False(pin.Pins(other));
        Assert.False(pin.Pins(null));
        Assert.True(pin.Take(game));
        Assert.True(pin.Take(game));
        Assert.False(pin.Take(other));
        Assert.False(pin.Take(null));
        Assert.True(pin.Take(game));
        Assert.Equal(3, pin.Disarm());
        Assert.False(pin.Armed);
        Assert.False(pin.Pins(game));
        Assert.False(pin.Take(game));
        Assert.Equal(-1, pin.Disarm());
        Assert.Equal(3, pin.PinnedTotal);
        Assert.Equal(3, pin.PassedTotal);
    }

    /// <summary>
    /// The window binds its owner by reference, never by equality, so an
    /// object that claims to equal the owner is still another game.
    /// </summary>
    [Fact(DisplayName = "The window binds its owner by reference, not equality")]
    public void Window_should_bind_by_reference()
    {
        var pin = new GenerationPin();
        var game = new EqualToEverything();
        var lookalike = new EqualToEverything();

        pin.Arm(game);

        Assert.True(lookalike.Equals(game));
        Assert.False(pin.Pins(lookalike));
        Assert.False(pin.Take(lookalike));
        Assert.True(pin.Take(game));
        Assert.Equal(1, pin.Disarm());
    }

    /// <summary>
    /// Arming again moves the window to the new game, closing the old
    /// one's: its calls pass through and its count is dropped, while the
    /// totals keep everything. A null owner is refused and leaves the
    /// window as it was.
    /// </summary>
    [Fact(DisplayName = "Re-arming moves the window; a null owner is refused")]
    public void Rearming_should_move_the_window()
    {
        var pin = new GenerationPin();
        var first = new object();
        var second = new object();

        pin.Arm(first);
        Assert.True(pin.Take(first));
        Assert.True(pin.Take(first));
        pin.Arm(second);

        Assert.False(pin.Take(first));
        Assert.True(pin.Take(second));
        Assert.Throws<ArgumentNullException>(() => pin.Arm(null!));
        Assert.True(pin.Pins(second));
        Assert.Equal(1, pin.Disarm());
        Assert.Equal(3, pin.PinnedTotal);
        Assert.Equal(1, pin.PassedTotal);
    }

    /// <summary>
    /// Calls from many threads at once (the map generates on a long
    /// event's worker while the main thread may ask) are each decided and
    /// counted exactly once.
    /// </summary>
    [Fact(DisplayName = "The window counts calls from many threads exactly")]
    public void Window_should_count_concurrent_calls_exactly()
    {
        var pin = new GenerationPin();
        var game = new object();
        var other = new object();
        const int calls = 20000;

        pin.Arm(game);
        bool[] pinned = new bool[calls];
        Parallel.For(0, calls, i => pinned[i] = pin.Take(i % 2 == 0 ? game : other));

        Assert.Equal(calls / 2, pinned.Count(p => p));
        Assert.True(pinned.Where((p, i) => p != (i % 2 == 0)).Count() == 0);
        Assert.Equal(calls / 2, pin.Disarm());
        Assert.Equal(calls / 2, pin.PinnedTotal);
        Assert.Equal(calls / 2, pin.PassedTotal);
    }

    /// <summary>An object equal to every other, to test binding by reference.</summary>
    private sealed class EqualToEverything
    {
        /// <summary>Claims equality with anything.</summary>
        /// <param name="obj">The other object.</param>
        /// <returns>Always true.</returns>
        public override bool Equals(object? obj) => true;

        /// <summary>The same hash for every instance.</summary>
        /// <returns>Zero.</returns>
        public override int GetHashCode() => 0;
    }
}
