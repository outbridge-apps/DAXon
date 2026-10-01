////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Types;

// namespace OutSmart.DAXon.Text
// {
//     public class UnicodeChar { public UnicodeChar() {} public UnicodeChar(int cp) {} }
// }

namespace OutSmart.DAXon.Functions
{
    internal sealed class XQueryFunctionAnnotationHandler : IFunctionAnnotationHandler
    {
        // Must match the real impl: Configuration.Init registers this handler keyed by its assertion
        // namespace, and a null key throws ArgumentNullException from the backing Dictionary. The XQuery
        // 3.0 annotations namespace (per XQueryFunctionAnnotationHandler.cs:102).
        public NamespaceUri AssertionNamespace => NamespaceUri.Of(NamespaceConstant.XQUERY);
        public XQueryFunctionAnnotationHandler() { }
        // Was a hollow no-op, so `%public`/`%private` on an inline function were silently accepted (should
        // be XQST0125). %public/%private are only meaningful on a module-level function ("DF") or variable
        // ("DV") declaration; on an inline function ("IF") the annotation is a static error. (Duplicate
        // %public+%private on declarations is caught separately in XQueryParser via CheckPublicPrivateAnnotations.)
        public void Check(AnnotationList annotations, string construct)
        {
            if (construct != "IF")
            {
                return;
            }
            foreach (OutSmart.DAXon.XQuery.Annotation ann in annotations)
            {
                OutSmart.DAXon.Model.StructuredQName name = ann.AnnotationQName;
                if (name.Equals(OutSmart.DAXon.XQuery.Annotation.PUBLIC) || name.Equals(OutSmart.DAXon.XQuery.Annotation.PRIVATE))
                {
                    throw new OutSmart.DAXon.Transformation.XPathException(
                        "An inline function must not be annotated as %" + name.GetLocalPart(), "XQST0125");
                }
            }
        }
        public bool SatisfiesAssertion(Annotation assertion, AnnotationList annotationList) => true;
        public Affinity Relationship(AnnotationList firstList, AnnotationList secondList) => Affinity.OVERLAPS;
    }
}
