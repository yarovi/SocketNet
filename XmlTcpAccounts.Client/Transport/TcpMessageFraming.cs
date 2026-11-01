using System.Buffers.Binary;

namespace XmlTcpAccounts.Client.Transport;

using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;
using XmlTcpAccounts.Server.Transport;


/*
public static class TcpMessageFraming
{
    private const int HeaderSize = 4;
    private const int MaxMessageSize = 1024 * 1024;

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
                "Invalid message length.");
        }

        var header = new byte[HeaderSize];

        BinaryPrimitives.WriteInt32BigEndian(
            header,
            payload.Length);

        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async Task<string> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var header = new byte[HeaderSize];

        await stream.ReadExactlyAsync(
            header,
            cancellationToken);

        var length = BinaryPrimitives.ReadInt32BigEndian(header);

        if (length <= 0 || length > MaxMessageSize)
        {
            throw new InvalidDataException(
                "Invalid message length.");
        }

        var payload = new byte[length];

        await stream.ReadExactlyAsync(
            payload,
            cancellationToken);

        return new UTF8Encoding(false, true)
            .GetString(payload);
    }
}
*/

//Test for validation case

public static class TcpMessageFraming
{
    private const int HeaderSize = 4;
    private const int MaxMessageSize = 1024 * 1024;

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
                "Invalid message length.");
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

    public static async Task<string> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var header = new byte[HeaderSize];

        await stream.ReadExactlyAsync(
            header,
            cancellationToken);

        var length = BinaryPrimitives.ReadInt32BigEndian(header);

        if (length <= 0 || length > MaxMessageSize)
        {
            throw new InvalidDataException(
                "Invalid message length.");
        }

        var payload = new byte[length];

        await stream.ReadExactlyAsync(
            payload,
            cancellationToken);

        return new UTF8Encoding(false, true)
            .GetString(payload);
    }
}