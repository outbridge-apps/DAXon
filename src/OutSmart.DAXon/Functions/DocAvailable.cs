////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Values;
using System;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implement the fn:doc-available() function
    /// </summary>
    internal sealed class DocAvailable : SystemFunction
    {
        private bool IsDocAvailable(AtomicValue hrefVal, IXPathContext context)
        {
            if (hrefVal == null)
            {
                return false;
            }

            string href = hrefVal.GetStringValue();
            return DocAvailableFn(href, context);
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return BooleanValue.Get(IsDocAvailable((AtomicValue)arguments[0].Head(), context));
        }

        public bool DocAvailableFn(string href, IXPathContext context)
        {
            try
            {
                PackageData packageData = GetRetainedStaticContext().GetPackageData();
                DocumentKey documentKey = DocumentFn.ComputeDocumentKey(href, StaticBaseUriString, packageData, context);
                DocumentPool pool = context.GetController().GetDocumentPool();
                if (pool.IsMarkedUnavailable(documentKey))
                {
                    return false;
                }

                ITreeInfo doc = pool.Find(documentKey);
                if (doc != null)
                {
                    return true;
                }

                IItem item = DocumentFn.MakeDoc(href, StaticBaseUriString, packageData, null, context, null, true);
                if (item != null)
                {
                    return true;
                }
                else
                {

                    // The document does not exist; ensure that this remains the case
                    pool.MarkUnavailable(documentKey);
                    return false;
                }
            }
            catch (Exception)
            {
                // fn:doc-available never propagates a failure to make the document available — it yields
                // false (XP31 defines even an invalid URI such as ':/' as false). Java wraps resolution
                // failures in XPathException, but this port can leak raw .NET exceptions (e.g.
                // UriFormatException from an unparseable href), so catch broadly here.
                return false;
            }
        }
    }
}
