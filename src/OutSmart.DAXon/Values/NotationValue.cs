////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;

using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Values
{
    /// <summary>
    /// An xs:NOTATION value.
    /// </summary>
    internal sealed class NotationValue : QualifiedNameValue
    {

        public override BuiltInAtomicType PrimitiveType => BuiltInAtomicType.NOTATION;
        public NotationValue(string prefix, NamespaceUri uri, string localName, bool check) : base(new StructuredQName(prefix, uri, localName), BuiltInAtomicType.NOTATION)
        {
            if (check && !NameChecker.IsValidNCName(localName))
            {
                throw new XPathException("Malformed local name in NOTATION: '" + localName + '\'', "FORG0001");
            }

            prefix = prefix == null ? "" : prefix;
            if (check && uri.IsEmpty() && prefix.Length != 0)
            {
                throw new XPathException("NOTATION has null namespace but non-empty prefix", "FOCA0002");
            }
        }

        public NotationValue(string prefix, NamespaceUri uri, string localName, IAtomicType typeLabel) : base(new StructuredQName(prefix, uri, localName), typeLabel)
        {
        }

        public NotationValue(StructuredQName qName, IAtomicType typeLabel) : base(qName, typeLabel)
        {
        }

        public override AtomicValue CopyAsSubType(IAtomicType typeLabel)
        {
            return new NotationValue(GetStructuredQName(), typeLabel);
        }

        public override bool Equals(object other)
        {
            return other is NotationValue && qName.Equals(((NotationValue)other).qName);
        }

        public override int GetHashCode()
        {
            return qName.GetHashCode();
        }

        public override IXPathComparable GetXPathComparable(IStringCollator collator, int implicitTimezone)
        {
            return null;
        }

        public override string Show()
        {
            return "NOTATION(" + ClarkName + ')';
        }
    }
}