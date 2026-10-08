////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
namespace OutSmart.DAXon.Values
{
    public class QNameValue : QualifiedNameValue
    {

        public override BuiltInAtomicType PrimitiveType => BuiltInAtomicType.QNAME;
        public QNameValue(StructuredQName qName, IAtomicType typeLabel) : base(qName, typeLabel)
        {
        }

        public QNameValue(string prefix, NamespaceUri uri, string localName) : this(prefix, uri, localName, BuiltInAtomicType.QNAME)
        {
        }

        public QNameValue(string prefix, NamespaceUri uri, string localName, IAtomicType type) : base(new StructuredQName(prefix, uri, localName), type)
        {
        }

        public QNameValue(string prefix, NamespaceUri uri, string localName, IAtomicType type, bool check) : this(BuildStructuredQName(prefix, uri, localName, check), type)
        {
        }

        private static StructuredQName BuildStructuredQName(string prefix, NamespaceUri uri, string localName, bool check)
        {
            if (check && !NameChecker.IsValidNCName(localName))
            {
                throw new XPathException("Malformed local name in QName: '" + localName + '\'', "FORG0001");
            }

            prefix = prefix == null ? "" : prefix;
            if (check && uri.IsEmpty() && prefix.Length != 0)
            {
                throw new XPathException("QName has null namespace but non-empty prefix", "FOCA0002");
            }

            return new StructuredQName(prefix, uri, localName);
        }

        public override AtomicValue CopyAsSubType(IAtomicType typeLabel)
        {
            return new QNameValue(qName, typeLabel);
        }

        public override AtomicValue GetComponent(AccessorFn.Component part)
        {
            switch (part)
            {
                case AccessorFn.Component.LOCALNAME:
                    return new StringValue(LocalName, BuiltInAtomicType.NCNAME);
                case AccessorFn.Component.NAMESPACE:
                    return new AnyURIValue((GetNamespaceURI().ToUnicodeString()));
                case AccessorFn.Component.PREFIX:
                    string prefix = GetPrefix();
                    if ((prefix.Length == 0))
                    {
                        return null;
                    }
                    else
                    {
                        return new StringValue(prefix, BuiltInAtomicType.NCNAME);
                    }

                default:
                    throw new NotSupportedException("Component of QName must be URI, Local Name, or Prefix");
            }
        }

        public override bool Equals(object other)
        {
            return other is QNameValue && qName.Equals(((QNameValue)other).qName);
        }

        public override int GetHashCode()
        {
            return qName.GetHashCode();
        }

        public override IXPathComparable GetXPathComparable(IStringCollator collator, int implicitTimezone)
        {
            return null;
        }
    }
}