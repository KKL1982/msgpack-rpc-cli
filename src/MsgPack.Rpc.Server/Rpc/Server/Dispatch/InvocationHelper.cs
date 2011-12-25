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
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using MsgPack.Serialization;
using MsgPack.Rpc.Protocols;
using System.Diagnostics.Contracts;

namespace MsgPack.Rpc.Server.Dispatch
{
	/// <summary>
	///		<strong>This class is intended to MessagePack-RPC internal use.</strong>
	///		Defines helper methods for current service invoker implementation.
	/// </summary>
	[EditorBrowsable( EditorBrowsableState.Never )]
	public static class InvocationHelper
	{
		/// <summary>
		///		<see cref="MethodInfo"/> of <see cref="HandleArgumentDeserializationException"/>.
		/// </summary>
		internal static readonly MethodInfo HandleArgumentDeserializationExceptionMethod =
			FromExpression.ToMethod( ( Exception exception, string parameterName ) => HandleArgumentDeserializationException( exception, parameterName ) );

		/// <summary>
		///		<see cref="MethodInfo"/> of <see cref="HandleInvocationException"/>.
		/// </summary>
		internal static readonly MethodInfo HandleInvocationExceptionMethod =
			FromExpression.ToMethod( ( Exception exception ) => HandleInvocationException( exception ) );

		/// <summary>
		///		<strong>This member is intended to MessagePack-RPC internal use.</strong>
		///		Convert specified argument deserialization error to the RPC error.
		/// </summary>
		/// <param name="exception">The exception thrown by unpacker.</param>
		/// <param name="parameterName">The parameter name failed to deserialize.</param>
		/// <returns>
		///		<see cref="RpcErrorMessage"/>.
		/// </returns>
		[EditorBrowsable( EditorBrowsableState.Never )]
		public static RpcErrorMessage HandleArgumentDeserializationException( Exception exception, string parameterName )
		{
			return new RpcErrorMessage( RpcError.ArgumentError, String.Format( CultureInfo.CurrentCulture, "Argument '{0}' is invalid.", exception.ToString() ) );
		}

		/// <summary>
		///		<strong>This member is intended to MessagePack-RPC internal use.</strong>
		///		Convert the exception thrown by target method to the RPC error.
		/// </summary>
		/// <param name="exception">The exception thrown by target method.</param>
		/// <returns>
		///		<see cref="RpcErrorMessage"/>.
		/// </returns>
		[EditorBrowsable( EditorBrowsableState.Never )]
		public static RpcErrorMessage HandleInvocationException( Exception exception )
		{
			// FIXME: More precise
			RpcException rpcException;
			if ( exception is ArgumentException )
			{
				return new RpcErrorMessage( RpcError.ArgumentError, exception.Message, exception.ToString() );
			}
			else if ( ( rpcException = exception as RpcException ) != null )
			{
				return new RpcErrorMessage( rpcException.RpcError, rpcException.Message, rpcException.DebugInformation );
			}
			else
			{
				return new RpcErrorMessage( RpcError.RemoteRuntimeError, RpcError.RemoteRuntimeError.DefaultMessageInvariant, exception.ToString() );
			}
		}

		internal static void TraceInvocationResult<T>( MessageType messageType, int messageId, string operationId, RpcErrorMessage error, T result )
		{
			if ( error.IsSuccess )
			{
				if ( Tracer.Server.Switch.ShouldTrace( Tracer.EventType.OperationSucceeded ) )
				{
					// FIXME: Formatting
					Tracer.Server.TraceData(
						Tracer.EventType.OperationSucceeded,
						Tracer.EventId.OperationSucceeded,
						messageType,
						messageId,
						operationId,
						result
					);
				}
			}
			else
			{
				Tracer.Server.TraceData(
					Tracer.EventType.OperationFailed,
					Tracer.EventId.OperationFailed,
					messageType,
					messageId,
					operationId,
					error.ToString()
				);
			}
		}

		internal static void SerializeResponse<T>( Packer responsePacker, int messageId, RpcErrorMessage error, T returnValue, MessagePackSerializer<T> returnValueSerializer )
		{
			// Not notification
			responsePacker.PackArrayHeader( 4 );
			responsePacker.Pack( 1 );
			responsePacker.Pack( messageId );
			responsePacker.Pack( error.Error.Identifier );
			if ( error.IsSuccess )
			{
				Contract.Assert( returnValueSerializer != null );

				if ( returnValueSerializer == null )
				{
					// void
					responsePacker.PackNull();
				}
				else
				{
					returnValueSerializer.PackTo( responsePacker, returnValue );
				}
			}
			else
			{
				responsePacker.Pack( error.Detail );
			}
		}
	}
}
