using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CSharp;

namespace TranslationFilesGenerator.Tools
{
	public static class DebugLogging
	{
		public static readonly Func<object, string> ToDebugStringer = obj => obj.ToDebugString();
	}

	public static class DebugExtensions
	{
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
				return $"[Type=\"{Type}\", {(IsCache ? "IsCache=true" : $"Method=\"{Method}\"")}, Delegate=\"{Delegate}\"]";
			}
		}

		static readonly HashSet<Assembly> ToDebugStringAssemblies = new HashSet<Assembly>();

		static readonly Dictionary<Type, DynamicDispatchEntry> ToDebugStringDynamicDispatches = new Dictionary<Type, DynamicDispatchEntry>();

		static void InitializeToDebugStringDynamicDispatches(Assembly assembly)
		{
			if (ToDebugStringAssemblies.Contains(assembly))
				return;
			ToDebugStringAssemblies.Add(assembly);

			foreach (var type in assembly.GetTypes())
			{
				foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public))
				{
					if (method.Name is "ToDebugString" && method.IsDefined(typeof(ExtensionAttribute), true))
					{
						var parameters = method.GetParameters();
						if (parameters.Length == 1)
						{
							var targetType = parameters[0].ParameterType;
							if (targetType != typeof(object))
							{
								// Construct delegate: (object obj) => ToDebugString((T)obj) where T is what type represents.
								var paramExpr = Expression.Parameter(typeof(object), "obj");
								var @delegate = Expression.Lambda<Func<object, string>>(Expression.Call(method, Expression.Convert(paramExpr, targetType)), paramExpr).Compile();
								var dynamicDispatch = new DynamicDispatchEntry(targetType, method, @delegate);
								//Logging.Log($"Init {targetType} dispatch: {dynamicDispatch}");
								ToDebugStringDynamicDispatches.Add(targetType, dynamicDispatch);
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
						var dynamicDispatch = new DynamicDispatchEntry(type, method, @delegate);
						//Logging.Log($"Init {type} dispatch: {dynamicDispatch}");
						ToDebugStringDynamicDispatches.Add(type, dynamicDispatch);
					}
				}
			}
		}

		static readonly Func<object, string> ToStringDelegate =
			(Func<object, string>)Delegate.CreateDelegate(typeof(Func<object, string>), typeof(object).GetMethod(nameof(Object.ToString)));

		public static string ToDebugString(this object obj)
		{
			// Workaround for C# extension methods not supporting late binding - use a custom dynamic dispatch.
			// This is not a full-featured single parameter dynamic dispatch algorithm (for example, doesn't account for generic covariance/contravariance)
			// but it suffices for most use cases.
			if (obj is null)
				return "null";
			var objType = obj.GetType();
			InitializeToDebugStringDynamicDispatches(objType.Assembly);
			if (ToDebugStringDynamicDispatches.TryGetValue(objType, out var foundDynamicDispatch))
				return foundDynamicDispatch.Delegate(obj);
			var dynamicDispatches = new List<DynamicDispatchEntry>();
			foreach (var dynamicDispatch in ToDebugStringDynamicDispatches.Values)
			{
				if (!dynamicDispatch.IsCache && dynamicDispatch.Type.IsAssignableFrom(objType))
					dynamicDispatches.Add(dynamicDispatch);
			}
			//Logging.Log($"{dynamicDispatches.Count} matching delegates: " + dynamicDispatches.Join());
			if (dynamicDispatches.Count > 1)
			{
				var exception = new AmbiguousMatchException("The call is ambiguous between the following methods: " +
						dynamicDispatches.Select(dynamicDispatch => $"'{dynamicDispatch.Method.ToDebugString()}'").Join());
				foundDynamicDispatch = new DynamicDispatchEntry(objType, null, _ => throw exception);
			}
			else if (dynamicDispatches.Count == 0)
			{
				foundDynamicDispatch = new DynamicDispatchEntry(objType, null, ToStringDelegate);
			}
			else
			{
				foundDynamicDispatch = new DynamicDispatchEntry(objType, null, dynamicDispatches[0].Delegate);
			}
			//Logging.Log($"Cache {objType} dispatch: {foundDynamicDispatch}");
			ToDebugStringDynamicDispatches.Add(objType, foundDynamicDispatch);
			return foundDynamicDispatch.Delegate(obj);
		}

		static readonly Dictionary<Type, string> ProviderTypeOutputCache = new Dictionary<Type, string>();

		public static string ToDebugString(this Type type)
		{
			if (type is null)
				return "null";
			if (type.IsGenericType)
			{
				if (type.GetGenericTypeDefinition() == typeof(Nullable<>))
					return type.GetGenericArguments()[0].ToDebugString() + "?";
				var tickIndex = type.Name.IndexOf('`');
				return (type.Namespace == "System" ? "" : type.Namespace) +
					"." + (tickIndex == -1 ? type.Name : type.Name.Substring(0, tickIndex)) +
					"<" + type.GetGenericArguments().Select(genericTypeArg => genericTypeArg.ToDebugString()).Join() + ">";
			}
			if (type.IsPrimitive || type.IsArray)
			{
				if (!ProviderTypeOutputCache.TryGetValue(type, out string str))
				{
					using (var provider = new CSharpCodeProvider())
						str = provider.GetTypeOutput(new CodeTypeReference(type));
					if (type.IsArray)
						str = type.GetElementType().ToDebugString() + str.Substring(str.IndexOf('['));
					ProviderTypeOutputCache.Add(type, str);
				}
				return str;
			}
			if (type == typeof(void))
				return "void";
			if (type == typeof(object))
				return "object";
			if (type == typeof(string))
				return "string";
			if (type == typeof(decimal))
				return "decimal";
			if (type.Namespace == "System")
				return type.Name;
			return type.ToString().Split('+').Select(segment => segment[0] == '<' ? "'" + segment + "'" : segment).Join(delimiter: "/");
		}

		public static string ToDebugString(this FieldInfo field)
		{
			return field is null ? "null" : field.FieldType.ToDebugString() + " " + field.DeclaringType.ToDebugString() + "::" + field.Name;
		}

		public static string ToDebugString(this MethodBase method)
		{
			return method is null ? "null" :
				(method.IsStatic ? "" : "instance ") +
				(method is ConstructorInfo ? "void" : ((MethodInfo)method).ReturnType.ToDebugString()) + " " +
				method.DeclaringType.ToDebugString() + "::" +
				method.Name + "(" +
				method.GetParameters().Select(parameter => parameter.ParameterType.ToDebugString()).Join() + ")";
		}
	}
}
