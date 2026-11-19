using Juicebox.Server.Data;
using Juicebox.Server.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Juicebox.Server;

/// <summary>
/// Destroys sessions and members that stopped pinging, and deletes old destroyed rows.
/// Replaces the Azure WebJobs timers from the original service.
/// </summary>
public sealed class ExpiryService(
	IServiceScopeFactory scopeFactory,
	SessionRelay relay,
	IOptions<JuiceboxOptions> options,
	ILogger<ExpiryService> logger ) : BackgroundService
{
	protected override async Task ExecuteAsync( CancellationToken stoppingToken )
	{
		using var timer = new PeriodicTimer( options.Value.ExpiryInterval );
		do
		{
			try
			{
				await RunOnceAsync( stoppingToken );
			}
			catch ( Exception e ) when ( e is not OperationCanceledException )
			{
				logger.LogError( e, "Expiry run failed" );
			}
		} while ( await timer.WaitForNextTickAsync( stoppingToken ) );
	}

	public async Task RunOnceAsync( CancellationToken ct = default )
	{
		await using var scope = scopeFactory.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<JuiceboxDbContext>();
		var opts = options.Value;
		var now = DateTime.UtcNow;

		// Destroy first, with the timeout checked in the same statement, so a ping that lands mid-run is never
		// overridden. The rows destroyed by this run are then the ones stamped with `now`.
		var sessionCutoff = now - opts.SessionTimeout;
		var expiredSessionCount = await db.Sessions
			.Where( s => s.Destroyed == null && (s.LastPing ?? s.Created) < sessionCutoff )
			.ExecuteUpdateAsync( u => u.SetProperty( s => s.Destroyed, now ), ct );
		if ( expiredSessionCount > 0 )
		{
			var expiredSessions = await db.Sessions
				.Where( s => s.Destroyed == now )
				.Select( s => s.SessionId )
				.ToListAsync( ct );
			foreach ( var sessionId in expiredSessions )
			{
				relay.CloseSession( sessionId, "Session Closed" );
			}
		}

		var memberCutoff = now - opts.MemberTimeout;
		var expiredMemberCount = await db.Members
			.Where( m => m.Destroyed == null && (m.LastPing ?? m.Created) < memberCutoff )
			.ExecuteUpdateAsync( u => u.SetProperty( m => m.Destroyed, now ), ct );
		if ( expiredMemberCount > 0 )
		{
			var expiredMembers = await db.Members
				.Where( m => m.Destroyed == now )
				.Select( m => new { m.SessionId, m.MemberName } )
				.ToListAsync( ct );
			foreach ( var member in expiredMembers )
			{
				relay.CloseMember( member.SessionId, member.MemberName, "Member Expired" );
			}
		}

		var retainCutoff = now - opts.RetainDestroyedFor;
		var deletedMembers = await db.Members
			.Where( m => m.Destroyed != null && m.Destroyed < retainCutoff )
			.ExecuteDeleteAsync( ct );
		var deletedSessions = await db.Sessions
			.Where( s => s.Destroyed != null && s.Destroyed < retainCutoff )
			.ExecuteDeleteAsync( ct ); // members are removed by the cascade

		if ( expiredSessionCount + expiredMemberCount + deletedSessions + deletedMembers > 0 )
		{
			logger.LogInformation(
				"Expired {Sessions} sessions and {Members} members; deleted {DeletedSessions} sessions and {DeletedMembers} members",
				expiredSessionCount, expiredMemberCount, deletedSessions, deletedMembers );
		}
	}
}
