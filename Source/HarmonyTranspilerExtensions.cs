﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Harmony;
using Harmony.ILCopying;

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

		public static string RangeToDebugString(this List<CodeInstruction> instructions, int startIndex, int count, string label = "", string delimiter = "\n\t")
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

		public static string ToDebugString(this List<CodeInstruction> instructions, string label = "", string delimiter = "\n\t")
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

		// Gets the first label of an instruction, adding a new one if it doesn't exist.
		public static Label FirstOrNewAddedLabel(this CodeInstruction instruction, ILGenerator ilGenerator)
		{
			return instruction.labels.AddDefaultIfEmpty(() => ilGenerator.DefineLabel())[0];
		}

		// Convenience method for changing an intruction's opcode and operand, while keeping labels and blocks.
		public static void SetTo(this CodeInstruction instruction, OpCode opcode, object operand = null)
		{
			instruction.opcode = opcode;
			instruction.operand = operand;
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
				// Assume we're using MS .NET Framework mscorlib implementation. We'll have to construct dummy LocalBuilder's.
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

		static readonly OpCode[] LdlocNumOpCodes =
		{
			OpCodes.Ldloc_0,
			OpCodes.Ldloc_1,
			OpCodes.Ldloc_2,
			OpCodes.Ldloc_3,
		};

		static readonly OpCode[] StlocNumOpCodes =
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
							instruction.opcode = LdlocNumOpCodes[localVarIndex];
						else // opcode == OpCodes.Stloc_S
							instruction.opcode = StlocNumOpCodes[localVarIndex];
						instruction.operand = null;
					}
				}
			}
			return instructions;
		}

		public static void AddTryFinally(this List<CodeInstruction> methodInstructions, MethodBase method, ILGenerator ilGenerator,
			IList<CodeInstruction> finallyInstructions)
		{
			AddTryFinally(methodInstructions, method, ilGenerator, 0, methodInstructions.Count - 1, finallyInstructions);
		}

		public static void AddTryFinally(this List<CodeInstruction> methodInstructions, MethodBase method, ILGenerator ilGenerator,
			int tryStartIndex, int finallyInsertIndex, IList<CodeInstruction> finallyBlockInstructions)
		{
			if (tryStartIndex >= finallyInsertIndex)
				throw new ArgumentException($"tryStartIndex ({tryStartIndex}) cannot be >= finallyInsertIndex ({finallyInsertIndex})");
			if (finallyBlockInstructions.Count == 0)
				throw new ArgumentException($"finallyBlockInstructions.Count ({finallyBlockInstructions.Count}) cannot be 0");
			if (tryStartIndex < 0)
				throw new ArgumentOutOfRangeException($"tryStartIndex ({tryStartIndex}) cannot be < 0");
			var instructionCount = methodInstructions.Count;
			if (finallyInsertIndex >= instructionCount)
				throw new ArgumentOutOfRangeException($"finallyInsertIndex ({finallyInsertIndex}) cannot be >= methodInstructions.Count ({instructionCount})");

			var labelsWithinTryBlock = new HashSet<Label>();
			for (var index = tryStartIndex; index < finallyInsertIndex; index++)
			{
				foreach (var label in methodInstructions[index].labels)
					labelsWithinTryBlock.Add(label);
			}

			// Validate that the finally block instructions cannot have leave(.s), ret, rethrow, and jmp instructions,
			// and cannot have branch-type instructions (including conditional branches and switch) that target outside of the finally block.
			// It's the caller's responsibility to ensure this validation succeeds.
			var finallyInstructionCount = finallyBlockInstructions.Count;
			for (var index = 0; index < finallyInstructionCount; index++)
			{
				var opcode = finallyBlockInstructions[index].opcode;
				if (opcode == OpCodes.Leave || opcode == OpCodes.Leave_S || opcode == OpCodes.Ret || opcode == OpCodes.Rethrow || opcode == OpCodes.Jmp)
					throw new ArgumentException($"finallyBlockInstructions[{index}] {finallyBlockInstructions[index]} has invalid opcode in finally block");
			}
			foreach (var labelRef in InvalidBranchInstructions(finallyBlockInstructions, "finallyBlockInstructions", 0, finallyInstructionCount, validLabels: labelsWithinTryBlock))
			{
				throw new ArgumentException($"finallyBlockInstructions[{labelRef.Index}] {labelRef.Instruction} targets a label outside of finally block: {labelRef.Label}");
			}

			// Validate that branch instructions outside the try block cannot target inside the try block.
			// It's the caller's responsibility to ensure this validation succeeds.
			foreach (var labelRef in Enumerable.Concat(
				InvalidBranchInstructions(methodInstructions, "methodInstructions", 0, tryStartIndex, invalidLabels: labelsWithinTryBlock),
				InvalidBranchInstructions(methodInstructions, "methodInstructions", finallyInsertIndex, instructionCount, invalidLabels: labelsWithinTryBlock)))
			{
				throw new ArgumentException($"methodInstructions[{labelRef.Index}] {labelRef.Instruction} targets a label inside try block: {labelRef.Label}");
			}

			// Mark start of the try block.
			methodInstructions[tryStartIndex].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock, null));

			// Convert ret instructions in the try block into leave instructions to a final ret at the end of the method
			// (and thus also after the finally block instructions) with additional return value tracking if method has return type.
			ConvertToLeaveInstructions(methodInstructions, method, ilGenerator, tryStartIndex, ref finallyInsertIndex, OpCodes.Ret, allowReturnValueTracking: true);

			// Convert jmp instructions in the try block into leave instructions to a final jmp at the end of the method.
			ConvertToLeaveInstructions(methodInstructions, method, ilGenerator, tryStartIndex, ref finallyInsertIndex, OpCodes.Jmp, allowReturnValueTracking: false);

			// If last instruction in try block isn't a leave or throw instruction by now, insert a leave instruction to current instruction at finallyInsertIndex.
			var lastTryBlockInstruction = methodInstructions[finallyInsertIndex - 1];
			var afterExceptionBlockLabel = methodInstructions[finallyInsertIndex].FirstOrNewAddedLabel(ilGenerator);
			if (!lastTryBlockInstruction.opcode.EqualsIgnoreForm(OpCodes.Leave) && !lastTryBlockInstruction.opcode.EqualsIgnoreForm(OpCodes.Throw))
			{
				if (lastTryBlockInstruction.opcode == OpCodes.Nop)
				{
					lastTryBlockInstruction.opcode = OpCodes.Leave;
				}
				else
				{
					lastTryBlockInstruction = new CodeInstruction(OpCodes.Leave, afterExceptionBlockLabel);
					methodInstructions.Insert(finallyInsertIndex, lastTryBlockInstruction);
					finallyInsertIndex++;
				}
			}

			// Convert branch instructions within the try block that target outside the try block into leave instructions.
			// Since there are no conditional leave instructions, each conditional branch is instead redirected to a new leave instruction with the same label,
			// inserted at the end of the try block. Same applies for each target in a switch jump table.
			foreach (var labelRef in InvalidBranchInstructions(methodInstructions, "methodInstructions", tryStartIndex, finallyInsertIndex, validLabels: labelsWithinTryBlock))
			{
				if (labelRef.Instruction.opcode.FlowControl == FlowControl.Cond_Branch)
				{

					var newLeaveLabel = ilGenerator.DefineLabel();
					methodInstructions.Insert(finallyInsertIndex, new CodeInstruction(OpCodes.Leave, afterExceptionBlockLabel) { labels = { newLeaveLabel } });
					finallyInsertIndex++;
					labelRef.Label = newLeaveLabel;
				}
				else
				{
					labelRef.Instruction.opcode = OpCodes.Leave;
					labelRef.Label = afterExceptionBlockLabel;
				}
			}

			// Insert finally block instructions. First instruction is marked as start of finally block.
			// If last instruction in finally block isn't throw, add endfinally instruction. Mark last instruction as end of try block.
			finallyBlockInstructions[0].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginFinallyBlock, null));
			methodInstructions.InsertRange(finallyInsertIndex, finallyBlockInstructions);
			var lastFinallyBlockInstruction = finallyBlockInstructions[finallyInstructionCount - 1];
			if (lastFinallyBlockInstruction.opcode == OpCodes.Nop)
			{
				lastFinallyBlockInstruction.opcode = OpCodes.Endfinally;
			}
			else if (lastFinallyBlockInstruction.opcode != OpCodes.Throw)
			{
				lastFinallyBlockInstruction = new CodeInstruction(OpCodes.Endfinally);
				methodInstructions.Insert(finallyInsertIndex + finallyInstructionCount, lastFinallyBlockInstruction);
			}
			lastFinallyBlockInstruction.blocks.Add(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock, null));

		}

		// TODO: Support ldloc(.s/0/1/2/3), ldloca(.s), stloc(.s/0/1/2/3), ldarg(.s/0/1/2/3), ldarga(.s), starg(.s), ldc.i4(.s/0/1/2/3/4/5/6/7/8/m1)?
		// Although the .<num> instructions are non-trivial to support, since they have no operands to compare with, e.g. see DeoptimizeLocalVarInstructions.
		// Should call(virt) also be a group? Also problematic due to differing semantics between the two.
		static readonly OpCode[][] OpcodeFormGroups =
		{
			new[] { OpCodes.Beq_S, OpCodes.Beq },
			new[] { OpCodes.Bge_S, OpCodes.Bge },
			new[] { OpCodes.Bge_Un_S, OpCodes.Bge_Un },
			new[] { OpCodes.Bgt_S, OpCodes.Bgt },
			new[] { OpCodes.Bgt_Un_S, OpCodes.Bgt_Un },
			new[] { OpCodes.Ble_S, OpCodes.Ble },
			new[] { OpCodes.Ble_Un_S, OpCodes.Ble_Un },
			new[] { OpCodes.Blt_S, OpCodes.Blt },
			new[] { OpCodes.Blt_Un_S, OpCodes.Blt_Un },
			new[] { OpCodes.Bne_Un_S, OpCodes.Bne_Un },
			new[] { OpCodes.Brfalse_S, OpCodes.Brfalse },
			new[] { OpCodes.Brtrue_S, OpCodes.Brtrue },
			new[] { OpCodes.Br_S, OpCodes.Br },
			new[] { OpCodes.Leave_S, OpCodes.Leave },
		};

		static readonly Dictionary<OpCode, OpCode[]> OpcodeForms;

		// Called in static constructor to initialize OpcodeForms.
		static Dictionary<OpCode, OpCode[]> InitializeOpcodeForms()
		{
			var opcodeForms = new Dictionary<OpCode, OpCode[]>();
			foreach (var opcodeFormGroup in OpcodeFormGroups)
			{
				foreach (var opcode in opcodeFormGroup)
				{
					if (opcodeForms.TryGetValue(opcode, out var existingOpcodeFormGroup))
						throw new InvalidOperationException($"OpCodeForms unexpectedly already contains group for {opcode}: {existingOpcodeFormGroup.Join()}");
					opcodeForms.Add(opcode, opcodeFormGroup);
				}
			}
			return opcodeForms;
		}

		public static bool EqualsIgnoreForm(this OpCode opcode1, OpCode opcode2)
		{
			if (OpcodeForms.TryGetValue(opcode2, out var opcode2Forms))
				return Array.IndexOf(opcode2Forms, opcode2) != -1;
			else
				return opcode1 == opcode2;
		}

		abstract class LabelRef
		{
			public CodeInstruction Instruction;
			public int Index;

			public abstract Label Label { get; set; }
		}

		class BranchLabelRef : LabelRef
		{
			public override Label Label
			{
				get => (Label)Instruction.operand;
				set => Instruction.operand = value;
			}
		}

		class SwitchLabelRef : LabelRef
		{
			public int LabelIndex;

			public override Label Label
			{
				get => ((Label[])Instruction.operand)[LabelIndex];
				set => ((Label[])Instruction.operand)[LabelIndex] = value;
			}
		}

		static IEnumerable<LabelRef> InvalidBranchInstructions(IList<CodeInstruction> instructions, string instructionsName, int startIndex, int endIndexExcl,
			ICollection<Label> invalidLabels = null, ICollection<Label> validLabels = null)
		{
			for (var index = 0; index < endIndexExcl; index++)
			{
				var instruction = instructions[index];
				var opcode = instruction.opcode;
				if (opcode == OpCodes.Switch)
				{
					if (instruction.operand is Label[] labels)
					{
						for (var labelIndex = 0; labelIndex < labels.Length; labelIndex++)
						{
							var label = labels[labelIndex];
							if (invalidLabels != null && invalidLabels.Contains(label))
								yield return new SwitchLabelRef() { Instruction = instruction, Index = index, LabelIndex = labelIndex };
							if (validLabels != null && !validLabels.Contains(label))
								yield return new SwitchLabelRef() { Instruction = instruction, Index = index, LabelIndex = labelIndex };
						}
					}
					else
						throw new ArgumentException($"{instructionsName}[{index}] {instruction} unexpectedly does not have {typeof(Label[])} operand");
				}
				// Note: OpCodes.Switch.FlowControl == FlowControl.Cond_Branch; hence, why the OpCodes.Switch case is checked before this.
				else if (opcode == OpCodes.Br || opcode == OpCodes.Br_S || opcode.FlowControl == FlowControl.Cond_Branch)
				{
					if (instruction.operand is Label label)
					{
						if (invalidLabels != null && invalidLabels.Contains(label))
							yield return new BranchLabelRef() { Instruction = instruction, Index = index };
						if (validLabels != null && !validLabels.Contains(label))
							yield return new BranchLabelRef() { Instruction = instruction, Index = index };
					}
					else
						throw new ArgumentException($"{instructionsName}[{index}] {instruction} unexpectedly does not have {typeof(Label)} operand");
				}
			}
		}

		static void ConvertToLeaveInstructions(List<CodeInstruction> methodInstructions, MethodBase method, ILGenerator ilGenerator,
			int tryStartIndex, ref int finallyInsertIndex, OpCode opcodeToConvert, bool allowReturnValueTracking)
		{
			// Note: Replace all "ret" in the following comments with "opcodeToConvert", as this is also used to convert "jmp" instructions.
			// Need to search the try block range and convert any ret instructions to leave instructions to a ret instruction at the end of the method,
			// (and thus also after the finally instructions) with additional return value tracking if method has return type.
			// Note: If the try block range is the whole method, then either the method contains at least one ret instruction or it only has throw instructions.
			// In the latter case, there doesn't even need to be anything after the finally instructions.
			var equalsOpcodeToConvertPredicate = new Predicate<CodeInstruction>(instruction => instruction.opcode.EqualsIgnoreForm(opcodeToConvert));
			var instructionCount = methodInstructions.Count;
			var searchIndex = tryStartIndex;
			var count = finallyInsertIndex - tryStartIndex;
			var index = methodInstructions.FindIndex(searchIndex, count, equalsOpcodeToConvertPredicate);
			if (index != -1)
			{
				var finalLabel = default(Label?);
				// If method has void return type (constructor is treated as having void return type)...
				var returnType = (allowReturnValueTracking ? (method as MethodInfo)?.ReturnType : null) ?? typeof(void);
				if (returnType == typeof(void))
				{
					// If any ret instruction exists in the range, ensure there is a final final ret instruction.
					// Determine whether a final ret instruction that has an existing leave pointing to it already exists.
					if (instructionCount >= 1)
					{
						var finalReturnIndex = methodInstructions.FindLastIndex(instructionCount - 1, instructionCount - 1 - finallyInsertIndex,
							instruction => equalsOpcodeToConvertPredicate(instruction) && instruction.labels.Count > 0);
						if (finalReturnIndex != -1)
						{
							var finalReturnInstruction = methodInstructions[finalReturnIndex];
							var leaveIndex = methodInstructions.FindIndex(searchIndex, count,
								instruction => instruction.opcode.EqualsIgnoreForm(OpCodes.Leave) && finalReturnInstruction.labels.Any(label => instruction.labels.Contains(label)));
							if (leaveIndex != -1)
							{
								finalLabel = (Label)methodInstructions[leaveIndex].operand;
							}
						}
					}
					// If it doesn't exist, add the final ret instruction.
					if (finalLabel == null)
					{
						finalLabel = ilGenerator.DefineLabel();
						methodInstructions.Add(new CodeInstruction(opcodeToConvert) { labels = { finalLabel.Value } });
					}

					// Existing ret instructions in the range will be turned into a leave to the final ret instruction.
					do
					{
						methodInstructions[index].SetTo(OpCodes.Leave, finalLabel.Value);
						count -= index + 1 - searchIndex;
						searchIndex = index + 1;
						index = methodInstructions.FindIndex(searchIndex, count, equalsOpcodeToConvertPredicate);
					} while (index != -1);
				}
				// Else if method has non-void return type...
				else
				{
					var returnValueVar = default(LocalBuilder);
					// If any ret instruction exists in the range, ensure there is a final ldloc.s of variable that stores the return value, followed by a ret instruction.
					// Determine whether a final ldloc.s + ret instruction that has an existing stloc.s + leave pointing to it already exists.
					if (instructionCount >= 1)
					{
						var finalReturnIndex = methodInstructions.FindLastIndex(instructionCount - 1, instructionCount - 1 - finallyInsertIndex,
							instruction => instruction.opcode == OpCodes.Ldloc_S && instruction.labels.Count > 0,
							equalsOpcodeToConvertPredicate);
						if (finalReturnIndex != -1)
						{
							var finalReturnValueStoreInstruction = methodInstructions[finalReturnIndex];
							var finalReturnInstruction = methodInstructions[finalReturnIndex + 1];
							var leaveIndex = methodInstructions.FindIndex(searchIndex, count,
								instruction => instruction.opcode == OpCodes.Stloc_S && instruction.operand == finalReturnValueStoreInstruction.operand,
								instruction => instruction.opcode.EqualsIgnoreForm(OpCodes.Leave) && finalReturnInstruction.labels.Any(label => instruction.labels.Contains(label)));
							if (leaveIndex != -1)
							{
								returnValueVar = (LocalBuilder)methodInstructions[leaveIndex].operand;
								finalLabel = (Label)methodInstructions[leaveIndex + 1].operand;
							}
						}
					}
					// If it doesn't exist, add the final ldloc.s + ret instruction, declaring a new var for the ldloc.s instruction.
					if (finalLabel == null)
					{
						returnValueVar = ilGenerator.DeclareLocal(returnType);
						finalLabel = ilGenerator.DefineLabel();
						methodInstructions.AddRange(new[]
						{
							new CodeInstruction(OpCodes.Ldloc_S, returnValueVar) { labels = { finalLabel.Value } },
							new CodeInstruction(opcodeToConvert),
						});
					}

					// Existing ret instructions in the range will be turned into a stloc.s to the new return value variable, then a leave to the final ldloc.s instruction.
					do
					{
						methodInstructions[index].SetTo(OpCodes.Stloc_S, returnValueVar);
						methodInstructions.Insert(index + 1, new CodeInstruction(OpCodes.Leave, finalLabel.Value));
						finallyInsertIndex++;
						count -= index + 2 - searchIndex;
						searchIndex = index + 2;
						index = methodInstructions.FindIndex(searchIndex, count, equalsOpcodeToConvertPredicate);
					} while (index != -1);
				}
			}
		}

		static HarmonyTranspilerExtensions()
		{
			OpcodeForms = InitializeOpcodeForms();
		}
	}

	public static class CodeInstructionPredicateExtensions
	{
		public static Predicate<CodeInstruction> AsInstructionPredicate(this OpCode opcode)
		{
			return instruction => instruction.opcode == opcode;
		}

		// Functionally equivalent to: opcode.AsInstructionPredicate().Operand(operand)
		public static Predicate<CodeInstruction> AsInstructionPredicate(this OpCode opcode, object operand)
		{
			return instruction => instruction.opcode == opcode && instruction.operand == operand;
		}

		public static Predicate<CodeInstruction> Operand(this Predicate<CodeInstruction> instructionPredicate, object operand)
		{
			return instruction => instructionPredicate(instruction) && instruction.operand == operand;
		}

		public static Predicate<CodeInstruction> Operand<T>(this Predicate<CodeInstruction> instructionPredicate, Predicate<T> operandPredicate) where T : class
		{
			return instruction => instructionPredicate(instruction) && instruction.operand is T typedOperand && operandPredicate(typedOperand);
		}

		public static Predicate<CodeInstruction> LocalBuilder(this Predicate<CodeInstruction> instructionPredicate, Predicate<LocalBuilder> operandPredicate)
		{
			return instructionPredicate.Operand(operandPredicate);
		}

		public static Predicate<CodeInstruction> LocalBuilder(this Predicate<CodeInstruction> instructionPredicate, int localIndex)
		{
			return instructionPredicate.LocalBuilder(localBuilder => localBuilder.LocalIndex == localIndex);
		}

		public static Predicate<CodeInstruction> LocalBuilder(this Predicate<CodeInstruction> instructionPredicate, Type localBuilderType, bool useIsAssignableFrom = false)
		{
			if (useIsAssignableFrom)
				return instructionPredicate.LocalBuilder(localBuilder => localBuilderType.IsAssignableFrom(localBuilder.LocalType));
			else
				return instructionPredicate.LocalBuilder(localBuilder => localBuilderType == localBuilder.LocalType);
		}

		public static Predicate<CodeInstruction> AsInstructionPredicate(this Label label)
		{
			return instruction => instruction.labels.Contains(label);
		}

		public static Predicate<CodeInstruction> HasLabel(this Predicate<CodeInstruction> instructionPredicate, Label label)
		{
			return instruction => instructionPredicate(instruction) && instruction.labels.Contains(label);
		}

		public static Predicate<CodeInstruction> HasLabel(this Predicate<CodeInstruction> instructionPredicate, List<Label> labels)
		{
			return instruction => instructionPredicate(instruction) && labels.Any(label => instruction.labels.Contains(label));
		}


		public static Predicate<CodeInstruction> AsInstructionPredicate(this ExceptionBlockType blockType)
		{
			return instruction => instruction.blocks.Any(block => block.blockType == blockType);
		}

		public static Predicate<CodeInstruction> HasBlock(this Predicate<CodeInstruction> instructionPredicate, ExceptionBlockType blockType)
		{
			return instruction => instructionPredicate(instruction) && instruction.blocks.Any(block => block.blockType == blockType);
		}

		public static Predicate<CodeInstruction> HasBlock(this Predicate<CodeInstruction> instructionPredicate, Type catchType)
		{
			return instruction => instructionPredicate(instruction) && instruction.blocks.Any(block => block.catchType == catchType);
		}
	}
}
