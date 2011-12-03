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
using System.IO;
using System.Linq;

namespace MsgPack.Rpc
{
	internal sealed class ByteArraySegmentStream : Stream
	{
		private readonly IList<ArraySegment<byte>> _segments;
		private int _segmentIndex;
		private int _offsetInCurrentSegment;

		public sealed override bool CanRead
		{
			get { return true; }
		}

		public sealed override bool CanSeek
		{
			get { return true; }
		}

		public sealed override bool CanWrite
		{
			get { return false; }
		}

		public sealed override long Length
		{
			get { return this._segments.Sum( item => ( long )item.Count ); }
		}

		public sealed override long Position
		{
			get
			{
				return this._segments.Take( this._segmentIndex ).Sum( item => ( long )item.Count ) + this._offsetInCurrentSegment;
			}
			set
			{
				if ( value < 0 )
				{
					throw new ArgumentOutOfRangeException( "value" );
				}

				var position = this.Position;
				this.Seek( value - position );
			}
		}

		public ByteArraySegmentStream( IList<ArraySegment<byte>> underlying )
		{
			this._segments = underlying;
		}

		public sealed override int Read( byte[] buffer, int offset, int count )
		{
			int remains = count;
			int result = 0;
			while ( 0 < remains && this._segmentIndex < this._segments.Count )
			{
				int copied = this._segments[ this._segmentIndex ].CopyTo( this._offsetInCurrentSegment, buffer, offset + result, remains );
				result += copied;
				remains -= copied;
				this._offsetInCurrentSegment += copied;
				if ( this._offsetInCurrentSegment == this._segments[ this._segmentIndex ].Count )
				{
					this._segmentIndex++;
					this._offsetInCurrentSegment = 0;
				}
			}

			return result;
		}

		public sealed override long Seek( long offset, SeekOrigin origin )
		{
			long position = this.Position;
			long length = this.Length;
			long offsetFromCurrent;
			switch ( origin )
			{
				case SeekOrigin.Begin:
				{
					offsetFromCurrent = offset - position;
					break;
				}
				case SeekOrigin.Current:
				{
					offsetFromCurrent = offset;
					break;
				}
				case SeekOrigin.End:
				{
					offsetFromCurrent = length + offset - position;
					break;
				}
				default:
				{
					throw new ArgumentOutOfRangeException( "origin" );
				}
			}

			if ( offsetFromCurrent + position < 0 || length < offsetFromCurrent + position )
			{
				throw new ArgumentOutOfRangeException( "offset" );
			}

			this.Seek( offsetFromCurrent );
			return offsetFromCurrent + position;
		}

		private void Seek( long offsetFromCurrent )
		{
#if DEBUG
			Contract.Assert( 0 <= offsetFromCurrent + this.Position, offsetFromCurrent + this.Position + " < 0" );
			Contract.Assert( offsetFromCurrent + this.Position <= this.Length, this.Length + " <= " + offsetFromCurrent + this.Position );
#endif

			if ( offsetFromCurrent < 0 )
			{
				for ( long i = 0; offsetFromCurrent < i; i-- )
				{
					if ( this._offsetInCurrentSegment == 0 )
					{
						this._segmentIndex--;
						Contract.Assert( 0 <= this._segmentIndex );
						this._offsetInCurrentSegment = this._segments[ this._segmentIndex ].Count - 1;
					}
					else
					{
						this._offsetInCurrentSegment--;
					}
				}
			}
			else
			{
				for ( long i = 0; i < offsetFromCurrent; i++ )
				{
					if ( this._offsetInCurrentSegment == this._segments[ this._segmentIndex ].Count - 1 )
					{
						this._segmentIndex++;
						Contract.Assert( this._segmentIndex <= this._segments.Count );
						this._offsetInCurrentSegment = 0;
					}
					else
					{
						this._offsetInCurrentSegment++;
					}
				}
			}
		}

		public sealed override void Flush()
		{
			// nop
		}

		public override void SetLength( long value )
		{
			throw new NotSupportedException();
		}

		public sealed override IAsyncResult BeginWrite( byte[] buffer, int offset, int count, AsyncCallback callback, object state )
		{
			throw new NotSupportedException();
		}

		public sealed override void EndWrite( IAsyncResult asyncResult )
		{
			throw new NotSupportedException();
		}

		public sealed override void Write( byte[] buffer, int offset, int count )
		{
			throw new NotSupportedException();
		}

		public sealed override void WriteByte( byte value )
		{
			throw new NotSupportedException();
		}
	}
}
