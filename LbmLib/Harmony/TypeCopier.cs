using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using Harmony.ILCopying;

namespace LbmLib.Harmony
{
	public class TypeCopier
	{
		abstract class Entry
		{
			public MemberInfo Member { get; }
			public object Builder { get; }

			protected Entry(MemberInfo member, object builder)
			{
				Member = member;
				Builder = builder;
				Debug("new " + this);
			}

			public override string ToString() => $"{GetType().Name}(member: {MemberToString(Member)}, builder: {Builder.GetType()})";
		}

		sealed class TypeEntry : Entry
		{
			public Type Type => (Type)Member;
			public TypeBuilder TypeBuilder => (TypeBuilder)Builder;

			public TypeEntry(Type type, TypeBuilder typeBuilder) : base(type, typeBuilder) { }
		}

		sealed class TypeNonDefEntry : Entry
		{
			// typeBuilder isn't actually a TypeBuilder, but it's created from one via TypeBuilder.MakeGenericType or TypeBuilder.DefineGenericParameters.
			public TypeNonDefEntry(Type type, Type typeBuilder) : base(type, typeBuilder) { }
		}

		abstract class MethodDefEntry : Entry
		{
			public MethodBase MethodBase => (MethodBase)Member;
			public MethodBodyReader MethodReader;
			public ILGenerator ILGenerator;

			protected MethodDefEntry(MethodBase methodBase, object builder) : base(methodBase, builder) { }
		}

		sealed class MethodEntry : MethodDefEntry
		{
			public MethodEntry(MethodInfo method, MethodBuilder methodBuilder) : base(method, methodBuilder) { }
		}

		sealed class MethodNonDefEntry : Entry
		{
			// methodBuilder isn't actually a MethodBuilder, but it's created from one via MethodBuilder.MakeGenericMethod or TypeBuilder.GetMethod.
			public MethodNonDefEntry(MethodInfo method, MethodInfo methodBuilder) : base(method, methodBuilder) { }
		}

		sealed class ConstructorEntry : MethodDefEntry
		{
			public ConstructorEntry(ConstructorInfo constructor, ConstructorBuilder constructorBuilder) : base(constructor, constructorBuilder) { }
		}

		sealed class ConstructorNonDefEntry : Entry
		{
			// constructorBuilder isn't actually a ConstructorBuilder, but it's created from one via TypeBuilder.GetConstructor.
			public ConstructorNonDefEntry(ConstructorInfo constructor, ConstructorInfo constructorBuilder) : base(constructor, constructorBuilder) { }
		}

		sealed class PropertyEntry : Entry
		{
			public PropertyEntry(PropertyInfo property, PropertyBuilder propertyBuilder) : base(property, propertyBuilder) { }
		}

		sealed class EventEntry : Entry
		{
			public EventEntry(EventInfo @event, EventBuilder eventBuilder) : base(@event, eventBuilder) { }
		}

		sealed class FieldEntry : Entry
		{
			public FieldEntry(FieldInfo field, FieldBuilder fieldBuilder) : base(field, fieldBuilder) { }
		}

		sealed class FieldNonDefEntry : Entry
		{
			// fieldBuilder isn't actually a FieldBuilder, but it's created from one via TypeBuilder.GetField.
			public FieldNonDefEntry(FieldInfo field, FieldInfo fieldBuilder) : base(field, fieldBuilder) { }
		}

		readonly HashSet<Type> originalTypes;
		readonly Dictionary<MethodBase, MethodInfo> methodTranspilers; // original method => transpiler

		Dictionary<Type, TypeBuilder> typeBuilders; // original type => TypeBuilder for copying it
		Dictionary<MemberInfo, Entry> memberCache; // original member => Entry for it
		List<Entry> unfinalizedEntries;

		// Workaround for transpilers needing to be static yet still needing access to originalTypes and memberCache.
		[ThreadStatic]
		static HashSet<Type> threadLocalOriginalTypes;
		[ThreadStatic]
		static Dictionary<MemberInfo, Entry> threadLocalMemberCache;

		public TypeCopier(IEnumerable<Type> originalTypes = null, IDictionary<MethodBase, MethodInfo> methodTranspilers = null)
		{
			this.originalTypes = new HashSet<Type>();
			if (!(originalTypes is null))
			{
				foreach (var originalType in originalTypes)
					AddOriginalType(originalType);
			}
			this.methodTranspilers = methodTranspilers is null ?
				new Dictionary<MethodBase, MethodInfo>() :
				new Dictionary<MethodBase, MethodInfo>(methodTranspilers);
		}

		public TypeCopier AddOriginalType(Type originalType)
		{
			if (originalTypes.Count > 0 && originalType.Assembly != originalTypes.First().Assembly)
				throw new ArgumentException("originalTypes have different assemblies: " +
					originalTypes.Join(type => $"{type} => {type.Assembly}"));
			if (originalType.IsGenericParameter)
				throw new ArgumentException($"originalType {originalType} must not be a generic type parameter");
			if (originalType.IsGenericType && !originalType.IsGenericTypeDefinition)
				throw new ArgumentException($"originalType {originalType} must not be a constructed generic type " +
					"(if generic type, must be a generic type definition)");
			originalTypes.Add(originalType);
			return this;
		}

		public TypeCopier AddMethodTranspiler(MethodBase originalMethod, MethodInfo transpiler)
		{
			methodTranspilers[originalMethod] = transpiler;
			return this;
		}

