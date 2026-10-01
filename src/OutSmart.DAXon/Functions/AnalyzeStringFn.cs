////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//import com.saxonica.ee.config.StandardSchemaResolver;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Regex;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implements the fn:analyze-string function defined in XPath 3.0.
    /// </summary>
    internal sealed class AnalyzeStringFn : RegexFunction
    {
        private readonly object syncLock = new object();

        private ResultNamesAndTypes vocab = new ResultNamesAndTypes();
        protected override bool AllowRegexMatchingEmptyString()
        {
            return false;
        }

        private void Init(Configuration config, bool schemaAware)
        {
            lock (syncLock)
            {
                vocab.resultName = new FingerprintedQName("", NamespaceUri.FN, "analyze-string-result");
                vocab.nonMatchName = new FingerprintedQName("", NamespaceUri.FN, "non-match");
                vocab.matchName = new FingerprintedQName("", NamespaceUri.FN, "match");
                vocab.groupName = new FingerprintedQName("", NamespaceUri.FN, "group");
                vocab.groupNrName = new NoNamespaceName("nr");
            }
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            IItem inputItem = arguments[0].Head();
            UnicodeString input;
            if (inputItem == null)
            {
                input = EmptyUnicodeString.GetInstance();
            }
            else
            {
                input = inputItem.UnicodeStringValue;
            }

            IRegularExpression re = GetRegularExpression(arguments, 1, 2);
            IRegexIterator iter = re.Analyze(input);
            Init(context.GetConfiguration(), false);
            Builder builder = context.GetController().MakeBuilder();
            ComplexContentOutputter @out = new ComplexContentOutputter(builder);
            LocalRegexMatchHandler handler = new LocalRegexMatchHandler(@out, vocab);
            builder.BaseURI = StaticBaseUriString;
            builder.SetDurability(Durability.TEMPORARY);
            @out.Open();
            @out.StartElement(vocab.resultName, vocab.resultType, Loc.NONE, ReceiverOption.NONE);
            @out.StartContent();
            for (IItem item; (item = iter.Next()) != null;)
            {
                if (iter.IsMatching())
                {
                    @out.StartElement(vocab.matchName, vocab.matchType, Loc.NONE, ReceiverOption.NONE);
                    @out.StartContent();
                    iter.ProcessMatchingSubstring(handler);
                    @out.EndElement();
                }
                else
                {
                    @out.StartElement(vocab.nonMatchName, vocab.nonMatchType, Loc.NONE, ReceiverOption.NONE);
                    @out.StartContent();
                    @out.Characters(item.UnicodeStringValue, Loc.NONE, ReceiverOption.NONE);
                    @out.EndElement();
                }
            }

            @out.EndElement();
            @out.Close();
            return builder.CurrentRoot;
        }
        private sealed class ResultNamesAndTypes
        {
            public INodeName resultName;
            public INodeName nonMatchName;
            public INodeName matchName;
            public INodeName groupName;
            public INodeName groupNrName;
            public ISchemaType resultType = Untyped.INSTANCE;
            public ISchemaType nonMatchType = Untyped.INSTANCE;
            public ISchemaType matchType = Untyped.INSTANCE;
            public ISchemaType groupType = Untyped.INSTANCE;
            public ISimpleType groupNrType = BuiltInAtomicType.UNTYPED_ATOMIC;
        }

        private sealed class LocalRegexMatchHandler : IRegexMatchHandler
        {
            private readonly ComplexContentOutputter @out;
            private readonly ResultNamesAndTypes vocab;
            public LocalRegexMatchHandler(ComplexContentOutputter @out, ResultNamesAndTypes vocab)
            {
                this.@out = @out;
                this.vocab = vocab;
            }

            public void Characters(UnicodeString s)
            {
                @out.Characters(s, Loc.NONE, ReceiverOption.NONE);
            }

            public void OnGroupStart(int groupNumber)
            {
                @out.StartElement(vocab.groupName, vocab.groupType, Loc.NONE, ReceiverOption.NONE);
                @out.Attribute(vocab.groupNrName, vocab.groupNrType, "" + groupNumber, Loc.NONE, ReceiverOption.NONE);
                @out.StartContent();
            }

            public void OnGroupEnd(int groupNumber)
            {
                @out.EndElement();
            }
        }
    }
}
