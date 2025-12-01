namespace noVNCClient.Models;

/// <summary>
/// Configuration options for Websockify middleware.
/// </summary>
public class WebsockifyOptions
{
    /// <summary>
    /// The request path to match.
    /// </summary>
    public string Path { get; set; } = "/websockify";

    /// <summary>
    /// The hostname of the target server.
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// The TCP port of the target server.
    /// </summary>
    public int Port { get; set; } = 5900;
}

