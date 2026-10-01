////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;

// Stub PromoterToX classes (TypeChecker.cs references unqualified).
// Java source has these as nested in Converter; we expose them in OutSmart.DAXon.Values
// since TypeChecker has `using OutSmart.DAXon.Values`.
namespace OutSmart.DAXon.Values
{
    // Was an empty shell: the inherited Convert(object) => null meant TypeChecker rule-3 promotion
    // (decimal -> float for a declared xs:float) silently produced no conversion — a function declared
    // `as xs:float { 1 div 3 }` returned the raw 18-digit xs:decimal (K2-FunctionProlog-7). Mirrors
    // upstream Converter.PromoterToFloat (Converter.java:771): double -> float is NOT promotable.
    internal sealed class PromoterToFloat : Converter
    {
        public PromoterToFloat(object rules) { }
        public override IConversionResult Convert(object value)
        {
            AtomicValue input = (AtomicValue)value;
            if (input is FloatValue)
            {
                return input;
            }
            if (input is DoubleValue)
            {
                var derr = new ValidationFailure("Cannot promote from xs:double to xs:float");
                derr.SetErrorCode("XPTY0004");
                return derr;
            }
            if (input is NumericValue)
            {
                return new FloatValue((float)((NumericValue)input).GetDoubleValue());
            }
            if (input.IsUntypedAtomic())
            {
                try
                {
                    string s = input.GetStringValue().Trim();
                    switch (s)
                    {
                        case "INF": case "+INF": return new FloatValue(float.PositiveInfinity);
                        case "-INF": return new FloatValue(float.NegativeInfinity);
                        case "NaN": return new FloatValue(float.NaN);
                        default: return new FloatValue(float.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
                    }
                }
                catch (FormatException)
                {
                    var verr = new ValidationFailure("Cannot convert string \"" + input.GetStringValue() + "\" to xs:float");
                    verr.SetErrorCode("FORG0001");
                    return verr;
                }
            }
            var err = new ValidationFailure("Cannot promote non-numeric value to xs:float");
            err.SetErrorCode("XPTY0004");
            return err;
        }
    }
}
