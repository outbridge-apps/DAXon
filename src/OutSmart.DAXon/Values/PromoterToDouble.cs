////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Types;

// Stub PromoterToX classes (TypeChecker.cs references unqualified).
// Java source has these as nested in Converter; we expose them in OutSmart.DAXon.Values
// since TypeChecker has `using OutSmart.DAXon.Values`.
namespace OutSmart.DAXon.Values
{
    // Was an empty shell: the inherited Convert(object) => null meant TypeChecker rule-3 promotion
    // (decimal/float -> double for a declared xs:double) silently produced no conversion — a function
    // declared `as xs:double` returned the raw xs:decimal (K2-FunctionProlog-7 family). Mirrors upstream
    // Converter.PromoterToDouble (Converter.java:734).
    internal sealed class PromoterToDouble : Converter
    {
        private readonly StringToDouble stringToDouble;

        public PromoterToDouble(object rules)
        {
            stringToDouble = (rules as OutSmart.DAXon.Lib.ConversionRules)?.StringToDoubleConverter ?? OutSmart.DAXon.Functions.StringToDouble11.GetInstance();
        }

        public override IConversionResult Convert(object value)
        {
            AtomicValue input = (AtomicValue)value;
            if (input is DoubleValue)
            {
                return input;
            }
            if (input is NumericValue)
            {
                return new DoubleValue(((NumericValue)input).GetDoubleValue());
            }
            if (input.IsUntypedAtomic())
            {
                // By the lexical rules of xs:double, as a cast reads it. double.Parse took "1,000" for 1000 and "1,5"
                // for 15, knew "Infinity", and on .NET Framework threw for a magnitude no double holds.
                if (stringToDouble.TryStringToNumber(input.UnicodeStringValue, out double d))
                {
                    return new DoubleValue(d);
                }

                var verr = new ValidationFailure("Cannot convert string \"" + input.GetStringValue() + "\" to xs:double");
                verr.SetErrorCode("FORG0001");
                return verr;
            }
            var err = new ValidationFailure("Cannot promote non-numeric value to xs:double");
            err.SetErrorCode("XPTY0004");
            return err;
        }
    }
}
