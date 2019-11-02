/*
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Harmony;

// TODO: All the following is old and incomplete and needs to be revamped.
namespace LbmLib.Harmony.Experimental
{
	public class ILExpression
	{
		// The start instruction index of this whole expression.
		public int StartInstructionIndex => Inputs.Length > 0 ? Inputs[0].StartInstructionIndex : EndInstructionIndex;

		// The end instruction index of this whole expression.
		public int EndInstructionIndex { get; }

		// The instruction at the end instruction index of this whole expression that pops/consumes values pushed onto the CIL stack pushed by its inputs.
		public CodeInstruction Instruction { get; }

		// Array of prefix instructions (meta flow control type) that can modify the behavior of this instruction.
		public CodeInstruction[] Prefixes { get; }

		// Array of "child" expressions that each push a value onto the CIL stack which this instruction pops and consumes.
		// The size of this array is this instruction's stack pop size.
		public ILExpression[] Inputs { get; }

		// Array of "child" expressions that represent instructions after the last input and before this instruction
		// that do NOT push a value onto the CIL stack which this instruction pops and consumes.
		// This excludes any prefix instructions (meta flow control type) that precede this instruction.
		public ILExpression[] NonInputs { get; }

		// Array of "parent" expressions that pops and consumes values pushed onto the CIL stack by this instruction.
		// The size of this array is this instruction's stack push size, and is almost always either 0 or 1.
		//
		// There's only one instruction opcode that has a stack push size > 1: dup, which pops/consumes 1 from CIL stack, then pushes 2 to the CIL stack.
		// In this case, the outputs array is of size 2 with the following possible values:
		// a) If the parent expression pops off both of the values that dup pushes onto the CIL stack, both items of the output array refer to the same parent,
		//    and that parent expression has two consecutive inputs that refer to this same dup expression.
		// b) If the parent expression can only pop off one of the values that dup pushes on the CIL stack, then a grandparent expression pops off the other value,
		//    then those two are the items of the output array in that order.
		//    If the grandparent expression isn't known (if analysis isn't done on it), then the second output is null.
		public ILExpression[] Outputs { get; }

		//// The total CIL stack size change of this whole expression, starting (backwards) from this instruction to the first input,
		//// adding each instruction's stack push size and subtracting each instruction's stack pop size.
		//// This is typically 0, but can be 1 if the first input is a dup instruction that pushes more output values onto the CIL stack than the whole expression
		//// can handle by itself (leaving the parent expression to handle the remainder output).
		//public int CILStackSizeChange { get; }

		public ILExpression(int endIndex, CodeInstruction instruction,
			CodeInstruction[] prefixes, ILExpression[] inputs, ILExpression[] nonInputs, ILExpression[] outputs)
		{
			EndInstructionIndex = endIndex;
			Instruction = instruction;
			Prefixes = prefixes;
			Inputs = inputs;
			NonInputs = nonInputs;
			Outputs = outputs;
		}

		public List<CodeInstruction> GetInstructions(List<CodeInstruction> methodInstructions)
		{
			return methodInstructions.GetRange(StartInstructionIndex, EndInstructionIndex + 1 - StartInstructionIndex);
		}

		public override string ToString()
		{
			return ToString("", "\t");
		}

		string ToString(string currentIndent, in string indent)
		{
			StringBuilder sb = new StringBuilder();
			if (StartInstructionIndex == EndInstructionIndex)
			{
				sb.Append($"{currentIndent}ILExpression{{{StartInstructionIndex}}}: {Instruction}");
			}
			else
			{
				sb.Append($"{currentIndent}ILExpression{{{StartInstructionIndex}..{EndInstructionIndex}}}: {Instruction}");
				currentIndent += indent;
				for (int i = 0; i < Inputs.Length; i++)
				{
					ILExpression input = Inputs[i];
					sb.Append($"\n{currentIndent}Inputs[{i}]: {input.ToString(currentIndent, indent)}");
				}
				for (int i = 0; i < NonInputs.Length; i++)
				{
					ILExpression nonInput = NonInputs[i];
					sb.Append($"\n{currentIndent}NonInputs[{i}]: {nonInput.ToString(currentIndent, indent)}");
				}
				int instructionIndex = EndInstructionIndex - Prefixes.Length;
				foreach (CodeInstruction prefix in Prefixes)
				{
					sb.Append($"\n{currentIndent}Prefix{instructionIndex}: {prefix}");
					instructionIndex++;
				}
			}
			// TODO: Should Outputs be included somewhere here?
			return sb.ToString();
		}
	}

	public class ILBlock
	{
		public List<ILExpression> Expressions;

		public ILBlock(List<ILExpression> expressions)
		{
			Expressions = expressions;
		}
	}

	public class TranspilerContext
	{
		public List<CodeInstruction> Instructions { get; }

		public MethodBase Method { get; }

		public ILGenerator ILGenerator { get; }

		public TranspilerContext(List<CodeInstruction> instructions, MethodBase method = null, ILGenerator ilGenerator = null)
		{
			Instructions = instructions;
			Method = method;
			ILGenerator = ilGenerator;
		}

		public ILExpression Analyze()
		{
			// TODO: create a directed cyclic graph of instruction flow control, mapping [conditional] branches, throws, returns, and endfilter/endfinally instructions.
			// Then create a tree of instruction blocks based off this graph.
			// Finally create the tree of expression using the instruction blocks as input.

			return Analyze(0, new Stack<ILExpression>());
		}

		ILExpression Analyze(int instructionIndex, Stack<ILExpression> pendingInputs)
		{
			var prefixStartIndex = -1;
			while (instructionIndex < 0)
			{
				var instruction = Instructions[instructionIndex];

				if (instruction.opcode.FlowControl == FlowControl.Meta)
				{
					if (prefixStartIndex == -1)
					{
						prefixStartIndex = instructionIndex;
					}
					instructionIndex++;
					continue;
				}
				CodeInstruction[] prefixes;
				if (prefixStartIndex != -1)
				{
					prefixes = new CodeInstruction[instructionIndex - prefixStartIndex];
					prefixStartIndex = -1;
				}
				else
				{
					prefixes = new CodeInstruction[0];
				}

				var inputs = new ILExpression[instruction.StackPopSize()];
				if (inputs.Length > pendingInputs.Count)
					throw new InvalidOperationException($"Analyze unexpectedly found instruction requiring {inputs.Length} inputs, but only {pendingInputs.Count} pending inputs on stack");
				var inputIndex = inputs.Length - 1;
				while (pendingInputs.Count > 0)
				{
					inputs[inputIndex] = pendingInputs.Pop();
					inputIndex--;
				}

				var outputs = new ILExpression[instruction.StackPushSize()];


			}
			return null; // TEMP
		}
	}

	public static class HarmonyTranspilerExtensionMethods
	{
		// TODO: Following won't work properly due to dup instruction - analysis has to go forward, not backward.
		public static ILExpression AnalyzeExpression(this List<CodeInstruction> instructions, int index)
		{
			return instructions.AnalyzeExpression(index, new List<ILExpression>());
		}

		static ILExpression AnalyzeExpression(this List<CodeInstruction> instructions, int endInstructionIndex, List<ILExpression> pendingInputs)
		{
			var instructionIndex = endInstructionIndex;
			var endInstruction = instructions[instructionIndex];
			if (endInstruction.opcode.FlowControl == FlowControl.Meta)
				throw new ArgumentException($"AnalyzeExpression cannot be called on instruction with FlowControl type {FlowControl.Meta}: {endInstruction}");

			var inputs = new ILExpression[endInstruction.StackPopSize()];
			var outputs = new ILExpression[endInstruction.StackPushSize()];

			var prefixCount = 0;
			instructionIndex--;
			while (instructionIndex >= 0)
			{
				var instruction = instructions[instructionIndex];
				if (instruction.opcode.FlowControl != FlowControl.Meta)
					break;
				prefixCount++;
				instructionIndex--;
			}
			var prefixes = new CodeInstruction[prefixCount];
			for (var prefixIndex = 0; prefixIndex < prefixCount; prefixIndex++)
			{
				prefixes[prefixIndex] = instructions[instructionIndex + 1 + prefixIndex];
			}

			var inputIndex = inputs.Length - 1;

			// Unknown # of non-inputs at this point, so using a List.
			var nonInputList = new List<ILExpression>();
			instructionIndex--;
			while (instructionIndex >= 0)
			{
				var instruction = instructions[instructionIndex - 1];
				if (instruction.StackPushSize() > 0)
					break;
				var nonInputExpression = AnalyzeExpression(instructions, instructionIndex, pendingInputs);
				nonInputList.Add(nonInputExpression);
				// dup instruction can result in a pending input.
				if (pendingInputs.Count > 0)
					break;
				instructionIndex = nonInputExpression.StartInstructionIndex - 1;
			}
			nonInputList.Reverse();
			var nonInputs = nonInputList.ToArray();

			// TODO
			while (instructionIndex >= 0)
			{
			}

			if (instructionIndex < 0 && inputIndex > 0)
				throw new InvalidOperationException("AnalyzeExpression unexpectedly encountered start of instructions array " +
					$"without encountering expected # inputs for instruction {endInstruction}: encountered {inputs.Length - inputIndex} inputs, expected {inputs.Length} inputs");

			return new ILExpression(endInstructionIndex, endInstruction, prefixes, inputs, nonInputs, outputs);
		}

		public static int StackPopSize(this CodeInstruction instruction)
		{
			OpCode opcode = instruction.opcode;
			if (opcode == OpCodes.Call || opcode == OpCodes.Newobj)
			{
				if (instruction.operand is MethodBase method)
				{
					return method.GetParameters().Length;
				}
				throw new ArgumentException($"Instruction {opcode} unexpectedly has non-MethodBase operand: ${instruction.operand}");
			}
			if (opcode == OpCodes.Callvirt)
			{
				if (instruction.operand is MethodBase method)
				{
					return 1 + method.GetParameters().Length;
				}
				throw new ArgumentException($"Instruction {opcode} unexpectedly has non-MethodBase operand: ${instruction.operand}");
			}
			if (opcode == OpCodes.Ret)
			{
				// ret instruction requires knowing whether the containing method has a non-void return type.
				throw new NotSupportedException($"Instruction {opcode} is currently unsupported");
			}
			if (opcode == OpCodes.Leave || opcode == OpCodes.Leave_S)
			{
				// leave and leave.s instructions empty the stack, which requires knowing the method-level stack size at this instruction.
				throw new NotSupportedException($"Instruction {opcode} is currently unsupported");
			}
			StackBehaviour stackBehaviour = opcode.StackBehaviourPop;
			switch (stackBehaviour)
			{
				case StackBehaviour.Pop0:
					return 0;
				case StackBehaviour.Pop1:
				case StackBehaviour.Popi:
				case StackBehaviour.Popref:
					return 1;
				case StackBehaviour.Pop1_pop1:
				case StackBehaviour.Popi_pop1:
				case StackBehaviour.Popi_popi:
				case StackBehaviour.Popi_popi8:
				case StackBehaviour.Popi_popr4:
				case StackBehaviour.Popi_popr8:
				case StackBehaviour.Popref_pop1:
				case StackBehaviour.Popref_popi:
					return 2;
				case StackBehaviour.Popi_popi_popi:
				case StackBehaviour.Popref_popi_popi:
				case StackBehaviour.Popref_popi_popi8:
				case StackBehaviour.Popref_popi_popr4:
				case StackBehaviour.Popref_popi_popr8:
				case StackBehaviour.Popref_popi_popref:
				case StackBehaviour.Popref_popi_pop1:
					return 3;
				case StackBehaviour.Varpop:
					throw new NotSupportedException($"Instruction {opcode} with Varpop StackBehaviourPop is currently unsupported");
				default:
					throw new NotSupportedException($"Instruction {opcode} with currently unsupported StackBehaviourPop: {stackBehaviour}");
			}
		}

		public static int StackPushSize(this CodeInstruction instruction)
		{
			OpCode opcode = instruction.opcode;
			if (opcode == OpCodes.Call || opcode == OpCodes.Callvirt)
			{
				if (instruction.operand is MethodInfo method)
				{
					return method.ReturnType == typeof(void) ? 0 : 1;
				}
				throw new ArgumentException($"Instruction {opcode} unexpectedly has non-MethodInfo operand: ${instruction.operand}");
			}
			if (opcode == OpCodes.Newobj)
			{
				return 1;
			}
			StackBehaviour stackBehaviour = opcode.StackBehaviourPush;
			switch (stackBehaviour)
			{
				case StackBehaviour.Push0:
					return 0;
				case StackBehaviour.Push1:
				case StackBehaviour.Pushi:
				case StackBehaviour.Pushi8:
				case StackBehaviour.Pushr4:
				case StackBehaviour.Pushr8:
				case StackBehaviour.Pushref:
					return 1;
				case StackBehaviour.Push1_push1:
					return 2;
				case StackBehaviour.Varpush:
					throw new NotSupportedException($"Instruction {opcode} with Varpush StackBehaviourPush is currently unsupported");
				default:
					throw new NotSupportedException($"Instruction {opcode} with currently unsupported StackBehaviourPush: {stackBehaviour}");
			}
		}
	}
}
*/
