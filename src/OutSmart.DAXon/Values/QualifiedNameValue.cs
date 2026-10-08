////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Types;
using System;
namespace OutSmart.DAXon.Values
{
    public abstract class QualifiedNameValue : AtomicValue, IAtomicMatchKey
    {
        protected readonly StructuredQName qName;

        public override UnicodeString PrimitiveStringValue => StringView.Of(qName.DisplayName).Tidy();

        public string ClarkName => qName.ClarkName;

        public string EQName => qName.EQName;

        public string LocalName => qName.GetLocalPart();
        public QualifiedNameValue(StructuredQName qName, IAtomicType typeLabel) : base(typeLabel)
        {
            if (qName == null)
                throw new NullReferenceException();
            this.qName = qName;
        }

        public static AtomicValue MakeQName(string prefix, NamespaceUri uri, string local, IAtomicType targetType, UnicodeString lexicalForm, ConversionRules rules)
        {
            if (targetType.Fingerprint == StandardNames.XS_QNAME)
            {
                return new QNameValue(prefix, uri, local, BuiltInAtomicType.QNAME, true);
            }
            else
            {
                QualifiedNameValue qnv;
                if (targetType.PrimitiveType == StandardNames.XS_QNAME)
                {
                    qnv = new QNameValue(prefix, uri, local, targetType, true);
                }
                else
                {
                    qnv = new NotationValue(prefix, uri, local, targetType);
                }

                ValidationFailure vf = targetType.Validate(qnv, lexicalForm, rules);
                if (vf != null)
                {
                    throw vf.MakeException();
                }

                return qnv;
            }
        }

        public NamespaceUri GetNamespaceURI()
        {
            return qName.GetNamespaceUri();
        }

        public string GetPrefix()
        {
            return qName.GetPrefix();
        }

        public override IAtomicMatchKey GetXPathMatchKey(IStringCollator collator, int implicitTimezone)
        {
            return this;
        }

        public override int GetHashCode()
        {
            return qName.GetHashCode();
        }

        public override bool IsIdentical(AtomicValue v)
        {
            return base.IsIdentical(v) && qName.GetPrefix().Equals(((QualifiedNameValue)v).GetPrefix());
        }

        public override int IdentityHashCode()
        {
            return qName.IdentityHashCode();
        }

        public override string Show()
        {
            return "QName(\"" + GetNamespaceURI() + "\", \"" + LocalName + "\")";
        }

        public virtual System.Xml.XmlQualifiedName ToXmlQualifiedName()
        {
            return qName.ToXmlQualifiedName();
        }

        public virtual StructuredQName GetStructuredQName()
        {
            return qName;
        }
    }
}