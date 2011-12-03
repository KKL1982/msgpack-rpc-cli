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
using System.Diagnostics;

namespace MsgPack.Rpc.Server
{
	internal static class Tracer
	{
		private static readonly TraceSource _server = new TraceSource( "MsgPack.Rpc.Server" );
		public static TraceSource Server
		{
			get { return _server; }
		}

		private static readonly TraceSource _protocols = new TraceSource( "MsgPack.Rpc.Server.Prptocols" );
		public static TraceSource Protocols
		{
			get { return _protocols; }
		}

		public static class EventType
		{
			public const TraceEventType StartServer = TraceEventType.Start;
			public const TraceEventType StartListen = TraceEventType.Start;
			public const TraceEventType UnexpectedLastOperation = TraceEventType.Warning;
			public const TraceEventType BoundSocket = TraceEventType.Start;
			public const TraceEventType CloseTransport = TraceEventType.Stop;
			public const TraceEventType AcceptInboundTcp = TraceEventType.Verbose;
			public const TraceEventType ReceiveInboundData = TraceEventType.Verbose;
			public const TraceEventType DeserializeRequest = TraceEventType.Verbose;
			public const TraceEventType DispatchRequest = TraceEventType.Verbose;
			public const TraceEventType SerializeResponse = TraceEventType.Verbose;
			public const TraceEventType SendOutboundData = TraceEventType.Verbose;
			public const TraceEventType SentOutboundData = TraceEventType.Verbose;
		}

		public static class EventId
		{
			public const int StartServer = 1;
			public const int StartListen = 2;
			public const int BoundSocket = 1001;
			public const int CloseTransport = 1002;
			public const int UnexpectedLastOperation = 1091;
			public const int AcceptInboundTcp = 1201;
			public const int ReceiveInboundData = 1101;
			public const int DeserializeRequest = 1111;
			public const int DispatchRequest = 1131;
			public const int SerializeResponse = 1141;
			public const int SendOutboundData = 1151;
			public const int SentOutboundData = 1152;
		}
	}
}
