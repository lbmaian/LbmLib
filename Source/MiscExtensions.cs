using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CSharp;
using Verse;

namespace TranslationFilesGenerator
{
	public static class MiscExtensions
	{
		public static T Logged<T>(this T obj)
		{
			Log.Message(obj.ToStringSafe());
			return obj;
		}

		public static T LabelLogged<T>(this T obj, string label)
		{
			Log.Message(label + ": " + obj.ToStringSafe());
			return obj;
		}

		public static List<T> AsList<T>(this IEnumerable<T> enumerable)
		{
			return enumerable as List<T> ?? new List<T>(enumerable);
		}

		public static List<T> PopAll<T>(this ICollection<T> collection)
		{
			var collectionCopy = new List<T>(collection);
			collection.Clear();
			return collectionCopy;
		}

		public static string Join<T>(this IEnumerable<T> enumerable, string delimiter = ", ")
		{
			return enumerable.Aggregate("", (string prev, T curr) => prev + ((prev != "") ? delimiter : "") + curr?.ToString() ?? "null");
		}

		static Dictionary<Type, string> providerTypeOutputCache = new Dictionary<Type, string>();

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
				if (!providerTypeOutputCache.TryGetValue(type, out string str))
				{
					using (var provider = new CSharpCodeProvider())
						str = provider.GetTypeOutput(new CodeTypeReference(type));
					if (type.IsArray)
						str = type.GetElementType().ToDebugString() + str.Substring(str.IndexOf('['));
					providerTypeOutputCache.Add(type, str);
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

		public static string LanguageLabel(this LoadedLanguage language)
		{
			if (language.FriendlyNameNative == language.FriendlyNameEnglish)
				return language.FriendlyNameNative;
			return $"{language.FriendlyNameNative} [{language.FriendlyNameEnglish}]";
		}

		// Tries to reset the target language to before its loaded state, including clearing any recorded errors.
		public static void ResetDataAndErrors(this LoadedLanguage language)
		{
			typeof(LoadedLanguage).GetField("dataIsLoaded", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(language, false);
			language.loadErrors.Clear();
			language.backstoriesLoadErrors.Clear();
			language.anyKeyedReplacementsXmlParseError = false;
			language.lastKeyedReplacementsXmlParseErrorInFile = null;
			language.anyDefInjectionsXmlParseError = false;
			language.lastDefInjectionsXmlParseErrorInFile = null;
			language.anyError = false;
			language.icon = BaseContent.BadTex;
			language.keyedReplacements.Clear();
			language.defInjections.Clear();
			language.stringFiles.Clear();
		}
	}
}
