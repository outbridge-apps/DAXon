////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
// Ported from the nested class StringConverter.StringToIntegerSubtype in upstream
// net/sf/saxon/type/StringConverter.java (replaces the Phase 4.8c excluded stub). Converts a lexical
// string to a built-in subtype of xs:integer (xs:int / xs:long / xs:short / xs:byte / xs:unsignedLong /
// xs:unsignedInt / xs:unsignedShort / xs:unsignedByte / xs:nonNegativeInteger / xs:positiveInteger /
// xs:nonPositiveInteger / xs:negativeInteger), range-checking the value against the target type.

using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Types
{
    using OutSmart.DAXon.Text;

    /// <summary>
    /// Converts a string to a built-in subtype of integer.
    /// </summary>
    internal sealed class StringToIntegerSubtype : StringConverter
    {
        private readonly BuiltInAtomicType targetType;

        public StringToIntegerSubtype(BuiltInAtomicType targetType)
        {
            this.targetType = targetType;
        }

        public override IConversionResult ConvertString(UnicodeString input)
        {
            IConversionResult iv = IntegerValue.StringToInteger(input);
            if (iv is Int64Value)
            {
                bool ok = IntegerValue.CheckRange(((Int64Value)iv).LongValue(), targetType);
                if (ok)
                {
                    return ((Int64Value)iv).CopyAsSubType(targetType);
                }
                else
                {
                    return new ValidationFailure("Integer value is out of range for type " + targetType);
                }
            }
            else if (iv is BigIntegerValue)
            {
                bool ok = IntegerValue.CheckBigRange(((BigIntegerValue)iv).AsBigInteger(), targetType);
                if (ok)
                {
                    return ((BigIntegerValue)iv).CopyAsSubType(targetType);
                }
                else
                {
                    return new ValidationFailure("Integer value is out of range for type " + targetType);
                }
            }
            else
            {
                // iv is a ValidationFailure (bad lexical form)
                return iv;
            }
        }
    }
}
