using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Spikes.WebViewSearch;

/// <summary>
/// Minimal HTTP server on 127.0.0.1 that records every request it receives.
/// Ground truth for the private-network tests: a blocked request never reaches it.
/// </summary>
internal sealed class LoopbackServer : IDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource cts = new();

    public ConcurrentQueue<string> Hits { get; } = new();
    public int Port { get; }

    public LoopbackServer()
    {
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        _ = AcceptLoop();
    }

    private async Task AcceptLoop()
    {
        while (!cts.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            _ = Serve(client);
        }
    }

    private async Task Serve(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 4096, leaveOpen: true);
                var requestLine = await reader.ReadLineAsync() ?? "";
                var parts = requestLine.Split(' ');
                if (parts.Length >= 2) Hits.Enqueue($"{parts[0]} {parts[1]}");
                var body = Encoding.UTF8.GetBytes("<html><body>LOOPBACK REACHED</body></html>");
                var head = "HTTP/1.1 200 OK\r\nContent-Type: text/html\r\nAccess-Control-Allow-Origin: *\r\n"
                    + $"Content-Length: {body.Length}\r\nConnection: close\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(head));
                await stream.WriteAsync(body);
            }
            catch (IOException)
            {
                // client went away
            }
        }
    }

    public string[] HitsWithPrefix(string prefix) =>
        Hits.Where(h => h.Split(' ')[1].TrimStart('/').StartsWith(prefix, StringComparison.Ordinal)).ToArray();

    public void Dispose()
    {
        cts.Cancel();
        listener.Stop();
    }
}
