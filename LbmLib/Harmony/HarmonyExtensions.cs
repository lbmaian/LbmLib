/*
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using Harmony.ILCopying;
using LbmLib.Language;

// TODO: This is a bad idea - do NOT use. Better to use an updated version of Harmony and take advantage of self-patching.
namespace LbmLib.Harmony
{
	public static class HarmonyExtensions
	{
		const string HarmonyId = "HarmonyExtensions";

		static HarmonyInstance HarmonyExt;

		public static void PatchHarmony()
		{
			if (HarmonyExt is null)
			{
				HarmonyExt = HarmonyInstance.Create(HarmonyId);
			}

			var emitterFormatArgumentMethod = typeof(Emitter).GetMethod(nameof(Emitter.FormatArgument));
			if (HarmonyExt.GetPatchInfo(emitterFormatArgumentMethod) is null)
			{
				HarmonyExt.Patch(emitterFormatArgumentMethod,
					prefix: new HarmonyMethod(typeof(HarmonyExtensions).GetMethod(nameof(Emitter_FormatArgument_PrefixPatch), AccessTools.all)));
			}

			var emitterLogLocalVariableMethod = typeof(Emitter).GetMethod(nameof(Emitter.LogLocalVariable));
			if (HarmonyExt.GetPatchInfo(emitterLogLocalVariableMethod) is null)
			{
				HarmonyExt.Patch(emitterLogLocalVariableMethod,
					transpiler: new HarmonyMethod(typeof(HarmonyExtensions).GetMethod(nameof(Emitter_LogLocalVariable_TranspilerPatch), AccessTools.all)));
			}

			var harmonyInstancePatchAllMethod = typeof(HarmonyInstance).GetMethod(nameof(HarmonyInstance.PatchAll), new[] { typeof(Assembly) });
			if (HarmonyExt.GetPatchInfo(harmonyInstancePatchAllMethod) is null)
			{
				HarmonyExt.Patch(harmonyInstancePatchAllMethod,
					prefix: new HarmonyMethod(typeof(HarmonyExtensions).GetMethod(nameof(HarmonyInstance_PatchAll_PrefixPatch), AccessTools.all)));
			}

			var codeTranspilerConvertToGeneralInstructionsMethod = typeof(CodeTranspiler).GetMethod(nameof(CodeTranspiler.ConvertToGeneralInstructions));
			if (HarmonyExt.GetPatchInfo(codeTranspilerConvertToGeneralInstructionsMethod) is null)
			{
				HarmonyExt.Patch(codeTranspilerConvertToGeneralInstructionsMethod,
					prefix: new HarmonyMethod(typeof(HarmonyExtensions).GetMethod(nameof(CodeTranspiler_ConvertToGeneralInstructions_PrefixPatch), AccessTools.all)));
			}

			var codeTranspilerGetTranspilerCallParametersMethod = typeof(CodeTranspiler).GetMethod(nameof(CodeTranspiler.GetTranspilerCallParameters));
			if (HarmonyExt.GetPatchInfo(codeTranspilerGetTranspilerCallParametersMethod) is null)
			{
				HarmonyExt.Patch(codeTranspilerGetTranspilerCallParametersMethod,
					prefix: new HarmonyMethod(typeof(HarmonyExtensions).GetMethod(nameof(CodeTranspiler_GetTranspilerCallParameters_PrefixPatch), AccessTools.all)));
			}
		}

		static bool Emitter_FormatArgument_PrefixPatch(ref string __result, object argument)
		{
			__result = HarmonyTranspilerDebugExtensions.OperandToDebugString(argument);
			return false;
		}

		static IEnumerable<CodeInstruction> Emitter_LogLocalVariable_TranspilerPatch(IEnumerable<CodeInstruction> instructions)
		{
			var typeFullNameGetMethod = typeof(Type).GetProperty(nameof(Type.FullName)).GetGetMethod();
			var toDebugStringMethod = typeof(DebugExtensions).GetMethod(nameof(DebugExtensions.ToDebugString), new[] { typeof(Type) });
			foreach (var instruction in instructions)
			{
				if (instruction.opcode == OpCodes.Callvirt && (MethodInfo)instruction.operand == typeFullNameGetMethod)
					instruction.SetTo(OpCodes.Call, toDebugStringMethod);
				yield return instruction;
			}
		}

		static bool HarmonyInstance_PatchAll_PrefixPatch(HarmonyInstance __instance, Assembly assembly)
		{
			assembly.GetTypes().Do(type =>
			{
				var parentMethodInfos = type.GetHarmonyMethods();
				if (parentMethodInfos != null && parentMethodInfos.Count() > 0)
				{
					var info = HarmonyMethod.Merge(parentMethodInfos);
					using (new HarmonyWithDebug(type.IsDefined(typeof(HarmonyDebugAttribute), false)))
					{
						var processor = new PatchProcessor(__instance, type, info);
						processor.Patch();
					}
				}
			});
			return false;
		}

		static bool CodeTranspiler_ConvertToGeneralInstructions_PrefixPatch(ref IEnumerable __result,
			MethodInfo transpiler, IEnumerable enumerable, out Dictionary<object, Dictionary<string, object>> unassignedValues)
		{
			var type = transpiler.GetParameters()
				.Select(p => p.ParameterType)
				.FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition().Name is var name &&
					(name.StartsWith("IEnumerable", StringComparison.Ordinal) || name.StartsWith("ICollection", StringComparison.Ordinal) ||
					name.StartsWith("IList", StringComparison.Ordinal) || name.StartsWith("List", StringComparison.Ordinal)));
			__result = CodeTranspiler.ConvertInstructionsAndUnassignedValues(type, enumerable, out unassignedValues);
			return false;
		}

		static bool CodeTranspiler_GetTranspilerCallParameters_PrefixPatch(ref List<object> __result,
			ILGenerator generator, MethodInfo transpiler, MethodBase method, IEnumerable instructions)
		{
			var parameters = new List<object>();
			var instructionType = instructions.GetType().GetGenericArguments()[0];
			var transpilerContextType = typeof(TranspilerContext<>).MakeGenericType(instructionType);
			transpiler.GetParameters().Select(param => param.ParameterType).Do(type =>
			{
				if (type.IsAssignableFrom(typeof(ILGenerator)))
					parameters.Add(generator);
				else if (type.IsAssignableFrom(typeof(MethodBase)))
					parameters.Add(method);
				else if (type == transpilerContextType)
				{
					var transpilerContextConstructor = transpilerContextType.GetConstructor(new[] { typeof(ILGenerator), typeof(MethodBase), typeof(IEnumerable) });
					parameters.Add(transpilerContextConstructor.Invoke(new object[] { generator, method, instructions }));
				}
				else
					parameters.Add(instructions);
			});
			__result = parameters;
			return false;
		}
	}
}
*/
