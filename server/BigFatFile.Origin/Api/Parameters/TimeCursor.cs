using System.Buffers.Binary;
using System.Buffers.Text;

namespace BigFatFile.Origin.Api.Parameters;

/// <summary>The cursor of a listing that is ordered by time: the time and the ID of the page's last item.</summary>
internal static class TimeCursor
{
    private const int Length = sizeof(long) + 16;

    public static string Encode(DateTimeOffset time, Guid id)
    {
        Span<byte> bytes = stackalloc byte[Length];
        BinaryPrimitives.WriteInt64BigEndian(bytes, time.UtcTicks);
        id.TryWriteBytes(bytes[sizeof(long)..], bigEndian: true, out _);
        return Base64Url.EncodeToString(bytes);
    }

    public static bool TryDecode(string cursor, out (DateTimeOffset Time, Guid Id) position)
    {
        position = default;
        Span<byte> bytes = stackalloc byte[Length];
        if (!Base64Url.TryDecodeFromChars(cursor, bytes, out var written) || written != Length)
        {
            return false;
        }

        var ticks = BinaryPrimitives.ReadInt64BigEndian(bytes);
        if (ticks < DateTimeOffset.MinValue.UtcTicks || ticks > DateTimeOffset.MaxValue.UtcTicks)
        {
            return false;
        }

        position = (new DateTimeOffset(ticks, TimeSpan.Zero), new Guid(bytes[sizeof(long)..], bigEndian: true));
        return true;
    }
}
