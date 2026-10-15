using System;
using System.Globalization;
using System.Text.Json;
using BatshitTechnology.Core;
using Xunit;

namespace BatshitTechnology.Tests;

/// <summary>
/// Verifies the one-line JSON writer behind the tour verifier's SUMMARY
/// line: strict JSON that PowerShell and any other reader can parse.
/// </summary>
public sealed class JsonLineTests
{
    /// <summary>
    /// Members keep the order they were added in; adding a name again
    /// replaces its value in place; an empty object is "{}".
    /// </summary>
    [Fact(DisplayName = "JSON members keep their order; a repeat replaces in place")]
    public void Members_should_keep_order_and_replace_in_place()
    {
        Assert.Equal("{}", new JsonLine().ToString());
        var json = new JsonLine { { "b", 1 }, { "a", 2 }, { "c", 3 } };
        json.Add("a", "two");
        Assert.Equal("{\"b\":1,\"a\":\"two\",\"c\":3}", json.ToString());
        Assert.Equal(3, json.Count);
        Assert.Equal("two", json["a"]);
        Assert.Null(json["missing"]);
        Assert.Throws<ArgumentNullException>(() => json.Add(null!, 1));
    }

    /// <summary>
    /// Strings escape quotes, backslashes and control characters, keep
    /// everything else, and read back exactly through a real parser.
    /// </summary>
    [Fact(DisplayName = "JSON strings escape what they must and read back exactly")]
    public void Strings_should_escape_and_round_trip()
    {
        string nasty = "say \"hi\"\\ tab\t line\n cr\r bell\u0001 café ☃ \u2028";
        var json = new JsonLine { { "s", nasty }, { "c", 'q' } };
        string text = json.ToString();
        Assert.DoesNotContain("\n", text, StringComparison.Ordinal);
        Assert.Contains("\\u0001", text, StringComparison.Ordinal);
        using JsonDocument doc = JsonDocument.Parse(text);
        Assert.Equal(nasty, doc.RootElement.GetProperty("s").GetString());
        Assert.Equal("q", doc.RootElement.GetProperty("c").GetString());
    }

    /// <summary>
    /// Numbers use the invariant culture whatever the current one (a
    /// comma-decimal culture would otherwise break the JSON), read back
    /// exactly, and NaN and the infinities, which JSON cannot hold, are
    /// written as null.
    /// </summary>
    [Fact(DisplayName = "JSON numbers are invariant, exact, and never NaN")]
    public void Numbers_should_be_invariant_and_finite()
    {
        CultureInfo was = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var json = new JsonLine
            {
                { "half", 1.5 }, { "f", 0.1f }, { "big", long.MaxValue },
                { "u", ulong.MaxValue }, { "neg", -7 }, { "m", 2.25m },
                { "nan", double.NaN }, { "inf", float.PositiveInfinity },
                { "third", 1.0 / 3.0 },
            };
            string text = json.ToString();
            Assert.Contains("\"half\":1.5", text, StringComparison.Ordinal);
            Assert.Contains("\"nan\":null", text, StringComparison.Ordinal);
            Assert.Contains("\"inf\":null", text, StringComparison.Ordinal);
            using JsonDocument doc = JsonDocument.Parse(text);
            JsonElement root = doc.RootElement;
            Assert.Equal(long.MaxValue, root.GetProperty("big").GetInt64());
            Assert.Equal(ulong.MaxValue, root.GetProperty("u").GetUInt64());
            Assert.Equal(1.0 / 3.0, root.GetProperty("third").GetDouble());
            Assert.Equal(0.1f, (float)root.GetProperty("f").GetDouble());
            Assert.Equal(2.25m, root.GetProperty("m").GetDecimal());
        }
        finally
        {
            CultureInfo.CurrentCulture = was;
        }
    }

    /// <summary>
    /// Objects nest, sequences become arrays, and null and bools are
    /// JSON's own; anything else is written as its invariant text.
    /// </summary>
    [Fact(DisplayName = "JSON nests objects and arrays")]
    public void Values_should_nest()
    {
        var scene = new JsonLine { { "outcome", "completed" }, { "ticks", 2400 } };
        var json = new JsonLine
        {
            { "scenes", new JsonLine { { "home", scene } } },
            { "volleys", new[] { 256, 16 } },
            { "kinds", new System.Collections.Generic.List<string> { "Boomalope" } },
            { "empty", Array.Empty<int>() },
            { "none", null },
            { "yes", true },
            { "no", false },
            { "day", DayOfWeek.Friday },
        };
        Assert.Equal(
            "{\"scenes\":{\"home\":{\"outcome\":\"completed\",\"ticks\":2400}},"
                + "\"volleys\":[256,16],\"kinds\":[\"Boomalope\"],\"empty\":[],"
                + "\"none\":null,\"yes\":true,\"no\":false,\"day\":\"Friday\"}",
            json.ToString());
        using JsonDocument doc = JsonDocument.Parse(json.ToString());
        Assert.Equal(2400, doc.RootElement.GetProperty("scenes").GetProperty("home")
            .GetProperty("ticks").GetInt32());
    }
}
