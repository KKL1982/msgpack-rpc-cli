#region -- License Terms --
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
using System.Net.Sockets;
using System.Threading;
using MsgPack.Rpc.Protocols;

namespace MsgPack.Rpc.Server.Protocols
{
	public abstract class ServerTransportManager : IDisposable
	{
		private readonly ObjectPool<ServerRequestSocketAsyncEventArgs> _requestContextPool;

		public ObjectPool<ServerRequestSocketAsyncEventArgs> RequestContextPool
		{
			get { return this._requestContextPool; }
		}

		private readonly ObjectPool<ServerResponseSocketAsyncEventArgs> _responseContextPool;

		public ObjectPool<ServerResponseSocketAsyncEventArgs> ResponseContextPool
		{
			get { return this._responseContextPool; }
		}

		private readonly RpcServerConfiguration _configuration;

		protected RpcServerConfiguration Configuration
		{
			get { return this._configuration; }
		}

		private bool _isDisposed;

		protected bool IsDisposed
		{
			get { return this._isDisposed; }
		}

		private bool _isInShutdown;

		public bool IsInShutdown
		{
			get { return this._isInShutdown; }
		}

		public event EventHandler<RpcTransportErrorEventArgs> Error;

		protected virtual void OnError( RpcTransportErrorEventArgs e )
		{
			if ( e == null )
			{
				throw new ArgumentNullException( "e" );
			}

			this.OnErrorCore( e );
		}

		internal void OnErrorCore( RpcTransportErrorEventArgs e )
		{
			var handler = this.Error;
			if ( handler != null )
			{
				handler( this, e );
			}
		}

		protected ServerTransportManager( RpcServer server )
		{
			if ( server == null )
			{
				throw new ArgumentNullException( "server" );
			}

			this._configuration = server.Configuration;
			this._requestContextPool = server.Configuration.RequestContextPoolProvider( () => new ServerRequestSocketAsyncEventArgs() );
			this._responseContextPool = server.Configuration.ResponseContextPoolProvider( () => new ServerResponseSocketAsyncEventArgs() );
		}

		public void Dispose()
		{
			this.Dispose( true );
			GC.SuppressFinalize( this );
		}

		protected virtual void Dispose( bool disposing )
		{
			this._isDisposed = true;
			Thread.MemoryBarrier();
		}

		public void BeginShutdown()
		{
			this.BeginShutdownCore();
		}

		protected virtual void BeginShutdownCore()
		{
			this._isInShutdown = true;
			Thread.MemoryBarrier();
		}

		protected internal bool HandleError( object sender, SocketAsyncEventArgs e )
		{
			bool? isError = e.SocketError.IsError();
			if ( isError == null )
			{
				Tracer.Protocols.TraceEvent(
					Tracer.EventType.IgnoreableError,
					Tracer.EventId.IgnoreableError,
					"Ignoreable error. [ \"Socket.Handle\" : 0x{0:x}, \"Remote endpoint\" : \"{1}\", \"LastOperation\" : \"{2}\", \"SocketError\" : \"{3}\", \"ErrorCode\" : 0x{4:x8} ]",
					( sender as Socket ).Handle,
					e.RemoteEndPoint,
					e.LastOperation,
					e.SocketError,
					( int )e.SocketError
				);
				return true;
			}
			else if ( isError.GetValueOrDefault() )
			{
				this.HandleError( e.LastOperation, e.SocketError );
				return false;
			}

			return true;
		}

		internal void HandleError( SocketAsyncOperation operation, SocketError socketErrorCode )
		{
			this.OnErrorCore( new RpcTransportErrorEventArgs( operation, socketErrorCode ) );
		}

		internal abstract void ReturnTransport( ServerTransport transport );
	}
}
