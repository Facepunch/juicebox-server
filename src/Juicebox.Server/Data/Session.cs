namespace Juicebox.Server.Data;

/// <summary>
/// A game session hosted by a game client. Players join it using <see cref="JoinPassword"/>.
/// </summary>
public sealed class Session
{
	public long SessionId { get; set; }

	public required string HostSecretKey { get; set; }

	public required string JoinPassword { get; set; }

	/// <summary>
	/// Locked sessions reject new members. Nothing sets this yet; it's kept from the original service.
	/// </summary>
	public bool IsLocked { get; set; }

	public DateTime Created { get; set; }

	public DateTime? LastPing { get; set; }

	public DateTime? Destroyed { get; set; }

	public List<Member> Members { get; set; } = [];
}
