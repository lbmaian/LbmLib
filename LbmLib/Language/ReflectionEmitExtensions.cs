using System;
using System.Reflection.Emit;

namespace LbmLib.Language
{
	public static class ReflectionEmitExtensions
	{
		public static bool CanEmitConstant(this ILGenerator _, object argument)
		{
			if (argument is null)
				return true;
			var type = argument.GetType();
			switch (Type.GetTypeCode(type))
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
			// IntPtr is treated specially in CIL as native int, but Type.GetTypeCode(typeof(IntPtr)) returns TypeCode.Object.
			case TypeCode.Object when type == typeof_IntPtr:
				return true;
			}
			return false;
		}

		public static bool TryEmitConstant(this ILGenerator ilGenerator, object argument)
		{
			if (argument is null)
			{
				ilGenerator.Emit(OpCodes.Ldnull);
				return true;
			}
			var type = argument.GetType();
			switch (Type.GetTypeCode(type))
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
				ilGenerator.EmitLdcI((int)argument);
				return true;
			// Likewise, ulong and long don't have to be treated differently, but if ldc.i4* is used, needs a conv.i8 afterwards.
			case TypeCode.Int64:
			case TypeCode.UInt64:
				ilGenerator.EmitLdcI((long)argument);
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
			// IntPtr is treated specially in CIL as native int, but Type.GetTypeCode(typeof(IntPtr)) returns TypeCode.Object.
			case TypeCode.Object when type == typeof_IntPtr:
				ilGenerator.EmitLdcI((IntPtr)argument);
				return true;
			}
			return false;
		}

		public static void EmitLdcI(this ILGenerator ilGenerator, IntPtr intPtr)
		{
			if (IntPtr.Size is 4)
				ilGenerator.EmitLdcI(intPtr.ToInt32());
			else
				ilGenerator.EmitLdcI(intPtr.ToInt64());
			ilGenerator.Emit(OpCodes.Conv_I);
		}

		public static void EmitLdcI(this ILGenerator ilGenerator, long value)
		{
			switch (value)
			{
			case -1:
				ilGenerator.Emit(OpCodes.Ldc_I4_M1);
				ilGenerator.Emit(OpCodes.Conv_I8);
				break;
			case 0:
				ilGenerator.Emit(OpCodes.Ldc_I4_0);
				ilGenerator.Emit(OpCodes.Conv_I8);
				break;
			case 1:
				ilGenerator.Emit(OpCodes.Ldc_I4_1);
				ilGenerator.Emit(OpCodes.Conv_I8);
				break;
			case 2:
				ilGenerator.Emit(OpCodes.Ldc_I4_2);
				ilGenerator.Emit(OpCodes.Conv_I8);
				break;
			case 3:
				ilGenerator.Emit(OpCodes.Ldc_I4_3);
				ilGenerator.Emit(OpCodes.Conv_I8);
				break;
			case 4:
				ilGenerator.Emit(OpCodes.Ldc_I4_4);
				ilGenerator.Emit(OpCodes.Conv_I8);
				break;
			case 5:
				ilGenerator.Emit(OpCodes.Ldc_I4_5);
				ilGenerator.Emit(OpCodes.Conv_I8);
				break;
			case 6:
				ilGenerator.Emit(OpCodes.Ldc_I4_6);
				ilGenerator.Emit(OpCodes.Conv_I8);
				break;
			case 7:
				ilGenerator.Emit(OpCodes.Ldc_I4_7);
				ilGenerator.Emit(OpCodes.Conv_I8);
				break;
			case 8:
				ilGenerator.Emit(OpCodes.Ldc_I4_8);
				ilGenerator.Emit(OpCodes.Conv_I8);
				break;
			case var _ when value >= sbyte.MinValue && value <= sbyte.MaxValue:
				ilGenerator.Emit(OpCodes.Ldc_I4_S, (sbyte)value);
				ilGenerator.Emit(OpCodes.Conv_I8);
				break;
			case var _ when value >= int.MinValue && value <= int.MaxValue:
				ilGenerator.Emit(OpCodes.Ldc_I4, (int)value);
				ilGenerator.Emit(OpCodes.Conv_I8);
				break;
			default:
				ilGenerator.Emit(OpCodes.Ldc_I8, value);
				break;
			}
		}

		public static void EmitLdcI(this ILGenerator ilGenerator, int value)
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
			case var _ when value >= sbyte.MinValue && value <= sbyte.MaxValue:
				ilGenerator.Emit(OpCodes.Ldc_I4_S, (sbyte)value);
				break;
			default:
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
			case var _ when localIndex <= byte.MaxValue:
				ilGenerator.Emit(OpCodes.Ldloc_S, localVar);
				break;
			default:
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
			case var _ when localIndex <= byte.MaxValue:
				ilGenerator.Emit(OpCodes.Stloc_S, localVar);
				break;
			default:
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
				ilGenerator.Emit(OpCodes.Ldarg_2);
				break;
			case 3:
				ilGenerator.Emit(OpCodes.Ldarg_3);
				break;
			case var _ when index <= byte.MaxValue:
				ilGenerator.Emit(OpCodes.Ldarg_S, (byte)index);
				break;
			default:
				ilGenerator.Emit(OpCodes.Ldarg, index);
				break;
			}
		}

		public static void EmitLdind(this ILGenerator ilGenerator, Type type)
		{
			switch (Type.GetTypeCode(type))
			{
			case TypeCode.Boolean:
			case TypeCode.SByte:
				ilGenerator.Emit(OpCodes.Ldind_I1);
				break;
			case TypeCode.Byte:
				ilGenerator.Emit(OpCodes.Ldind_U1);
				break;
			case TypeCode.Int16:
				ilGenerator.Emit(OpCodes.Ldind_I2);
				break;
			// Character is encoded in UTF-16 so effectively an unsigned 16-bit integer.
			case TypeCode.Char:
			case TypeCode.UInt16:
				ilGenerator.Emit(OpCodes.Ldind_U2);
				break;
			case TypeCode.Int32:
				ilGenerator.Emit(OpCodes.Ldind_I4);
				break;
			case TypeCode.UInt32:
				ilGenerator.Emit(OpCodes.Ldind_U4);
				break;
			case TypeCode.Int64:
				ilGenerator.Emit(OpCodes.Ldind_I8);
				break;
			case TypeCode.UInt64:
				ilGenerator.Emit(OpCodes.Ldind_I8);
				ilGenerator.Emit(OpCodes.Conv_U8);
				break;
			case TypeCode.Single:
				ilGenerator.Emit(OpCodes.Ldind_R4);
				break;
			case TypeCode.Double:
				ilGenerator.Emit(OpCodes.Ldind_R8);
				break;
			// IntPtr is treated specially in CIL as native int, but Type.GetTypeCode(typeof(IntPtr)) returns TypeCode.Object.
			case TypeCode.Object when type == typeof_IntPtr:
				ilGenerator.Emit(OpCodes.Ldind_I);
				break;
			// decimal and DateTime are value types.
			case TypeCode.Decimal:
			case TypeCode.DateTime:
			case TypeCode.Object when type.IsValueType:
				ilGenerator.Emit(OpCodes.Ldobj, type);
				break;
			// Default case handles string, DBNull, null (should be impossible), and any other object type.
			default:
				ilGenerator.Emit(OpCodes.Ldind_Ref);
				break;
			};
		}

		public static void EmitStind(this ILGenerator ilGenerator, Type type)
		{
			// Note: There are no stind.u* instructions. The corresponding stind.i* instructions should work fine for unsigned integers.
			switch (Type.GetTypeCode(type))
			{
			case TypeCode.Boolean:
			case TypeCode.SByte:
			case TypeCode.Byte:
				ilGenerator.Emit(OpCodes.Stind_I1);
				break;
			case TypeCode.Int16:
			// Character is encoded in UTF-16 so effectively an unsigned 16-bit integer.
			case TypeCode.Char:
			case TypeCode.UInt16:
				ilGenerator.Emit(OpCodes.Stind_I2);
				break;
			case TypeCode.Int32:
			case TypeCode.UInt32:
				ilGenerator.Emit(OpCodes.Stind_I4);
				break;
			case TypeCode.Int64:
			case TypeCode.UInt64:
				ilGenerator.Emit(OpCodes.Stind_I8);
				break;
			case TypeCode.Single:
				ilGenerator.Emit(OpCodes.Stind_R4);
				break;
			case TypeCode.Double:
				ilGenerator.Emit(OpCodes.Stind_R8);
				break;
			// IntPtr is treated specially in CIL as native int, but Type.GetTypeCode(typeof(IntPtr)) returns TypeCode.Object.
			case TypeCode.Object when type == typeof_IntPtr:
				ilGenerator.Emit(OpCodes.Stind_I);
				break;
			// decimal and DateTime are value types.
			case TypeCode.Decimal:
			case TypeCode.DateTime:
			case TypeCode.Object when type.IsValueType:
				ilGenerator.Emit(OpCodes.Stobj, type);
				break;
			// Default case handles string, DBNull, null (should be impossible), and any other object type.
			default:
				ilGenerator.Emit(OpCodes.Stind_Ref);
				break;
			};
		}

		public static void EmitLdelem(this ILGenerator ilGenerator, Type type)
		{
			switch (Type.GetTypeCode(type))
			{
			case TypeCode.Boolean:
			case TypeCode.SByte:
				ilGenerator.Emit(OpCodes.Ldelem_I1);
				break;
			case TypeCode.Byte:
				ilGenerator.Emit(OpCodes.Ldelem_U1);
				break;
			case TypeCode.Int16:
				ilGenerator.Emit(OpCodes.Ldelem_I2);
				break;
			// Character is encoded in UTF-16 so effectively an unsigned 16-bit integer.
			case TypeCode.Char:
			case TypeCode.UInt16:
				ilGenerator.Emit(OpCodes.Ldelem_U2);
				break;
			case TypeCode.Int32:
				ilGenerator.Emit(OpCodes.Ldelem_I4);
				break;
			case TypeCode.UInt32:
				ilGenerator.Emit(OpCodes.Ldelem_U4);
				break;
			case TypeCode.Int64:
				ilGenerator.Emit(OpCodes.Ldelem_I8);
				break;
			case TypeCode.UInt64:
				ilGenerator.Emit(OpCodes.Ldelem_I8);
				ilGenerator.Emit(OpCodes.Conv_U8);
				break;
			case TypeCode.Single:
				ilGenerator.Emit(OpCodes.Ldelem_R4);
				break;
			case TypeCode.Double:
				ilGenerator.Emit(OpCodes.Ldelem_R8);
				break;
			// IntPtr is treated specially in CIL as native int, but Type.GetTypeCode(typeof(IntPtr)) returns TypeCode.Object.
			case TypeCode.Object when type == typeof_IntPtr:
				ilGenerator.Emit(OpCodes.Ldelem_I);
				break;
			// decimal and DateTime are value types.
			case TypeCode.Decimal:
			case TypeCode.DateTime:
			case TypeCode.Object when type.IsValueType:
				ilGenerator.Emit(OpCodes.Ldelem, type);
				break;
			// Default case handles string, DBNull, null (should be impossible), and any other object type.
			default:
				ilGenerator.Emit(OpCodes.Ldelem_Ref);
				break;
			};
		}

		public static void EmitStelem(this ILGenerator ilGenerator, Type type)
		{
			// Note: There are no Stelem.u* instructions. The corresponding Stelem.i* instructions should work fine for unsigned integers.
			switch (Type.GetTypeCode(type))
			{
			case TypeCode.Boolean:
			case TypeCode.SByte:
			case TypeCode.Byte:
				ilGenerator.Emit(OpCodes.Stelem_I1);
				break;
			case TypeCode.Int16:
			// Character is encoded in UTF-16 so effectively an unsigned 16-bit integer.
			case TypeCode.Char:
			case TypeCode.UInt16:
				ilGenerator.Emit(OpCodes.Stelem_I2);
				break;
			case TypeCode.Int32:
			case TypeCode.UInt32:
				ilGenerator.Emit(OpCodes.Stelem_I4);
				break;
			case TypeCode.Int64:
			case TypeCode.UInt64:
				ilGenerator.Emit(OpCodes.Stelem_I8);
				break;
			case TypeCode.Single:
				ilGenerator.Emit(OpCodes.Stelem_R4);
				break;
			case TypeCode.Double:
				ilGenerator.Emit(OpCodes.Stelem_R8);
				break;
			// IntPtr is treated specially in CIL as native int, but Type.GetTypeCode(typeof(IntPtr)) returns TypeCode.Object.
			case TypeCode.Object when type == typeof_IntPtr:
				ilGenerator.Emit(OpCodes.Stelem_I);
				break;
			// decimal and DateTime are value types.
			case TypeCode.Decimal:
			case TypeCode.DateTime:
			case TypeCode.Object when type.IsValueType:
				ilGenerator.Emit(OpCodes.Stelem, type);
				break;
			// Default case handles string, DBNull, null (should be impossible), and any other object type.
			default:
				ilGenerator.Emit(OpCodes.Stelem_Ref);
				break;
			};
		}

		static readonly Type typeof_IntPtr = typeof(IntPtr);
	}
}
