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
using System.Runtime.Serialization;

namespace MsgPack.Rpc
{
	/// <summary>
	///		Thrown when RPC invocation was time out.
	/// </summary>
#if !SILVERLIGHT
	[Serializable]
#endif
	public sealed class RpcTimeoutException : RpcException
	{
		private const string _clientTimeoutKey = "ClientTimeout";
		private static readonly MessagePackObject _clientTimeoutKeyUtf8 = MessagePackConvert.EncodeString( _clientTimeoutKey );

		// NOT readonly for safe deserialization
		private TimeSpan? _clientTimeout;

		/// <summary>
		///		Gets the timeout value which was expired in client.
		/// </summary>
		/// <value>The timeout value in client. This value may be <c>null</c> when the server turnes</value>
		public TimeSpan? ClientTimeout
		{
			get { return this._clientTimeout; }
		}

		/// <summary>
		///		Initializes a new instance of the <see cref="RpcTimeoutException"/> class with the default error message.
		/// </summary>
		/// <param name="timeout">Timeout value in client.</param>
		public RpcTimeoutException( TimeSpan timeout ) : this( timeout, null, null, null ) { }

		/// <summary>
		///		Initializes a new instance of the <see cref="RpcTimeoutException"/> class with a specified error message.
		/// </summary>
		/// <param name="timeout">Timeout value in client.</param>
		/// <param name="message">
		///		Error message to desribe condition. Note that this message should not include security related information.
		///	</param>
		/// <param name="debugInformation">
		///		Debug information of error.
		///		This value can be null for security reason, and its contents are for developers, not end users.
		/// </param>
		/// <remarks>
		///		<para>
		///			For example, if some exception is occurred in server application,
		///			the value of <see cref="Exception.ToString()"/> should specify for <paramref name="debugInformation"/>.
		///			And then, user-friendly, safe message should be specified to <paramref name="message"/> like 'Internal Error."
		///		</para>
		///		<para>
		///			MessagePack-RPC for CLI runtime does not propagate <see cref="RpcException.DebugInformation"/> for remote endpoint.
		///			So you should specify some error handler to instrument it (e.g. logging handler).
		///		</para>
		/// </remarks>
		public RpcTimeoutException( TimeSpan timeout, string message, string debugInformation )
			: this( timeout, message, debugInformation, null ) { }

		/// <summary>
		///		Initializes a new instance of the <see cref="RpcTimeoutException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception. 
		/// </summary>
		/// <param name="timeout">Timeout value in client.</param>
		/// <param name="message">
		///		Error message to desribe condition. Note that this message should not include security related information.
		///	</param>
		/// <param name="debugInformation">
		///		Debug information of error.
		///		This value can be null for security reason, and its contents are for developers, not end users.
		/// </param>
		/// <param name="inner">
		///		Exception which caused this error.
		/// </param>
		/// <remarks>
		///		<para>
		///			For example, if some exception is occurred in server application,
		///			the value of <see cref="Exception.ToString()"/> should specify for <paramref name="debugInformation"/>.
		///			And then, user-friendly, safe message should be specified to <paramref name="message"/> like 'Internal Error."
		///		</para>
		///		<para>
		///			MessagePack-RPC for CLI runtime does not propagate <see cref="RpcException.DebugInformation"/> for remote endpoint.
		///			So you should specify some error handler to instrument it (e.g. logging handler).
		///		</para>
		/// </remarks>
		public RpcTimeoutException( TimeSpan timeout, string message, string debugInformation, Exception inner )
			: base( RpcError.TimeoutError, message, debugInformation, inner )
		{
			this._clientTimeout = timeout;
		}

		/// <summary>
		///		Initializes a new instance of the <see cref="RpcTimeoutException"/> class with the unpacked data.
		/// </summary>
		/// <param name="unpackedException">
		///		Exception data from remote MessagePack-RPC server.
		///	</param>
		/// <exception cref="SerializationException">
		///		Cannot deserialize instance from <paramref name="unpackedException"/>.
		/// </exception>
		internal RpcTimeoutException( MessagePackObject unpackedException )
			: base( RpcError.TimeoutError, unpackedException )
		{
			this._clientTimeout = unpackedException.GetTimeSpan( _clientTimeoutKeyUtf8 );
		}

#if !SILVERLIGHT
		/// <summary>
		///		When overridden on the derived class, handles <see cref="E:Exception.SerializeObjectState"/> event to add type-specified serialization state.
		/// </summary>
		/// <param name="sender">The <see cref="Exception"/> instance itself.</param>
		/// <param name="e">
		///		The <see cref="System.Runtime.Serialization.SafeSerializationEventArgs"/> instance containing the event data.
		///		The overriding method adds its internal state to this object via <see cref="M:SafeSerializationEventArgs.AddSerializedState"/>.
		///	</param>
		/// <seealso cref="ISafeSerializationData"/>
		protected override void OnSerializeObjectState( object sender, SafeSerializationEventArgs e )
		{
			base.OnSerializeObjectState( sender, e );
			e.AddSerializedState(
				new SerializedState()
				{
					ClientTimeout = this._clientTimeout
				}
			);
		}

		[Serializable]
		private sealed class SerializedState : ISafeSerializationData
		{
			public TimeSpan? ClientTimeout;

			public void CompleteDeserialization( object deserialized )
			{
				var enclosing = deserialized as RpcTimeoutException;
				enclosing._clientTimeout = this.ClientTimeout;
			}
		}
#endif
	}
}
