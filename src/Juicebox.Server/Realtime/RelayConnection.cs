using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;

namespace Juicebox.Server.Realtime;

/// <summary>
/// Called for each complete text message. <paramref name="utf8"/> is only valid during the call.
/// </summary>
public delegate void RelayMessageHandler( RelayConnection from, ReadOnlyMemory<byte> utf8 );

/// <summary>
/// One accepted WebSocket. Outgoing messages are queued and written by a single send loop because
/// <see cref="WebSocket.SendAsync(ArraySegment{byte}, WebSocketMessageType, bool, CancellationToken)"/>
/// doesn't allow concurrent calls.
/// </summary>
public sealed class RelayConnection
{
	/// <summary>
	/// Most messages a connection may have waiting to be sent.
	/// </summary>
	public const int OutboxCapacity = 64;

	/// <summary>
	/// Most bytes a connection may have waiting to be sent, as a multiple of the largest allowed message.
	/// </summary>
	public const int OutboxMessageBudget = 4;

	private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds( 5 );

	private readonly WebSocket _socket;
	private readonly Channel<ReadOnlyMemory<byte>> _outbox;
	private readonly CancellationTokenSource _cts;
	private readonly long _maxQueuedBytes;
	private long _queuedBytes;
	private WebSocketCloseStatus _closeStatus = WebSocketCloseStatus.NormalClosure;
	private string? _closeReason;
	private int _closing;

	public SocketIdentity Identity { get; }

	public RelayConnection( SocketIdentity identity, WebSocket socket, int maxMessageBytes, CancellationToken requestAborted )
	{
		Identity = identity;
		_socket = socket;
		_maxQueuedBytes = (long)maxMessageBytes * OutboxMessageBudget;
		_cts = CancellationTokenSource.CreateLinkedTokenSource( requestAborted );
		_outbox = Channel.CreateBounded<ReadOnlyMemory<byte>>( new BoundedChannelOptions( OutboxCapacity )
		{
			SingleReader = true, FullMode = BoundedChannelFullMode.Wait
		} );
	}

	public void Send( ReadOnlyMemory<byte> utf8Text )
	{
		if ( Volatile.Read( ref _closing ) != 0 )
		{
			return;
		}

		var queued = Interlocked.Add( ref _queuedBytes, utf8Text.Length );
		if ( queued <= _maxQueuedBytes && _outbox.Writer.TryWrite( utf8Text ) )
		{
			return;
		}

		// The client isn't keeping up; drop it rather than buffering without limit.
		Interlocked.Add( ref _queuedBytes, -utf8Text.Length );
		Close( WebSocketCloseStatus.PolicyViolation, "Too many pending messages" );
	}

	/// <summary>
	/// Sends a close frame after any queued messages and stops the connection if the client doesn't respond in time.
	/// </summary>
	public void Close( WebSocketCloseStatus status, string reason )
	{
		if ( Interlocked.Exchange( ref _closing, 1 ) != 0 )
		{
			return;
		}

		_closeStatus = status;
		_closeReason = reason;
		_outbox.Writer.TryComplete();

		try
		{
			_cts.CancelAfter( CloseTimeout );
		}
		catch ( ObjectDisposedException )
		{
			// connection already finished
		}
	}

	/// <summary>
	/// Runs the connection until either side closes it.
	/// </summary>
	public async Task RunAsync( int maxMessageBytes, RelayMessageHandler onMessage )
	{
		var sendLoop = SendLoopAsync();

		try
		{
			await ReceiveLoopAsync( maxMessageBytes, onMessage );
		}
		catch ( Exception e ) when ( e is OperationCanceledException or WebSocketException )
		{
			// client went away or didn't finish the close handshake
		}
		finally
		{
			Close( WebSocketCloseStatus.NormalClosure, "" );
			await sendLoop;
			_cts.Dispose();
		}
	}

	private async Task ReceiveLoopAsync( int maxMessageBytes, RelayMessageHandler onMessage )
	{
		var buffer = ArrayPool<byte>.Shared.Rent( 16 * 1024 );
		var message = new ArrayBufferWriter<byte>();

		try
		{
			while ( true )
			{
				var result = await _socket.ReceiveAsync( buffer.AsMemory(), _cts.Token );
				if ( result.MessageType == WebSocketMessageType.Close )
				{
					return;
				}

				if ( message.WrittenCount + result.Count > maxMessageBytes )
				{
					Close( WebSocketCloseStatus.MessageTooBig, "Message too large" );
					return;
				}

				message.Write( buffer.AsSpan( 0, result.Count ) );
				if ( !result.EndOfMessage )
				{
					continue;
				}

				if ( result.MessageType == WebSocketMessageType.Binary )
				{
					Send( RelayReplies.BinaryNotSupported );
				}
				else
				{
					onMessage( this, message.WrittenMemory );
				}

				message.ResetWrittenCount();
			}
		}
		finally
		{
			ArrayPool<byte>.Shared.Return( buffer );
		}
	}

	private async Task SendLoopAsync()
	{
		try
		{
			await foreach ( var data in _outbox.Reader.ReadAllAsync( _cts.Token ) )
			{
				await _socket.SendAsync( data, WebSocketMessageType.Text, true, _cts.Token );
				Interlocked.Add( ref _queuedBytes, -data.Length );
			}

			if ( _socket.State is WebSocketState.Open or WebSocketState.CloseReceived )
			{
				await _socket.CloseOutputAsync( _closeStatus, _closeReason, _cts.Token );
			}
		}
		catch ( Exception e ) when ( e is OperationCanceledException or WebSocketException or ObjectDisposedException )
		{
			// connection is gone
		}
		finally
		{
			if ( _socket.State is not (WebSocketState.Closed or WebSocketState.Aborted or WebSocketState.CloseSent) )
			{
				_socket.Abort();
			}

			// Unblock the receive loop if we failed to send.
			Close( WebSocketCloseStatus.InternalServerError, "" );
		}
	}
}

/// <summary>
/// The fixed replies sent back to whoever sent a message, kept from the original Web PubSub hub.
/// </summary>
public static class RelayReplies
{
	public static readonly ReadOnlyMemory<byte> Ack = "ack"u8.ToArray();
	public static readonly ReadOnlyMemory<byte> BinaryNotSupported = "fail:0"u8.ToArray();
	public static readonly ReadOnlyMemory<byte> InvalidJson = "fail:2"u8.ToArray();
	public static readonly ReadOnlyMemory<byte> MissingMessage = "fail:4"u8.ToArray();
}
