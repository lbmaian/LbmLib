#if DEBUG
#define TRACE_LOGGING
#endif

using System;
using System.Diagnostics;
using System.Reflection;

namespace LbmLib.Language
{
	public enum AccessibilityLevel
	{
		Private,
		Private_Protected,
		Internal,
		Protected,
		Protected_Internal,
		Public,
	}

	public static class AccessibilityLevelExtensions
	{
		public static bool IsPublic(this AccessibilityLevel accessibility) =>
			accessibility is AccessibilityLevel.Public;

		public static bool IsPrivate(this AccessibilityLevel accessibility) =>
			accessibility is AccessibilityLevel.Private;

		public static bool IsProtected(this AccessibilityLevel accessibility) =>
			accessibility is AccessibilityLevel.Protected ||
			accessibility is AccessibilityLevel.Private_Protected;

		public static bool IsPotentiallyProtected(this AccessibilityLevel accessibility) =>
			accessibility is AccessibilityLevel.Protected ||
			accessibility is AccessibilityLevel.Protected_Internal ||
			accessibility is AccessibilityLevel.Private_Protected;

		public static bool IsInternal(this AccessibilityLevel accessibility) =>
			accessibility is AccessibilityLevel.Internal ||
			accessibility is AccessibilityLevel.Private_Protected;

		public static bool IsPotentiallyInternal(this AccessibilityLevel accessibility) =>
			accessibility is AccessibilityLevel.Internal ||
			accessibility is AccessibilityLevel.Protected_Internal ||
			accessibility is AccessibilityLevel.Private_Protected;

		// Workaround for TypeAttributes being such a messed up enum.
		public static AccessibilityLevel GetAccessibility(this Type type)
		{
			var attributes = type.Attributes & TypeAttributes.VisibilityMask;
			return attributes switch
			{
				TypeAttributes.NotPublic => AccessibilityLevel.Internal,
				TypeAttributes.Public => AccessibilityLevel.Public,
				TypeAttributes.NestedPublic => AccessibilityLevel.Public,
				TypeAttributes.NestedPrivate => AccessibilityLevel.Private,
				TypeAttributes.NestedFamily => AccessibilityLevel.Protected,
				TypeAttributes.NestedAssembly => AccessibilityLevel.Internal,
				TypeAttributes.NestedFamANDAssem => AccessibilityLevel.Private_Protected,
				TypeAttributes.NestedFamORAssem => AccessibilityLevel.Protected_Internal,
				_ => throw new NotSupportedException($"Unknown accessibility ({attributes}) on type ({type.ToDebugString()})"),
			};
		}

		// Workaround for MethodAttributes being such a messed up enum.
		public static AccessibilityLevel GetAccessibility(this MethodBase methodBase)
		{
			var attributes = methodBase.Attributes & MethodAttributes.MemberAccessMask;
			return attributes switch
			{
				MethodAttributes.Private => AccessibilityLevel.Private,
				MethodAttributes.FamANDAssem => AccessibilityLevel.Private_Protected,
				MethodAttributes.Assembly => AccessibilityLevel.Internal,
				MethodAttributes.Family => AccessibilityLevel.Protected,
				MethodAttributes.FamORAssem => AccessibilityLevel.Protected_Internal,
				MethodAttributes.Public => AccessibilityLevel.Public,
				_ => throw new NotSupportedException($"Unknown accessibility ({attributes}) on method ({methodBase.ToDebugString()})"),
			};
		}

		// Workaround for FieldAttributes being such a messed up enum.
		public static AccessibilityLevel GetAccessibility(this FieldInfo field)
		{
			var attributes = field.Attributes & FieldAttributes.FieldAccessMask;
			return attributes switch
			{
				FieldAttributes.Private => AccessibilityLevel.Private,
				FieldAttributes.FamANDAssem => AccessibilityLevel.Private_Protected,
				FieldAttributes.Assembly => AccessibilityLevel.Internal,
				FieldAttributes.Family => AccessibilityLevel.Protected,
				FieldAttributes.FamORAssem => AccessibilityLevel.Protected_Internal,
				FieldAttributes.Public => AccessibilityLevel.Public,
				_ => throw new NotSupportedException($"Unknown accessibility ({attributes}) on field ({field.ToDebugString()})"),
			};
		}
	}

	public class MemberInfoAccessibility
	{
		readonly MemberInfo sourceMember;
		readonly Func<Type, bool> declaringTypeFilter;

		public MemberInfoAccessibility(MemberInfo sourceMember, Func<Type, bool> declaringTypeFilter = null)
		{
			this.sourceMember = sourceMember;
			this.declaringTypeFilter = declaringTypeFilter ?? (type => true);
		}

