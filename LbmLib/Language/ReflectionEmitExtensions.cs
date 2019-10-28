using System;
using System.Reflection.Emit;

namespace LbmLib.Language
{
	public static class ReflectionEmitExtensions
	{
		public static bool CanEmitConstant(this ILGenerator ilGenerator, object argument)
		{
			if (argument is null)
				return false;
			switch (Type.GetTypeCode(argument.GetType()))
			{
			case TypeCode.Boolean:
			case TypeCode.SByte:
			case TypeCode.Int16:
			case TypeCode.Int32:
			case TypeCode.Char:
			case TypeCode.Byte:
			case TypeCode.UInt16:
			case TypeCode.UInt32:
			case TypeCode.Int64:
			case TypeCode.UInt64:
			case TypeCode.Single:
			case TypeCode.Double:
			case TypeCode.String:
				return false;
			}
			return true;
		}

		public static bool TryEmitConstant(this ILGenerator ilGenerator, object argument)
		{
			if (argument is null)
			{
				ilGenerator.Emit(OpCodes.Ldnull);
				return true;
			}
			switch (Type.GetTypeCode(argument.GetType()))
			{
			case TypeCode.Boolean:
				ilGenerator.Emit((bool)argument ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
				return true;
			// Apparently don't need to handle signed and unsigned integer types of size 4 bytes or less differently from each other.
			case TypeCode.SByte:
			case TypeCode.Int16:
			case TypeCode.Int32:
			case TypeCode.Char:
			case TypeCode.Byte:
			case TypeCode.UInt16:
			case TypeCode.UInt32:
				ilGenerator.EmitLdcI4((int)argument);
				return true;
			// Likewise, ulong and long don't have to be treated differently, but if ldc.i4* is used, needs a conv.i8 afterwards.
			case TypeCode.Int64:
			case TypeCode.UInt64:
				var longArgument = (long)argument;
				if (longArgument >= int.MinValue && longArgument <= int.MaxValue)
				{
					ilGenerator.EmitLdcI4((int)longArgument);
					ilGenerator.Emit(OpCodes.Conv_I8);
				}
				else
				{
					ilGenerator.Emit(OpCodes.Ldc_I8, longArgument);
				}
				return true;
			case TypeCode.Single:
				ilGenerator.Emit(OpCodes.Ldc_R4, (float)argument);
				return true;
			case TypeCode.Double:
				ilGenerator.Emit(OpCodes.Ldc_R8, (double)argument);
				return true;
			case TypeCode.String:
				ilGenerator.Emit(OpCodes.Ldstr, (string)argument);
				return true;
			}
			return false;
		}

		public static void EmitLdcI4(this ILGenerator ilGenerator, int value)
		{
			switch (value)
			{
			case -1:
				ilGenerator.Emit(OpCodes.Ldc_I4_M1);
				break;
			case 0:
				ilGenerator.Emit(OpCodes.Ldc_I4_0);
				break;
			case 1:
				ilGenerator.Emit(OpCodes.Ldc_I4_1);
				break;
			case 2:
				ilGenerator.Emit(OpCodes.Ldc_I4_2);
				break;
			case 3:
				ilGenerator.Emit(OpCodes.Ldc_I4_3);
				break;
			case 4:
				ilGenerator.Emit(OpCodes.Ldc_I4_4);
				break;
			case 5:
				ilGenerator.Emit(OpCodes.Ldc_I4_5);
				break;
			case 6:
				ilGenerator.Emit(OpCodes.Ldc_I4_6);
				break;
			case 7:
				ilGenerator.Emit(OpCodes.Ldc_I4_7);
				break;
			case 8:
				ilGenerator.Emit(OpCodes.Ldc_I4_8);
				break;
			default:
				if (value >= -sbyte.MinValue && value <= sbyte.MaxValue)
					ilGenerator.Emit(OpCodes.Ldc_I4_S, (sbyte)value);
				else
					ilGenerator.Emit(OpCodes.Ldc_I4, value);
				break;
			}
		}

		public static void EmitLdloc(this ILGenerator ilGenerator, LocalBuilder localVar)
		{
			var localIndex = localVar.LocalIndex;
			switch (localIndex)
			{
			case 0:
				ilGenerator.Emit(OpCodes.Ldloc_0);
				break;
			case 1:
				ilGenerator.Emit(OpCodes.Ldloc_1);
				break;
			case 2:
				ilGenerator.Emit(OpCodes.Ldloc_2);
				break;
			case 3:
				ilGenerator.Emit(OpCodes.Ldloc_3);
				break;
			default:
				if (localIndex <= byte.MaxValue)
					ilGenerator.Emit(OpCodes.Ldloc_S, localVar);
				else
					ilGenerator.Emit(OpCodes.Ldloc, localVar);
				break;
			}
		}

		public static void EmitLdloca(this ILGenerator ilGenerator, LocalBuilder localVar)
		{
			if (localVar.LocalIndex <= byte.MaxValue)
				ilGenerator.Emit(OpCodes.Ldloca_S, localVar);
			else
				ilGenerator.Emit(OpCodes.Ldloca, localVar);
		}

		public static void EmitStloc(this ILGenerator ilGenerator, LocalBuilder localVar)
		{
			var localIndex = localVar.LocalIndex;
			switch (localIndex)
			{
			case 0:
				ilGenerator.Emit(OpCodes.Stloc_0);
				break;
			case 1:
				ilGenerator.Emit(OpCodes.Stloc_1);
				break;
			case 2:
				ilGenerator.Emit(OpCodes.Stloc_2);
				break;
			case 3:
				ilGenerator.Emit(OpCodes.Stloc_3);
				break;
			default:
				if (localIndex <= byte.MaxValue)
					ilGenerator.Emit(OpCodes.Stloc_S, localVar);
				else
					ilGenerator.Emit(OpCodes.Stloc, localVar);
				break;
			}
		}

		public static void EmitLdarg(this ILGenerator ilGenerator, short index)
		{
			switch (index)
			{
			case 0:
				ilGenerator.Emit(OpCodes.Ldarg_0);
				break;
			case 1:
				ilGenerator.Emit(OpCodes.Ldarg_1);
				break;
			case 2:
				ilGenerator.Emit(OpCodes.Ldarg_1);
				break;
			case 3:
				ilGenerator.Emit(OpCodes.Ldarg_1);
				break;
			default:
				if (index <= byte.MaxValue)
					ilGenerator.Emit(OpCodes.Ldarg_S, (byte)index);
				else
					ilGenerator.Emit(OpCodes.Ldarg, index);
				break;
			}
		}
	}
}
