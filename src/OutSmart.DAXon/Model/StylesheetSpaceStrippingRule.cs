////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Types;
using System;
namespace OutSmart.DAXon.Model
{
    /// <summary>
    /// A whitespace stripping rule that strips whitespace according to the rules defined for XSLT stylesheets
    /// </summary>
    internal sealed class StylesheetSpaceStrippingRule : ISpaceStrippingRule
    {
        //    Any child of one of the following elements is removed from the tree,
        //    regardless of any xml:space attributes. Note that this array must be in numeric
        //    order for binary chop to work correctly.
        private static readonly int[] specials = new[]
        {
            StandardNames.XSL_ANALYZE_STRING,
            StandardNames.XSL_APPLY_IMPORTS,
            StandardNames.XSL_APPLY_TEMPLATES,
            StandardNames.XSL_ATTRIBUTE_SET,
            StandardNames.XSL_CALL_TEMPLATE,
            StandardNames.XSL_CHARACTER_MAP,
            StandardNames.XSL_CHOOSE,
            StandardNames.XSL_EVALUATE,
            StandardNames.XSL_MERGE,
            StandardNames.XSL_MERGE_SOURCE,
            StandardNames.XSL_NEXT_ITERATION,
            StandardNames.XSL_NEXT_MATCH,
            StandardNames.XSL_STYLESHEET,
            StandardNames.XSL_TRANSFORM
        };
        private readonly NamePool namePool;
        public StylesheetSpaceStrippingRule(NamePool pool)
        {
            this.namePool = pool;
        }

        public int IsSpacePreserving(INodeName elementName, ISchemaType schemaType)
        {
            int fingerprint = elementName.ObtainFingerprint(namePool);
            if (fingerprint == (StandardNames.XSL_TEXT & NamePool.FP_MASK))
            {
                return Stripper.ALWAYS_PRESERVE;
            }

            if (Array.BinarySearch(specials, fingerprint) >= 0)
            {
                return Stripper.ALWAYS_STRIP;
            }

            return Stripper.STRIP_DEFAULT;
        }

        public ProxyReceiver MakeStripper(IReceiver next)
        {
            return new Stripper(this, next);
        }

        public void Export(ExpressionPresenter presenter)
        {
        }
    }
}