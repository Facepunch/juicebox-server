using Microsoft.EntityFrameworkCore;

namespace Juicebox.Server.Data;

public sealed class JuiceboxDbContext( DbContextOptions<JuiceboxDbContext> options ) : DbContext( options )
{
	public DbSet<Session> Sessions => Set<Session>();
	public DbSet<Member> Members => Set<Member>();

	protected override void OnModelCreating( ModelBuilder modelBuilder )
	{
		modelBuilder.Entity<Session>( e =>
		{
			e.HasKey( s => s.SessionId );
			e.Property( s => s.HostSecretKey ).HasMaxLength( Secrets.SecretKeyLength );
			e.Property( s => s.JoinPassword ).HasMaxLength( Secrets.MaxJoinCodeLength );

			// Join codes only need to be unique among live sessions.
			e.HasIndex( s => s.JoinPassword )
				.IsUnique()
				.HasFilter( "\"Destroyed\" IS NULL" );

			e.HasIndex( s => s.Destroyed );

			e.HasMany( s => s.Members )
				.WithOne( m => m.Session )
				.HasForeignKey( m => m.SessionId )
				.OnDelete( DeleteBehavior.Cascade );
		} );

		modelBuilder.Entity<Member>( e =>
		{
			e.HasKey( m => new { m.SessionId, m.MemberName } );

			// The original service ran on SQL Server with a case-insensitive collation.
			e.Property( m => m.MemberName )
				.HasMaxLength( Member.MaxNameLength )
				.UseCollation( "NOCASE" );

			e.Property( m => m.MemberSecretKey ).HasMaxLength( Secrets.SecretKeyLength );

			e.HasIndex( m => m.Destroyed );
		} );
	}
}
