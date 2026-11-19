using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json.Nodes;
using Juicebox.Server.Realtime;
using Microsoft.Extensions.DependencyInjection;

namespace Juicebox.Server.Tests;

public sealed class RelayTests : IDisposable
{
	private readonly JuiceboxFactory _factory = new();

	public void Dispose() => _factory.Dispose();

	[Fact]
	public async Task HostAndMembers_ExchangeMessages()
	{
		var host = await _factory.CreateSessionAsync();
		var alice = await _factory.JoinAsync( host.JoinPassword, "Alice" );
		var bob = await _factory.JoinAsync( host.JoinPassword, "Bob" );

		using var hostSocket = await _factory.ConnectHostAsync( host );
		using var aliceSocket = await _factory.ConnectMemberAsync( alice );

		var connected = await hostSocket.ReceiveJsonAsync();
		Assert.Equal( "Connected", connected["Type"]!.GetValue<string>() );
		Assert.Equal( "Alice", connected["MemberName"]!.GetValue<string>() );

		using var bobSocket = await _factory.ConnectMemberAsync( bob );
		Assert.Equal( "Bob", (await hostSocket.ReceiveJsonAsync())["MemberName"]!.GetValue<string>() );

		// Broadcast, the way the s&box library sends a display to everyone.
		await hostSocket.SendJsonAsync( new { To = (string?)null, Message = new { Type = "Display", Display = new { Stage = new { Title = "Hi" } } } } );
		Assert.Equal( "ack", await hostSocket.ReceiveTextAsync() );
		foreach ( var socket in new[] { aliceSocket, bobSocket } )
		{
			var display = await socket.ReceiveJsonAsync();
			Assert.Equal( "Display", display["Type"]!.GetValue<string>() );
			Assert.Equal( "Hi", display["Display"]!["Stage"]!["Title"]!.GetValue<string>() );
		}

		// Direct message to one member (names match case-insensitively).
		await hostSocket.SendJsonAsync( new { To = "bob", Message = new { Type = "Display", Display = new { } } } );
		Assert.Equal( "ack", await hostSocket.ReceiveTextAsync() );
		Assert.Equal( "Display", (await bobSocket.ReceiveJsonAsync())["Type"]!.GetValue<string>() );

		// Member response, the way the website submits a form.
		await aliceSocket.SendJsonAsync( new { Type = "Response", Fields = new { answer = "potato" } } );
		Assert.Equal( "ack", await aliceSocket.ReceiveTextAsync() );
		var message = await hostSocket.ReceiveJsonAsync();
		Assert.Equal( "Message", message["Type"]!.GetValue<string>() );
		Assert.Equal( "Alice", message["MemberName"]!.GetValue<string>() );
		Assert.Equal( "potato", message["Message"]!["Fields"]!["answer"]!.GetValue<string>() );

		// Members must send JSON objects.
		await bobSocket.SendTextAsync( "not json" );
		Assert.Equal( "fail:2", await bobSocket.ReceiveTextAsync() );

		await aliceSocket.CloseAsync( WebSocketCloseStatus.NormalClosure, "", CancellationToken.None );
		var disconnected = await hostSocket.ReceiveJsonAsync();
		Assert.Equal( "Disconnected", disconnected["Type"]!.GetValue<string>() );
		Assert.Equal( "Alice", disconnected["MemberName"]!.GetValue<string>() );
	}

	[Fact]
	public async Task HostFrameWithoutMessage_Fails()
	{
		var host = await _factory.CreateSessionAsync();
		using var hostSocket = await _factory.ConnectHostAsync( host );

		await hostSocket.SendJsonAsync( new { To = "nobody" } );
		Assert.Equal( "fail:4", await hostSocket.ReceiveTextAsync() );

		await hostSocket.SendAsync( new byte[] { 1, 2, 3 }, WebSocketMessageType.Binary, true, CancellationToken.None );
		Assert.Equal( "fail:0", await hostSocket.ReceiveTextAsync() );
	}

	[Fact]
	public async Task LargeMessages_AreRelayed()
	{
		var host = await _factory.CreateSessionAsync();
		var alice = await _factory.JoinAsync( host.JoinPassword, "Alice" );
		using var hostSocket = await _factory.ConnectHostAsync( host );
		using var aliceSocket = await _factory.ConnectMemberAsync( alice );
		await hostSocket.ReceiveJsonAsync(); // Connected

		var drawing = "data:image/png;base64," + new string( 'A', 500_000 );
		await aliceSocket.SendJsonAsync( new { Type = "Response", Fields = new { drawing } } );
		Assert.Equal( "ack", await aliceSocket.ReceiveTextAsync() );

		var message = await hostSocket.ReceiveJsonAsync();
		Assert.Equal( drawing, message["Message"]!["Fields"]!["drawing"]!.GetValue<string>() );
	}

	[Fact]
	public async Task DestroyingSession_ClosesSockets()
	{
		var host = await _factory.CreateSessionAsync();
		var alice = await _factory.JoinAsync( host.JoinPassword, "Alice" );
		using var aliceSocket = await _factory.ConnectMemberAsync( alice );

		using var client = _factory.CreateClient();
		await client.PostAsJsonAsync( "/api/sessions/destroy", new { host.SessionId, host.HostSecretKey } );

		Assert.Null( await aliceSocket.ReceiveTextAsync() );
		Assert.Equal( "Session Closed", aliceSocket.CloseStatusDescription );
		await WaitUntil( () => _factory.Services.GetRequiredService<SessionRelay>().ConnectionCount( host.SessionId ) == 0 );
	}

	[Fact]
	public async Task InvalidToken_IsRejected()
	{
		var client = _factory.Server.CreateWebSocketClient();
		var error = await Assert.ThrowsAnyAsync<Exception>( () =>
			client.ConnectAsync( new Uri( "ws://localhost/socket?ticket=garbage" ), CancellationToken.None ) );
		Assert.Contains( "401", error.Message );
	}

	private static async Task WaitUntil( Func<bool> condition )
	{
		for ( var i = 0; i < 50 && !condition(); i++ )
		{
			await Task.Delay( 100 );
		}

		Assert.True( condition() );
	}
}
