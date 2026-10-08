////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Lib
{
    public interface IFunctionAnnotationHandler
    {
        NamespaceUri AssertionNamespace { get; }
        void Check(AnnotationList annotations, string construct);
        bool SatisfiesAssertion(Annotation assertion, AnnotationList annotationList);
        Affinity Relationship(AnnotationList firstList, AnnotationList secondList);
    }
}