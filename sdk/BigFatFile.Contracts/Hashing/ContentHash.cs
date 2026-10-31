using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace BigFatFile.Contracts.Hashing;

/// <summary>
/// A BLAKE3 hash of some bytes, written as <c>blake3:</c> followed by 64 lowercase hex digits.
/// </summary>
[JsonConverter(typeof(ContentHashJsonConverter))]
public readonly struct ContentHash : IEquatable<ContentHash>
{
    public const int ByteLength = 32;
    public const string Prefix = "blake3:";
    public const int TextLength = 71;

    private readonly HashBytes bytes;

    private ContentHash(ReadOnlySpan<byte> source)
    {
        source.CopyTo(bytes);
    }

    public static ContentHash Compute(ReadOnlySpan<byte> data)
    {
        Span<byte> output = stackalloc byte[ByteLength];
        Blake3.Hasher.Hash(data, output);
        return new ContentHash(output);
    }

    public static ContentHash FromBytes(ReadOnlySpan<byte> source)
    {
        if (source.Length != ByteLength)
        {
            throw new ArgumentException($"A content hash is {ByteLength} bytes.", nameof(source));
        }

        return new ContentHash(source);
    }

    public static ContentHash Parse(string text) =>
        TryParse(text, out var hash) ? hash : throw new FormatException($"'{text}' is not a content hash.");

    public static bool TryParse([NotNullWhen(true)] string? text, out ContentHash hash) =>
        TryParse(text.AsSpan(), out hash);

    public static bool TryParse(ReadOnlySpan<char> text, out ContentHash hash)
    {
        hash = default;
        if (text.Length != TextLength || !text.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var hex = text[Prefix.Length..];
        foreach (var c in hex)
        {
            // Only lowercase, so that every hash has exactly one text form.
            if (!char.IsAsciiDigit(c) && c is not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }

        Span<byte> buffer = stackalloc byte[ByteLength];
        Convert.FromHexString(hex, buffer, out _, out _);
        hash = new ContentHash(buffer);
        return true;
    }

    public void CopyTo(Span<byte> destination) => ((ReadOnlySpan<byte>)bytes).CopyTo(destination);

    public bool Equals(ContentHash other) => ((ReadOnlySpan<byte>)bytes).SequenceEqual(other.bytes);

    public override bool Equals(object? obj) => obj is ContentHash other && Equals(other);

    public override int GetHashCode() => BinaryPrimitives.ReadInt32LittleEndian(bytes);

    public override string ToString() => Prefix + Convert.ToHexStringLower(bytes);

    public static bool operator ==(ContentHash left, ContentHash right) => left.Equals(right);

    public static bool operator !=(ContentHash left, ContentHash right) => !left.Equals(right);

    [InlineArray(ByteLength)]
    private struct HashBytes
    {
        private byte first;
    }
}
