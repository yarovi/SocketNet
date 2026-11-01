using System.Buffers.Binary;
using System.Text;

namespace XmlTcpAccounts.Server.Transport;

public static class TcpMessageFraming
{
    private const int HeaderSize = 4;
    private const int MaxMessageSize = 1024 * 1024;

    public static async Task<string?> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var header = new byte[HeaderSize];

        var firstByte = await stream.ReadAsync(
            header.AsMemory(0, 1),
            cancellationToken);

        if (firstByte == 0)
            return null;

        await stream.ReadExactlyAsync(
            header.AsMemory(1, HeaderSize - 1),
            cancellationToken);

        var length = BinaryPrimitives.ReadInt32BigEndian(header);

        if (length <= 0 || length > MaxMessageSize)
            throw new InvalidDataException(
                "Invalid TCP message length.");

        var payload = new byte[length];

        await stream.ReadExactlyAsync(
            payload,
            cancellationToken);

        return new UTF8Encoding(false, true)
            .GetString(payload);
    }

    public static async Task WriteAsync(
        Stream stream,
        string message,
        CancellationToken cancellationToken)
    {
        var payload = Encoding.UTF8.GetBytes(message);

        if (payload.Length == 0 ||
            payload.Length > MaxMessageSize)
        {
            throw new InvalidDataException(
                "Invalid TCP message length.");
        }

        var header = new byte[HeaderSize];

        BinaryPrimitives.WriteInt32BigEndian(
            header,
            payload.Length);

        await stream.WriteAsync(
            header,
            cancellationToken);

        await stream.WriteAsync(
            payload,
            cancellationToken);

        await stream.FlushAsync(cancellationToken);
    }
}