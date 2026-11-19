using Microsoft.EntityFrameworkCore;

namespace Juicebox.Server.Data;

/// <summary>
/// The one definition of a live session or member, shared by the HTTP endpoints and the socket endpoint.
/// </summary>
public static class LiveQueries
{
	public static IQueryable<Session> Live( this IQueryable<Session> sessions ) =>
		sessions.Where( s => s.Destroyed == null );

	public static IQueryable<Member> Live( this IQueryable<Member> members ) =>
		members.Where( m => m.Destroyed == null && m.Session.Destroyed == null );

	public static Task<Session?> FindLiveSessionAsync( this JuiceboxDbContext db, long sessionId, string hostSecretKey ) =>
		db.Sessions.Live().SingleOrDefaultAsync( s => s.SessionId == sessionId && s.HostSecretKey == hostSecretKey );

	public static Task<Member?> FindLiveMemberAsync( this JuiceboxDbContext db, long sessionId, string memberName,
		string memberSecretKey ) =>
		db.Members.Live().SingleOrDefaultAsync( m =>
			m.SessionId == sessionId && m.MemberName == memberName && m.MemberSecretKey == memberSecretKey );
}
