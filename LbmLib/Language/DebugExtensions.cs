using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace LbmLib.Language
{
	public static class DebugExtensions
	{
		// TODO: See if the following custom function overload resolution logic could all be simplfied by using Type.DefaultBinder.
		// The following is all a workaround for C# extension methods not supporting late binding for function overload resolution:
		// Specifically, use a custom dynamic dispatch algorithm in static string ToDebugString(this object obj).
		// This is not a full-featured single parameter function overload resolution algorithm but it suffices for most uses cases.
		// (For example, doesn't account for generic type contravariance or other generic type constraints.)
		// The actual function overload resolution algorithm is documented here:
		// https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#overload-resolution
		// Also note that using dynamic type wouldn't work, since:
		// a) dynamic type doesn't work with extension methods; and
		// b) dynamic type isn't supported in .NET 3.5 (and thus legacy Unity apps) anyway.

		struct DynamicDispatchEntry
		{
			public readonly Type Type;
			public readonly MethodInfo Method;
			public readonly Func<object, string> Delegate;

			public bool IsCache => Method is null;

			internal DynamicDispatchEntry(Type type, MethodInfo method, Func<object, string> @delegate)
			{
				Type = type;
				Method = method;
				Delegate = @delegate;
			}

			public override string ToString()
			{
				return $"[Type=\"{Type}\", {(IsCache ? "IsCache=true" : $"Method=\"{Method}\"")}, HasDelegate={(Delegate is null ? "false" : "true")}]";
			}
		}

		static readonly HashSet<Assembly> ToDebugStringAssemblies = InitializeToDebugStringAssemblies();

		static HashSet<Assembly> InitializeToDebugStringAssemblies()
		{
			//Logging.Log(AppDomain.CurrentDomain.GetAssemblies().Select(assembly => $"{assembly}: " + assembly.AssemblyProductValue()).Join("\n\t"), "Assemblies");
			var assemblyBlacklist = AppDomain.CurrentDomain.GetAssemblies().Where(assembly =>
			{
				var name = assembly.GetName().Name;
				if (name is "System" || name is "mscorlib" || name is "UnityEngine")
					return true;
				if (name.StartsWith("System.") || name.StartsWith("UnityEngine."))
					return true;
				var product = assembly.AssemblyProductValue();
				if (product.StartsWith("MONO ") || product.StartsWith("Microsoft "))
					return true;
				return false;
			});
			//Logging.Log(assemblyBlacklist.Join("\n\t"), "assemblyBlacklist");
			return new HashSet<Assembly>(assemblyBlacklist);
		}

		static string AssemblyProductValue(this Assembly assembly)
		{
			return ((AssemblyProductAttribute)assembly.GetCustomAttributes(typeof(AssemblyProductAttribute), false).FirstOrDefault())?.Product ?? "";
		}

		static readonly Dictionary<Type, DynamicDispatchEntry> ToDebugStringDynamicDispatches = InitializeToDebugStringDynamicDispatches();

		static Dictionary<Type, DynamicDispatchEntry> InitializeToDebugStringDynamicDispatches()
		{
			var toDebugStringDynamicDispatches = new Dictionary<Type, DynamicDispatchEntry>() { { typeof(object), BaseObjectToStringDynamicDispatchEntry } };
			InitializeToDebugStringDynamicDispatches(toDebugStringDynamicDispatches, Assembly.GetExecutingAssembly());
			return toDebugStringDynamicDispatches;
		}

		static void InitializeToDebugStringDynamicDispatches(Dictionary<Type, DynamicDispatchEntry> toDebugStringDynamicDispatches, Assembly assembly)
		{
			if (ToDebugStringAssemblies.Contains(assembly))
				return;
			ToDebugStringAssemblies.Add(assembly);
			//Logging.Log($"InitializeToDebugStringDynamicDispatches for assembly {assembly}: {AssemblyProductValue(assembly)}");

			foreach (var type in assembly.GetTypes())
			{
				if (IsStaticClass(type))
				{
					foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public))
					{
						if (method.Name is "ToDebugString" && method.IsDefined(typeof(ExtensionAttribute), false))
						{
							var parameters = method.GetParameters();
							if (parameters.Length == 1 && parameters[0].ParameterType is var targetType && targetType != typeof(object))
							{
								if (method.ContainsGenericParameters)
								{
									// If the method has generic type parameters, we can't construct a delegate yet;
									// it'll be constructed within ToDebugString when the full object type with closed generic type arguments are known.
									var dispatch = new DynamicDispatchEntry(targetType, method, null);
									//Logging.Log($"Partial init {targetType} dispatch: {dispatch}");
									toDebugStringDynamicDispatches.Add(targetType, dispatch);
								}
								else
								{
									// Construct delegate: (object obj) => ToDebugString((T)obj) where T is what targetType represents.
									var paramExpr = Expression.Parameter(typeof(object), "obj");
									var @delegate = Expression.Lambda<Func<object, string>>(Expression.Call(method,
										Expression.Convert(paramExpr, targetType)), paramExpr).Compile();
									var dispatch = new DynamicDispatchEntry(targetType, method, @delegate);
									//Logging.Log($"Init {targetType} dispatch: {dispatch}");
									toDebugStringDynamicDispatches.Add(targetType, dispatch);
								}
							}
						}
					}
				}
				foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
				{
					if (method.Name is "ToDebugString" && method.GetParameters().Length == 0)
					{
						// Construct delegate: (object obj) => ((T)obj).ToDebugString() where T is what type represents.
						var paramExpr = Expression.Parameter(typeof(object), "obj");
						var @delegate = Expression.Lambda<Func<object, string>>(Expression.Call(Expression.Convert(paramExpr, type), method), paramExpr).Compile();
						var dispatch = new DynamicDispatchEntry(type, method, @delegate);
						//Logging.Log($"Init {type} dispatch: {dispatch}");
						toDebugStringDynamicDispatches.Add(type, dispatch);
					}
				}
			}
		}

		static bool IsStaticClass(Type type)
		{
			return type.IsClass && type.IsAbstract && type.IsSealed;
		}

		static readonly DynamicDispatchEntry BaseObjectToStringDynamicDispatchEntry = InitializeBaseObjectToStringDynamicDispatchEntry();

		static DynamicDispatchEntry InitializeBaseObjectToStringDynamicDispatchEntry()
		{
			var type = typeof(object);
			var method = type.GetMethod(nameof(object.ToString));
			var @delegate = (Func<object, string>)Delegate.CreateDelegate(typeof(Func<object, string>), typeof(object).GetMethod(nameof(object.ToString)));
			return new DynamicDispatchEntry(type, method, @delegate);
		}

		public static string ToDebugString(this object obj)
		{
			if (obj is null)
				return "null";
			if (obj is true)
				return "true";
			if (obj is false)
				return "false";

			var objType = obj.GetType();
			InitializeToDebugStringDynamicDispatches(ToDebugStringDynamicDispatches, objType.Assembly);
			if (ToDebugStringDynamicDispatches.TryGetValue(objType, out var foundDispatch))
			{
				if (foundDispatch.Delegate is null)
					throw new InvalidOperationException("Type of passed object unexpectedly has open generic type parameters: " + objType.ToDebugString());
				return foundDispatch.Delegate(obj);
			}

			foreach (var parentType in objType.GetParentTypes())
				InitializeToDebugStringDynamicDispatches(ToDebugStringDynamicDispatches, parentType.Assembly);

			var dispatches = new List<DynamicDispatchEntry>();
			foreach (var dispatch in ToDebugStringDynamicDispatches.Values)
			{
				if (!dispatch.IsCache)
				{
					var dispatchMethod = dispatch.Method;
					var dispatchType = dispatch.Type;
					if (dispatchMethod.ContainsGenericParameters)
					{
						var methodGenericArgs = dispatch.Method.GetGenericArguments();
						var constructedDispatchType = GetConstructedDispatchType(objType, dispatchType, methodGenericArgs);
						//Logging.Log(constructedDispatchType, "constructedDispatchType");
						if (!(constructedDispatchType is null))
						{
							// Any still open generic arguments or substituted with dummy types (arbitrarily choosing typeof(object) for this).
							// This happens if the method has a generic type parameter that its method parameter doesn't need.
							for (var index = 0; index < methodGenericArgs.Length; index++)
							{
								if (methodGenericArgs[index].IsGenericParameter)
									methodGenericArgs[index] = typeof(object);
							}

							// Assertion: dispatch.Delegate is null and needs to be computed.
							// Construct delegate: (object obj) => ToDebugString<T1,T2,...>((T)obj) where T is what type represents,
							// and T1,T2,... are generic type parameters used within T.
							var method = dispatchMethod.MakeGenericMethod(methodGenericArgs);
							var paramExpr = Expression.Parameter(typeof(object), "obj");
							var @delegate = Expression.Lambda<Func<object, string>>(Expression.Call(method,
								Expression.Convert(paramExpr, objType)), paramExpr).Compile();
							//Logging.Log($"Late init {objType} dispatch to {constructedDispatchType}");
							// Using original non-constructed generic method, so that a possible AmbiguousMatchException has the correct message.
							dispatches.Add(new DynamicDispatchEntry(constructedDispatchType, dispatchMethod, @delegate));
						}
					}
					else
					{
						if (dispatchType.IsAssignableFrom(objType))
							dispatches.Add(dispatch);
					}
				}
			}
			var exception = default(Exception);
			// Note: If it becomes apparent that this is still too trigger-happy with AmbiguousMatchException,
			// implement more of the official "better function member" algorithm here:
			// https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#better-function-member
			//Logging.Log(dispatches.Join("\n\t"), "dispatches");
			if (dispatches.Count > 1)
			{
				var dispatchesExcludingCommonDerivedType = dispatches.PopAll(dispatch => !dispatches.All(otherDispatch => otherDispatch.Type.IsAssignableFrom(dispatch.Type)));
				//Logging.Log(dispatchesExcludingCommonDerivedType.Join("\n\t"), "dispatchesExcludingCommonDerivedType");
				//Logging.Log(dispatches.Join("\n\t"), "dispatches - dispatchesExcludingCommonDerivedType");
				if (dispatches.Count == 0)
					dispatches = dispatchesExcludingCommonDerivedType;
				if (dispatches.Count > 1)
				{
					var dispatchesWithGenericMethods = dispatches.PopAll(dispatch => dispatch.Method.ContainsGenericParameters);
					//Logging.Log(dispatchesWithGenericMethods.Join("\n\t"), "dispatchesWithGenericMethods");
					//Logging.Log(dispatches.Join("\n\t"), "dispatches - dispatchesWithGenericMethods");
					if (dispatches.Count == 0)
						dispatches = dispatchesWithGenericMethods;
					if (dispatches.Count > 1)
					{
						exception = new AmbiguousMatchException("The call is ambiguous between the following methods: " +
							dispatches.Select(dispatch => $"'{dispatch.Method.ToDebugString()}'").Join());
						foundDispatch = new DynamicDispatchEntry(objType, null, _ => throw exception);
					}
				}
			}
			if (exception is null)
			{
				var sourceDispatch = dispatches.Count == 0 ? BaseObjectToStringDynamicDispatchEntry : dispatches[0];
				foundDispatch = new DynamicDispatchEntry(objType, null, sourceDispatch.Delegate);
				//Logging.Log($"Cache {objType} dispatch from source: {sourceDispatch}");
			}
			else
			{
				//Logging.Log($"Cache {objType} dispatch with exception: {exception}");
			}
			ToDebugStringDynamicDispatches.Add(objType, foundDispatch);
			return foundDispatch.Delegate(obj);
		}

		// Type.IsAssignableFrom can't handle open generic type parameters, so we have to effectively do the check ourselves.
		// For example, IEnumerable<T> should match int[], since int[] implements interface IEnumerable<int>.
		// If a match is found, returns a constructed generic type that represents dispatchType with closed generic arguments (e.g. IEnumerable<int>).
		// We also modify methodGenericArgs for each open type for which we found a closed type.
		// Simplifying assumption: Generic type parameters are all covariant.
		static Type GetConstructedDispatchType(Type objType, Type dispatchType, Type[] methodGenericArgs)
		{
			if (dispatchType.IsGenericParameter)
			{
				methodGenericArgs[methodGenericArgs.IndexOf(dispatchType)] = objType;
				return objType;
			}
			else if (dispatchType.ContainsGenericParameters)
			{
				var dispatchGenericTypeDefinition = dispatchType.GetGenericTypeDefinition();
				foreach (var candidateType in objType.GetParentTypes(includeThisType: true))
				{
					if (candidateType.IsGenericType &&
						candidateType.GetGenericTypeDefinition() is var candidateGenericTypeDefinition &&
						candidateGenericTypeDefinition == dispatchGenericTypeDefinition)
					{
						var candidateGenericArgs = candidateType.GetGenericArguments();
						var dispatchGenericArgs = dispatchType.GetGenericArguments();
						for (var index = 0; index < dispatchGenericArgs.Length; index++)
						{
							var constructedDispatchGenericArg = GetConstructedDispatchType(candidateGenericArgs[index], dispatchGenericArgs[index], methodGenericArgs);
							if (constructedDispatchGenericArg is null)
								return null;
							dispatchGenericArgs[index] = constructedDispatchGenericArg;
						}
						return dispatchGenericTypeDefinition.MakeGenericType(dispatchGenericArgs);
					}
				}
				return null;
			}
			else
			{
				if (dispatchType.IsAssignableFrom(objType))
					return dispatchType;
				return null;
			}
		}

		public static string ToDebugString(this string str)
		{
			return str;
		}

		public static string ToDebugString(this IEnumerable enumerable)
		{
			if (enumerable is null)
				return "null";
			return enumerable.GetType().ToDebugString(includeNamespace: false, includeDeclaringType: false) + " {" + enumerable.Join() + "}";
		}

		public static string ToDebugString<T>(this IEnumerable<T> enumerable)
		{
			if (enumerable is null)
				return "null";
			return enumerable.GetType().ToDebugString(includeNamespace: false, includeDeclaringType: false) + " { " + enumerable.Join() + " }";
		}

		static readonly Dictionary<Type, string> TypeToDebugStringCache = new Dictionary<Type, string>()
		{
			{ typeof(void), "void" },
			{ typeof(object), "object" },
			{ typeof(string), "string" },
			{ typeof(bool), "bool" },
			{ typeof(byte), "byte" },
			{ typeof(sbyte), "sbyte" },
			{ typeof(short), "short" },
			{ typeof(ushort), "ushort" },
			{ typeof(int), "int" },
			{ typeof(uint), "uint" },
			{ typeof(long), "long" },
			{ typeof(ulong), "ulong" },
			{ typeof(char), "char" },
			{ typeof(float), "float" },
			{ typeof(double), "double" },
			{ typeof(decimal), "decimal" },
		};

		public static string ToDebugString(this Type type)
		{
			return ToDebugString(type, includeNamespace: true, includeDeclaringType: true);
		}

		public static string ToDebugString(this Type type, bool includeNamespace, bool includeDeclaringType)
		{
			if (type is null)
				return "null";
			if (type.IsByRef)
			{
				// Assume this is a ParameterInfo.ParameterType, and that ParameterInfo.ToDebugString() already handles the ref/in/out serialization.
				return type.GetElementType().ToDebugString();
			}
			if (type.IsPointer)
			{
				return type.GetElementType().ToDebugString() + "*";
			}
			if (type.IsGenericParameter)
			{
				return type.Name;
			}
			if (!TypeToDebugStringCache.TryGetValue(type, out string str))
			{
				if (type.IsArray)
				{
					var elementType = type;
					do
						elementType = elementType.GetElementType();
					while (elementType.IsArray);
					var bracketStr = type.Name.Substring(type.Name.IndexOf('[')).SplitKeepStringDelimiter("][", keepDelimiterIndex: 1).Reverse().Join("");
					str = elementType.ToDebugString() + bracketStr;
				}
				else if (type.IsGenericType)
				{
					if (type.GetGenericTypeDefinition() == typeof(Nullable<>))
					{
						str = type.GetGenericArguments()[0].ToDebugString() + "?";
					}
					else
					{
						str = ToDebugStringTypeInternal(type, includeNamespace, includeDeclaringType);
						var backTickIndex = str.IndexOf('`');
						str = (backTickIndex == -1 ? str : str.Substring(0, backTickIndex)) +
							"<" + type.GetGenericArguments().Select(genericTypeArg => genericTypeArg.ToDebugString()).Join() + ">";
					}
				}
				// TODO: Somehow support delegate types better?
				else
				{
					str = ToDebugStringTypeInternal(type, includeNamespace, includeDeclaringType);
				}
				TypeToDebugStringCache.Add(type, str);
			}
			return str;
		}

		static string ToDebugStringTypeInternal(Type type, bool includeNamespace, bool includeDeclaringType)
		{
			return (!includeDeclaringType || type.DeclaringType is null ? "" : type.DeclaringType.ToDebugString() + "/") +
				(!includeNamespace || type.Namespace == "System" ? "" : type.Namespace + ".") +
				(type.Name.StartsWith("<") ? "'" + type.Name + "'" : type.Name);
		}

		public static string ToDebugString(this FieldInfo field)
		{
			return field is null ? "null" : field.FieldType.ToDebugString() + " " + field.DeclaringType.ToDebugString() + "::" + field.Name;
		}

		public static string ToDebugString(this MethodBase method)
		{
			return method is null ? "null" :
				(method.IsStatic ? "" : "instance ") +
				(method is MethodInfo methodInfo ? methodInfo.ReturnType.ToDebugString() : "void") + " " +
				(method.DeclaringType is null ? "" : method.DeclaringType.ToDebugString() + "::") + method.Name +
				"(" + method.GetParameters().Select(parameter => parameter.ToDebugString()).Join() + ")";
		}

		public static string ToDebugString(this ParameterInfo parameter)
		{
			return parameter is null ? "null" :
				(parameter.IsDefined(typeof(ParamArrayAttribute), false) ? "params " :
					(parameter.ParameterType.IsByRef ? (parameter.IsIn ? "in " : parameter.IsOut ? "out " : "ref ") : "")) +
				parameter.ParameterType.ToDebugString() + " " + parameter.Name;
		}
	}
}
