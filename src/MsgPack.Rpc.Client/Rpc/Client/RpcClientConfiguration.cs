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
using System.Diagnostics.Contracts;
using System.Net;
using MsgPack.Rpc.Client.Protocols;

namespace MsgPack.Rpc.Client
{
	/// <summary>
	///		Represents client side configuration settings.
	/// </summary>
	public sealed partial class RpcClientConfiguration : FreezableObject
	{
		private static readonly RpcClientConfiguration _default = new RpcClientConfiguration().Freeze();

		/// <summary>
		///		Gets the default frozen instance.
		/// </summary>
		/// <value>
		///		The default frozen instance.
		///		This value will not be <c>null</c>.
		/// </value>
		public static RpcClientConfiguration Default
		{
			get
			{
				Contract.Ensures( Contract.Result<RpcClientConfiguration>() != null );

				return RpcClientConfiguration._default;
			}
		}

		/// <summary>
		///		Initializes a new instance of the <see cref="RpcClientConfiguration"/> class.
		/// </summary>
		public RpcClientConfiguration() { }

		/// <summary>
		///		Creates the <see cref="ObjectPoolConfiguration"/> for the transport pool corresponds to values of this instance.
		/// </summary>
		/// <returns>
		///		The <see cref="ObjectPoolConfiguration"/> for the transport pool corresponds to values of this instance.
		///		This value will not be <c>null</c>.
		/// </returns>
		public ObjectPoolConfiguration CreateTransportPoolConfiguration()
		{
			Contract.Ensures( Contract.Result<ObjectPoolConfiguration>() != null );

			return new ObjectPoolConfiguration() { ExhausionPolicy = ExhausionPolicy.BlockUntilAvailable, MaximumPooled = this.MaximumConcurrentRequest, MinimumReserved = this.MinimumConcurrentRequest };
		}

		/// <summary>
		///		Creates the <see cref="ObjectPoolConfiguration"/> for the <see cref="MsgPack.Rpc.Client.Protocols.ClientRequestContext"/> pool corresponds to values of this instance.
		/// </summary>
		/// <returns>
		///		The <see cref="ObjectPoolConfiguration"/> for the <see cref="MsgPack.Rpc.Client.Protocols.ClientRequestContext"/> pool corresponds to values of this instance.
		///		This value will not be <c>null</c>.
		/// </returns>
		public ObjectPoolConfiguration CreateRequestContextPoolConfiguration()
		{
			Contract.Ensures( Contract.Result<ObjectPoolConfiguration>() != null );

			return new ObjectPoolConfiguration() { ExhausionPolicy = ExhausionPolicy.BlockUntilAvailable, MaximumPooled = this.MaximumConcurrentRequest, MinimumReserved = this.MinimumConcurrentRequest };
		}

		/// <summary>
		///		Creates the <see cref="ObjectPoolConfiguration"/> for the <see cref="MsgPack.Rpc.Client.Protocols.ClientResponseContext"/> pool corresponds to values of this instance.
		/// </summary>
		/// <returns>
		///		The <see cref="ObjectPoolConfiguration"/> for the <see cref="MsgPack.Rpc.Client.Protocols.ClientResponseContext"/> pool corresponds to values of this instance.
		///		This value will not be <c>null</c>.
		/// </returns>
		public ObjectPoolConfiguration CreateResponseContextPoolConfiguration()
		{
			Contract.Ensures( Contract.Result<ObjectPoolConfiguration>() != null );

			return new ObjectPoolConfiguration() { ExhausionPolicy = ExhausionPolicy.BlockUntilAvailable, MaximumPooled = this.MaximumConcurrentRequest, MinimumReserved = this.MinimumConcurrentRequest };
		}

		/// <summary>
		///		Clones all of the fields of this instance.
		/// </summary>
		/// <returns>
		///		The shallow copy of this instance.
		/// </returns>
		public RpcClientConfiguration Clone()
		{
			Contract.Ensures( Contract.Result<RpcClientConfiguration>() != null );
			Contract.Ensures( !Object.ReferenceEquals( Contract.Result<RpcClientConfiguration>(), this ) );
			Contract.Ensures( Contract.Result<RpcClientConfiguration>().IsFrozen == this.IsFrozen );

			return this.CloneCore() as RpcClientConfiguration;
		}

		/// <summary>
		///		Freezes this instance.
		/// </summary>
		/// <returns>
		///		This instance.
		/// </returns>
		public RpcClientConfiguration Freeze()
		{
			Contract.Ensures( Object.ReferenceEquals( Contract.Result<RpcClientConfiguration>(), this ) );
			Contract.Ensures( this.IsFrozen );

			return this.FreezeCore() as RpcClientConfiguration;
		}

		/// <summary>
		/// Gets the frozen copy of this instance.
		/// </summary>
		/// <returns>
		/// This instance if it is already frozen.
		/// Otherwise, frozen copy of this instance.
		/// </returns>
		public RpcClientConfiguration AsFrozen()
		{
			Contract.Ensures( Contract.Result<RpcClientConfiguration>() != null );
			Contract.Ensures( !Object.ReferenceEquals( Contract.Result<RpcClientConfiguration>(), this ) );
			Contract.Ensures( Contract.Result<RpcClientConfiguration>().IsFrozen );
			Contract.Ensures( this.IsFrozen == Contract.OldValue( this.IsFrozen ) );

			return this.AsFrozenCore() as RpcClientConfiguration;
		}
	}
}
