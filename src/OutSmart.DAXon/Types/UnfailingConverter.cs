////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Misc Java stdlib types Saxon references but we don't yet shim individually.

// Saxon-internal stub namespaces — sub-packages permanently excluded for now.
// Stubs only what's needed for top-level references to resolve.

namespace OutSmart.DAXon.Types
{
    // Top-level DownCastingConverter removed; nested Converter.DownCastingConverter
    // serves all sites (both bare `DownCastingConverter` (with using-static) and `Converter.DownCastingConverter`).

    // UnfailingConverter sibling stub. In real Saxon this is a nested abstract
    // class inside Converter. (Its Promoter* nested copies were dead duplicates — the live
    // promoters TypeChecker instantiates are the top-level Values.PromoterTo* classes.)
    internal abstract class UnfailingConverter : Converter
    {
        // Ported 2026-07-06 from upstream Converter.DownCastingConverter (was a hollow stub whose null Convert
        // NRE'd CastExpression.PreEvaluate for downcasts like xs:integer -> xs:positiveInteger). Checks that a
        // value belonging to a supertype is a valid instance of the subtype, returning the subtype instance or
        // a ValidationFailure.
        internal sealed class DownCastingConverter : Converter
        {
            private readonly IAtomicType newType;
            private readonly string errorCode;
            public new ISimpleType TargetType => newType;
            public DownCastingConverter(object target, object rules)
            {
                newType = (IAtomicType)target;
                SetConversionRules(rules);
            }
            public DownCastingConverter(object target, object rules, object errorCode) { newType = (IAtomicType)target; SetConversionRules(rules); this.errorCode = (string)errorCode; } // ASC.MakeDownCaster 3-arg form (target, rules, "XPTY0004")

            public override IConversionResult Convert(object value)
            {
                var input = (OutSmart.DAXon.Values.AtomicValue)value;
                return Convert(input, input.CanonicalLexicalRepresentation);
            }

            public IConversionResult Convert(OutSmart.DAXon.Values.AtomicValue input, OutSmart.DAXon.Text.UnicodeString lexicalForm)
            {
                if (Types.Type.IsSubType(input.GetItemType(), newType))
                {
                    return input;
                }

                if (input.GetUType() != newType.GetUType())
                {
                    return new ValidationFailure("Cannot convert " + input.ToShortString() + " to " + newType.DisplayName, errorCode);
                }

                ValidationFailure f = newType.Validate(input, lexicalForm, GetConversionRules());
                if (f == null)
                {
                    return input.CopyAsSubType(newType);
                }
                else
                {
                    if (errorCode != null)
                    {
                        f.SetErrorCode(errorCode);
                    }

                    return f;
                }
            }

            public ValidationFailure Validate(OutSmart.DAXon.Values.AtomicValue input, OutSmart.DAXon.Text.UnicodeString lexicalForm)
            {
                return newType.Validate(input, lexicalForm, GetConversionRules());
            }

            public override Converter SetNamespaceResolver(object resolver) => this;
        }
    }
}