		public bool IsAccessible(MemberInfo targetMember)
		{
			Trace($"IsAccessible(sourceMember: {sourceMember.ToDebugString()}, targetMember: {targetMember.ToDebugString()})");
			// Source member is typically a method, but there's no requirement that it must be a method.
			// If the source member is accessing a target member outside of the member cache (i.e. original types and their contents),
			// it's one of the following possibilities:
			// 1) The target member is public, and its declaring type is accessible, which means its declaring type:
			// 1a) is public, and its declaring type is accessible (recursively); or
			// 1b) is the same as or is a parent type of any of the source member's declaring types.
			// 2) The target member is protected (protected, protected internal, or protected private), and its declaring type:
			// 2a) is a parent type of the source member's declaring type; or
			// 2b) is the same as or is a parent type of any of the declaring types of the source member's declaring type (a nested type).
			// 3) The target member is internal (internal, protected internal, or protected private).
			// 4) The target member is private, and its declaring type is the same as any of the declaring types of the source member's
			//    declaring type (a nested type).
			// Of all these possibilities, only case 1 and 3 are valid within the dynamic assembly, with the following change to 1b and 2b:
			// The target member's declaring type is a parent type of any of the source member's declaring types **that are in the member cache**.
			// Type comparisons and parent type checks should be agnostic to generics as needed.
			// Also, "parent type" includes both classes and interfaces (due to default interface members).
			if (targetMember is Type targetType)
				return IsAccessible(targetType);
			if (targetMember is MethodBase targetMethodBase)
				return IsAccessible(targetMethodBase);
			if (targetMember is FieldInfo targetField)
				return IsAccessible(targetField);
			else
				throw new ArgumentException($"Unexpected {targetMember.MemberType} member: {targetMember}");
		}

		public bool IsAccessible(Type targetType)
		{
			var accessibility = targetType.GetAccessibility();
			Trace($"{targetType.MemberType} accessibility: {accessibility}");
			if (targetType.IsNested)
			{
				if (accessibility.IsPublic())
					return IsAccessible(targetType.DeclaringType);
				if (accessibility.IsPotentiallyProtected())
					return IsTargetDeclaringTypeAncestorTypeOfSourceDeclaringTypes(sourceMember, targetType);
			}
			else
			{
				if (accessibility.IsPublic())
					return true;
			}
			return false;
		}

		public bool IsAccessible(MethodBase targetMethodBase)
		{
			var accessibility = targetMethodBase.GetAccessibility();
			Trace($"{targetMethodBase.MemberType} accessibility: {accessibility}");
			if (accessibility.IsPublic())
				return IsAccessible(targetMethodBase.DeclaringType);
			if (accessibility.IsPotentiallyProtected())
				return IsTargetDeclaringTypeAncestorTypeOfSourceDeclaringTypes(sourceMember, targetMethodBase);
			return false;
		}

		public bool IsAccessible(FieldInfo targetField)
		{
			var accessibility = targetField.GetAccessibility();
			Trace($"{targetField.MemberType} accessibility: {accessibility}");
			if (accessibility.IsPublic() && targetField.DeclaringType is var declaringType)
				return declaringType is null || IsAccessible(declaringType);
			else if (accessibility.IsPotentiallyProtected())
				return IsTargetDeclaringTypeAncestorTypeOfSourceDeclaringTypes(sourceMember, targetField);
			else
				return false;
		}

		bool IsTargetDeclaringTypeAncestorTypeOfSourceDeclaringTypes(MemberInfo sourceMember, MemberInfo targetMember)
		{
			var targetDeclaringType = targetMember.DeclaringType;
			var sourceDeclaringType = sourceMember.DeclaringType;
			while (!(sourceDeclaringType is null) && declaringTypeFilter(sourceDeclaringType))
			{
				if (IsAncestorTypeOf(targetDeclaringType, sourceDeclaringType))
					return true;
				sourceDeclaringType = sourceDeclaringType.DeclaringType;
			}
			return false;
		}

		static bool IsAncestorTypeOf(Type parentType, Type type)
		{
			if (parentType.IsClass)
				return IsAncestorClassOf(AsTypeDef(parentType), type);
			else if (parentType.IsInterface)
				return IsInterfaceOf(AsTypeDef(parentType), type);
			else
				return false;
		}

		static bool IsAncestorClassOf(Type ancestorClassDef, Type type)
		{
			if (type.BaseType is Type baseType)
			{
				if (ancestorClassDef == AsTypeDef(baseType) || IsAncestorClassOf(ancestorClassDef, baseType))
					return true;
			}
			return false;
		}

		static bool IsInterfaceOf(Type interfaceDef, Type type)
		{
			// Type.GetInterfaces() gets all interfaces, including both directly implemented or indirectly inherited,
			// so no need for recursion.
			foreach (var @interface in type.GetInterfaces())
			{
				if (interfaceDef == AsTypeDef(@interface))
					return true;
			}
			return false;
		}

		static Type AsTypeDef(Type type) => type.IsGenericType && !type.IsGenericTypeDefinition ? type.GetGenericTypeDefinition() : type;

		[Conditional("TRACE_LOGGING")]
		static void Trace(string str) => Logging.Log(str);
	}
}
