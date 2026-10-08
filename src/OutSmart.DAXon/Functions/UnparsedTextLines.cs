////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Trees.Iterators;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implements the fn:unparsed-text-lines function: reads the resource as text and returns its lines
    /// as a sequence of strings. Line endings (#xA, #xD, #xD#xA) are the separators and are not returned;
    /// a trailing line-ending does not produce a final empty line.
    /// </summary>
    internal sealed class UnparsedTextLines : UnparsedTextFunction
    {
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            StringValue hrefVal = (StringValue)arguments[0].Head();
            if (hrefVal == null)
            {
                return EmptySequence.GetInstance();
            }

            string encoding = null;
            if (GetArity() == 2)
            {
                IItem enc = arguments[1].Head();
                encoding = enc == null ? null : enc.GetStringValue();
            }

            try
            {
                // Faithful to upstream evalUnparsedTextLines: the lines are delivered LAZILY by an
                // UnparsedTextIterator, so the resource is never held as one big string and the lines
                // are never materialized as one big list. With stable-unparsed-text the content must
                // be read (and cached) whole for repeatability; the line split still streams.
                bool stable = context.GetConfiguration().GetBooleanProperty(Feature<bool>.STABLE_UNPARSED_TEXT);
                ISequenceIterator iter;
                if (stable)
                {
                    StringValue content = UnparsedText.EvalUnparsedText(hrefVal, StaticBaseUriString, encoding, context);
                    URI abs = GetAbsoluteURI(hrefVal.GetStringValue(), StaticBaseUriString);
                    iter = new UnparsedTextIterator(new System.IO.StringReader(content.GetStringValue()), abs, context);
                }
                else
                {
                    URI abs = GetAbsoluteURI(hrefVal.GetStringValue(), StaticBaseUriString);
                    iter = new UnparsedTextIterator(abs, context, encoding);
                }

                return new LazySequence(iter);
            }
            catch (XPathException e)
            {
                e.MaybeSetErrorCode("FOUT1170");
                if (GetArity() == 2)
                {
                    throw e.ReplacingErrorCode("FOUT1200", "FOUT1190");
                }

                throw;
            }
        }
    }
}
