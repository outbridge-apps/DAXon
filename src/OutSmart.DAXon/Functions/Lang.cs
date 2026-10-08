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
using System;
namespace OutSmart.DAXon.Functions
{
    internal sealed class Lang : SystemFunction
    {
        public static bool IsLang(string arglang, NodeInfo target)
        {
            string doclang = null;
            NodeInfo node = target;
            while (node != null)
            {
                doclang = node.GetAttributeValue(NamespaceUri.XML, "lang");
                if (doclang != null)
                {
                    break;
                }

                node = node.GetParent();
                if (node == null)
                {
                    return false;
                }
            }

            if (doclang == null)
            {
                return false;
            }

            while (true)
            {
                if (arglang.Equals(doclang, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                int hyphen = doclang.LastIndexOf('-');
                if (hyphen < 0)
                {
                    return false;
                }

                doclang = doclang.Substring(0, hyphen);
            }
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            NodeInfo target;
            if (arguments.Length > 1)
            {
                target = (NodeInfo)arguments[1].Head();
            }
            else
            {
                target = GetAndCheckContextItem(context);
            }

            IItem arg0Val = arguments[0].Head();
            string testLang = arg0Val == null ? "" : arg0Val.GetStringValue();
            return BooleanValue.Get(IsLang(testLang, target));
        }

        private NodeInfo GetAndCheckContextItem(IXPathContext context)
        {
            NodeInfo target;
            IItem current = context.GetContextItem();
            if (current == null)
            {
                throw new XPathException("The context item for lang() is absent").WithErrorCode("XPDY0002").WithXPathContext(context);
            }

            if (!(current is NodeInfo))
            {
                throw new XPathException("The context item for lang() is not a node").WithErrorCode("XPTY0004").WithXPathContext(context);
            }

            target = (NodeInfo)current;
            return target;
        }
    }
}