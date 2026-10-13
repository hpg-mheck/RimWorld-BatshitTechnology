using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies AnyRim's signs, labels and messages in <see cref="AnyRimText"/>
/// against docs/SHOWCASE.md, "Signs and Labels".
/// </summary>
public sealed class AnyRimTextTests
{
    /// <summary>The welcome sign at full strength.</summary>
    [Fact(DisplayName = "Welcome sign at 8")]
    public void WelcomeSign_should_read_eight()
    {
        Assert.Equal(
            "WELCOME TO ANYRIM\nA Typical Rim Community\nPop. 8, subject to change\n"
            + "Est. this morning. Re-est. as needed.",
            AnyRimText.WelcomeSign(8));
    }

    /// <summary>One resident left: the count changes, nothing else.</summary>
    [Fact(DisplayName = "Welcome sign at 1")]
    public void WelcomeSign_should_read_one()
    {
        string[] lines = AnyRimText.WelcomeSign(1).Split('\n');
        Assert.Equal(4, lines.Length);
        Assert.Equal("Pop. 1, subject to change", lines[2]);
        Assert.Equal(AnyRimText.WelcomeSign(8).Split('\n').Where((_, i) => i != 2),
            lines.Where((_, i) => i != 2));
    }

    /// <summary>At zero the population line thanks the town.</summary>
    [Fact(DisplayName = "Welcome sign at 0")]
    public void WelcomeSign_should_thank_at_zero()
    {
        Assert.Equal("Pop. 0. Thank you for your participation.",
            AnyRimText.WelcomeSign(0).Split('\n')[2]);
        Assert.Throws<ArgumentOutOfRangeException>(() => AnyRimText.WelcomeSign(-1));
    }

    /// <summary>The census message, quiet and calm.</summary>
    [Fact(DisplayName = "Census message")]
    public void CensusMessage_should_read_as_designed()
    {
        Assert.Equal(
            "AnyRim census revised to 7. The sign has been updated. Please remain calm.",
            AnyRimText.CensusMessage(7));
        Assert.StartsWith("AnyRim census revised to 0.", AnyRimText.CensusMessage(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => AnyRimText.CensusMessage(-2));
    }

    /// <summary>The opening message the showcase shows once the town is up.</summary>
    [Fact(DisplayName = "Opening message")]
    public void OpeningMessage_should_read_as_designed()
    {
        Assert.Equal(
            "AnyRim, a small town in the north-west, is available for testing. "
            + "Its residents have been informed.",
            AnyRimText.OpeningMessage);
        Assert.Equal("AnyRim", AnyRimText.TownName);
    }

    /// <summary>Every label kind has its documented text.</summary>
    /// <param name="kind">The label.</param>
    /// <param name="expected">Its text.</param>
    [Theory(DisplayName = "Every label reads as designed")]
    [InlineData(LabelKind.Exit, "NOW LEAVING ANYRIM\nCome back soon. We will try to be here.")]
    [InlineData(LabelKind.Cemetery, "ANYRIM MEMORIAL GARDENS\nReservations only.")]
    [InlineData(LabelKind.Substation, "SUBSTATION No. 1\nCapacity 250 kW. Demand: a few.")]
    [InlineData(LabelKind.Evacuation, "EVACUATION ROUTE: NORTH")]
    [InlineData(LabelKind.Incident, "DAYS WITHOUT INCIDENT: 3")]
    [InlineData(LabelKind.GroundZero, "GROUND ZERO")]
    public void Label_should_read_as_designed(LabelKind kind, string expected)
    {
        Assert.Equal(expected, AnyRimText.Label(kind, 8, 3));
    }

    /// <summary>
    /// The welcome label is the welcome sign at the live population; the
    /// incident board counts from zero; nonsense is rejected.
    /// </summary>
    [Fact(DisplayName = "Labels carry live numbers and reject nonsense")]
    public void Label_should_carry_live_numbers()
    {
        Assert.Equal(AnyRimText.WelcomeSign(5), AnyRimText.Label(LabelKind.Welcome, 5, 0));
        Assert.Equal("DAYS WITHOUT INCIDENT: 0", AnyRimText.Label(LabelKind.Incident, 0, 0));
        Assert.All(Enum.GetValues(typeof(LabelKind)).Cast<LabelKind>(),
            k => Assert.False(string.IsNullOrWhiteSpace(AnyRimText.Label(k, 0, 0))));
        Assert.Throws<ArgumentOutOfRangeException>(() => AnyRimText.Label((LabelKind)99, 8, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => AnyRimText.Label(LabelKind.Incident, 8, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => AnyRimText.Label(LabelKind.Welcome, -1, 0));
    }

    /// <summary>
    /// Numbers are plain digits in every locale, and no sign line is too
    /// long to read above a three-cell sign.
    /// </summary>
    [Fact(DisplayName = "Signs ignore the locale and stay short")]
    public void Signs_should_ignore_locale_and_stay_short()
    {
        CultureInfo before = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("ar-SA");
            Assert.Contains("Pop. 1234,", AnyRimText.WelcomeSign(1234));
            Assert.Contains("revised to 1234.", AnyRimText.CensusMessage(1234));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = before;
        }

        string[] lines = Enum.GetValues(typeof(LabelKind)).Cast<LabelKind>()
            .Concat(new[] { LabelKind.Welcome })
            .SelectMany(k => AnyRimText.Label(k, 0, 999).Split('\n'))
            .ToArray();
        Assert.All(lines, l => Assert.InRange(l.Length, 1, 42));
    }
}
