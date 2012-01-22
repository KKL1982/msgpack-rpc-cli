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
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MsgPack.Serialization;
using NLiblet.Reflection;

namespace MsgPack.Rpc.Server.Dispatch
{
	/// <summary>
	///		Generates <see cref="ServiceInvoker{T}"/> implementation.
	/// </summary>
	internal sealed class ServiceInvokerGenerator : IDisposable
	{
		private static readonly ConstructorInfo _debuggableAttributeCtor =
			typeof( DebuggableAttribute ).GetConstructor( new[] { typeof( bool ), typeof( bool ) } );
		private static readonly object[] _debuggableAttributeCtorArguments = new object[] { true, true };
		private static readonly MethodInfo _func_1_Invoke =
			FromExpression.ToMethod( ( Func<object> @this ) => @this.Invoke() );
		private static readonly FieldInfo _missingValueProperty =
			typeof( Missing ).GetField( "Value" );
		private static readonly PropertyInfo _rpcErrorMessageSuccessProperty =
			FromExpression.ToProperty( () => RpcErrorMessage.Success );
		private static readonly MethodInfo _unpackerReadSubtreeMethod =
			FromExpression.ToMethod( ( Unpacker unpacker ) => unpacker.ReadSubtree() );
		private static readonly MethodInfo _unpackerReadMethod =
			FromExpression.ToMethod( ( Unpacker unpacker ) => unpacker.Read() );
		private static readonly PropertyInfo _cultureInfoCurrentCultureProperty =
			FromExpression.ToProperty( () => CultureInfo.CurrentCulture );
		private static readonly MethodInfo _stringFormatMethod =
			FromExpression.ToMethod( ( IFormatProvider formatProvider, String format, object[] args ) => String.Format( formatProvider, format, args ) );
		private static readonly ConstructorInfo _serializationExceptionCtorStringConstructor =
			FromExpression.ToConstructor( ( string message ) => new SerializationException( message ) );
		private static readonly MethodInfo _idisposableDisposeMethod =
			FromExpression.ToMethod( ( IDisposable disposable ) => disposable.Dispose() );


		private static ServiceInvokerGenerator _default = new ServiceInvokerGenerator( false );

		/// <summary>
		///		Gets the default instance.
		/// </summary>
		/// <value>
		///		The default instance. 
		///		This value will not be <c>null</c>.
		/// </value>
		public static ServiceInvokerGenerator Default
		{
			get
			{
				Contract.Ensures( Contract.Result<ServiceInvokerGenerator>() != null );

				return _default;
			}
			internal set  // For test
			{
				if ( value != null )
				{
					_default = value;
				}
			}
		}

		internal void Dump()
		{
			this._assemblyBuilder.Save( this._assemblyName.Name + ".dll" );
		}

		private static long _assemblySequence;
		private long _typeSequence;
		private readonly AssemblyName _assemblyName;

		// For debugging purposes.
		internal AssemblyName AssemblyName
		{
			get { return this._assemblyName; }
		}

		private readonly AssemblyBuilder _assemblyBuilder;
		private readonly ModuleBuilder _moduleBuilder;
		private readonly ReaderWriterLockSlim _lock;
		private readonly IDictionary<RuntimeMethodHandle, IAsyncServiceInvoker> _cache;
		private readonly bool _isDebuggable;

		internal ServiceInvokerGenerator( bool isDebuggable )
		{
			this._isDebuggable = isDebuggable;
			this._assemblyName = new AssemblyName( "SeamGeneratorHolder" + Interlocked.Increment( ref _assemblySequence ) );
			this._assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly( this._assemblyName, isDebuggable ? AssemblyBuilderAccess.RunAndSave : AssemblyBuilderAccess.Run );

			if ( isDebuggable )
			{
				this._assemblyBuilder.SetCustomAttribute( new CustomAttributeBuilder( _debuggableAttributeCtor, _debuggableAttributeCtorArguments ) );
			}
			else
			{
				this._assemblyBuilder.SetCustomAttribute(
					new CustomAttributeBuilder(
						typeof( DebuggableAttribute ).GetConstructor( new[] { typeof( DebuggableAttribute.DebuggingModes ) } ),
						new object[] { DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints }
					)
				);
			}

			this._assemblyBuilder.SetCustomAttribute(
				new CustomAttributeBuilder(
					typeof( System.Runtime.CompilerServices.CompilationRelaxationsAttribute ).GetConstructor( new[] { typeof( int ) } ),
					new object[] { 8 }
				)
			);
#if !SILVERLIGHT
			this._assemblyBuilder.SetCustomAttribute(
				new CustomAttributeBuilder(
					typeof( System.Security.SecurityRulesAttribute ).GetConstructor( new[] { typeof( System.Security.SecurityRuleSet ) } ),
					new object[] { System.Security.SecurityRuleSet.Level2 },
					new[] { typeof( System.Security.SecurityRulesAttribute ).GetProperty( "SkipVerificationInFullTrust" ) },
					new object[] { true }
				)
			);
#endif
			if ( isDebuggable )
			{
				this._moduleBuilder = this._assemblyBuilder.DefineDynamicModule( this._assemblyName.Name, this._assemblyName.Name + ".dll", true );
			}
			else
			{
				this._moduleBuilder = this._assemblyBuilder.DefineDynamicModule( this._assemblyName.Name, true );
			}

			this._cache = new Dictionary<RuntimeMethodHandle, IAsyncServiceInvoker>();
			this._lock = new ReaderWriterLockSlim( LockRecursionPolicy.NoRecursion );
		}

