using System.Buffers;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Juicebox.Server.Realtime;

/// <summary>
/// Routes messages between a session's host and its members, using the original Juicebox wire protocol:
/// <list type="bullet">
/// <item>Host sends <c>{"To": name|null, "Message": {...}}</c>. The message goes to that member, or every member when To is empty.</item>
/// <item>Member sends any JSON object. The host receives <c>{"Type":"Message","MemberName":...,"Message":{...}}</c>.</item>
/// <item>The host receives <c>Connected</c> and <c>Disconnected</c> events as member sockets come and go, plus a
/// <c>Connected</c> event for every member already connected when the host (re)connects.</item>
/// <item>Every message received gets <c>ack</c> or <c>fail:N</c> sent back to its sender.</item>
/// </list>
/// Message bodies are forwarded as the exact JSON the sender wrote. Connections live in memory,
/// so only one server instance can serve a given session.
/// </summary>
public sealed class SessionRelay
{
	private static readonly JsonWriterOptions WriterOptions = new()
	{
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, SkipValidation = true
	};

	private readonly Lock _lock = new();
	private readonly Dictionary<long, List<RelayConnection>> _sessions = new();

	public async Task RunAsync( SocketIdentity identity, WebSocket socket, int maxMessageBytes, CancellationToken ct )
	{
		var connection = new RelayConnection( identity, socket, maxMessageBytes, ct );
		Add( connection );
		try
		{
			await connection.RunAsync( maxMessageBytes, HandleMessage );
		}
		finally
		{
			Remove( connection );
		}
	}

	/// <summary>
	/// Closes every connection belonging to a session.
	/// </summary>
	public void CloseSession( long sessionId, string reason )
	{
		foreach ( var connection in Snapshot( sessionId, _ => true ) )
		{
			connection.Close( WebSocketCloseStatus.NormalClosure, reason );
		}
	}

	/// <summary>
	/// Closes every connection belonging to one member of a session.
	/// </summary>
	public void CloseMember( long sessionId, string memberName, string reason )
	{
		foreach ( var connection in Snapshot( sessionId, c => IsMember( c, memberName ) ) )
		{
			connection.Close( WebSocketCloseStatus.NormalClosure, reason );
		}
	}

	public int ConnectionCount( long sessionId ) => Snapshot( sessionId, _ => true ).Count;

	/// <summary>
	/// Compares member names the way the database does (SQLite NOCASE): ASCII letters ignore case, everything else must match exactly.
	/// </summary>
	public static bool NamesEqual( string a, string b )
	{
		if ( a.Length != b.Length )
		{
			return false;
		}

		for ( var i = 0; i < a.Length; i++ )
		{
			if ( a[i] != b[i] && (!char.IsAsciiLetter( a[i] ) || !char.IsAsciiLetter( b[i] ) || (a[i] | 0x20) != (b[i] | 0x20)) )
			{
				return false;
			}
		}

		return true;
	}

	private void Add( RelayConnection connection )
	{
		var identity = connection.Identity;
		List<string> alreadyConnected;
		lock ( _lock )
		{
			if ( !_sessions.TryGetValue( identity.SessionId, out var connections ) )
			{
				connections = [];
				_sessions.Add( identity.SessionId, connections );
			}

			connections.Add( connection );
			alreadyConnected = identity.IsHost
				? connections.Where( c => !c.Identity.IsHost ).Select( c => c.Identity.MemberName! ).Distinct().ToList()
				: [];
		}

		if ( identity.IsHost )
		{
			// Let a (re)connecting host know who is already here, so it can mark them connected and re-send displays.
			foreach ( var name in alreadyConnected )
			{
				connection.Send( MemberEvent( "Connected", name ) );
			}
		}
		else
		{
			SendToHosts( identity.SessionId, MemberEvent( "Connected", identity.MemberName! ) );
		}
	}

	private void Remove( RelayConnection connection )
	{
		var identity = connection.Identity;
		lock ( _lock )
		{
			if ( _sessions.TryGetValue( identity.SessionId, out var connections ) )
			{
				connections.Remove( connection );
				if ( connections.Count == 0 )
				{
					_sessions.Remove( identity.SessionId );
				}
			}
		}

		if ( !identity.IsHost )
		{
			SendToHosts( identity.SessionId, MemberEvent( "Disconnected", identity.MemberName! ) );
		}
	}