		public AssemblyBuilder CreateAssembly(string saveDirectory = null)
		{
			if (originalTypes.Count == 0)
				throw new ArgumentException("originalTypes must be non-empty");

			var assemblyName = new AssemblyName(originalTypes.First().Assembly.GetName().Name + "Patched");
			AssemblyBuilder assemblyBuilder;
			ModuleBuilder moduleBuilder;
			string saveFileName;
			if (saveDirectory is null)
			{
				saveFileName = null;
				assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
				moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);
			}
			else
			{
				Directory.CreateDirectory(saveDirectory);
				saveFileName = assemblyName.Name + ".dll";
				assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave, saveDirectory);
				moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name, saveFileName);
			}

			var declaringTypeBuildersOfOriginalTypes = new Dictionary<Type, TypeBuilder>();
			typeBuilders = new Dictionary<Type, TypeBuilder>();
			foreach (var type in originalTypes)
				PrepareTypeBuilders(type, moduleBuilder, declaringTypeBuildersOfOriginalTypes);

			memberCache = new Dictionary<MemberInfo, Entry>();
			threadLocalMemberCache = memberCache;
			unfinalizedEntries = new List<Entry>();
			foreach (var type in originalTypes)
				CopyTypeDef(type, typeBuilders[type], isDeclaringTypeOfOriginalType: false);
			// Declaring types of original types are minimally defined, not added to the member cache (only added to unfinalizedEntries),
			// and only containing their generic type parameters and the necessary members up to the original types.
			foreach (var typeBuilderPair in declaringTypeBuildersOfOriginalTypes)
				CopyTypeDef(typeBuilderPair.Key, typeBuilderPair.Value, isDeclaringTypeOfOriginalType: true);

			threadLocalOriginalTypes = originalTypes;
			var prevUnfinalizedCount = unfinalizedEntries.Count;
			var iterationCount = 0;
			while (prevUnfinalizedCount > 0)
			{
				Debug($"Finalize iteration {iterationCount}: {prevUnfinalizedCount} remaining entries");
				var unfinalizedMembers = new HashSet<MemberInfo>(unfinalizedEntries.Select(entry => entry.Member));
				unfinalizedEntries = unfinalizedEntries.Where(entry => !FinalizeMember(entry, unfinalizedMembers)).ToList();
				var unfinishedCount = unfinalizedEntries.Count;
				if (prevUnfinalizedCount == unfinishedCount)
					throw new InvalidOperationException($"Finalize iteration {iterationCount}: " +
						$"unexpectedly could not process {unfinishedCount} remaining entries:\n" +
						unfinalizedEntries.Join(delimiter: "\n"));
				prevUnfinalizedCount = unfinishedCount;
				iterationCount++;
			}

			threadLocalMemberCache = null;
			threadLocalOriginalTypes = null;

			Debug("Dynamic assembly types:\n\t" + assemblyBuilder.GetTypes().Join(delimiter: "\n\t"));
			if (!(saveDirectory is null))
			{
				assemblyBuilder.Save(saveFileName);
				Debug("Saved dynamic assembly to " + Path.Combine(saveDirectory, saveFileName));
			}
			return assemblyBuilder;
		}

		static readonly BindingFlags allDeclared = AccessTools.all | BindingFlags.DeclaredOnly;

		void PrepareTypeBuilders(Type originalType, ModuleBuilder moduleBuilder, Dictionary<Type, TypeBuilder> declaringTypeBuildersOfOriginalTypes)
		{
			Debug($"PrepareTypeBuilders({originalType}, moduleBuilder: {moduleBuilder}, declaringTypesOfOriginalTypes: ...)");
			// If original type is a nested type, define declaring (containing) types. Note that these declaring types will be minimally defined,
			// only containing their generic type parameters and the necessary members up to the original types.
			var declaringTypeStack = new Stack<Type>();
			declaringTypeStack.Push(originalType);
			var type = originalType.DeclaringType;
			TypeBuilder typeBuilder = null;
			while (!(type is null) && !declaringTypeBuildersOfOriginalTypes.TryGetValue(type, out typeBuilder))
			{
				declaringTypeStack.Push(type);
				type = type.DeclaringType;
			}
			if (typeBuilder is null)
			{
				type = declaringTypeStack.Pop();
				typeBuilder = moduleBuilder.DefineType(type.Namespace + "." + type.Name, type.Attributes, type.BaseType, type.GetInterfaces());
				(declaringTypeStack.Count == 0 ? typeBuilders : declaringTypeBuildersOfOriginalTypes).Add(type, typeBuilder);
			}
			while (declaringTypeStack.Count > 0)
			{
				type = declaringTypeStack.Pop();
				typeBuilder = typeBuilder.DefineNestedType(type.Name, type.Attributes, type.BaseType, type.GetInterfaces());
				(declaringTypeStack.Count == 0 ? typeBuilders : declaringTypeBuildersOfOriginalTypes).Add(type, typeBuilder);
			}
			foreach (var nestedType in type.GetNestedTypes(allDeclared))
				PrepareTypeBuilders(nestedType, typeBuilder);
		}

		void PrepareTypeBuilders(Type type, TypeBuilder declaringTypeBuilder)
		{
			Debug($"PrepareTypeBuilders({type}, declaringTypeBuilder: {declaringTypeBuilder}, declaringTypesOfOriginalTypes: ...)");
			var typeBuilder = declaringTypeBuilder.DefineNestedType(type.Name, type.Attributes, type.BaseType, type.GetInterfaces());
			typeBuilders.Add(type, typeBuilder);
			foreach (var nestedType in type.GetNestedTypes(allDeclared))
				PrepareTypeBuilders(nestedType, typeBuilder);
		}

		TypeBuilder GetDeclaringTypeBuilder(MemberInfo member)
		{
			var declaringType = member.DeclaringType;
			// Non-nested types have null DeclaringType.
			if (declaringType is null)
				return null;
			// Only type definitions have a corresponding TypeBuilder.
			if (declaringType.IsGenericType && !declaringType.IsGenericTypeDefinition)
				declaringType = declaringType.GetGenericTypeDefinition();
			if (typeBuilders.TryGetValue(declaringType, out var typeBuilder))
				return typeBuilder;
			return null;
		}

		// For the given member, determines whether it should be copied, and if so, copies it.
		// If declaringTypeBuilder is not null, that member is known to be within an original type, and so always copies it.
		// If the member is outside all original types, it's not copied, and this method returns the member itself.
		// Else, returns the copied member (typically a *Builder instance).
		// If the copy already exists in memberCache, returns that instead.
		// Return type is object rather than MemberInfo, since some *Builder types don't derive from MemberInfo.
		object CopyMember(MemberInfo member, TypeBuilder declaringTypeBuilder = null)
		{
			if (member is null)
				return null;
			if (memberCache.TryGetValue(member, out var entry))
				return entry.Builder;
			Debug($"CopyMember({member.GetType()}{{{MemberToString(member)}}}, declaringTypeBuilder: {declaringTypeBuilder?.ToString() ?? "null"})");
			switch (member)
			{
			case Type type:
				return CopyType(type, checkDeclaringType: declaringTypeBuilder is null, checkCache: false);
			case MethodInfo method:
				return CopyMethod(method, declaringTypeBuilder, checkCache: false);
			case ConstructorInfo constructor:
				return CopyConstructor(constructor, declaringTypeBuilder, checkCache: false);
			case PropertyInfo property:
				return CopyProperty(property, declaringTypeBuilder, checkCache: false);
			case EventInfo @event:
				return CopyEvent(@event, declaringTypeBuilder, checkCache: false);
			case FieldInfo field:
				return CopyField(field, declaringTypeBuilder, checkCache: false);
			default:
				throw new NotSupportedException("Unsupported member: " + member);
			}
		}

		// Notes regarding the following Copy* methods:
		// Entry is added to cache as soon as the builder is available to prevent potential duplicates, or worse, infinite loops.
		// A lot of this code looks as if it refactored to reduce duplicate code, but *Builder classes don't share any builder interfaces
		// that would allow such a refactoring.

		Type CopyType(Type type, bool checkDeclaringType = true, bool checkCache = true)
		{
			if (checkCache)
			{
				if (type is null)
					return null;
				if (memberCache.TryGetValue(type, out var entry))
					return (Type)entry.Builder;
			}
			Debug($"CopyType({MemberToString(type)}, checkDeclaringType: {checkDeclaringType})");
			// It's possible for CopyType(member) to be called on an original type via a member from another original type before
			// CreateAssembly calls CopyTypeDef on that original type. Since GetDeclaringTypeBuilder(originalType) would return null,
			// and we know CopyTypeDef is guaranteed to be called on the original type, just return the already prepared type builder for it.
			if (originalTypes.Contains(type))
				return typeBuilders[type];
			if (checkDeclaringType && GetDeclaringTypeBuilder(type) is null)
			{
				// Default to given type if it doesn't have one of the original types as a declaring type root.
				// TODO: Handle (generic) types with generic type parameters that contain original member types.
				return type;
			}
			// Generic type parameters are handled in CopyGenericTypeParameters.
			// Afterwards, they should be accessible via memberCache.
			if (type.IsGenericParameter)
				throw new InvalidOperationException($"Member is unexpectedly a generic type parameter: " + type);
			else if (type.IsGenericType && !type.IsGenericTypeDefinition)
				return CopyTypeNonDef(type);
			else
				return CopyTypeDef(type, typeBuilders[type], isDeclaringTypeOfOriginalType: false);
		}

		MethodInfo CopyMethod(MethodInfo method, TypeBuilder declaringTypeBuilder = null, bool checkCache = true)
		{
			if (checkCache)
			{
				if (method is null)
					return null;
				if (memberCache.TryGetValue(method, out var entry))
					return (MethodInfo)entry.Builder;
			}
			Debug($"CopyMethod({MemberToString(method)}, declaringTypeBuilder: {declaringTypeBuilder?.ToString() ?? "null"})");
			if (declaringTypeBuilder is null)
			{
				declaringTypeBuilder = GetDeclaringTypeBuilder(method);
				if (declaringTypeBuilder is null)
				{
					// TODO: Handle outside (generic) methods with generic type parameters that contain original member types.
					// TODO: What to do if outside method has parameters that contain original member types?
					// Default to given member if it doesn't have one of the original types as a declaring type root.
					return method;
				}
			}
			var declaringType = method.DeclaringType;
			var isMethodGenericNonDef = method.IsGenericMethod && !method.IsGenericMethodDefinition;
			var isDeclaringTypeNonDef = declaringType.IsGenericType && !declaringType.IsGenericTypeDefinition;
			if (isMethodGenericNonDef || isDeclaringTypeNonDef)
				return CopyMethodNonDef(method, declaringTypeBuilder, isMethodGenericNonDef, isDeclaringTypeNonDef);
			else
				return CopyMethodDef(method, declaringTypeBuilder);
		}

		ConstructorInfo CopyConstructor(ConstructorInfo constructor, TypeBuilder declaringTypeBuilder = null, bool checkCache = true)
		{
			if (checkCache)
			{
				if (constructor is null)
					return null;
				if (memberCache.TryGetValue(constructor, out var entry))
					return (ConstructorInfo)entry.Builder;
			}
			Debug($"CopyConstructor({MemberToString(constructor)}, declaringTypeBuilder: {declaringTypeBuilder?.ToString() ?? "null"})");
			if (declaringTypeBuilder is null)
			{
				declaringTypeBuilder = GetDeclaringTypeBuilder(constructor);
				if (declaringTypeBuilder is null)
				{
					// TODO: Handle constructors with generic type parameters (from declaring types) that contain original member types.
					// TODO: What to do if outside constructor has parameters that contain original member types?
					// Default to given member if it doesn't have one of the original types as a declaring type root.
					return constructor;
				}
			}
			var declaringType = constructor.DeclaringType;
			if (declaringType.IsGenericType && !declaringType.IsGenericTypeDefinition)
				return CopyConstructorNonDef(constructor, declaringTypeBuilder);
			else
				return CopyConstructorDef(constructor, declaringTypeBuilder);
		}

		PropertyInfo CopyProperty(PropertyInfo property, TypeBuilder declaringTypeBuilder = null, bool checkCache = true)
		{
			if (checkCache)
			{
				if (property is null)
					return null;
				if (memberCache.TryGetValue(property, out var entry))
					return (PropertyInfo)entry.Builder;
			}
			Debug($"CopyProperty({MemberToString(property)}, declaringTypeBuilder: {declaringTypeBuilder?.ToString() ?? "null"})");
			if (declaringTypeBuilder is null)
			{
				declaringTypeBuilder = GetDeclaringTypeBuilder(property);
				// Default to given member if it doesn't have one of the original types as a declaring type root.
				if (declaringTypeBuilder is null)
					return property;
			}
			return CopyPropertyDef(property, declaringTypeBuilder);
		}

		// EventBuilder is not a subclass of EventInfo for some reason, so have to use object return type.
		object CopyEvent(EventInfo @event, TypeBuilder declaringTypeBuilder = null, bool checkCache = true)
		{
			if (checkCache)
			{
				if (@event is null)
					return null;
				if (memberCache.TryGetValue(@event, out var entry))
					return entry.Builder;
			}
			Debug($"CopyEvent({MemberToString(@event)}, declaringTypeBuilder: {declaringTypeBuilder?.ToString() ?? "null"})");
			if (declaringTypeBuilder is null)
			{
				declaringTypeBuilder = GetDeclaringTypeBuilder(@event);
				// Default to given member if it doesn't have one of the original types as a declaring type root.
				if (declaringTypeBuilder is null)
					return @event;
			}
			return CopyEventDef(@event, declaringTypeBuilder);
		}

		FieldInfo CopyField(FieldInfo field, TypeBuilder declaringTypeBuilder = null, bool checkCache = true)
		{
			if (checkCache)
			{
				if (field is null)
					return null;
				if (memberCache.TryGetValue(field, out var entry))
					return (FieldInfo)entry.Builder;
			}
			Debug($"CopyField({MemberToString(field)}, declaringTypeBuilder: {declaringTypeBuilder?.ToString() ?? "null"})");
			if (declaringTypeBuilder is null)
			{
				declaringTypeBuilder = GetDeclaringTypeBuilder(field);
				// Default to given member if it doesn't have one of the original types as a declaring type root.
				if (declaringTypeBuilder is null)
					return field;
			}
			var declaringType = field.DeclaringType;
			if (declaringType.IsGenericType && !declaringType.IsGenericTypeDefinition)
				return CopyFieldNonDef(field, declaringTypeBuilder);
			else
				return CopyFieldDef(field, declaringTypeBuilder);
		}

		Type CopyTypeNonDef(Type type)
		{
			Debug($"CopyTypeNonDef({MemberToString(type)})");
			var genericTypeDefBuilder = (TypeBuilder)CopyType(type.GetGenericTypeDefinition(), checkDeclaringType: false);
			// Note: Nested types inherit all generic arguments from all declaring (containing) types,
			// so there's no need to call CopyType(type.DeclaringType) to recursively ensure all generic arguments are copied.
			var typeBuilder = genericTypeDefBuilder.MakeGenericType(
				type.GetGenericArguments().Select(genericArg => CopyType(genericArg)).ToArray());
			memberCache.Add(type, new TypeNonDefEntry(type, typeBuilder));
			return typeBuilder;
		}

		TypeBuilder CopyTypeDef(Type type, TypeBuilder typeBuilder, bool isDeclaringTypeOfOriginalType)
		{
			Debug($"CopyTypeDef({MemberToString(type)}, typeBuilder: {typeBuilder}, isDeclaringTypeOfOriginalType: {isDeclaringTypeOfOriginalType})");
			var entry = new TypeEntry(type, typeBuilder);
			if (!isDeclaringTypeOfOriginalType)
				memberCache.Add(type, entry);
			if (type.IsGenericType)
			{
				// Note: Nested types inherit all generic arguments from all declaring (containing) types,
				// so there's no need to call CopyType(type.DeclaringType) to recursively ensure all generic arguments are copied.
				var genericArgs = type.GetGenericArguments();
				var genericParameterBuilders =
					typeBuilder.DefineGenericParameters(genericArgs.Select(genericArg => genericArg.Name).ToArray());
				CopyGenericTypeParameters(genericArgs, genericParameterBuilders, isDeclaringTypeOfOriginalType);
			}
			if (!isDeclaringTypeOfOriginalType)
			{
				if (type.BaseType is Type parentType)
					CopyType(parentType);
				foreach (var @interface in type.GetInterfaces())
					CopyType(@interface);
				var customAttributes = CopyCustomAttributes(CustomAttributeData.GetCustomAttributes(type));
				foreach (var customAttribute in customAttributes)
					typeBuilder.SetCustomAttribute(customAttribute);
				foreach (var typeMember in type.GetMembers(allDeclared))
					CopyMember(typeMember, typeBuilder);
			}
			unfinalizedEntries.Add(entry);
			return typeBuilder;
		}

		MethodInfo CopyMethodNonDef(MethodInfo method, TypeBuilder declaringTypeBuilder,
			bool isMethodGenericNonDef, bool isDeclaringTypeNonDef)
		{
			Debug($"CopyMethodNonDef({MemberToString(method)}, declaringTypeBuilder: {declaringTypeBuilder?.ToString() ?? "null"}, " +
				$"isMethodGenericNonDef: {isMethodGenericNonDef}, isDeclaringTypeNonDef: {isDeclaringTypeNonDef})");
			var declaringType = method.DeclaringType;
			// Need to get the (generic) method definition on the (generic) type definition.
			var methodDef = isMethodGenericNonDef ? method.GetGenericMethodDefinition() : method;
			if (isDeclaringTypeNonDef)
				methodDef = (MethodInfo)MethodBase.GetMethodFromHandle(methodDef.MethodHandle,
					declaringType.GetGenericTypeDefinition().TypeHandle);
			// For a builder copy of this method to be referencable in a CIL instruction, we need to use:
			// TypeBuilder.GetMethod(<builder copy of method.DeclaringType>, <builder copy of (generic) method definition]>)
			// Note that <builder copy of method.DeclaringType> isn't declaringTypeBuilder due to either method or method's declaring type
			// being generic yet not generic method/type definitions.
			var methodBuilder = CopyMethod(methodDef, declaringTypeBuilder);
			if (isDeclaringTypeNonDef)
				methodBuilder = TypeBuilder.GetMethod(CopyType(declaringType), methodBuilder);
			if (isMethodGenericNonDef)
				methodBuilder = methodBuilder.MakeGenericMethod(
					method.GetGenericArguments().Select(genericArg => CopyType(genericArg)).ToArray());
			memberCache.Add(method, new MethodNonDefEntry(method, methodBuilder));
			// Need to call CopyType on method's return type or parameter types if they are generic types.
			// From above CopyType(...) calls, we've only guaranteed the following are already copied:
			// a) non-generic types
			// b) generic type definition type parameters
			// c) generic method definition type parameters
			// It notably does NOT include a generic types that nest generic type parameters, e.g. MyGenericType<T>
			// where T is a generic type/method definition type parameter.
			foreach (var parameterType in method.GetParameters()
				.Select(parameter => parameter.ParameterType)
				.Where(parameterType => parameterType.IsGenericType))
				CopyType(parameterType);
			return methodBuilder;
		}

		MethodBuilder CopyMethodDef(MethodInfo method, TypeBuilder declaringTypeBuilder)
		{
			Debug($"CopyMethodDef({MemberToString(method)}, declaringTypeBuilder: {declaringTypeBuilder?.ToString() ?? "null"})");
			var methodBuilder = declaringTypeBuilder.DefineMethod(method.Name, method.Attributes, method.CallingConvention);
			var entry = new MethodEntry(method, methodBuilder);
			memberCache.Add(method, entry);
			// In case declaringType has generic arguments and method arguments need them.
			CopyType(method.DeclaringType);
			// Need to copy generic type parameters first, in case they're used in the method parameters.
			if (method.IsGenericMethod)
			{
				var genericArgs = method.GetGenericArguments();
				var genericTypeParameterBuilder =
					methodBuilder.DefineGenericParameters(genericArgs.Select(genericArg => genericArg.Name).ToArray());
				CopyGenericTypeParameters(genericArgs, genericTypeParameterBuilder, isDeclaringTypeOfOriginalType: false);
			}
			methodBuilder.SetReturnType(CopyType(method.ReturnType));
			var parameters = method.GetParameters();
			methodBuilder.SetParameters(parameters.Select(parameter => CopyType(parameter.ParameterType)).ToArray());
			var returnParameter = method.ReturnParameter;
			var parameterBuilder = methodBuilder.DefineParameter(0, returnParameter.Attributes, returnParameter.Name);
			CopyParameterMisc(returnParameter, parameterBuilder);
			foreach (var parameter in parameters)
			{
				parameterBuilder = methodBuilder.DefineParameter(parameter.Position + 1, parameter.Attributes, parameter.Name);
				CopyParameterMisc(parameter, parameterBuilder);
			}
			var customAttributes = CopyCustomAttributes(CustomAttributeData.GetCustomAttributes(method));
			foreach (var customAttribute in customAttributes)
				methodBuilder.SetCustomAttribute(customAttribute);
			entry.ILGenerator = methodBuilder.GetILGenerator();
			entry.MethodReader = ScanMethod(method, entry.ILGenerator);
			unfinalizedEntries.Add(entry);
			return methodBuilder;
		}

		ConstructorInfo CopyConstructorNonDef(ConstructorInfo constructor, TypeBuilder declaringTypeBuilder)
		{
			Debug($"CopyConstructorNonDef({MemberToString(constructor)}, declaringTypeBuilder: {declaringTypeBuilder?.ToString() ?? "null"})");
			var declaringType = constructor.DeclaringType;
			// Need to get the constructor definition on the (generic) type definition.
			var constructorDef = (ConstructorInfo)MethodBase.GetMethodFromHandle(constructor.MethodHandle,
				declaringType.GetGenericTypeDefinition().TypeHandle);
			// For a builder copy of this constructor to be referencable in a CIL instruction, we need to use:
			// TypeBuilder.GetConstructor(<builder copy of constructor.DeclaringType>, <builder copy of constructor definition>)
			// Note that <builder copy of constructor.DeclaringType> isn't declaringTypeBuilder due to constructor's declaring type
			// being generic yet not a generic type definition.
			var constructorBuilder = TypeBuilder.GetConstructor(CopyType(declaringType),
				CopyConstructor(constructorDef, declaringTypeBuilder));
			memberCache.Add(constructor, new ConstructorNonDefEntry(constructor, constructorBuilder));
			// Need to call CopyType on constructor parameter types if they are generic types.
			// From above CopyType(...) calls, we've only guaranteed the following are already copied:
			// a) non-generic types
			// b) generic type definition type parameters
			// It notably does NOT include a generic types that nest generic type parameters, e.g. MyGenericType<T>.
			foreach (var parameterType in constructor.GetParameters()
				.Select(parameter => parameter.ParameterType)
				.Where(parameterType => parameterType.IsGenericType))
				CopyType(parameterType);
			return constructorBuilder;
		}

		ConstructorBuilder CopyConstructorDef(ConstructorInfo constructor, TypeBuilder declaringTypeBuilder)
		{
			Debug($"CopyConstructorDef({MemberToString(constructor)}, declaringTypeBuilder: {declaringTypeBuilder?.ToString() ?? "null"})");
			// There's no ConstructorBuilder.SetParameters, so we have to CopyType the parameters first.
			var parameters = constructor.GetParameters();
			var parameterTypeBuilders = parameters.Select(parameter => CopyType(parameter.ParameterType)).ToArray();
			var constructorBuilder = declaringTypeBuilder.DefineConstructor(constructor.Attributes, constructor.CallingConvention, parameterTypeBuilders);
			var entry = new ConstructorEntry(constructor, constructorBuilder);
			memberCache.Add(constructor, entry);
			// In case declaringType has generic arguments and constructor arguments need them.
			CopyType(constructor.DeclaringType);
			foreach (var parameter in parameters)
			{
				var parameterBuilder = constructorBuilder.DefineParameter(parameter.Position + 1, parameter.Attributes, parameter.Name);
				CopyParameterMisc(parameter, parameterBuilder);
			}
			var customAttributes = CopyCustomAttributes(CustomAttributeData.GetCustomAttributes(constructor));
			foreach (var customAttribute in customAttributes)
				constructorBuilder.SetCustomAttribute(customAttribute);
			entry.ILGenerator = constructorBuilder.GetILGenerator();
			entry.MethodReader = ScanMethod(constructor, entry.ILGenerator);
			unfinalizedEntries.Add(entry);
			return constructorBuilder;
		}

		PropertyBuilder CopyPropertyDef(PropertyInfo property, TypeBuilder declaringTypeBuilder)
		{
			Debug($"CopyPropertyDef({MemberToString(property)}, declaringTypeBuilder: {declaringTypeBuilder?.ToString() ?? "null"})");
			var propertyTypeBuilder = CopyType(property.PropertyType);
			var parameterTypeBuilders = property.GetIndexParameters().Select(parameter => CopyType(parameter.ParameterType)).ToArray();
			var propertyBuilder = declaringTypeBuilder.DefineProperty(property.Name, property.Attributes, property.PropertyType, parameterTypeBuilders);
			memberCache.Add(property, new PropertyEntry(property, propertyBuilder));
			var customAttributes = CopyCustomAttributes(CustomAttributeData.GetCustomAttributes(property));
			foreach (var customAttribute in customAttributes)
				propertyBuilder.SetCustomAttribute(customAttribute);
			// Note: Although accessor methods will already be included via Type.GetMembers,
			// start copying them now so that we can set the builder's accessors.
			if (property.GetGetMethod(nonPublic: true) is MethodInfo getAccessor)
				propertyBuilder.SetGetMethod((MethodBuilder)CopyMethod(getAccessor, declaringTypeBuilder));
			if (property.GetSetMethod(nonPublic: true) is MethodInfo setAccessor)
				propertyBuilder.SetSetMethod((MethodBuilder)CopyMethod(setAccessor, declaringTypeBuilder));
			return propertyBuilder;
		}

		EventBuilder CopyEventDef(EventInfo @event, TypeBuilder declaringTypeBuilder)
		{
			Debug($"CopyEventDef({MemberToString(@event)}, declaringTypeBuilder: {declaringTypeBuilder?.ToString() ?? "null"})");
			var eventBuilder = declaringTypeBuilder.DefineEvent(@event.Name, @event.Attributes, @event.EventHandlerType);
			memberCache.Add(@event, new EventEntry(@event, eventBuilder));
			var customAttributes = CopyCustomAttributes(CustomAttributeData.GetCustomAttributes(@event));
			foreach (var customAttribute in customAttributes)
				eventBuilder.SetCustomAttribute(customAttribute);
			// Note: Although accessor methods will already be included via Type.GetMembers,
			// start copying them now so that we can set the builder's accessors.
			if (@event.GetAddMethod(nonPublic: true) is MethodInfo getAccessor)
				eventBuilder.SetAddOnMethod((MethodBuilder)CopyMethod(getAccessor, declaringTypeBuilder));
			if (@event.GetRemoveMethod(nonPublic: true) is MethodInfo setAccessor)
				eventBuilder.SetRemoveOnMethod((MethodBuilder)CopyMethod(setAccessor, declaringTypeBuilder));
			if (@event.GetRaiseMethod(nonPublic: true) is MethodInfo raiseAccessor)
				eventBuilder.SetRaiseMethod((MethodBuilder)CopyMethod(raiseAccessor, declaringTypeBuilder));
			return eventBuilder;
		}

		FieldInfo CopyFieldNonDef(FieldInfo field, TypeBuilder declaringTypeBuilder)
		{
			Debug($"CopyFieldNonDef({MemberToString(field)}, declaringTypeBuilder: {declaringTypeBuilder?.ToString() ?? "null"})");
			var declaringType = field.DeclaringType;
			// Need to get the constructor definition on the (generic) type definition.
			var fieldDef = declaringType.GetGenericTypeDefinition().GetField(field.Name, allDeclared);
			// For a builder copy of this field to be referencable in a CIL instruction, we need to use:
			// TypeBuilder.GetField(<builder copy of field.DeclaringType>, <builder copy of field definition>)
			// Note that <builder copy of field.DeclaringType> isn't declaringTypeBuilder due to field's declaring type
			// being generic yet not a generic type definition.
			var fieldBuilder = TypeBuilder.GetField(CopyType(declaringType), CopyField(fieldDef, declaringTypeBuilder));
			memberCache.Add(field, new FieldNonDefEntry(field, fieldBuilder));
			return fieldBuilder;
		}

		FieldBuilder CopyFieldDef(FieldInfo field, TypeBuilder declaringTypeBuilder)
		{
			Debug($"CopyFieldDef({MemberToString(field)}, declaringTypeBuilder: {declaringTypeBuilder?.ToString() ?? "null"})");
			var fieldTypeBuilder = CopyType(field.FieldType);
			var fieldBuilder = declaringTypeBuilder.DefineField(field.Name, fieldTypeBuilder, field.Attributes);
			memberCache.Add(field, new FieldEntry(field, fieldBuilder));
			var customAttributes = CopyCustomAttributes(CustomAttributeData.GetCustomAttributes(field));
			foreach (var customAttribute in customAttributes)
				fieldBuilder.SetCustomAttribute(customAttribute);
			return fieldBuilder;
		}

		void CopyGenericTypeParameters(Type[] genericArgs, GenericTypeParameterBuilder[] genericParameterBuilders, bool isDeclaringTypeOfOriginalType)
		{
			Debug($"CopyGenericTypeParameters({genericArgs.Join()}, genericParameterBuilders: {genericParameterBuilders.Join()})");
			for (var i = 0; i < genericArgs.Length; i++)
			{
				var genericArg = genericArgs[i];
				if (!genericArg.IsGenericParameter)
					throw new InvalidOperationException("Unexpected non-generic type parameter: " + genericArg);
				var genericParameterBuilder = genericParameterBuilders[i];
				if (!isDeclaringTypeOfOriginalType)
					memberCache.Add(genericArg, new TypeNonDefEntry(genericArg, genericParameterBuilder));
				var typeConstraints = genericArg.GetGenericParameterConstraints();
				genericParameterBuilder.SetGenericParameterAttributes(genericArg.GenericParameterAttributes);
				var interfaceConstraints = new List<Type>(typeConstraints.Length);
				foreach (var typeConstraint in typeConstraints)
				{
					CopyType(typeConstraint);
					if (typeConstraint.IsInterface)
						interfaceConstraints.Add(typeConstraint);
					else
						genericParameterBuilder.SetBaseTypeConstraint(typeConstraint);
				}
				if (interfaceConstraints.Count > 0)
					genericParameterBuilder.SetInterfaceConstraints(interfaceConstraints.ToArray());
			}
		}

		IEnumerable<CustomAttributeBuilder> CopyCustomAttributes(IList<CustomAttributeData> customAttributes)
		{
			Debug($"CopyCustomAttributes({customAttributes.Join()})");
			foreach (var customAttribute in customAttributes)
			{
				CopyType(customAttribute.Constructor.DeclaringType);
				var namedArguments = customAttribute.NamedArguments;
				var namedArgumentsCount = namedArguments.Count;
				var namedProperties = new List<PropertyInfo>(namedArgumentsCount);
				var propertyVals = new List<object>(namedArgumentsCount);
				var namedFields = new List<FieldInfo>(namedArgumentsCount);
				var fieldVals = new List<object>(namedArgumentsCount);
				foreach (var namedArg in namedArguments)
				{
					var namedMember = namedArg.MemberInfo;
					if (namedMember is PropertyInfo namedProperty)
					{
						namedProperties.Add(namedProperty);
						propertyVals.Add(namedArg.TypedValue.Value);
					}
					else if (namedMember is FieldInfo namedField)
					{
						namedFields.Add(namedField);
						fieldVals.Add(namedArg.TypedValue.Value);
					}
				}
				yield return new CustomAttributeBuilder(customAttribute.Constructor,
					customAttribute.ConstructorArguments.Select(arg => arg.Value).ToArray(),
					namedProperties.ToArray(), propertyVals.ToArray(),
					namedFields.ToArray(), fieldVals.ToArray());
			}
		}

		void CopyParameterMisc(ParameterInfo parameter, ParameterBuilder parameterBuilder)
		{
			Debug($"CopyParameterMisc({parameter}, parameterBuilder: {parameterBuilder})");
			// Note: CopyType(parameter.ParameterType) should already have been done by now.
			if ((parameter.Attributes & ParameterAttributes.HasDefault) == ParameterAttributes.HasDefault)
				parameterBuilder.SetConstant(parameter.DefaultValue);
			var customAttributes = CopyCustomAttributes(CustomAttributeData.GetCustomAttributes(parameter));
			foreach (var customAttribute in customAttributes)
				parameterBuilder.SetCustomAttribute(customAttribute);
		}

		static readonly FieldInfo localsField = typeof(MethodBodyReader).GetField("locals", AccessTools.all);
		static readonly FieldInfo ilInstructionsField = typeof(MethodBodyReader).GetField("ilInstructions", AccessTools.all);

		MethodBodyReader ScanMethod(MethodBase methodBase, ILGenerator ilGenerator)
		{
			Debug($"ScanMethod({MemberToString(methodBase)}, ilGenerator: ...)");
			// Don't try to scan/copy extern methods or any other method that somehow has no method body.
			if (methodBase.GetMethodBody() is null)
				return null;
			var methodReader = new MethodBodyReader(methodBase, ilGenerator);
			var locals = (IList<LocalVariableInfo>)localsField.GetValue(methodReader);
			Debug("Locals:" + (locals.Count == 0 ? " (none)" : "\n\t" + locals.Select((local, i) => $"{i:d2}: {local}").Join(delimiter: "\n\t")));
			methodReader.DeclareVariables(locals.Select(local => ilGenerator.DeclareLocal(CopyType(local.LocalType), local.IsPinned)).ToArray());
			methodReader.ReadInstructions();
			var instructions = (IList<ILInstruction>)ilInstructionsField.GetValue(methodReader);
			Debug($"Instructions:\n\t" + instructions.Select((instruction, i) => $"{i:x4}: {instruction}").Join(delimiter: "\n\t"));
			foreach (var instruction in instructions)
			{
				if (instruction.operand is MemberInfo member)
					CopyMember(member);
				foreach (var block in instruction.blocks)
					CopyType(block.catchType);
			}
			return methodReader;
		}

		bool FinalizeMember(Entry entry, HashSet<MemberInfo> unfinalizedMembers)
		{
			Debug($"FinalizeMember({entry}, unfinalizedMembers: ...)");
			var isFinalized = false;
			if (entry is TypeEntry typeEntry)
			{
				if (typeEntry.Type.GetMembers(allDeclared).All(typeMember => !unfinalizedMembers.Contains(typeMember)))
				{
					var createdType = typeEntry.TypeBuilder.CreateType();
					if (createdType == typeEntry.TypeBuilder)
						throw new InvalidOperationException($"{entry}.TypeBuilder.CreateType() is unexpectedly the same as typeEntry.TypeBuilder: " +
							createdType);
					isFinalized = true;
				}
			}
			else if (entry is MethodDefEntry methodBaseEntry)
			{
				if (!(methodBaseEntry.MethodReader is null))
					FinalizeMethod(methodBaseEntry.MethodBase, methodBaseEntry.MethodReader, methodBaseEntry.ILGenerator);
				isFinalized = true;
			}
			else
			{
				throw new InvalidOperationException($"Unexpected {entry.Member.MemberType} member: {entry.Member}");
			}
			Debug($"FinalizeMember({entry}, unfinalizedMembers: ...) => {isFinalized}");
			return isFinalized;
		}

		// This is called during the finalize phase. Technically, method/constructor definitions could be finalized right after ScanMethod,
		// but that can lead to convoluted stack (traces) during processing, and it's just cleaner to do it later during the finalize phase.
		void FinalizeMethod(MethodBase methodBase, MethodBodyReader methodReader, ILGenerator ilGenerator)
		{
			Debug($"FinalizeMethod({MemberToString(methodBase)}, methodReader: ..., ilGenerator: ...)");
			var transpilers = new List<MethodInfo>() { memberTranslationTranspiler };
			if (methodTranspilers.TryGetValue(methodBase, out var transpiler))
				transpilers.Add(transpiler);
			var endLabels = new List<Label>();
			var endBlocks = new List<ExceptionBlock>();
			methodReader.FinalizeILCodes(transpilers, endLabels, endBlocks);
			foreach (var label in endLabels)
				Emitter.MarkLabel(ilGenerator, label);
			foreach (var block in endBlocks)
				Emitter.MarkBlockAfter(ilGenerator, block);
			Emitter.Emit(ilGenerator, OpCodes.Ret);
		}

		static readonly MethodInfo memberTranslationTranspiler = typeof(TypeCopier).GetMethod(nameof(MemberTranslationTranspiler), AccessTools.all);

		static IEnumerable<CodeInstruction> MemberTranslationTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase sourceMethod)
		{
			foreach (var instruction in instructions)
			{
				if (instruction.operand is MemberInfo member)
				{
					if (threadLocalMemberCache.TryGetValue(member, out var memberEntry))
					{
						Debug($"MemberTranslationTranspiler: {instruction}\n\treplacing {MemberToString(member)}\n\twith " +
							memberEntry.Builder.GetType());
						yield return new CodeInstruction(instruction.opcode, memberEntry.Builder)
						{
							labels = instruction.labels,
							blocks = instruction.blocks,
						};
						continue;
					}
					// If the member is internal (access modifier) and wasn't found in the member cache, the dynamic assembly can't access it.
					// ldtoken instructions are apparently exempt from this accessibility check.
					// TODO: Try to use reflection within the method to access such members? Possible edge cases like ldftn/ldvirtftn.
					if (instruction.opcode != OpCodes.Ldtoken && !IsAccessible(sourceMethod, member))
					{
						//throw new MethodAccessException($"Attempt by method '{MemberToString(sourceMethod)}' to access " +
						//	$"'{MemberToString(member)}' failed.");
						Warning($"Method '{MemberToString(sourceMethod)}' cannot access '{MemberToString(member)}' yet still attempts access");
					}
				}
				yield return instruction;
			}
		}

		static bool IsAccessible(MethodBase sourceMethod, MemberInfo targetMember)
		{
			Debug($"IsAccessible(sourceMethod: {MemberToString(sourceMethod)}, targetMember: {MemberToString(targetMember)})");
			// If the source method is accessing a target member outside of the member cache (i.e. original types and their contents),
			// it's one of the following possibilities:
			// 1) The target member is public, and its declaring type is accessible, which means its declaring type:
			// 1a) is public, and its declaring type is accessible (recursively); or
			// 1b) is the same as or is a parent type of any of the source method's declaring types.
			// 2) The target member is protected (protected, protected internal, or protected private), and its declaring type:
			// 2a) is a parent type of the source method's declaring type; or
			// 2b) is the same as or is a parent type of any of the declaring types of the source method's declaring type (a nested type).
			// 3) The target member is internal (internal, protected internal, or protected private).
			// 4) The target member is private, and its declaring type is the same as any of the declaring types of the source method's
			//    declaring type (a nested type).
			// Of all these possibilities, only case 1 and 3 are valid within the dynamic assembly, with the following change to 1b and 2b:
			// The target member's declaring type is a parent type of any of the source method's declaring types **that are in the member cache**.
			// Type comparisons and parent type checks should be agnostic to generics as needed.
			// Also, "parent type" includes both classes and interfaces (due to default interface members).
			if (targetMember is Type targetType)
				return IsAccessible(sourceMethod, targetType);
			if (targetMember is MethodBase targetMethodBase)
				return IsAccessible(sourceMethod, targetMethodBase);
			if (targetMember is FieldInfo targetField)
				return IsAccessible(sourceMethod, targetField);
			else
				throw new InvalidOperationException($"Unexpected {targetMember.MemberType} member: {targetMember}");
		}

		static bool IsAccessible(MethodBase sourceMethod, Type targetType)
		{
			var access = targetType.Attributes & TypeAttributes.VisibilityMask;
			Debug($"{targetType.MemberType} accessibility: {AccessibilityToString(access)}");
			if (access == TypeAttributes.Public)
				return true;
			else if (access == TypeAttributes.NestedPublic)
				return IsAccessible(sourceMethod, targetType.DeclaringType);
			else if (access == TypeAttributes.NestedFamily || access == TypeAttributes.NestedFamANDAssem || access == TypeAttributes.NestedFamORAssem)
				return IsTargetDeclaringTypeAncestorTypeOfSourceDeclaringTypes(sourceMethod, targetType);
			else
				return false;
		}

		static bool IsTargetDeclaringTypeAncestorTypeOfSourceDeclaringTypes(MethodBase sourceMethod, MemberInfo targetMember)
		{
			var targetDeclaringType = targetMember.DeclaringType;
			var sourceDeclaringType = sourceMethod.DeclaringType;
			while (!(sourceDeclaringType is null) && threadLocalMemberCache.ContainsKey(sourceDeclaringType))
			{
				if (IsAncestorTypeOf(targetDeclaringType, sourceDeclaringType))
					return true;
				sourceDeclaringType = sourceDeclaringType.DeclaringType;
			}
			return false;
		}

		static bool IsAccessible(MethodBase sourceMethod, MethodBase targetMethodBase)
		{
			var access = targetMethodBase.Attributes & MethodAttributes.MemberAccessMask;
			Debug($"{targetMethodBase.MemberType} accessibility: {AccessibilityToString(access)}");
			if (access == MethodAttributes.Public)
				return IsAccessible(sourceMethod, targetMethodBase.DeclaringType);
			else if (access == MethodAttributes.Family || access == MethodAttributes.FamANDAssem || access == MethodAttributes.FamORAssem)
				return IsTargetDeclaringTypeAncestorTypeOfSourceDeclaringTypes(sourceMethod, targetMethodBase);
			else
				return false;
		}

		static bool IsAccessible(MethodBase sourceMethod, FieldInfo targetField)
		{
			var access = targetField.Attributes & FieldAttributes.FieldAccessMask;
			Debug($"{targetField.MemberType} accessibility: {AccessibilityToString(access)}");
			if (access == FieldAttributes.Public)
				return IsAccessible(sourceMethod, targetField.DeclaringType);
			else if (access == FieldAttributes.Family || access == FieldAttributes.FamANDAssem || access == FieldAttributes.FamORAssem)
				return IsTargetDeclaringTypeAncestorTypeOfSourceDeclaringTypes(sourceMethod, targetField);
			else
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

		static string MemberToString(MemberInfo member)
		{
			var str = member.DeclaringType is Type declaringType ? MemberToString(declaringType) + "." + member.Name : member.Name;
			if (member is Type type)
			{
				if (type.IsGenericType)
					str += "<" + type.GetGenericArguments().Join(delimiter: ",") + ">";
			}
			else if (member is MethodBase methodBase)
			{
				if (methodBase is MethodInfo method && method.IsGenericMethod)
					str += "<" + method.GetGenericArguments().Join(delimiter: ",") + ">()";
				str += "(" + methodBase.GetParameters().Join(parameter => MemberToString(parameter.ParameterType), delimiter: ", ") + ")";
			}
			return str;
		}

		// Workaround for TypeAttributes being such a messed up enum.
		static string AccessibilityToString(TypeAttributes attributes)
		{
			switch (attributes & TypeAttributes.VisibilityMask)
			{
			case TypeAttributes.NotPublic:
				return "NotPublic";
			case TypeAttributes.Public:
				return "Public";
			case TypeAttributes.NestedPublic:
				return "NestedPublic";
			case TypeAttributes.NestedPrivate:
				return "NestedPrivate";
			case TypeAttributes.NestedFamily:
				return "NestedFamily";
			case TypeAttributes.NestedAssembly:
				return "NestedAssembly";
			case TypeAttributes.NestedFamANDAssem:
				return "NestedFamANDAssem";
			case TypeAttributes.NestedFamORAssem:
				return "NestedFamORAssem";
			default:
				return null;
			}
		}

		// Workaround for MethodAttributes being such a messed up enum.
		static string AccessibilityToString(MethodAttributes attributes)
		{
			switch (attributes & MethodAttributes.MemberAccessMask)
			{
			case MethodAttributes.Private:
				return "Private";
			case MethodAttributes.FamANDAssem:
				return "FamANDAssem";
			case MethodAttributes.Assembly:
				return "Assembly";
			case MethodAttributes.Family:
				return "Family";
			case MethodAttributes.FamORAssem:
				return "FamORAssem";
			case MethodAttributes.Public:
				return "Public";
			default:
				return null;
			}
		}

		// Workaround for FieldAttributes being such a messed up enum.
		static string AccessibilityToString(FieldAttributes attributes)
		{
			switch (attributes & FieldAttributes.FieldAccessMask)
			{
			case FieldAttributes.Private:
				return "Private";
			case FieldAttributes.FamANDAssem:
				return "FamANDAssem";
			case FieldAttributes.Assembly:
				return "Assembly";
			case FieldAttributes.Family:
				return "Family";
			case FieldAttributes.FamORAssem:
				return "FamORAssem";
			case FieldAttributes.Public:
				return "Public";
			default:
				return null;
			}
		}

		static void Debug(string str) => Language.Logging.Log(str);

		static void Warning(string str) => Language.Logging.Log("WARNING: " + str);
	}
}
