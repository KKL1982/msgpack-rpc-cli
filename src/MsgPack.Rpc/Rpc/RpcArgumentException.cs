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
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.Serialization;
using MsgPack.Rpc.Protocols;

namespace MsgPack.Rpc
{
	/// <summary>
	///		Thrown if some arguments are wrong like its type was not match, its value was out of range, its value was null but it is not illegal, so on.
	/// </summary>
#if !SILVERLIGHT
	[Serializable]
#endif
	public sealed class RpcArgumentException : RpcMethodInvocationException
	{
		private const string _parameterNameKey = "ParameterName";
		private static readonly MessagePackObject _parameterNameKeyUtf8 = MessagePackConvert.EncodeString( _parameterNameKey );

		// NOT readonly for safe deserialization
		private string _parameterName;

		/// <summary>
		///		Gets the name of parameter causing this exception.
		/// </summary>
		/// <value>
		///		The mame of parameter causing this exception. This value will not be empty but may be <c>null</c>.
		/// </value>
		public string ParameterName
		{
			get
			{
				Contract.Ensures( Contract.Result<string>() != null );
				return this._parameterName ?? String.Empty;
			}
		}

		/// <summary>
		///		Initializes a new instance of the <see cref="RpcArgumentException"/> class with the default error message.
		/// </summary>
		///	<param name="methodName">
		///		Name of method which is related to this error.
		///	</param>
		///	<param name="parameterName">
		///		Name of parameter which is invalid.
		///	</param>
		/// <exception cref="ArgumentNullException">
		///		<paramref name="methodName"/> or <paramref name="parameterName"/> is null.
		/// </exception>
		/// <exception cref="ArgumentException">
		///		<paramref name="methodName"/> or <paramref name="parameterName"/> is empty or blank.
		/// </exception>
		public RpcArgumentException( string methodName, string parameterName )
			: this( methodName, parameterName, null, null, null ) { }

		/// <summary>
		///		Initializes a new instance of the <see cref="RpcArgumentException"/> class with a specified error message.
		/// </summary>
		///	<param name="methodName">
		///		Name of method which is related to this error.
		///	</param>
		///	<param name="parameterName">
		///		Name of parameter which is invalid.
		///	</param>
		/// <param name="message">
		///		Error message to desribe condition. Note that this message should not include security related information.
		///	</param>
		/// <param name="debugInformation">
		///		Debug information of error.
		///		This value can be null for security reason, and its contents are for developers, not end users.
		/// </param>
		/// <exception cref="ArgumentNullException">
		///		<paramref name="methodName"/> or <paramref name="parameterName"/> is null.
		/// </exception>
		/// <exception cref="ArgumentException">
		///		<paramref name="methodName"/> or <paramref name="parameterName"/> is empty or blank.
		/// </exception>
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
		public RpcArgumentException( string methodName, string parameterName, string message, string debugInformation )
			: this( methodName, parameterName, message, debugInformation, null ) { }

		/// <summary>
		///		Initializes a new instance of the <see cref="RpcArgumentException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception. 
		/// </summary>
		///	<param name="methodName">
		///		Name of method which is related to this error.
		///	</param>
		///	<param name="parameterName">
		///		Name of parameter which is invalid.
		///	</param>
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
		/// <exception cref="ArgumentNullException">
		///		<paramref name="methodName"/> or <paramref name="parameterName"/> is null.
		/// </exception>
		/// <exception cref="ArgumentException">
		///		<paramref name="methodName"/> or <paramref name="parameterName"/> is empty or blank.
		/// </exception>
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
		public RpcArgumentException( string methodName, string parameterName, string message, string debugInformation, Exception inner )
			: base( RpcError.ArgumentError, methodName, message ?? RpcError.ArgumentError.DefaultMessage, debugInformation, inner )
		{
			if ( parameterName == null )
			{
				throw new ArgumentNullException( "parameterName" );
			}

			if ( String.IsNullOrWhiteSpace( parameterName ) )
			{
				throw new ArgumentException( "'parameterName' cannot be empty.", "parameterName" );
			}

			Contract.EndContractBlock();

			this._parameterName = parameterName;
		}

		/// <summary>
		///		Initializes a new instance of the <see cref="RpcException"/> class with the unpacked data.
		/// </summary>
		/// <param name="unpackedException">
		///		Exception data from remote MessagePack-RPC server.
		///	</param>
		/// <exception cref="SerializationException">
		///		Cannot deserialize instance from <paramref name="unpackedException"/>.
		/// </exception>
		internal RpcArgumentException( MessagePackObject unpackedException )
			: base( RpcError.ArgumentError, unpackedException )
		{
			this._parameterName = unpackedException.GetString( _parameterNameKeyUtf8 );
		}

		/// <summary>
		///		Stores derived type specific information to specified dictionary.
		/// </summary>
		/// <param name="store">
		///		Dictionary to be stored. This value will not be <c>null</c>.
		///	</param>
		/// <param name="includesDebugInformation">
		///		<c>true</c>, when this method should include debug information; otherwise, <c>false</c>.
		///	</param>
		protected sealed override void GetExceptionMessage( IDictionary<MessagePackObject, MessagePackObject> store, bool includesDebugInformation )
		{
			base.GetExceptionMessage( store, includesDebugInformation );
			store.Add( _parameterNameKeyUtf8, MessagePackConvert.EncodeString( this._parameterName ) );
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
					ParameterName = this._parameterName
				}
			);
		}

		[Serializable]
		private sealed class SerializedState : ISafeSerializationData
		{
			public string ParameterName;

			public void CompleteDeserialization( object deserialized )
			{
				var enclosing = deserialized as RpcArgumentException;
				enclosing._parameterName = this.ParameterName;
			}
		}
#endif
	}
}
