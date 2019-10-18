using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CSharp;
using UnityEngine;
using Verse;

namespace TranslationFilesGenerator.Tools
{
	public static class DebugExtensions
	{
		// TODO: Move logging stuff into own Logger class?
		public static Action<string> DefaultLogger = str => Log.Message(str);

		public static T Logged<T>(this T obj, Action<string> logger = null)
		{
			return obj.LabelLogged("", logger);
		}

		public static T LabelLogged<T>(this T obj, string label, Action<string> logger = null)
		{
			(logger ?? DefaultLogger)(label + (obj is string str ? str :
				obj is System.Collections.IEnumerable enumerable ? enumerable.ToStringSafeEnumerable() : obj.ToStringSafe()));
			return obj;
		}

		public static T MessageLogged<T>(this T obj)
		{
			return obj.Logged(str => Log.Message(str));
		}

		public static T LabelMessageLogged<T>(this T obj, string label)
		{
			return obj.LabelLogged(label, str => Log.Message(str));
		}

		public static T DebugLogged<T>(this T obj)
		{
			return obj.Logged(str => Debug.Log(str));
		}

		public static T LabelDebugLogged<T>(this T obj, string label)
		{
			return obj.LabelLogged(label, str => Debug.Log(str));
		}

		public static T ConsoleLogged<T>(this T obj)
		{
			return obj.Logged(str => Console.WriteLine(str));
		}

		public static T LabelConsoleLogged<T>(this T obj, string label)
		{
			return obj.LabelLogged(label, str => Console.WriteLine(str));
		}

		static readonly Dictionary<Type, string> ProviderTypeOutputCache = new Dictionary<Type, string>();

		public static string ToDebugString(this Type type)
		{
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
			return field.FieldType.ToDebugString() + " " + field.DeclaringType.ToDebugString() + "::" + field.Name;
		}

		public static string ToDebugString(this MethodBase method)
		{
			return (method.IsStatic ? "" : "instance ") +
				(method is ConstructorInfo ? "void" : ((MethodInfo)method).ReturnType.ToDebugString()) + " " +
				method.DeclaringType.ToDebugString() + "::" +
				method.Name + "(" +
				method.GetParameters().Select(parameter => parameter.ParameterType.ToDebugString()).Join() + ")";
		}
	}
}
