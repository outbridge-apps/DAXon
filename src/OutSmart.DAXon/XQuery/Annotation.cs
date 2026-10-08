////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
namespace OutSmart.DAXon.XQuery
{
    /// <summary>
    /// This class represents an annotation that appears in a function or variable declarations
    /// </summary>
    public class Annotation
    {
        public static readonly StructuredQName UPDATING = NamespaceUri.XQUERY.QName("updating");
        public static readonly StructuredQName SIMPLE = NamespaceUri.XQUERY.QName("simple");
        public static readonly StructuredQName PRIVATE = NamespaceUri.XQUERY.QName("private");
        public static readonly StructuredQName PUBLIC = NamespaceUri.XQUERY.QName("public");
        // The name of the annotation
        private StructuredQName qName = null;
        // The list of parameters (all strings or numbers) associated with the annotation
        private IList<AtomicValue> annotationParameters = null;

        public virtual StructuredQName AnnotationQName => qName;

        public virtual IList<AtomicValue> AnnotationParameters
        {
            get
            {
                if (annotationParameters == null)
                {
                    annotationParameters = new List<AtomicValue>();
                }

                return annotationParameters;
            }
        }
        public Annotation(StructuredQName name)
        {
            this.qName = name;
        }

        public virtual void AddAnnotationParameter(AtomicValue value)
        {
            if (annotationParameters == null)
            {
                annotationParameters = new List<AtomicValue>();
            }

            annotationParameters.Add(value);
        }

        public override bool Equals(object other)
        {
            if (!(other is Annotation && qName.Equals(((Annotation)other).qName) && AnnotationParameters.Count == ((Annotation)other).AnnotationParameters.Count))
            {
                return false;
            }

            for (int i = 0; i < annotationParameters.Count; i++)
            {
                if (!AnnotationParamEqual(annotationParameters[i], ((Annotation)other).annotationParameters[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AnnotationParamEqual(AtomicValue a, AtomicValue b)
        {
            if (a is StringValue && b is StringValue)
            {
                return a.UnicodeStringValue.Equals(b.UnicodeStringValue);
            }
            else if (a is NumericValue && b is NumericValue)
            {
                return ((NumericValue)a).GetDoubleValue() == ((NumericValue)b).GetDoubleValue();
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return qName.GetHashCode() ^ annotationParameters.GetHashCode();
        }
    }
}