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
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace MsgPack.Rpc
{
	partial class RpcException : IStackTracePreservable
	{
		private List<string> _preservedStackTrace;

		void IStackTracePreservable.PreserveStackTrace()
		{
			if ( this._preservedStackTrace == null )
			{
				this._preservedStackTrace = new List<string>();
			}

			this._preservedStackTrace.Add( new StackTrace( this, true ).ToString() );
		}

		public override string StackTrace
		{
			get
			{
				if ( this._preservedStackTrace == null || this._preservedStackTrace.Count == 0 )
				{
					return base.StackTrace;
				}

				var buffer = new StringBuilder();
				foreach ( var preserved in this._preservedStackTrace )
				{
					buffer.Append( preserved );
					buffer.AppendLine( "   --- End of preserved stack trace ---" );
				}

				buffer.Append( base.StackTrace );
				return buffer.ToString();
			}
		}
	}
}
