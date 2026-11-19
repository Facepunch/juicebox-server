using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Juicebox.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Juicebox.Server.Tests;

public sealed class ApiTests : IDisposable
{
	private readonly JuiceboxFactory _factory = new();

	public void Dispose() => _factory.Dispose();

	[Fact]
	public async Task Create_ReturnsSixCharacterJoinCode()
	{
		var host = await _factory.CreateSessionAsync();

		Assert.True( host.SessionId > 0 );
		Assert.Equal( 32, host.HostSecretKey.Length );
		Assert.Matches( "^[A-Z1-9]{6}$", host.JoinPassword );
	}

	[Fact]
	public async Task Join_ThenHostPingListsMember()
	{
		var host = await _factory.CreateSessionAsync();
		var member = await _factory.JoinAsync( host.JoinPassword.ToLowerInvariant(), "  Alice  " );

		Assert.Equal( host.SessionId, member.SessionId );
		Assert.Equal( "Alice", member.MemberName );

		using var client = _factory.CreateClient();
		var ping = await client.PostAsJsonAsync( "/api/sessions/ping", new { host.SessionId, host.HostSecretKey } );
		ping.EnsureSuccessStatusCode();
		var json = await ping.Content.ReadFromJsonAsync<JsonObject>();
		Assert.Equal( ["Alice"], json!["MemberNames"]!.AsArray().Select( n => n!.GetValue<string>() ) );

		var memberPing = await client.PostAsJsonAsync( "/api/members/ping", member );
		Assert.Equal( HttpStatusCode.NoContent, memberPing.StatusCode );
	}

	[Fact]
	public async Task Join_ReturnsExpectedErrors()
	{
		var host = await _factory.CreateSessionAsync();
		await _factory.JoinAsync( host.JoinPassword, "Alice" );
		using var client = _factory.CreateClient();

		var duplicate = await client.PostAsJsonAsync( "/api/members/join",
			new { JoinPassword = host.JoinPassword, MemberName = "ALICE" } );
		Assert.Equal( HttpStatusCode.Conflict, duplicate.StatusCode );

		var unknown = await client.PostAsJsonAsync( "/api/members/join",
			new { JoinPassword = "ZZZZZZZ", MemberName = "Bob" } );
		Assert.Equal( HttpStatusCode.NotFound, unknown.StatusCode );

		var missing = await client.PostAsJsonAsync( "/api/members/join", new { JoinPassword = host.JoinPassword } );
		Assert.Equal( HttpStatusCode.BadRequest, missing.StatusCode );

		await using ( var scope = _factory.Services.CreateAsyncScope() )
		{
			var db = scope.ServiceProvider.GetRequiredService<JuiceboxDbContext>();
			await db.Sessions.Where( s => s.SessionId == host.SessionId )
				.ExecuteUpdateAsync( u => u.SetProperty( s => s.IsLocked, true ) );
		}

		var locked = await client.PostAsJsonAsync( "/api/members/join",
			new { JoinPassword = host.JoinPassword, MemberName = "Carol" } );
		Assert.Equal( (HttpStatusCode)423, locked.StatusCode );
	}

	[Fact]
	public async Task WrongSecretKey_IsNotFound()
	{
		var host = await _factory.CreateSessionAsync();
		using var client = _factory.CreateClient();

		var ping = await client.PostAsJsonAsync( "/api/sessions/ping",
			new { host.SessionId, HostSecretKey = "wrong-key-wrong-key" } );
		Assert.Equal( HttpStatusCode.NotFound, ping.StatusCode );
	}

	[Fact]
	public async Task Destroy_EndsSessionForMembers()
	{
		var host = await _factory.CreateSessionAsync();
		var member = await _factory.JoinAsync( host.JoinPassword, "Alice" );
		using var client = _factory.CreateClient();

		var destroy = await client.PostAsJsonAsync( "/api/sessions/destroy", new { host.SessionId, host.HostSecretKey } );
		Assert.Equal( HttpStatusCode.NoContent, destroy.StatusCode );

		Assert.Equal( HttpStatusCode.NotFound, (await client.PostAsJsonAsync( "/api/members/ping", member )).StatusCode );
		Assert.Equal( HttpStatusCode.NotFound, (await client.PostAsJsonAsync( "/api/members/join",
			new { JoinPassword = host.JoinPassword, MemberName = "Bob" } )).StatusCode );
	}

	[Fact]
	public async Task Expiry_DestroysStaleSessionsAndMembers_AndExpiredNamesCanRejoin()
	{
		var host = await _factory.CreateSessionAsync();
		var alice = await _factory.JoinAsync( host.JoinPassword, "Alice" );
		var bob = await _factory.JoinAsync( host.JoinPassword, "Bob" );

		await using ( var scope = _factory.Services.CreateAsyncScope() )
		{
			var db = scope.ServiceProvider.GetRequiredService<JuiceboxDbContext>();
			var old = DateTime.UtcNow.AddMinutes( -10 );
			await db.Members.Where( m => m.MemberName == "Alice" )
				.ExecuteUpdateAsync( u => u.SetProperty( m => m.Created, old ) );
		}

		await _factory.Services.GetRequiredService<ExpiryService>().RunOnceAsync();

		using var client = _factory.CreateClient();
		Assert.Equal( HttpStatusCode.NotFound, (await client.PostAsJsonAsync( "/api/members/ping", alice )).StatusCode );
		Assert.Equal( HttpStatusCode.NoContent, (await client.PostAsJsonAsync( "/api/members/ping", bob )).StatusCode );

		var rejoined = await _factory.JoinAsync( host.JoinPassword, "alice" );
		Assert.Equal( "Alice", rejoined.MemberName );
		Assert.NotEqual( alice.MemberSecretKey, rejoined.MemberSecretKey );

		await using ( var scope = _factory.Services.CreateAsyncScope() )
		{
			var db = scope.ServiceProvider.GetRequiredService<JuiceboxDbContext>();
			await db.Sessions.ExecuteUpdateAsync( u => u.SetProperty( s => s.Created, DateTime.UtcNow.AddMinutes( -10 ) ) );
		}

		await _factory.Services.GetRequiredService<ExpiryService>().RunOnceAsync();

		var ping = await client.PostAsJsonAsync( "/api/sessions/ping", new { host.SessionId, host.HostSecretKey } );
		Assert.Equal( HttpStatusCode.NotFound, ping.StatusCode );
	}

	[Fact]
	public async Task UnknownApiRoute_IsNotFound()
	{
		using var client = _factory.CreateClient();
		var response = await client.GetAsync( "/api/nope" );
		Assert.Equal( HttpStatusCode.NotFound, response.StatusCode );
	}
}