	private void HandleMessage( RelayConnection from, ReadOnlyMemory<byte> utf8 )
	{
		JsonDocument document;
		try
		{
			document = JsonDocument.Parse( utf8 );
		}
		catch ( JsonException )
		{
			from.Send( RelayReplies.InvalidJson );
			return;
		}

		using ( document )
		{
			var reply = from.Identity.IsHost
				? HandleHostMessage( from.Identity, document.RootElement )
				: HandleMemberMessage( from.Identity, document.RootElement );
			from.Send( reply );
		}
	}

	private ReadOnlyMemory<byte> HandleHostMessage( SocketIdentity host, JsonElement frame )
	{
		if ( frame.ValueKind == JsonValueKind.Null )
		{
			return RelayReplies.MissingMessage;
		}

		if ( frame.ValueKind != JsonValueKind.Object )
		{
			return RelayReplies.InvalidJson;
		}

		string? to = null;
		JsonElement? message = null;
		foreach ( var property in frame.EnumerateObject() )
		{
			if ( string.Equals( property.Name, "To", StringComparison.OrdinalIgnoreCase ) )
			{
				switch ( property.Value.ValueKind )
				{
					case JsonValueKind.String: to = property.Value.GetString(); break;
					case JsonValueKind.Null: to = null; break;
					default: return RelayReplies.InvalidJson;
				}
			}
			else if ( string.Equals( property.Name, "Message", StringComparison.OrdinalIgnoreCase ) )
			{
				switch ( property.Value.ValueKind )
				{
					case JsonValueKind.Object: message = property.Value; break;
					case JsonValueKind.Null: message = null; break;
					default: return RelayReplies.InvalidJson;
				}
			}
		}

		if ( message == null )
		{
			return RelayReplies.MissingMessage;
		}

		ReadOnlyMemory<byte> payload = JsonMarshal.GetRawUtf8Value( message.Value ).ToArray();
		var targets = string.IsNullOrEmpty( to )
			? Snapshot( host.SessionId, c => !c.Identity.IsHost )
			: Snapshot( host.SessionId, c => IsMember( c, to ) );

		foreach ( var target in targets )
		{
			target.Send( payload );
		}

		return RelayReplies.Ack;
	}

	private ReadOnlyMemory<byte> HandleMemberMessage( SocketIdentity member, JsonElement message )
	{
		if ( message.ValueKind != JsonValueKind.Object )
		{
			return RelayReplies.InvalidJson;
		}

		var buffer = new ArrayBufferWriter<byte>();
		using ( var writer = new Utf8JsonWriter( buffer, WriterOptions ) )
		{
			writer.WriteStartObject();
			writer.WriteString( "Type", "Message" );
			writer.WriteString( "MemberName", member.MemberName );
			writer.WritePropertyName( "Message" );
			writer.WriteRawValue( JsonMarshal.GetRawUtf8Value( message ), skipInputValidation: true );
			writer.WriteEndObject();
		}

		SendToHosts( member.SessionId, buffer.WrittenMemory );
		return RelayReplies.Ack;
	}

	private static ReadOnlyMemory<byte> MemberEvent( string type, string memberName )
	{
		var buffer = new ArrayBufferWriter<byte>();
		using ( var writer = new Utf8JsonWriter( buffer, WriterOptions ) )
		{
			writer.WriteStartObject();
			writer.WriteString( "Type", type );
			writer.WriteString( "MemberName", memberName );
			writer.WriteEndObject();
		}

		return buffer.WrittenMemory;
	}

	private void SendToHosts( long sessionId, ReadOnlyMemory<byte> payload )
	{
		foreach ( var host in Snapshot( sessionId, c => c.Identity.IsHost ) )
		{
			host.Send( payload );
		}
	}

	private List<RelayConnection> Snapshot( long sessionId, Func<RelayConnection, bool> filter )
	{
		lock ( _lock )
		{
			return _sessions.TryGetValue( sessionId, out var connections )
				? connections.Where( filter ).ToList()
				: [];
		}
	}

	private static bool IsMember( RelayConnection connection, string memberName ) =>
		!connection.Identity.IsHost && NamesEqual( connection.Identity.MemberName!, memberName );
}
