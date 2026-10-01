////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;

namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implement the fn:uri-collection() function (new in XQuery 3.0/XSLT 3.0). This is responsible for
    /// calling the registered <see cref="ICollectionFinder"/>. For the effect of the default
    /// system-supplied CollectionFinder, see <see cref="OutSmart.DAXon.Resources.StandardCollectionFinder"/>.
    /// </summary>
    internal sealed class UriCollection : SystemFunction, ICallable
    {

        private ISequenceIterator GetUris(string absoluteURI, IXPathContext context)
        {
            // Use a collection registered with the configuration if there is one; otherwise call the CollectionFinder.
            IResourceCollection collection = context.GetConfiguration().GetRegisteredCollection(absoluteURI);
            if (collection == null)
            {
                ICollectionFinder collectionFinder = context.GetController().GetCollectionFinder();
                if (collectionFinder != null)
                {
                    collection = collectionFinder.FindCollection(context, absoluteURI);
                }
            }

            if (collection == null)
            {
                // Should not happen; we're calling user code so we check for it.
                throw new XPathException("No collection has been defined for href: " + (absoluteURI == null ? "" : absoluteURI), "FODC0002", context);
            }

            return new UriIterator(collection.GetResourceURIs(context));
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            string href;
            if (arguments.Length == 0)
            {
                href = null;
            }
            else
            {
                IItem arg = arguments[0].Head();
                href = arg == null ? null : arg.GetStringValue();
            }

            if (href == null)
            {
                href = context.GetConfiguration().DefaultCollection;
                if (href == null)
                {
                    throw new XPathException("No default collection has been defined", "FODC0002", context);
                }
            }

            string abs = CollectionFn.GetAbsoluteCollectionURI(GetRetainedStaticContext().StaticBaseUriString, href, context);
            return new LazySequence(GetUris(abs, context));
        }
        // ISequenceIterator over the resource URIs of a collection, delivering each as an xs:anyURI.
        private sealed class UriIterator : ISequenceIterator
        {
            private readonly IEnumerator<string> sources;
            public UriIterator(IEnumerator<string> sources) { this.sources = sources; }
            public IItem Next()
            {
                if (sources.MoveNext())
                {
                    return new AnyURIValue(sources.Current);
                }
                return null;
            }
            public void Dispose() { (sources as IDisposable)?.Dispose(); }
        }
    }
}
