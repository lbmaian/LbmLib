using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using Harmony;
using Microsoft.CSharp;

namespace TranslationFilesGenerator
{
	public static class HarmonyTranspilerExtensions
	{
		public static string ToDebugString(this CodeInstruction instruction)
		{
			// Certain operands aren't being special-cased in CodeInstruction.ToString, so replace them with better ToString's.
			// Or in the case of LocalBuilder, replaced it's ToString of the its LocalType with a better ToString.
			var str = instruction.ToString();
			if (instruction.operand is Type type)
				str = str.Replace(type.ToString(), type.ToDebugString());
			else if (instruction.operand is FieldInfo field)
				str = str.Replace(field.ToString(), field.ToDebugString());
			else if (instruction.operand is MethodBase method)
				str = str.Replace(method.ToString(), method.ToDebugString());
			else if (instruction.operand is LocalBuilder localBuilder)
				str = str.Replace(localBuilder.LocalType.ToString(), localBuilder.LocalType.ToDebugString());
			else if (instruction.operand is null && instruction.opcode.OperandType == OperandType.InlineNone)
				str = str.Replace(" NULL", "");
			return str;
		}

		public static string ItemToDebugString(this List<CodeInstruction> instructions, int index, string label = "")
		{
			if (index < 0 || index >= instructions.Count)
				throw new ArgumentOutOfRangeException($"{label}{index}: <out of range [0..{instructions.Count - 1}]>");
			return $"{label}{index}: {instructions[index].ToDebugString()}";
		}

		public static string RangeToDebugString(this List<CodeInstruction> instructions, int startIndex, int count, string label="", string delimiter = "\n\t")
		{
			var sb = new StringBuilder(label);
			for (int index = startIndex; index < startIndex + count; index++)
			{
				if (index != startIndex)
					sb.Append(delimiter);
				sb.Append(instructions.ItemToDebugString(index));
			}
			return sb.ToString();
		}

		public static string ToDebugString(this List<CodeInstruction> instructions, string label="", string delimiter = "\n\t")
		{
			return instructions.RangeToDebugString(0, instructions.Count, label, delimiter);
		}

		public static List<CodeInstruction> CloneRange(this List<CodeInstruction> instructions, int index, int count)
		{
			var clonedInstructions = new List<CodeInstruction>(instructions.Count);
			for (int i = 0; i < count; i++)
			{
				clonedInstructions.Add(instructions[index + i].Clone());
			}
			return clonedInstructions;
		}

