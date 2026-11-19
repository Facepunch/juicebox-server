namespace Juicebox.Server.Data;

/// <summary>
/// A player who joined a session from the website. Names are unique (case-insensitive) within a session.
/// </summary>
public sealed class Member
{
	public const int MaxNameLength = 64;

	public long SessionId { get; set; }

	public required string MemberName { get; set; }

	public required string MemberSecretKey { get; set; }

	public DateTime Created { get; set; }

	public DateTime? LastPing { get; set; }

	public DateTime? Destroyed { get; set; }

	public Session Session { get; set; } = null!;
}
