////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Collections;
using System.Collections.Generic;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implementation of fn:unparsed-text() - with one argument or two
    /// </summary>
    internal sealed class UnparsedText : UnparsedTextFunction, IPushableFunction
    {

        private const int errorValue = 0;
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            StringValue hrefVal = (StringValue)arguments[0].Head();
            string encoding;
            if (GetArity() == 2)
            {
                IItem enc = arguments[1].Head();
                encoding = enc == null ? null : enc.GetStringValue();
            }
            else
            {
                encoding = null;
            }

            try
            {
                return SequenceTool.ItemOrEmpty(EvalUnparsedText(hrefVal, StaticBaseUriString, encoding, context));
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

        public void Process(Outputter destination, IXPathContext context, ISequence[] arguments)
        {
            bool stable = context.GetConfiguration().GetBooleanProperty(Feature<bool>.STABLE_UNPARSED_TEXT);
            if (stable)
            {
                ISequence result = Call(context, arguments);
                StringValue value = (StringValue)result.Head();
                if (value != null)
                {
                    destination.Append(value, Loc.NONE, ReceiverOption.NONE);
                }
            }
            else
            {
                StringValue href = (StringValue)arguments[0].Head();
                URI absoluteURI = GetAbsoluteURI(href.GetStringValue(), StaticBaseUriString);
                string encoding = GetArity() == 2 ? arguments[1].Head().GetStringValue() : null;
                IUniStringConsumer consumer = destination.GetStringReceiver(false, Loc.NONE);
                consumer.Open();
                try
                {
                    ReadFile(absoluteURI, encoding, consumer, context);
                    consumer.Close();
                }
                catch (XPathException e)
                {
                    if (GetArity() == 2 && e.HasErrorCode("FOUT1200"))
                    {
                        e.SetErrorCode("FOUT1190");
                    }

                    throw;
                }
            }
        }
        public static StringValue EvalUnparsedText(StringValue hrefVal, string @base, string encoding, IXPathContext context)
        {
            UnicodeString content;
            StringValue result;
            bool stable = context.GetConfiguration().GetBooleanProperty(Feature<bool>.STABLE_UNPARSED_TEXT);
            try
            {
                if (hrefVal == null)
                {
                    return null;
                }

                string href = hrefVal.GetStringValue();
                URI absoluteURI = GetAbsoluteURI(href, @base);
                if (stable)
                {
                    Controller controller = context.GetController();

                    lock (controller.syncLock)
                    {
                        Dictionary<URI, UnicodeString> cache = (Dictionary<URI, UnicodeString>)controller.GetUserData("unparsed-text-cache", "");
                        if (cache != null)
                        {
                            UnicodeString existing = cache.GetOrDefault(absoluteURI);
                            if (existing != null)
                            {
                                if (existing.Length() > 0 && existing.CodePointAt(0) == errorValue)
                                {
                                    throw new XPathException(existing.Substring(1).ToString(), "FOUT1170");
                                }

                                return new StringValue(existing);
                            }
                        }

                        XPathException error = null;
                        try
                        {
                            UniStringCollector consumer = new UniStringCollector();
                            ReadFile(absoluteURI, encoding, consumer, context);
                            content = consumer.ToUnicodeString();
                        }
                        catch (XPathException e)
                        {
                            error = e;
                            content = StringView.Tidy((char)errorValue + e.Message);
                        }

                        if (cache == null)
                        {
                            cache = new Dictionary<URI, UnicodeString>();
                            controller.SetUserData("unparsed-text-cache", "", cache);
                            cache[absoluteURI] = content;
                        }

                        if (error != null)
                        {
                            throw error;
                        }
                    }
                }
                else
                {
                    // Latin1-honest byte collector: keep unparsed-text on ASCII/Latin1 text on the
                    // byte path (BlockCopy per chunk, zero-copy Slice8/Twine8 result) instead of widening
                    // every chunk into the int[] UnicodeBuilder and narrowing back. Wide/astral input
                    // takes the collector's StringBuilder path, byte-identical to the old result.
                    UniStringCollector consumer = new UniStringCollector();
                    ReadFile(absoluteURI, encoding, consumer, context);
                    return new StringValue(consumer.ToUnicodeString());
                }

                result = new StringValue(content);
            }
            catch (XPathException err)
            {
                err.MaybeSetErrorCode("FOUT1170");
                throw;
            }

            return result;
        }
    }
}
