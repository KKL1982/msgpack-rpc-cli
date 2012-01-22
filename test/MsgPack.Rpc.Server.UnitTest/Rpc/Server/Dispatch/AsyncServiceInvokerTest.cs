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
using NUnit.Framework;
using System.Threading.Tasks;
using MsgPack.Rpc.Server.Protocols;
using MsgPack.Serialization;
using System.Linq;

namespace MsgPack.Rpc.Server.Dispatch
{
	/// <summary>
	///Tests the Async Service Invoker 
	/// </summary>
	[TestFixture()]
	public class AsyncServiceInvokerTest
	{
		[Test()]
		public void TestInvokeAsync_Success_TaskSetSerializedReturnValue()
		{
			ServerRequestContext requestContext = DispatchTestHelper.CreateRequestContext();
			ServerResponseContext responseContext = new ServerResponseContext();
			using ( var result = new Target( null, RpcErrorMessage.Success ).InvokeAsync( requestContext, responseContext ) )
			{
				result.Wait();
			}

			Assert.That( responseContext.GetReturnValueData(), Is.EqualTo( new byte[] { 123 } ) );
			Assert.That( responseContext.GetErrorData(), Is.EqualTo( new byte[] { 0xC0 } ) );
		}

		[Test()]
		public void TestInvokeAsync_FatalError_TaskSetSerializedError()
		{
			ServerRequestContext requestContext = DispatchTestHelper.CreateRequestContext();
			ServerResponseContext responseContext = new ServerResponseContext();
			using ( var result = new Target( new Exception( "FAIL" ), RpcErrorMessage.Success ).InvokeAsync( requestContext, responseContext ) )
			{
				result.Wait();
			}

			var error = Unpacking.UnpackObject( responseContext.GetErrorData() );
			var errorDetail = Unpacking.UnpackObject( responseContext.GetReturnValueData() );
			Assert.That( error.Value.Equals( RpcError.CallError.Identifier ) );
			Assert.That( errorDetail.Value.IsNil, Is.False );
		}

		[Test()]
		public void TestInvokeAsync_MethodError_TaskSetSerializedError()
		{
			ServerRequestContext requestContext = DispatchTestHelper.CreateRequestContext();
			ServerResponseContext responseContext = new ServerResponseContext();
			using ( var result = new Target( null, new RpcErrorMessage( RpcError.ArgumentError, MessagePackObject.Nil ) ).InvokeAsync( requestContext, responseContext ) )
			{
				result.Wait();
			}

			var error = Unpacking.UnpackObject( responseContext.GetErrorData() );
			var errorDetail = Unpacking.UnpackObject( responseContext.GetReturnValueData() );
			Assert.That( error.Value.Equals( RpcError.ArgumentError.Identifier ) );
			Assert.That( errorDetail.Value.IsNil, Is.True );
		}

		private sealed class Target : AsyncServiceInvoker<int>
		{
			private readonly Exception _fatalError;
			private readonly RpcErrorMessage _methodError;

			public Target( Exception fatalError, RpcErrorMessage methodError )
				: base( RpcServerConfiguration.Default, new SerializationContext(), new ServiceDescription( "Dummy", () => new object() ), typeof( object ).GetMethod( "ToString" ) )
			{
				this._fatalError = fatalError;
				this._methodError = methodError;
			}

			protected override void InvokeCore( Unpacker arguments, out Task task, out RpcErrorMessage error )
			{
				if ( this._fatalError != null )
				{
					throw this._fatalError;
				}

				if ( this._methodError.IsSuccess )
				{
					task = Task.Factory.StartNew( () => 123 );
					error = RpcErrorMessage.Success;
				}
				else
				{
					task = Task.Factory.StartNew( () => 1 );
					error = this._methodError;
				}
			}
		}

	}
}
