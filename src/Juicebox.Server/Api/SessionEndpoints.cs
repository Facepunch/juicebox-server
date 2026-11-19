using Juicebox.Server.Data;
using Juicebox.Server.Realtime;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Juicebox.Server.Api;

/// <summary>
/// Endpoints used by the game (the session host).
/// </summary>
public static class SessionEndpoints
{
	public static void MapSessionEndpoints( this IEndpointRouteBuilder app )
	{
		var group = app.MapGroup( "/api/sessions" );
		group.MapPost( "/create", Create ).RequireRateLimiting( RateLimits.Create );
		group.MapPost( "/negotiate", Negotiate );
		group.MapPost( "/ping", Ping );
		group.MapPost( "/destroy", Destroy );
	}

	// The request body is ignored. Older game clients send a PublicKey which isn't needed anymore.
	private static async Task<IResult> Create( JuiceboxDbContext db, IOptions<JuiceboxOptions> options )
	{
		const int maxAttempts = 10;
		for ( var attempt = 1; ; attempt++ )
		{
			var session = new Session
			{
				HostSecretKey = Secrets.GenerateSecretKey(),
				JoinPassword = Secrets.GenerateJoinCode( options.Value.JoinCodeLength ),
				Created = DateTime.UtcNow
			};

			db.Sessions.Add( session );
			try
			{
				await db.SaveChangesAsync();
			}
			catch ( DbUpdateException e ) when ( e.IsUniqueConstraintViolation() && attempt < maxAttempts )
			{
				// Join code collided with a live session, try another one.
				db.Entry( session ).State = EntityState.Detached;
				continue;
			}

			return Results.Ok( new SessionCreateResponse( session.SessionId, session.HostSecretKey, session.JoinPassword ) );
		}
	}

	private static async Task<IResult> Negotiate( SessionActionRequest request, JuiceboxDbContext db,
		SocketTickets tickets, HttpRequest httpRequest )
	{
		if ( string.IsNullOrWhiteSpace( request.HostSecretKey ) )
		{
			return ApiResults.MissingFields();
		}

		var session = await db.FindLiveSessionAsync( request.SessionId, request.HostSecretKey );
		if ( session == null )
		{
			return Results.NotFound();
		}

		var endpoint = tickets.BuildEndpoint( httpRequest, new SocketIdentity( session.SessionId, null ),
			session.HostSecretKey );
		return Results.Ok( new NegotiateResponse( endpoint ) );
	}

	private static async Task<IResult> Ping( SessionActionRequest request, JuiceboxDbContext db )
	{
		if ( string.IsNullOrWhiteSpace( request.HostSecretKey ) )
		{
			return ApiResults.MissingFields();
		}

		var session = await db.FindLiveSessionAsync( request.SessionId, request.HostSecretKey );
		if ( session == null )
		{
			return Results.NotFound();
		}

		session.LastPing = DateTime.UtcNow;
		await db.SaveChangesAsync();

		var memberNames = await db.Members
			.Where( m => m.SessionId == session.SessionId && m.Destroyed == null )
			.OrderBy( m => m.Created )
			.Select( m => m.MemberName )
			.ToListAsync();

		return Results.Ok( new SessionPingResponse( memberNames ) );
	}

	private static async Task<IResult> Destroy( SessionActionRequest request, JuiceboxDbContext db, SessionRelay relay )
	{
		if ( string.IsNullOrWhiteSpace( request.HostSecretKey ) )
		{
			return ApiResults.MissingFields();
		}

		var session = await db.FindLiveSessionAsync( request.SessionId, request.HostSecretKey );
		if ( session == null )
		{
			return Results.NotFound();
		}

		session.Destroyed = DateTime.UtcNow;
		await db.SaveChangesAsync();

		relay.CloseSession( session.SessionId, "Session Closed" );

		return Results.NoContent();
	}

	internal static bool IsUniqueConstraintViolation( this DbUpdateException e ) =>
		e.InnerException is SqliteException { SqliteErrorCode: 19 }; // SQLITE_CONSTRAINT
}

public sealed record SessionActionRequest( long SessionId, string? HostSecretKey );

public sealed record SessionCreateResponse( long SessionId, string HostSecretKey, string JoinPassword );

public sealed record SessionPingResponse( List<string> MemberNames );

public sealed record NegotiateResponse( string Endpoint );