		/// <summary>
		///		Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			if ( this._lock != null )
			{
				this._lock.Dispose();
			}
		}

		/// <summary>
		///		Gets service invoker.
		///		If the concrete type is already created, returns the cached instance.
		///		Else creates new concrete type and its instance, then returns it.
		/// </summary>
		/// <param name="context">
		///		<see cref="SerializationContext"/> which manages serializer for arguments and return value.
		/// </param>
		/// <param name="serviceDescription">
		///		<see cref="ServiceDescription"/> which holds the service spec.
		/// </param>
		/// <param name="targetOperation">
		///		<see cref="MethodInfp"/> of the target operation.
		/// </param>
		/// <returns>
		///		<see cref="ServiceInvoker{T}"/> where T is return type of the target method.
		/// </returns>
		public IAsyncServiceInvoker GetServiceInvoker( RpcServerConfiguration configuration, SerializationContext context, ServiceDescription serviceDescription, MethodInfo targetOperation )
		{
			Contract.Assert( configuration != null );
			Contract.Assert( context != null );
			Contract.Assert( serviceDescription != null );
			Contract.Assert( targetOperation != null );

			bool isReadLockHeld = false;
			try
			{
				try { }
				finally
				{
					this._lock.EnterUpgradeableReadLock();
					isReadLockHeld = true;
				}

				IAsyncServiceInvoker result;
				if ( this._cache.TryGetValue( targetOperation.MethodHandle, out result ) )
				{
					return result;
				}

				IAsyncServiceInvoker newInvoker = CreateInvoker( configuration, context, serviceDescription, targetOperation );

				bool isWriteLockHeld = false;
				try
				{
					try { }
					finally
					{
						this._lock.EnterWriteLock();
						isWriteLockHeld = true;
					}

					if ( !this._cache.TryGetValue( targetOperation.MethodHandle, out result ) )
					{
						this._cache[ targetOperation.MethodHandle ] = newInvoker;
						result = newInvoker;
					}

					return result;
				}
				finally
				{
					if ( isWriteLockHeld )
					{
						this._lock.ExitWriteLock();
					}
				}
			}
			finally
			{
				if ( isReadLockHeld )
				{
					this._lock.ExitUpgradeableReadLock();
				}
			}
		}

		private IAsyncServiceInvoker CreateInvoker( RpcServerConfiguration configuration, SerializationContext context, ServiceDescription serviceDescription, MethodInfo targetOperation )
		{
			var parameters = targetOperation.GetParameters();
			CheckParameters( parameters );
			bool isWrapperNeeded = !typeof( Task ).IsAssignableFrom( targetOperation.ReturnType );

			var emitter = new ServiceInvokerEmitter( this._moduleBuilder, Interlocked.Increment( ref this._typeSequence ), targetOperation.DeclaringType, targetOperation.ReturnType, this._isDebuggable );
			EmitInvokeCore( emitter, targetOperation, parameters, typeof( Task ), isWrapperNeeded );

			return emitter.CreateInstance( configuration, context, serviceDescription, targetOperation );
		}

