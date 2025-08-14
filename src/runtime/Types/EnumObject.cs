using System;
using System.Runtime.CompilerServices;

namespace Python.Runtime
{
    /// <summary>
    /// Managed class that provides the implementation for reflected enum types.
    /// </summary>
    [Serializable]
    internal class EnumObject : ClassBase
    {
        internal EnumObject(Type type) : base(type)
        {
        }

        /// <summary>
        /// Standard comparison implementation for instances of enum types.
        /// </summary>
        public static NewReference tp_richcompare(BorrowedReference ob, BorrowedReference other, int op)
        {
            object rightInstance;
            CLRObject leftClrObject;
            int comparisonResult;

            switch (op)
            {
                case Runtime.Py_EQ:
                case Runtime.Py_NE:
                    var pytrue = Runtime.PyTrue;
                    var pyfalse = Runtime.PyFalse;

                    // swap true and false for NE
                    if (op != Runtime.Py_EQ)
                    {
                        pytrue = Runtime.PyFalse;
                        pyfalse = Runtime.PyTrue;
                    }

                    if (ob == other)
                    {
                        return new NewReference(pytrue);
                    }

                    if (!TryGetSecondCompareOperandInstance(ob, other, out leftClrObject, out rightInstance))
                    {
                        return new NewReference(pyfalse);
                    }

                    if (rightInstance != null &&
                        TryCompare(leftClrObject.inst as Enum, rightInstance, out comparisonResult) &&
                        comparisonResult == 0)
                    {
                        return new NewReference(pytrue);
                    }
                    else
                    {
                        return new NewReference(pyfalse);
                    }

                case Runtime.Py_LT:
                case Runtime.Py_LE:
                case Runtime.Py_GT:
                case Runtime.Py_GE:
                    if (!TryGetSecondCompareOperandInstance(ob, other, out leftClrObject, out rightInstance))
                    {
                        return Exceptions.RaiseTypeError("Cannot get managed object");
                    }

                    if (rightInstance == null)
                    {
                        return Exceptions.RaiseTypeError($"Cannot compare {leftClrObject.inst.GetType()} to None");
                    }

                    try
                    {
                        if (!TryCompare(leftClrObject.inst as Enum, rightInstance, out comparisonResult))
                        {
                            return Exceptions.RaiseTypeError($"Cannot compare {leftClrObject.inst.GetType()} with {rightInstance.GetType()}");
                        }

                        return new NewReference(GetComparisonResult(op, comparisonResult));
                    }
                    catch (ArgumentException e)
                    {
                        return Exceptions.RaiseTypeError(e.Message);
                    }

                default:
                    return new NewReference(Runtime.PyNotImplemented);
            }
        }

        /// <summary>
        /// Tries comparing the give enum to the right operand by converting it to the appropriate type if possible
        /// </summary>
        /// <returns>True if the right operand was converted to a supported type and the comparison was performed successfully</returns>
        private static bool TryCompare(Enum left, object right, out int result)
        {
            result = int.MinValue;
            var conversionSuccessful = true;
            var leftType = left.GetType();
            var leftIsUnsigned = () => leftType.GetEnumUnderlyingType() == typeof(UInt64);

            switch (right)
            {
                case Enum when leftType == right.GetType():
                    // Same enum type
                    result = left.CompareTo(right);
                    break;
                case Enum rightEnum:
                    // Different enum type
                    result = Compare(left, rightEnum, leftIsUnsigned());
                    break;
                case string rightString:
                    result = left.ToString().CompareTo(rightString);
                    break;
                case double rightDouble:
                    result = Compare(left, rightDouble, leftIsUnsigned());
                    break;
                case long rightLong:
                    result = Compare(left, rightLong, leftIsUnsigned());
                    break;
                case ulong rightULong:
                    result = Compare(left, rightULong, leftIsUnsigned());
                    break;
                case int rightInt:
                    result = Compare(left, (long)rightInt, leftIsUnsigned());
                    break;
                case uint rightUInt:
                    result = Compare(left, (ulong)rightUInt, leftIsUnsigned());
                    break;
                default:
                    conversionSuccessful = false;
                    break;
            }

            return conversionSuccessful;
        }

        #region Comparison against integers

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Compare(long a, ulong b)
        {
            if (a < 0) return -1;
            return ((ulong)a).CompareTo(b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Compare(Enum a, long b, bool isUnsigned)
        {

            if (isUnsigned)
            {
                return -Compare(b, Convert.ToUInt64(a));
            }
            return Convert.ToInt64(a).CompareTo(b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Compare(Enum a, ulong b, bool inUnsigned)
        {
            if (inUnsigned)
            {
                return Convert.ToUInt64(a).CompareTo(b);
            }
            return Compare(Convert.ToInt64(a), b);
        }

        #endregion

        #region Comparison against doubles

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Compare(Enum a, double b, bool isUnsigned)
        {
            if (isUnsigned)
            {
                var uIntA = Convert.ToUInt64(a);
                if (uIntA < b) return -1;
                if (uIntA > b) return 1;
                return 0;
            }

            var intA = Convert.ToInt64(a);
            if (intA < b) return -1;
            if (intA > b) return 1;
            return 0;
        }

        #endregion

        #region Comparison against other enum types

        /// <summary>
        /// We support comparing enums of different types by comparing their underlying values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Compare(Enum a, Enum b, bool isUnsigned)
        {
            if (b.GetType().GetEnumUnderlyingType() == typeof(UInt64))
            {
                return Compare(a, Convert.ToUInt64(b), isUnsigned);
            }
            return Compare(a, Convert.ToInt64(b), isUnsigned);
        }

        #endregion
    }
}
