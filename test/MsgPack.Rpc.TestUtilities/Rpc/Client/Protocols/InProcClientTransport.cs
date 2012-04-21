﻿#region -- License Terms --
//
// MessagePack for CLI
//
// Copyright (C) 2010 FUJIWARA, Yusuke
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//
#endregion -- License Terms --

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using MsgPack.Rpc.Protocols;
using MsgPack.Rpc.Server.Protocols;

namespace MsgPack.Rpc.Client.Protocols
{
	/// <summary>
	///		Implements <see cref="ClientTransport"/> with in-proc method invocation.
	/// </summary>
	/// <remarks>
	///		This transport only support one session per manager.
	/// </remarks>
	public sealed class InProcClientTransport : ClientTransport
	{
		/// <summary>
		///		The queue for inbound data.
		/// </summary>
		private readonly BlockingCollection<byte[]> _inboundQueue;

		/// <summary>
		///		The queue to store pending data.
		/// </summary>
		private readonly ConcurrentQueue<InProcPacket> _pendingPackets;

		private InProcServerTransport _destination;

		private readonly InProcClientTransportManager _manager;

		/// <summary>
		///		Occurs when message sent.
		/// </summary>
		public event EventHandler<InProcMessageSentEventArgs> MessageSent;

		/// <summary>
		///		Occurs when response received.
		/// </summary>
		public event EventHandler<InProcResponseReceivedEventArgs> ResponseReceived;

		/// <summary>
		///		Initializes a new instance of the <see cref="InProcClientTransport"/> class.
		/// </summary>
		/// <param name="manager">The manager which will manage this instance.</param>
		/// <exception cref="ArgumentNullException">
		///		<paramref name="manager"/> is <c>null</c>.
		/// </exception>
		public InProcClientTransport( InProcClientTransportManager manager )
			: base( manager )
		{
			this._manager = manager;
			this._inboundQueue = new BlockingCollection<byte[]>();
			this._pendingPackets = new ConcurrentQueue<InProcPacket>();
		}

		protected override void Dispose( bool disposing )
		{
			if ( disposing )
			{
				var destination = Interlocked.Exchange( ref this._destination, null );
				if ( destination != null )
				{
					destination.Response -= this.OnDestinationResponse;
				}
			}

			base.Dispose( disposing );
		}

		internal void SetDestination( InProcServerTransport destination )
		{
			this._destination = destination;
			destination.Response += this.OnDestinationResponse;
		}

		private void OnDestinationResponse( object sender, InProcResponseEventArgs e )
		{
			var handler = this.ResponseReceived;

			if ( handler == null )
			{
				this._inboundQueue.Add( e.Data, this._manager.CancellationToken );
				return;
			}

			var eventArgs = new InProcResponseReceivedEventArgs( e.Data );
			handler( this, eventArgs );
			if ( eventArgs.ChunkedReceivedData == null )
			{
				this._inboundQueue.Add( e.Data, this._manager.CancellationToken );
			}
			else
			{
				foreach ( var data in eventArgs.ChunkedReceivedData )
				{
					this._inboundQueue.Add( data, this._manager.CancellationToken );
				}
			}
		}

		protected sealed override void ShutdownSending()
		{
			this._destination.FeedData( new byte[ 0 ] );

			base.ShutdownSending();
		}

		protected sealed override void SendCore( ClientRequestContext context )
		{
			var destination = this._destination;
			if ( destination == null )
			{
				throw new ObjectDisposedException( this.ToString() );
			}

			destination.FeedData( context.BufferList.SelectMany( segment => segment.Array.Skip( segment.Offset ).Take( segment.Count ) ).ToArray() );

			var handler = this.MessageSent;
			if ( handler != null )
			{
				handler( this, new InProcMessageSentEventArgs( context ) );
			}

			this.OnSent( context );
		}

		protected sealed override void ReceiveCore( ClientResponseContext context )
		{
			InProcPacket.ProcessReceive( this._inboundQueue, this._pendingPackets, context, this._manager.CancellationToken );
			this.OnReceived( context );
		}
	}
}
