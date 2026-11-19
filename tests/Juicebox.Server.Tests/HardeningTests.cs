using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;

namespace Juicebox.Server.Tests;

/// <summary>
/// Socket tickets, limits, and configuration.
/// </summary>
public sealed class HardeningTests
{
	[Fact]
	public async Task SocketTicket_CanOnlyBeUsedOnce()
	{
		using var factory = new JuiceboxFactory();
		var host = await factory.CreateSessionAsync();
		var alice = await factory.JoinAsync( host.JoinPassword, "Alice" );
		var endpoint = await factory.NegotiateMemberAsync( alice );

		using var first = await factory.ConnectAsync( endpoint );
		var error = await Assert.ThrowsAnyAsync<Exception>( () => factory.ConnectAsync( endpoint ) );
		Assert.Contains( "401", error.Message );
	}

	[Fact]
	public async Task SocketTicket_Expires()
	{
		using var factory = new JuiceboxFactory( ("Juicebox:SocketTicketLifetime", "00:00:00.200") );
		var host = await factory.CreateSessionAsync();
		var alice = await factory.JoinAsync( host.JoinPassword, "Alice" );
		var endpoint = await factory.NegotiateMemberAsync( alice );

		await Task.Delay( 500 );

		var error = await Assert.ThrowsAnyAsync<Exception>( () => factory.ConnectAsync( endpoint ) );
		Assert.Contains( "401", error.Message );
	}

	[Fact]
	public async Task SocketTicket_IsInvalidOnceTheNameIsReclaimed()
	{
		using var factory = new JuiceboxFactory();
		var host = await factory.CreateSessionAsync();
		var alice = await factory.JoinAsync( host.JoinPassword, "Alice" );
		var oldEndpoint = await factory.NegotiateMemberAsync( alice );

		// Alice expires and someone else joins with her name.
		await factory.ExpireMemberAsync( alice );
		var impostor = await factory.JoinAsync( host.JoinPassword, "Alice" );
		Assert.NotEqual( alice.MemberSecretKey, impostor.MemberSecretKey );

		var error = await Assert.ThrowsAnyAsync<Exception>( () => factory.ConnectAsync( oldEndpoint ) );
		Assert.Contains( "404", error.Message );

		using var socket = await factory.ConnectMemberAsync( impostor );
		Assert.Equal( WebSocketState.Open, socket.State );
	}

	[Fact]
	public async Task OversizedMessage_ClosesTheSocket()
	{
		using var factory = new JuiceboxFactory( ("Juicebox:MaxMessageBytes", "1000") );
		var host = await factory.CreateSessionAsync();
		var alice = await factory.JoinAsync( host.JoinPassword, "Alice" );
		using var socket = await factory.ConnectMemberAsync( alice );

		await socket.SendJsonAsync( new { Type = "Response", Fields = new { text = new string( 'x', 2000 ) } } );

		Assert.Null( await socket.ReceiveTextAsync() );
		Assert.Equal( WebSocketCloseStatus.MessageTooBig, socket.CloseStatus );
	}

	[Fact]
	public async Task Join_IsRateLimited()
	{
		using var factory = new JuiceboxFactory( ("Juicebox:JoinRateLimitPerMinute", "2") );
		var host = await factory.CreateSessionAsync();
		using var client = factory.CreateClient();

		var codes = new List<HttpStatusCode>();
		foreach ( var name in new[] { "A", "B", "C" } )
		{
			var response = await client.PostAsJsonAsync( "/api/members/join",
				new { JoinPassword = host.JoinPassword, MemberName = name } );
			codes.Add( response.StatusCode );
		}

		Assert.Equal( [HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], codes );
	}

	[Fact]
	public async Task Cors_AllowsConfiguredOrigin()
	{
		using var factory = new JuiceboxFactory( ("Juicebox:CorsOrigins:0", "http://localhost:3000") );
		using var client = factory.CreateClient();

		using var allowed = Preflight( "http://localhost:3000" );
		var response = await client.SendAsync( allowed );
		Assert.Equal( HttpStatusCode.NoContent, response.StatusCode );
		Assert.Equal( "http://localhost:3000", response.Headers.GetValues( "Access-Control-Allow-Origin" ).Single() );

		using var denied = Preflight( "https://evil.example" );
		response = await client.SendAsync( denied );
		Assert.False( response.Headers.Contains( "Access-Control-Allow-Origin" ) );

		static HttpRequestMessage Preflight( string origin )
		{
			var request = new HttpRequestMessage( HttpMethod.Options, "/api/members/join" );
			request.Headers.Add( "Origin", origin );
			request.Headers.Add( "Access-Control-Request-Method", "POST" );
			request.Headers.Add( "Access-Control-Request-Headers", "content-type" );
			return request;
		}
	}

	[Theory]
	[InlineData( "Juicebox:ExpiryInterval", "00:00:00" )]
	[InlineData( "Juicebox:MaxMessageBytes", "0" )]
	[InlineData( "Juicebox:PublicUrl", "juicebox.example.com" )]
	[InlineData( "Juicebox:TrustedProxies:0", "not-an-ip" )]
	[InlineData( "Juicebox:JoinCodeLength", "4" )]
	public void InvalidOptions_FailStartup( string key, string value )
	{
		using var factory = new JuiceboxFactory( (key, value) );
		var error = Assert.ThrowsAny<Exception>( () => factory.CreateClient() );
		Assert.Contains( key.Split( ':' )[1], error.ToString() );
	}
}