		private static void EmitInvokeCore( ServiceInvokerEmitter emitter, MethodInfo targetOperation, ParameterInfo[] parameters, Type returnType, bool isWrapperNeeded )
		{
			var methodReturnType = targetOperation.ReturnType == typeof( void ) ? typeof( Missing ) : targetOperation.ReturnType;
			var asyncInvokerIsDebugModeProperty = typeof( AsyncServiceInvoker<> ).MakeGenericType( methodReturnType ).GetProperty( "IsDebugMode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic );
			var il = emitter.GetInvokeCoreMethodILGenerator();
			try
			{
				var endOfMethod = il.DefineLabel();
				var unpackedArguments = parameters.Select( item => il.DeclareLocal( item.ParameterType, item.Name ) ).ToArray();
				var serializers = unpackedArguments.Select( item => emitter.RegisterSerializer( item.LocalType ) ).ToArray();

				/*
				 *	using( var argumentsItemUnpacker = arguments.ReadSubTree() )
				 *	{
				 *		argumentsItemUnpacker.
				 *		...
				 *	}
				 */

				var argumentsItemUnpacker = il.DeclareLocal( typeof( Unpacker ), "argumentsItemUnpacker" );
				il.EmitAnyLdarg( 1 );
				il.EmitAnyCall( _unpackerReadSubtreeMethod );
				il.EmitAnyStloc( argumentsItemUnpacker );
				il.BeginExceptionBlock();

				for ( int i = 0; i < parameters.Length; i++ )
				{
					/*
					 *	if ( !argumentsItemUnpacker.Read() )
					 *	{
					 *		throw new SerializationException( String.Format( CultureInfo.CurrentCuture, "Stream unexpectedly ends at argument {0}.", N ) );
					 *	}
					 *	
					 *	T argN;
					 *	try
					 *	{
					 *		argN = this._serializerN.UnpackFrom( argumentsItemUnpacker );
					 *	}
					 *	catch( Exception ex )
					 *	{
					 *		error = InvocationHelper.HandleArgumentDeserializationException( ex, "argN" );
					 *		returnValue = default( T );
					 *		return;
					 *	}
					 */
					il.EmitAnyLdloc( argumentsItemUnpacker );
					il.EmitAnyCall( _unpackerReadMethod );
					var endIf = il.DefineLabel();
					il.EmitBrtrue_S( endIf );
					var args = il.DeclareLocal( typeof( object[] ), "args" );
					il.EmitNewarr( typeof( object ), 1 );
					il.EmitAnyStloc( args );
					il.EmitGetProperty( _cultureInfoCurrentCultureProperty );
					// TODO: Localization
					il.EmitLdstr( "Stream unexpectedly ends at arguments array {0}." );
					il.EmitAnyLdloc( args );
					il.EmitAnyLdc_I4( 0 );
					il.EmitAnyLdc_I4( i );
					il.EmitBox( typeof( int ) );
					il.EmitStelem( typeof( object ) );
					il.EmitAnyLdloc( args );
					il.EmitAnyCall( _stringFormatMethod );
					il.EmitNewobj( _serializationExceptionCtorStringConstructor );
					il.EmitThrow();
					il.MarkLabel( endIf );

					il.BeginExceptionBlock();
					il.EmitAnyLdarg( 0 );
					il.EmitLdfld( serializers[ i ] );
					il.EmitAnyLdloc( argumentsItemUnpacker );
					il.EmitAnyCall( serializers[ i ].FieldType.GetMethod( "UnpackFrom", BindingFlags.Public | BindingFlags.Instance ) );
					il.EmitAnyStloc( unpackedArguments[ i ] );

					EmitExceptionHandling(
						il,
						returnType,
						endOfMethod,
						( il0, exception ) =>
						{
							il0.EmitAnyLdloc( exception );
							il0.EmitLdstr( parameters[ i ].Name );
							il0.EmitAnyLdarg( 0 );
							il0.EmitGetProperty( asyncInvokerIsDebugModeProperty );
							il0.EmitCall( InvocationHelper.HandleArgumentDeserializationExceptionMethod );
						}
					);
				}

				il.BeginFinallyBlock();
				il.EmitAnyLdloc( argumentsItemUnpacker );
				il.EmitAnyCall( _idisposableDisposeMethod );
				il.EndExceptionBlock();

				/*
				 *	TService service = this._serviceDescription.Initializer()
				 */

				var service = il.DeclareLocal( targetOperation.DeclaringType, "service" );
				il.EmitAnyLdarg( 0 );
				il.EmitGetProperty( typeof( AsyncServiceInvoker<> ).MakeGenericType( methodReturnType ).GetProperty( "ServiceDescription" ) );
				il.EmitGetProperty( ServiceDescription.InitializerProperty );
				il.EmitAnyCall( _func_1_Invoke );
				il.EmitCastclass( service.LocalType );
				il.EmitAnyStloc( service );

				/*
				 *	#if IS_TASK
				 *	returnValue = service.Target( arg1, ..., argN );
				 *	#else
				 *	returnValue = Task.Factory.StartNew( this.PrivateInvokeCore( state as Tuple<...> ), new Tuple<...>(...) );
				 *	#endif
				 *	error = RpcErrorMessage.Success;
				 *	return;
				 */
				// Dereference managed pointer.
				il.EmitAnyLdarg( 2 );

				if ( !isWrapperNeeded )
				{
					il.EmitAnyLdloc( service );
					foreach ( var arg in unpackedArguments )
					{
						il.EmitAnyLdloc( arg );
					}

					il.EmitAnyCall( targetOperation );
				}
				else
				{
					EmitWrapperInvocation( emitter, il, service, targetOperation, returnType, unpackedArguments );
				}

				// Set to arg.2
				il.EmitStobj( returnType );

				// Dereference managed pointer.
				il.EmitAnyLdarg( 3 );
				il.EmitGetProperty( _rpcErrorMessageSuccessProperty );
				// Set to arg.3
				il.EmitStobj( typeof( RpcErrorMessage ) );
				il.MarkLabel( endOfMethod );
				il.EmitRet();
			}
			finally
			{
				il.FlushTrace();
			}
		}

		private static readonly Type[] _delegateConstructorParameterTypes = new[] { typeof( object ), typeof( IntPtr ) };
		private static readonly PropertyInfo _taskFactoryProperty =
			FromExpression.ToProperty( () => Task.Factory );
		private static readonly MethodInfo _taskFactoryStartNew_Action_1_Object_Object =
			FromExpression.ToMethod( ( TaskFactory @this, Action<Object> action, object state ) => @this.StartNew( action, state ) );
		private static readonly MethodInfo _taskFactoryStartNew_Func_1_Object_T_Object =
			typeof( TaskFactory ).GetMethods( BindingFlags.Public | BindingFlags.Instance ).Single(
				m =>
					m.Name == "StartNew"
					&& m.IsGenericMethod
					&& m.GetParameters().Length == 2
					&& m.GetParameters()[ 0 ].ParameterType.IsGenericType
					&& m.GetParameters()[ 0 ].ParameterType.GetGenericTypeDefinition() == typeof( Func<,> )
					&& m.GetParameters()[ 1 ].ParameterType == typeof( object )
			);

		private static void EmitWrapperInvocation( ServiceInvokerEmitter emitter, TracingILGenerator il, LocalBuilder service, MethodInfo targetOperation, Type returnType, LocalBuilder[] unpackedArguments )
		{
			/*
			 * returnValue = Task.Factory.StartNew( this.PrivateInvokeCore( state as Tuple<...> ), new Tuple<...>(...) );
			 */
			var itemTypes = Enumerable.Repeat( service, 1 ).Concat( unpackedArguments ).Select( item => item.LocalType ).ToArray();
			var tupleTypes = TupleItems.CreateTupleTypeList( itemTypes );
			EmitPrivateInvoke( emitter.GetPrivateInvokeMethodILGenerator( targetOperation.ReturnType ), targetOperation, tupleTypes, itemTypes );

			il.EmitGetProperty( _taskFactoryProperty );

			// new DELEGATE( PrivateInvoke ) ->new DELGATE( null, funcof( PrivateInvoke ) )
			il.EmitLdnull();
			il.EmitLdftn( emitter.PrivateInvokeMethod );
			il.EmitNewobj(
				targetOperation.ReturnType == typeof( void )
				? typeof( Action<object> ).GetConstructor( _delegateConstructorParameterTypes )
				: typeof( Func<,> ).MakeGenericType( typeof( object ), targetOperation.ReturnType ).GetConstructor( _delegateConstructorParameterTypes )
			);

			il.EmitAnyLdloc( service );

			foreach ( var item in unpackedArguments )
			{
				il.EmitAnyLdloc( item );
			}

			foreach ( var tupleType in tupleTypes )
			{
				il.EmitNewobj( tupleType.GetConstructors().Single() );
			}

			if ( targetOperation.ReturnType == typeof( void ) )
			{
				il.EmitAnyCall( _taskFactoryStartNew_Action_1_Object_Object );
			}
			else
			{
				il.EmitAnyCall( _taskFactoryStartNew_Func_1_Object_T_Object.MakeGenericMethod( targetOperation.ReturnType ) );
			}

		}

		/// <summary>
		///		Emits helper function to avoid lexical closure emitting.
		/// </summary>
		/// <param name="il"><see cref="TracingILGenerator"/>.</param>
		/// <param name="targetOperation"><see cref="MethodInfo"/> of the method to be invoked.</param>
		/// <param name="tupleTypes">The array of <see cref="Type"/> of nested tuples. The outermost is the first, innermost is the last.</param>
		/// <param name="itemTypes">The array of <see cref="Type"/> of flatten tuple items.</param>
		private static void EmitPrivateInvoke( TracingILGenerator il, MethodInfo targetOperation, IList<Type> tupleTypes, Type[] itemTypes )
		{
			/*
			 * private static void/T PrivateInvoke( object state )
			 * {
			 *		var tuple = state as Tuple<...>;
			 *		return 
			 *			tuple.Item1.Target(
			 *				tuple.Item2,
			 *					:
			 *				tuple.Rest.Rest....ItemN
			 *			);
			 * }
			 */
			var tuple = il.DeclareLocal( tupleTypes.First(), "tuple" );

			il.EmitAnyLdarg( 0 );
			il.EmitIsinst( tupleTypes.First() );
			il.EmitAnyStloc( tuple );

			int depth = -1;
			for ( int i = 0; i < itemTypes.Length; i++ )
			{
				if ( i % 7 == 0 )
				{
					depth++;
				}

				il.EmitAnyLdloc( tuple );

				for ( int j = 0; j < depth; j++ )
				{
					// .TRest.TRest ...
					var rest = tupleTypes[ j ].GetProperty( "Rest" );
					il.EmitGetProperty( rest );
				}

				var itemn = tupleTypes[ depth ].GetProperty( "Item" + ( ( i % 7 ) + 1 ) );
				il.EmitGetProperty( itemn );
			}

			il.EmitAnyCall( targetOperation );
			il.EmitRet();
		}

		private static void CheckParameters( IList<ParameterInfo> parameters )
		{
			foreach ( var item in parameters )
			{
				if ( item.ParameterType == typeof( void )
					|| item.ParameterType == typeof( Missing ) )
				{
					throw new NotSupportedException(
						String.Format(
							CultureInfo.CurrentCulture,
							"Parameter '{0}' at position {1} is invalid. Type '{2}' is not supported.",
							item.Name,
							item.Position,
							item.ParameterType.FullName
						)
					);
				}

				if ( item.ParameterType.IsByRef )
				{
					throw new NotSupportedException(
						String.Format(
							CultureInfo.CurrentCulture,
							"Parameter '{0}' at position {1} is invalid. Managed pointer ('byref') is not supported.",
							item.Name,
							item.Position
						)
					);
				}

				if ( item.ParameterType.IsPointer )
				{
					throw new NotSupportedException(
						String.Format(
							CultureInfo.CurrentCulture,
							"Parameter '{0}' at position {1} is invalid. Unmanaged pointer is not supported.",
							item.Name,
							item.Position
						)
					);
				}

				if ( item.ParameterType.IsArray && ( item.ParameterType.GetArrayRank() > 1 ) )
				{
					throw new NotSupportedException(
						String.Format(
							CultureInfo.CurrentCulture,
							"Parameter '{0}' at position {1} is invalid. Non vector array is not supported.",
							item.Name,
							item.Position
						)
					);
				}
			}
		}

		private static void EmitExceptionHandling(
			TracingILGenerator il,
			Type returnType,
			Label endOfMethod,
			Action<TracingILGenerator, LocalBuilder> exceptionHandlerInvocation
		)
		{
			il.BeginCatchBlock( typeof( Exception ) );
			var exception = il.DeclareLocal( typeof( Exception ), "exception" );
			il.EmitAnyStloc( exception );
			// Dereference (out) managed pointer.
			il.EmitAnyLdarg( 3 );
			exceptionHandlerInvocation( il, exception );
			// Set value to the location.
			il.EmitStobj( typeof( RpcErrorMessage ) );
			// Dereference (out) managed pointer.
			il.EmitAnyLdarg( 2 );
			// Init referenced location with default.
			il.EmitInitobj( returnType );
			il.EmitLeave( endOfMethod );
			il.EndExceptionBlock();
		}
	}
}
