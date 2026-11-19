using System.Text.Json.Nodes;

namespace Juicebox.Server.Tests;

/// <summary>
/// How the relay picks recipients and what exactly it forwards.
/// </summary>
public sealed class RoutingTests : IDisposable
{
	private readonly JuiceboxFactory _factory = new();

	public void Dispose() => _factory.Dispose();

	[Fact]
	public async Task ReconnectingHost_IsToldWhoIsAlreadyConnected()
	{
		var host = await _factory.CreateSessionAsync();
		var alice = await _factory.JoinAsync( host.JoinPassword, "Alice" );
		var bob = await _factory.JoinAsync( host.JoinPassword, "Bob" );
		using var aliceSocket = await _factory.ConnectMemberAsync( alice );
		using var bobSocket = await _factory.ConnectMemberAsync( bob );

		using var hostSocket = await _factory.ConnectHostAsync( host );
		var names = new[] { await hostSocket.ReceiveJsonAsync(), await hostSocket.ReceiveJsonAsync() }
			.Select( e =>
			{
				Assert.Equal( "Connected", e["Type"]!.GetValue<string>() );
				return e["MemberName"]!.GetValue<string>();
			} )
			.Order();
		Assert.Equal( ["Alice", "Bob"], names );
	}

	[Fact]
	public async Task NamesOnlyIgnoreAsciiCase_LikeTheDatabase()
	{
		var host = await _factory.CreateSessionAsync();
		var upper = await _factory.JoinAsync( host.JoinPassword, "Émile" );
		var lower = await _factory.JoinAsync( host.JoinPassword, "émile" ); // a different player to SQLite NOCASE

		using var hostSocket = await _factory.ConnectHostAsync( host );
		using var upperSocket = await _factory.ConnectMemberAsync( upper );
		using var lowerSocket = await _factory.ConnectMemberAsync( lower );
		await hostSocket.ReceiveJsonAsync();
		await hostSocket.ReceiveJsonAsync();

		await hostSocket.SendJsonAsync( new { To = "Émile", Message = new { Type = "Display", Secret = "upper" } } );
		Assert.Equal( "ack", await hostSocket.ReceiveTextAsync() );
		Assert.Equal( "upper", (await upperSocket.ReceiveJsonAsync())["Secret"]!.GetValue<string>() );

		// The other player gets nothing: the next thing it sees is the ack for its own message.
		await lowerSocket.SendJsonAsync( new { Type = "Action", Key = "x" } );
		Assert.Equal( "ack", await lowerSocket.ReceiveTextAsync() );

		Assert.True( Realtime.SessionRelay.NamesEqual( "ALICE", "alice" ) );
		Assert.False( Realtime.SessionRelay.NamesEqual( "Émile", "émile" ) );
	}

	[Fact]
	public async Task Messages_AreForwardedExactlyAsSent()
	{
		var host = await _factory.CreateSessionAsync();
		var alice = await _factory.JoinAsync( host.JoinPassword, "Zoë" );
		using var hostSocket = await _factory.ConnectHostAsync( host );
		using var aliceSocket = await _factory.ConnectMemberAsync( alice );
		await hostSocket.ReceiveJsonAsync(); // Connected

		const string memberMessage = """{ "Type":"Response", "Fields":{"drawing":"data:image/png;base64,ab+/cd==","text":"héllo 🎉 <b>"} }""";
		await aliceSocket.SendTextAsync( memberMessage );
		Assert.Equal( "ack", await aliceSocket.ReceiveTextAsync() );
		var forwarded = await hostSocket.ReceiveTextAsync();
		Assert.Equal( $$"""{"Type":"Message","MemberName":"Zoë","Message":{{memberMessage}}}""", forwarded );

		const string display = """{"Type":"Display","Display":{"Stage":{"Title":"a+b 🎉"}}}""";
		await hostSocket.SendTextAsync( $$"""{"To":null,"Message":{{display}}}""" );
		Assert.Equal( "ack", await hostSocket.ReceiveTextAsync() );
		Assert.Equal( display, await aliceSocket.ReceiveTextAsync() );

		Assert.IsType<JsonObject>( JsonNode.Parse( forwarded! ) );
	}
}
