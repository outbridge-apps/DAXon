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
        // The promotion of an argument to xs:string (Saxon's Converter.PromoterToString); the base Convert returns null.
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
