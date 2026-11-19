using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;

namespace Juicebox.Server.Realtime;

/// <summary>
/// Who a WebSocket connection belongs to: the session's host (the game) or one of its members (a player).
/// </summary>
public sealed record SocketIdentity( long SessionId, string? MemberName )
{
	public bool IsHost => MemberName == null;
}

/// <summary>
/// A redeemed ticket: who is connecting, and the secret key they had when the ticket was issued.
/// The socket endpoint checks the key is still current, so a ticket can't outlive the identity it was issued for.
/// </summary>
public sealed record SocketTicket( SocketIdentity Identity, string SecretKey );

/// <summary>
/// Issues the one-time tickets embedded in the WebSocket URLs returned by the negotiate endpoints.
/// Tickets are random, single use, and expire quickly; clients negotiate again every time they connect.
/// </summary>
public sealed class SocketTickets( IOptions<JuiceboxOptions> options, TimeProvider time )
{
	private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes( 1 );

	private readonly ConcurrentDictionary<string, Entry> _tickets = new( StringComparer.Ordinal );
	private long _nextSweepTicks;

	public string Issue( SocketIdentity identity, string secretKey )
	{
		var now = time.GetUtcNow();
		SweepExpired( now );

		var ticket = Secrets.GenerateSecretKey();
		_tickets[ticket] = new Entry( new SocketTicket( identity, secretKey ), now + options.Value.SocketTicketLifetime );
		return ticket;
	}

	public bool TryRedeem( string? ticket, [NotNullWhen( true )] out SocketTicket? redeemed )
	{
		redeemed = null;
		if ( string.IsNullOrEmpty( ticket ) || !_tickets.TryRemove( ticket, out var entry ) )
		{
			return false;
		}

		if ( entry.Expires < time.GetUtcNow() )
		{
			return false;
		}

		redeemed = entry.Ticket;
		return true;
	}

	/// <summary>
	/// Builds the WebSocket URL a client should connect to.
	/// </summary>
	public string BuildEndpoint( HttpRequest request, SocketIdentity identity, string secretKey )
	{
		var ticket = Issue( identity, secretKey );

		var publicUrl = options.Value.PublicUrl;
		var baseUrl = !string.IsNullOrWhiteSpace( publicUrl )
			? publicUrl.TrimEnd( '/' )
			: $"{request.Scheme}://{request.Host}{request.PathBase}";

		if ( baseUrl.StartsWith( "https://", StringComparison.OrdinalIgnoreCase ) )
		{
			baseUrl = "wss://" + baseUrl["https://".Length..];
		}
		else if ( baseUrl.StartsWith( "http://", StringComparison.OrdinalIgnoreCase ) )
		{
			baseUrl = "ws://" + baseUrl["http://".Length..];
		}

		return $"{baseUrl}{SocketEndpoint.Path}?ticket={ticket}";
	}

	private void SweepExpired( DateTimeOffset now )
	{
		var next = Interlocked.Read( ref _nextSweepTicks );
		if ( now.UtcTicks < next ||
		     Interlocked.CompareExchange( ref _nextSweepTicks, (now + SweepInterval).UtcTicks, next ) != next )
		{
			return;
		}

		foreach ( var (key, entry) in _tickets )
		{
			if ( entry.Expires < now )
			{
				_tickets.TryRemove( key, out _ );
			}
		}
	}

	private sealed record Entry( SocketTicket Ticket, DateTimeOffset Expires );
}
