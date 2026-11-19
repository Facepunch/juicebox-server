using Juicebox.Server.Data;
using Microsoft.Extensions.Options;

namespace Juicebox.Server.Realtime;

public static class SocketEndpoint
{
	public const string Path = "/socket";

	public static void MapSocketEndpoint( this IEndpointRouteBuilder app )
	{
		app.Map( Path, HandleAsync );
	}

	private static async Task HandleAsync( HttpContext context, SocketTickets tickets, JuiceboxDbContext db,
		SessionRelay relay, IOptions<JuiceboxOptions> options )
	{
		if ( !context.WebSockets.IsWebSocketRequest )
		{
			context.Response.StatusCode = StatusCodes.Status400BadRequest;
			return;
		}

		if ( !tickets.TryRedeem( context.Request.Query["ticket"], out var ticket ) )
		{
			context.Response.StatusCode = StatusCodes.Status401Unauthorized;
			return;
		}

		// The secret key must still be current: a member who expired and whose name was taken
		// by someone else can't use a ticket issued before that happened.
		var identity = ticket.Identity;
		var alive = identity.IsHost
			? await db.FindLiveSessionAsync( identity.SessionId, ticket.SecretKey ) != null
			: await db.FindLiveMemberAsync( identity.SessionId, identity.MemberName!, ticket.SecretKey ) != null;
		if ( !alive )
		{
			context.Response.StatusCode = StatusCodes.Status404NotFound;
			return;
		}

		using var socket = await context.WebSockets.AcceptWebSocketAsync();
		await relay.RunAsync( identity, socket, options.Value.MaxMessageBytes, context.RequestAborted );
	}
}
