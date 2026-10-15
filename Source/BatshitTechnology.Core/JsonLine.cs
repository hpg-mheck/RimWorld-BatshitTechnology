using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BatshitTechnology.Core;

/// <summary>
/// A JSON object whose members keep the order they were added in, written
/// as one line of JSON. RimWorld's runtime ships no JSON library, and the
/// tour verifier's SUMMARY line must parse in PowerShell, so this writes
/// strict JSON (RFC 8259): strings escaped, numbers in the invariant
/// culture, and anything that is not a finite number written as null.
/// </summary>
public sealed class JsonLine : IEnumerable<KeyValuePair<string, object?>>
{
    /// <summary>The members, in order.</summary>
    private readonly List<KeyValuePair<string, object?>> members =
        new List<KeyValuePair<string, object?>>();

    /// <summary>Gets how many members the object has.</summary>
    public int Count => members.Count;

    /// <summary>
    /// Adds a member, or replaces the value of one already named, keeping
    /// its place. A value may be null, a bool, a string, a number, another
    /// <see cref="JsonLine"/>, or any other sequence (an array); anything
    /// else is written as its invariant text.
    /// </summary>
    /// <param name="key">The member's name.</param>
    /// <param name="value">Its value.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="key"/> is null.
    /// </exception>
    public void Add(string key, object? value)
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        for (int i = 0; i < members.Count; i++)
        {
            if (members[i].Key == key)
            {
                members[i] = new KeyValuePair<string, object?>(key, value);
                return;
            }
        }

        members.Add(new KeyValuePair<string, object?>(key, value));
    }

    /// <summary>Gets a member's value, or null if there is none.</summary>
    /// <param name="key">The member's name.</param>
    /// <returns>The value.</returns>
    public object? this[string key]
    {
        get
        {
            foreach (KeyValuePair<string, object?> m in members)
            {
                if (m.Key == key)
                {
                    return m.Value;
                }
            }

            return null;
        }
    }

    /// <summary>The object as one line of JSON.</summary>
    /// <returns>The JSON text.</returns>
    public override string ToString()
    {
        var text = new StringBuilder();
        Write(text, this);
        return text.ToString();
    }

    /// <summary>Enumerates the members in order.</summary>
    /// <returns>The enumerator.</returns>
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() =>
        members.GetEnumerator();

    /// <summary>Enumerates the members in order.</summary>
    /// <returns>The enumerator.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Writes any value as JSON.</summary>
    /// <param name="text">Where to write.</param>
    /// <param name="value">The value.</param>
    public static void Write(StringBuilder text, object? value)
    {
        switch (value)
        {
            case null:
                text.Append("null");
                break;
            case bool b:
                text.Append(b ? "true" : "false");
                break;
            case string s:
                WriteString(text, s);
                break;
            case char c:
                WriteString(text, c.ToString());
                break;
            case JsonLine obj:
                text.Append('{');
                bool first = true;
                foreach (KeyValuePair<string, object?> m in obj.members)
                {
                    if (!first)
                    {
                        text.Append(',');
                    }

                    first = false;
                    WriteString(text, m.Key);
                    text.Append(':');
                    Write(text, m.Value);
                }

                text.Append('}');
                break;
            case float f:
                WriteDouble(text, f);
                break;
            case double d:
                WriteDouble(text, d);
                break;
            case decimal m:
                text.Append(m.ToString(CultureInfo.InvariantCulture));
                break;
            case sbyte _:
            case byte _:
            case short _:
            case ushort _:
            case int _:
            case uint _:
            case long _:
            case ulong _:
                text.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
            case IEnumerable sequence:
                text.Append('[');
                bool firstItem = true;
                foreach (object? item in sequence)
                {
                    if (!firstItem)
                    {
                        text.Append(',');
                    }

                    firstItem = false;
                    Write(text, item);
                }

                text.Append(']');
                break;
            default:
                WriteString(
                    text, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                break;
        }
    }

    /// <summary>
    /// Writes a number: the shortest text that reads back the same, or
    /// null for NaN and the infinities, which JSON cannot hold.
    /// </summary>
    /// <param name="text">Where to write.</param>
    /// <param name="d">The number.</param>
    private static void WriteDouble(StringBuilder text, double d)
    {
        if (double.IsNaN(d) || double.IsInfinity(d))
        {
            text.Append("null");
            return;
        }

        text.Append(d.ToString("R", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Writes a string in quotes: quote, backslash and control characters
    /// escaped, everything else as it is.
    /// </summary>
    /// <param name="text">Where to write.</param>
    /// <param name="s">The string.</param>
    private static void WriteString(StringBuilder text, string s)
    {
        text.Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '"':
                    text.Append("\\\"");
                    break;
                case '\\':
                    text.Append("\\\\");
                    break;
                case '\n':
                    text.Append("\\n");
                    break;
                case '\r':
                    text.Append("\\r");
                    break;
                case '\t':
                    text.Append("\\t");
                    break;
                default:
                    if (c < 0x20 || c == '\u2028' || c == '\u2029')
                    {
                        text.Append("\\u").Append(((int)c).ToString(
                            "x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        text.Append(c);
                    }

                    break;
            }
        }

        text.Append('"');
    }
}