		// Replaces ldloc.<num>/stloc.<num> instructions with ldloc.s/stloc.s instructions with (potentially dummy) LocalBuilder operands.
		// This allows searching instructions for any local variable access via ldloc.s/stloc.s (assuming there's no ldloc/stloc, which is usually a safe assumption).
		// Ensure that ReoptimizeLocalVarInstructions is called afterwards to reverts ldloc.s/stloc.s instructions back to ldloc.<num>/stloc.<num> instructions.
		// Returns the passed (and changed) instructions for convenience.
		public static IEnumerable<CodeInstruction> DeoptimizeLocalVarInstructions(this IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
		{
			LocalBuilder[] localBuilders;
			// Mono .NET mscorlib implementation of ILGenerator has a LocalBuilder[] field we can use.
			if (ilGenerator.GetType().GetFields(AccessTools.all).Where(field => field.FieldType == typeof(LocalBuilder[])).FirstOrDefault() is FieldInfo localBuildersField)
			{
				localBuilders = (LocalBuilder[])localBuildersField.GetValue(ilGenerator);
			}
			else
			{
				// Assume we're using MS .NET mscorlib implementation. We'll have to construct dummy LocalBuilder's.
				var localBuilderConstructor = typeof(LocalBuilder).GetConstructor(AccessTools.all, null, new[] { typeof(int), typeof(Type), typeof(MethodInfo), typeof(bool) }, null);
				if (localBuilderConstructor == null)
					throw new InvalidOperationException("Could find neither existing LocalBuilder's on ILGenerator nor an expected LocalBuilder constructor");
				var methodInfoField = ilGenerator.GetType().GetFields(AccessTools.all).Where(field => typeof(MethodInfo).IsAssignableFrom(field.FieldType)).FirstOrDefault();
				if (methodInfoField == null)
					throw new InvalidOperationException("Could find neither existing LocalBuilder's nor MethodInfo on ILGenerator");
				var method = (MethodInfo)methodInfoField.GetValue(ilGenerator);
				var localVars = method.GetMethodBody().LocalVariables;
				localBuilders = new LocalBuilder[Math.Min(4, localVars.Count)];
				for (var localVarIndex = 0; localVarIndex < localBuilders.Length; localVarIndex++)
				{
					var localVar = localVars[localVarIndex];
					localBuilders[localVarIndex] = (LocalBuilder)localBuilderConstructor.Invoke(new object[] { localVar.LocalIndex, localVar.LocalType, method, localVar.IsPinned });
				}
			}
			foreach (var instruction in instructions)
			{
				var opcode = instruction.opcode;
				var localVarIndex = -1;
				if (opcode == OpCodes.Ldloc_0 || opcode == OpCodes.Stloc_0)
					localVarIndex = 0;
				else if (opcode == OpCodes.Ldloc_1 || opcode == OpCodes.Stloc_1)
					localVarIndex = 1;
				else if (opcode == OpCodes.Ldloc_2 || opcode == OpCodes.Stloc_2)
					localVarIndex = 2;
				else if (opcode == OpCodes.Ldloc_3 || opcode == OpCodes.Stloc_3)
					localVarIndex = 3;
				if (localVarIndex != -1)
				{
					if (opcode.StackBehaviourPush == StackBehaviour.Push1)
						instruction.opcode = OpCodes.Ldloc_S;
					else // opcode.StackBehaviorPop == StackBehavior.Pop1
						instruction.opcode = OpCodes.Stloc_S;
					instruction.operand = localBuilders[localVarIndex];
				}
			}
			return instructions;
		}

		static readonly OpCode[] ldlocNumOpCodes =
		{
			OpCodes.Ldloc_0,
			OpCodes.Ldloc_1,
			OpCodes.Ldloc_2,
			OpCodes.Ldloc_3,
		};

		static readonly OpCode[] stlocNumOpCodes =
		{
			OpCodes.Stloc_0,
			OpCodes.Stloc_1,
			OpCodes.Stloc_2,
			OpCodes.Stloc_3,
		};

		// The inverse of DeoptimizeLocalVarInstructions that should be called after all changes to instructions are done,
		// to revert ldloc.s/stloc.s instructions back to ldloc.<num>/stloc.<num> instructions and remove the potentially dummy LocalBuilder operands.
		// Returns the passed (and changed) instructions for convenience.
		public static IEnumerable<CodeInstruction> ReoptimizeLocalVarInstructions(this IEnumerable<CodeInstruction> instructions)
		{
			foreach (var instruction in instructions)
			{
				var opcode = instruction.opcode;
				if (opcode == OpCodes.Ldloc_S || opcode == OpCodes.Stloc_S)
				{
					var localBuilder = (LocalBuilder)instruction.operand;
					int localVarIndex = localBuilder.LocalIndex;
					if (localVarIndex < 4)
					{
						if (opcode == OpCodes.Ldloc_S)
							instruction.opcode = ldlocNumOpCodes[localVarIndex];
						else // opcode == OpCodes.Stloc_S
							instruction.opcode = stlocNumOpCodes[localVarIndex];
						instruction.operand = null;
					}
				}
			}
			return instructions;
		}

		public static Predicate<CodeInstruction> AsInstructionPredicate(this OpCode opcode)
		{
			return instruction => instruction.opcode == opcode;
		}

		public static Predicate<CodeInstruction> AsInstructionPredicate(this OpCode opcode, object operand)
		{
			return instruction => instruction.opcode == opcode && instruction.operand == operand;
		}

		public static Predicate<CodeInstruction> AsInstructionPredicate<T>(this OpCode opcode, Predicate<T> operandPredicate) where T : class
		{
			return instruction => instruction.opcode == opcode && instruction.operand is T typedOperand && operandPredicate.Invoke(typedOperand);
		}

		public static Predicate<LocalBuilder> AsLocalVarTypePredicate(this Type localVarType, bool useIsAssignableFrom = false)
		{
			if (useIsAssignableFrom)
				return localVar => localVarType.IsAssignableFrom(localVar.LocalType);
			else
				return localVar => localVarType == localVar.LocalType;
		}

		public static Predicate<FieldInfo> AsFieldTypePredicate(this Type fieldType, string fieldName = null, bool useIsAssignableFrom = false)
		{
			if (fieldName != null)
			{
				if (useIsAssignableFrom)
					return field => fieldType.IsAssignableFrom(field.FieldType) && field.Name == fieldName;
				else
					return field => fieldType == field.FieldType && field.Name == fieldName;
			}
			else
			{
				if (useIsAssignableFrom)
					return field => fieldType.IsAssignableFrom(field.FieldType);
				else
					return field => fieldType == field.FieldType;
			}
		}

		// Convenience method for changing an intruction's opcode and operand, while keeping labels and blocks.
		public static void SetTo(this CodeInstruction instruction, OpCode opcode, object operand = null)
		{
			instruction.opcode = opcode;
			instruction.operand = operand;
		}
	}
}
