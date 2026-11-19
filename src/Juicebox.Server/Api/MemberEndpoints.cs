using System.Text;
using Juicebox.Server.Data;
using Juicebox.Server.Realtime;
using Microsoft.EntityFrameworkCore;

namespace Juicebox.Server.Api;

/// <summary>
/// Endpoints used by the website (session members).
/// </summary>
public static class MemberEndpoints
{
	public static void MapMemberEndpoints( this IEndpointRouteBuilder app )
	{
		var group = app.MapGroup( "/api/members" );
		group.MapPost( "/join", Join ).RequireRateLimiting( RateLimits.Join );
		group.MapPost( "/negotiate", Negotiate );
		group.MapPost( "/ping", Ping );
	}

	private static async Task<IResult> Join( MemberJoinRequest request, JuiceboxDbContext db )
	{
		var joinPassword = request.JoinPassword?.Trim().ToUpperInvariant();
		var memberName = request.MemberName?.Trim().Normalize( NormalizationForm.FormC );
		if ( string.IsNullOrEmpty( joinPassword ) || string.IsNullOrEmpty( memberName ) )
		{
			return ApiResults.MissingFields();
		}

		if ( memberName.Length > Member.MaxNameLength )
		{
			return Results.BadRequest( $"MemberName must be at most {Member.MaxNameLength} characters" );
		}

		var session = await db.Sessions.Live().SingleOrDefaultAsync( s => s.JoinPassword == joinPassword );
		if ( session == null )
		{
			return Results.NotFound();
		}

		if ( session.IsLocked )
		{
			return Results.StatusCode( StatusCodes.Status423Locked );
		}

		var now = DateTime.UtcNow;
		var member = await db.Members.SingleOrDefaultAsync( m =>
			m.SessionId == session.SessionId && m.MemberName == memberName );
		if ( member is { Destroyed: null } )
		{
			return Results.Conflict();
		}

		if ( member != null )
		{
			// This name timed out earlier. Let it be reused so a player whose phone went to sleep
			// can rejoin as the same player.
			member.MemberSecretKey = Secrets.GenerateSecretKey();
			member.Created = now;
			member.LastPing = null;
			member.Destroyed = null;
		}
		else
		{
			member = new Member
			{
				SessionId = session.SessionId,
				MemberName = memberName,
				MemberSecretKey = Secrets.GenerateSecretKey(),
				Created = now
			};
			db.Members.Add( member );
		}

		try
		{
			await db.SaveChangesAsync();
		}
		catch ( DbUpdateException e ) when ( e.IsUniqueConstraintViolation() )
		{
			return Results.Conflict(); // someone else took the name at the same time
		}

		return Results.Ok( new MemberJoinResponse( member.SessionId, member.MemberName, member.MemberSecretKey ) );
	}

	private static async Task<IResult> Negotiate( MemberActionRequest request, JuiceboxDbContext db,
		SocketTickets tickets, HttpRequest httpRequest )
	{
		if ( string.IsNullOrWhiteSpace( request.MemberName ) || string.IsNullOrWhiteSpace( request.MemberSecretKey ) )
		{
			return ApiResults.MissingFields();
		}

		var member = await db.FindLiveMemberAsync( request.SessionId, request.MemberName, request.MemberSecretKey );
		if ( member == null )
		{
			return Results.NotFound();
		}

		var endpoint = tickets.BuildEndpoint( httpRequest, new SocketIdentity( member.SessionId, member.MemberName ),
			member.MemberSecretKey );
		return Results.Ok( new NegotiateResponse( endpoint ) );
	}

	private static async Task<IResult> Ping( MemberActionRequest request, JuiceboxDbContext db )
	{
		if ( string.IsNullOrWhiteSpace( request.MemberName ) || string.IsNullOrWhiteSpace( request.MemberSecretKey ) )
		{
			return ApiResults.MissingFields();
		}

		var member = await db.FindLiveMemberAsync( request.SessionId, request.MemberName, request.MemberSecretKey );
		if ( member == null )
		{
			return Results.NotFound();
		}

		member.LastPing = DateTime.UtcNow;
		await db.SaveChangesAsync();

		return Results.NoContent();
	}
}

// Older website builds also send a ProjectId, which is ignored.
public sealed record MemberJoinRequest( string? JoinPassword, string? MemberName );

public sealed record MemberJoinResponse( long SessionId, string MemberName, string MemberSecretKey );

public sealed record MemberActionRequest( long SessionId, string? MemberName, string? MemberSecretKey );
