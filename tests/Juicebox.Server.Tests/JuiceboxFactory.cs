using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Juicebox.Server.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Juicebox.Server.Tests;

/// <summary>
/// Runs the server in memory against a throwaway SQLite database.
/// </summary>
public sealed class JuiceboxFactory( params (string Key, string Value)[] settings ) : WebApplicationFactory<Program>
{
	private readonly string _dataDirectory =
		Path.Combine( Path.GetTempPath(), "juicebox-tests", Guid.NewGuid().ToString( "N" ) );

	protected override void ConfigureWebHost( IWebHostBuilder builder )
	{
		builder.UseEnvironment( "Testing" );
		builder.UseSetting( "ConnectionStrings:Juicebox", $"Data Source={Path.Combine( _dataDirectory, "test.db" )}" );
		builder.UseSetting( "Juicebox:JoinRateLimitPerMinute", "1000" );
		builder.UseSetting( "Juicebox:CreateRateLimitPerMinute", "1000" );
		builder.UseSetting( "Juicebox:ExpiryInterval", "01:00:00" ); // tests run expiry by hand
		foreach ( var (key, value) in settings )
		{
			builder.UseSetting( key, value );
		}
	}

	protected override void Dispose( bool disposing )
	{
		base.Dispose( disposing );
		Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
		try
		{
			Directory.Delete( _dataDirectory, true );
		}
		catch ( IOException )
		{
		}
	}

	public async Task<HostSession> CreateSessionAsync()
	{
		using var client = CreateClient();
		var response = await client.PostAsJsonAsync( "/api/sessions/create", new { PublicKey = "ignored" } );
		response.EnsureSuccessStatusCode();
		var json = await response.Content.ReadFromJsonAsync<JsonObject>();
		return new HostSession(
			json!["SessionId"]!.GetValue<long>(),
			json["HostSecretKey"]!.GetValue<string>(),
			json["JoinPassword"]!.GetValue<string>() );
	}

	public async Task<JoinedMember> JoinAsync( string joinPassword, string name )
	{
		using var client = CreateClient();
		var response = await client.PostAsJsonAsync( "/api/members/join",
			new { ProjectId = 20, JoinPassword = joinPassword, MemberName = name } );
		response.EnsureSuccessStatusCode();
		var json = await response.Content.ReadFromJsonAsync<JsonObject>();
		return new JoinedMember(
			json!["SessionId"]!.GetValue<long>(),
			json["MemberName"]!.GetValue<string>(),
			json["MemberSecretKey"]!.GetValue<string>() );
	}

	public async Task<string> NegotiateAsync( string negotiatePath, object request )
	{
		using var client = CreateClient();
		var response = await client.PostAsJsonAsync( negotiatePath, request );
		response.EnsureSuccessStatusCode();
		var json = await response.Content.ReadFromJsonAsync<JsonObject>();
		return json!["Endpoint"]!.GetValue<string>();
	}

	public Task<WebSocket> ConnectAsync( string endpoint ) =>
		Server.CreateWebSocketClient().ConnectAsync( new Uri( endpoint ), CancellationToken.None );

	public async Task<WebSocket> ConnectAsync( string negotiatePath, object request ) =>
		await ConnectAsync( await NegotiateAsync( negotiatePath, request ) );

	/// <summary>
	/// Makes a member look like it stopped pinging long ago, then runs expiry.
	/// </summary>
	public async Task ExpireMemberAsync( JoinedMember member )
	{
		await using ( var scope = Services.CreateAsyncScope() )
		{
			var db = scope.ServiceProvider.GetRequiredService<JuiceboxDbContext>();
			await db.Members
				.Where( m => m.SessionId == member.SessionId && m.MemberName == member.MemberName )
				.ExecuteUpdateAsync( u => u.SetProperty( m => m.Created, DateTime.UtcNow.AddHours( -1 ) ) );
		}

		await Services.GetRequiredService<ExpiryService>().RunOnceAsync();
	}

	public Task<string> NegotiateMemberAsync( JoinedMember member ) =>
		NegotiateAsync( "/api/members/negotiate", new { member.SessionId, member.MemberName, member.MemberSecretKey } );

	public Task<WebSocket> ConnectHostAsync( HostSession host ) =>
		ConnectAsync( "/api/sessions/negotiate", new { host.SessionId, host.HostSecretKey } );

	public Task<WebSocket> ConnectMemberAsync( JoinedMember member ) =>
		ConnectAsync( "/api/members/negotiate", new { member.SessionId, member.MemberName, member.MemberSecretKey } );
}

public sealed record HostSession( long SessionId, string HostSecretKey, string JoinPassword );

public sealed record JoinedMember( long SessionId, string MemberName, string MemberSecretKey );

public static class WebSocketExtensions
{
	public static Task SendTextAsync( this WebSocket socket, string text ) =>
		socket.SendAsync( Encoding.UTF8.GetBytes( text ), WebSocketMessageType.Text, true, CancellationToken.None );

	public static Task SendJsonAsync( this WebSocket socket, object value ) =>
		socket.SendTextAsync( JsonSerializer.Serialize( value ) );

	/// <summary>
	/// Receives the next text message, or null if the socket was closed.
	/// </summary>
	public static async Task<string?> ReceiveTextAsync( this WebSocket socket, TimeSpan? timeout = null )
	{
		using var cts = new CancellationTokenSource( timeout ?? TimeSpan.FromSeconds( 5 ) );
		var buffer = new byte[64 * 1024];
		using var message = new MemoryStream();
		while ( true )
		{
			var result = await socket.ReceiveAsync( buffer, cts.Token );
			if ( result.MessageType == WebSocketMessageType.Close )
			{
				return null;
			}

			message.Write( buffer, 0, result.Count );
			if ( result.EndOfMessage )
			{
				return Encoding.UTF8.GetString( message.ToArray() );
			}
		}
	}

	public static async Task<JsonObject> ReceiveJsonAsync( this WebSocket socket )
	{
		var text = await socket.ReceiveTextAsync();
		Assert.NotNull( text );
		return Assert.IsType<JsonObject>( JsonNode.Parse( text ) );
	}
}
