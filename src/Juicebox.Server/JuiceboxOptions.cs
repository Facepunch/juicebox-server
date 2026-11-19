namespace Juicebox.Server;

/// <summary>
/// Settings bound from the "Juicebox" configuration section.
/// </summary>
public sealed class JuiceboxOptions
{
	public const string SectionName = "Juicebox";

	/// <summary>
	/// Number of characters in generated join codes. Must be between 6 and 8.
	/// </summary>
	public int JoinCodeLength { get; set; } = 6;

	/// <summary>
	/// Sessions are destroyed when the host hasn't pinged for this long.
	/// </summary>
	public TimeSpan SessionTimeout { get; set; } = TimeSpan.FromMinutes( 5 );

	/// <summary>
	/// Members are removed when they haven't pinged for this long.
	/// </summary>
	public TimeSpan MemberTimeout { get; set; } = TimeSpan.FromMinutes( 5 );

	/// <summary>
	/// Destroyed sessions and members are deleted from the database after this long.
	/// </summary>
	public TimeSpan RetainDestroyedFor { get; set; } = TimeSpan.FromDays( 7 );

	/// <summary>
	/// How often the expiry service runs.
	/// </summary>
	public TimeSpan ExpiryInterval { get; set; } = TimeSpan.FromMinutes( 1 );

	/// <summary>
	/// How long a WebSocket URL returned by the negotiate endpoints stays valid. Each URL can only be used once.
	/// </summary>
	public TimeSpan SocketTicketLifetime { get; set; } = TimeSpan.FromMinutes( 1 );

	/// <summary>
	/// Largest WebSocket message accepted, in bytes. Drawings are sent as PNG data URLs so this needs some room.
	/// </summary>
	public int MaxMessageBytes { get; set; } = 1024 * 1024;

	/// <summary>
	/// Optional public base URL (e.g. https://juicebox.example.com) used to build WebSocket URLs.
	/// When empty, the URL is built from the incoming request.
	/// </summary>
	public string? PublicUrl { get; set; }

	/// <summary>
	/// Origins allowed to call the API from a browser, for when the website is hosted separately.
	/// </summary>
	public string[] CorsOrigins { get; set; } = [];

	/// <summary>
	/// Reverse proxies (IP addresses or CIDR ranges) whose X-Forwarded-For and X-Forwarded-Proto headers are trusted.
	/// Loopback is always trusted. Client IPs matter for rate limiting, and the scheme for building wss:// URLs.
	/// </summary>
	public string[] TrustedProxies { get; set; } = [];

	/// <summary>
	/// Requests per minute per IP address for joining sessions.
	/// </summary>
	public int JoinRateLimitPerMinute { get; set; } = 30;

	/// <summary>
	/// Requests per minute per IP address for creating sessions.
	/// </summary>
	public int CreateRateLimitPerMinute { get; set; } = 10;
}
