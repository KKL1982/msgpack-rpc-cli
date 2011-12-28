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

namespace MsgPack.Rpc.Server.Protocols
{
	/// <summary>
	///		Holds context information of TCP socket listening.
	/// </summary>
	public sealed class ListeningContext : SocketAsyncEventArgs, ILeaseable<ListeningContext>
	{
		private ILease<ListeningContext> _lease;

		void ILeaseable<ListeningContext>.SetLease( ILease<ListeningContext> lease )
		{
			this._lease = lease;
		}

		public ListeningContext() { }

		internal void Return()
		{
			try { }
			finally
			{
				var lease = Interlocked.Exchange( ref this._lease, null );
				if ( lease != null )
				{
					lease.Dispose();
				}
			}
		}
	}
}
