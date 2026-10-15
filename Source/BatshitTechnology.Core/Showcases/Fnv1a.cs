using System.Text;

namespace BatshitTechnology.Showcases;

/// <summary>
/// The FNV-1a hash (Fowler, Noll and Vo), in its 32-bit and 64-bit forms,
/// over the UTF-8 bytes of text. It is defined here, byte by byte, so the
/// same text hashes the same under .NET in the tests and under RimWorld's
/// Mono runtime, in every process: unlike <c>string.GetHashCode</c>, which
/// a runtime may randomise per process, it can pin a showcase's seed and
/// fingerprint a map.
/// </summary>
public static class Fnv1a
{
    /// <summary>The 32-bit offset basis.</summary>
    public const uint Offset32 = 2166136261u;

    /// <summary>The 32-bit prime.</summary>
    public const uint Prime32 = 16777619u;

    /// <summary>The 64-bit offset basis.</summary>
    public const ulong Offset64 = 14695981039346656037UL;

    /// <summary>The 64-bit prime.</summary>
    public const ulong Prime64 = 1099511628211UL;

    /// <summary>The 32-bit hash of text's UTF-8 bytes.</summary>
    /// <param name="text">The text; null hashes as empty.</param>
    /// <returns>The hash.</returns>
    public static uint Hash32(string? text)
    {
        uint hash = Offset32;
        foreach (byte b in Utf8(text))
        {
            unchecked
            {
                hash = (hash ^ b) * Prime32;
            }
        }

        return hash;
    }

    /// <summary>The 64-bit hash of text's UTF-8 bytes.</summary>
    /// <param name="text">The text; null hashes as empty.</param>
    /// <returns>The hash.</returns>
    public static ulong Hash64(string? text) => Append64(Offset64, text);

    /// <summary>
    /// Continues a 64-bit hash over more text's UTF-8 bytes, as if the
    /// bytes had followed those already hashed.
    /// </summary>
    /// <param name="hash">The hash so far.</param>
    /// <param name="text">The text; null adds nothing.</param>
    /// <returns>The hash.</returns>
    public static ulong Append64(ulong hash, string? text)
    {
        foreach (byte b in Utf8(text))
        {
            hash = AppendByte64(hash, b);
        }

        return hash;
    }

    /// <summary>
    /// Continues a 64-bit hash over a number's eight bytes, least
    /// significant first.
    /// </summary>
    /// <param name="hash">The hash so far.</param>
    /// <param name="value">The number.</param>
    /// <returns>The hash.</returns>
    public static ulong Append64(ulong hash, ulong value)
    {
        for (int i = 0; i < 8; i++)
        {
            hash = AppendByte64(hash, (byte)(value >> (8 * i)));
        }

        return hash;
    }

    /// <summary>Continues a 64-bit hash over one byte.</summary>
    /// <param name="hash">The hash so far.</param>
    /// <param name="b">The byte.</param>
    /// <returns>The hash.</returns>
    public static ulong AppendByte64(ulong hash, byte b)
    {
        unchecked
        {
            return (hash ^ b) * Prime64;
        }
    }

    /// <summary>
    /// Text's UTF-8 bytes. ASCII, which every def name is, takes the
    /// quick path; anything else goes through the encoder, so a lone
    /// surrogate hashes as the encoder's replacement character, the same
    /// on every runtime.
    /// </summary>
    /// <param name="text">The text; null is empty.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Utf8(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return System.Array.Empty<byte>();
        }

        var bytes = new byte[text!.Length];
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c >= 0x80)
            {
                return Encoding.UTF8.GetBytes(text);
            }

            bytes[i] = (byte)c;
        }

        return bytes;
    }
}
