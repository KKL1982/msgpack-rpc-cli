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
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace MsgPack.Rpc.Server.Protocols
{
	public abstract class ServerTransportManager<TTransport> : ServerTransportManager
		where TTransport : ServerTransport, ILeaseable<TTransport>
	{
		private readonly ConcurrentDictionary<TTransport, object> _activeTransports;

		private ObjectPool<TTransport> _transportPool;

		protected ServerTransportManager( RpcServer server )
			: base( server )
		{
			this._activeTransports =
				new ConcurrentDictionary<TTransport, object>(
					server.Configuration.MinimumConnection,
					server.Configuration.MaximumConnection
				);
		}

		protected void SetTransportPool( ObjectPool<TTransport> transportPool )
		{
			if ( transportPool == null )
			{
				throw new ArgumentNullException( "transportPool" );
			}

			if ( this._transportPool != null )
			{
				throw new InvalidOperationException( "Already set." );
			}

			this._transportPool = transportPool;
		}

		protected override void BeginShutdownCore()
		{
			foreach ( var transport in this._activeTransports )
			{
				transport.Key.BeginShutdown();
			}

			base.BeginShutdownCore();
		}

		protected TTransport GetTransport( Socket bindingSocket )
		{
			if ( bindingSocket == null )
			{
				throw new ArgumentNullException( "bindingSocket" );
			}

			if ( this._transportPool == null )
			{
				throw new InvalidOperationException( "Transport pool must be set via SetTransportPool()." );
			}

			TTransport transport;
			try { }
			finally
			{
				transport = this._transportPool.Borrow();
				transport.BoundSocket = bindingSocket;
				this._activeTransports.TryAdd( transport, null );
			}

			return transport;
		}

		internal sealed override void ReturnTransport( ServerTransport transport )
		{
			this.ReturnTransport( ( TTransport )transport );
		}

		protected void ReturnTransport( TTransport transport )
		{
			if ( transport == null )
			{
				throw new ArgumentNullException( "transport" );
			}

			if ( !Object.ReferenceEquals( this, transport.Manager ) )
			{
				throw new ArgumentException( "The specified transport is not owned by this manager.", "transport" );
			}

			if ( this._transportPool == null )
			{
				throw new InvalidOperationException( "Transport pool must be set via SetTransportPool()." );
			}

			try { }
			finally
			{
				object dummy;
				this._activeTransports.TryRemove( transport, out dummy );
				this._transportPool.Return( transport );
			}
		}
		
		protected void ProcessRequet( TTransport transport )
		{
			var requestContext = this.RequestContextPool.Borrow();
			requestContext.SetTransport( transport );
			transport.Receive( requestContext );
		}
	}
}
