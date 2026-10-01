////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Top-level OutSmart.DAXon.Types.TwoPhaseConverter — this is the one ConversionRules.GetConverter actually
// instantiates (the sibling nested Converter.TwoPhaseConverter requires qualification and is unused there).
//
// Subtype-via-primitive casts route through here (e.g. xs:integer -> xs:positiveInteger = upcast to xs:decimal
// + downcast). Faithful Java (Converter.TwoPhaseConverter): run phaseOne, then phaseTwo on the intermediate result.

using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Types
{
    internal sealed class TwoPhaseConverter : Converter
    {
        private readonly Converter phaseOne;
        private readonly Converter phaseTwo;
        public TwoPhaseConverter(Converter a, Converter b) { phaseOne = a; phaseTwo = b; }

        public override IConversionResult Convert(object value)
        {
            if (phaseOne == null || phaseTwo == null)
            {
                return null;
            }

            IConversionResult temp = phaseOne.Convert(value);
            if (temp is ValidationFailure)
            {
                return temp;
            }

            // phaseTwo may be a DownCastingConverter; its object-taking Convert already derives the canonical
            // lexical form internally, so a plain Convert(intermediate) matches the upstream special case.
            return phaseTwo.Convert((AtomicValue)temp);
        }

        public override Converter SetNamespaceResolver(object resolver)
        {
            if (phaseOne == null || phaseTwo == null)
            {
                return this;
            }

            return new TwoPhaseConverter(phaseOne.SetNamespaceResolver(resolver), phaseTwo.SetNamespaceResolver(resolver));
        }
    }
}
