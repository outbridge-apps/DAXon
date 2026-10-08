////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
// Ported from the nested class Converter.UpCastingConverter in upstream net/sf/saxon/type/Converter.java
// (replaces the Phase 4.8c hollow stub whose inherited Convert()=>null NRE'd CastExpression.PreEvaluate via
// Literal.MakeLiteral(null) for casts like xs:integer -> xs:decimal — an upcast within the same primitive).

using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Types
{
    /// <summary>
    /// A Converter that handles conversion from a subtype to a supertype of the same primitive type (an
    /// "upcast", e.g. xs:integer -> xs:decimal): the value is unchanged, only its type annotation is widened.
    /// </summary>
    internal sealed class UpCastingConverter : Converter
    {
        private readonly IAtomicType newTypeAnnotation;

        public UpCastingConverter(IAtomicType annotation)
        {
            this.newTypeAnnotation = annotation;
        }

        public override IConversionResult Convert(object value)
        {
            return ((AtomicValue)value).CopyAsSubType(newTypeAnnotation);
        }
    }
}
