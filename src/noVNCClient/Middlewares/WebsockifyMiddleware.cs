using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace noVNCClient.Middlewares;

/// <summary>
/// Middleware for handling WebSocket connections and proxying data between WebSocket and TCP.
/// </summary>
public sealed class WebsockifyMiddleware
{
    /// <summary>
    /// The recommended buffer size for reading and writing data.
    /// </summary>
    public const int RecommendedBufferSize = 1024 * 64;

    private readonly RequestDelegate _next;
    private readonly string _hostname;
    private readonly int _tcpPort;
    private readonly int _bufferSizeInBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebsockifyMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="hostname">The hostname of the TCP server.</param>
    /// <param name="tcpPort">The TCP port number.</param>
    /// <param name="bufferSizeInBytes">The buffer size in bytes.</param>
    public WebsockifyMiddleware(
        RequestDelegate next,
        string hostname,
        int tcpPort,
        int bufferSizeInBytes = RecommendedBufferSize)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _hostname = hostname ?? throw new ArgumentNullException(nameof(hostname));

        if (tcpPort < IPEndPoint.MinPort || tcpPort > IPEndPoint.MaxPort)
            throw new ArgumentOutOfRangeException(nameof(tcpPort), tcpPort, "The TCP port number is invalid.");

        _tcpPort = tcpPort;
        _bufferSizeInBytes = bufferSizeInBytes < 1024 ? RecommendedBufferSize : bufferSizeInBytes;
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await _next(context);
            return;
        }

        var logger = context.RequestServices.GetRequiredService<ILogger<WebsockifyMiddleware>>();
        var appLifetime = context.RequestServices.GetRequiredService<IHostApplicationLifetime>();

        // 使用链接的 CancellationTokenSource 来在任一方完成后取消所有操作
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, appLifetime.ApplicationStopping);

        WebSocket? webSocket = null;
        Socket? socket = null;

        try
        {
            webSocket = await context.WebSockets.AcceptWebSocketAsync();

            socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true, // 禁用 Nagle 算法以降低延迟
                SendBufferSize = _bufferSizeInBytes,
                ReceiveBufferSize = _bufferSizeInBytes
            };

            await socket.ConnectAsync(_hostname, _tcpPort, cts.Token);

            // 启动双向数据传输任务
            var receiveTask = PumpTcpToWebSocketAsync(socket, webSocket, _bufferSizeInBytes, cts.Token);
            var sendTask = PumpWebSocketToTcpAsync(webSocket, socket, _bufferSizeInBytes, cts.Token);

            // 等待任意一方完成（关闭或出错）
            var completedTask = await Task.WhenAny(receiveTask, sendTask);

            // 如果任务因异常失败，记录错误
            if (completedTask.IsFaulted)
            {
                var ex = completedTask.Exception?.InnerException;
                if (ex is not null and not OperationCanceledException)
                {
                    logger.LogError(ex, "数据传输过程中发生错误");
                }
            }

            // 取消另一个正在运行的任务
            cts.Cancel();

            // 等待另一个任务优雅退出
            try
            {
                await Task.WhenAll(receiveTask, sendTask);
            }
            catch (OperationCanceledException) { }
            catch (Exception) { /* 忽略已处理或取消的异常 */ }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WebSocket 代理连接异常");
        }
        finally
        {
            socket?.Dispose();

            if (webSocket != null)
            {
                try
                {
                    var state = webSocket.State;
                    if (state == WebSocketState.Open || state == WebSocketState.CloseReceived)
                    {
                        // 尝试优雅关闭 WebSocket
                        using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await webSocket.CloseAsync(
                            webSocket.CloseStatus ?? WebSocketCloseStatus.NormalClosure,
                            webSocket.CloseStatusDescription ?? "连接关闭",
                            closeCts.Token);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "关闭 WebSocket 时发生异常");
                }
            }
        }
    }

    private static async Task PumpWebSocketToTcpAsync(WebSocket webSocket, Socket socket, int bufferSize, CancellationToken token)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            while (!token.IsCancellationRequested)
            {
                // 使用 ValueWebSocketReceiveResult 避免分配
                var result = await webSocket.ReceiveAsync(new Memory<byte>(buffer), token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                // 将数据发送到 TCP
                var data = new ReadOnlyMemory<byte>(buffer, 0, result.Count);
                while (data.Length > 0)
                {
                    var sent = await socket.SendAsync(data, SocketFlags.None, token);
                    if (sent == 0) break;
                    data = data.Slice(sent);
                }
            }
        }
        catch (OperationCanceledException) { /* 正常退出 */ }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task PumpTcpToWebSocketAsync(Socket socket, WebSocket webSocket, int bufferSize, CancellationToken token)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            while (!token.IsCancellationRequested)
            {
                // 直接使用 Socket 接收数据，避免 NetworkStream 开销
                var bytesRead = await socket.ReceiveAsync(new Memory<byte>(buffer), SocketFlags.None, token);

                if (bytesRead == 0) break; // TCP FIN

                await webSocket.SendAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), WebSocketMessageType.Binary, endOfMessage: true, token);
            }
        }
        catch (OperationCanceledException) { /* 正常退出 */ }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
