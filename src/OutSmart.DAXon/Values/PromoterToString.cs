////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;

// Stub PromoterToX classes (TypeChecker.cs references unqualified).
// Java source has these as nested in Converter; we expose them in OutSmart.DAXon.Values
// since TypeChecker has `using OutSmart.DAXon.Values`.
namespace OutSmart.DAXon.Values
{
    internal sealed class PromoterToString : Converter
    {
        public PromoterToString() { }
        // net472 port: the real OutSmart.DAXon.Types.Converter (poc/output/full/Converter.cs) is EXCLUDED from the
        // build (re-including cascades errors), so the active base is the OutSmart.DAXon.Internal Converter stub
        // whose `Convert(object) => null` made fn:string-argument atomization yield an empty sequence
        // (FinDim normalize-space(DIMENSIONVALUE) -> ""). Override Convert with the real xs:string promotion
        // rules (mirrors the excluded Converter.PromoterToString.Convert).
        public override IConversionResult Convert(object value)
        {
            AtomicValue input = (AtomicValue)value;
            int fp = input.PrimitiveType.Fingerprint;
            if (fp == StandardNames.XS_STRING)
            {
                return input;
            }
            if (fp == StandardNames.XS_ANY_URI || fp == StandardNames.XS_UNTYPED_ATOMIC)
            {
                return new StringValue(input.UnicodeStringValue);
            }
            var err = new ValidationFailure("Required type is xs:string; supplied value is " + Err.Depict(input));
            err.SetErrorCode("XPTY0004");
            return err;
        }
    }
}
