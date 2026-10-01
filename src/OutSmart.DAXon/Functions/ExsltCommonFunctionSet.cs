////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implementation of the exslt-common function library. This is available in all Saxon versions.
    /// </summary>
    internal sealed class ExsltCommonFunctionSet : BuiltInFunctionSet
    {
        private static readonly ExsltCommonFunctionSet THE_INSTANCE = new ExsltCommonFunctionSet();

        public override string ConventionalPrefix => "exsltCommon";

        private ExsltCommonFunctionSet()
        {
            Init();
        }

        public static ExsltCommonFunctionSet GetInstance()
        {
            return THE_INSTANCE;
        }

        private void Init()
        {
            Register("node-set", 1, (e) => e.Populate(() => new NodeSetFn(), AnyItemType.GetInstance(), OPT, 0)
                .Arg(0, AnyItemType.GetInstance(), OPT, EMPTY));
            Register("object-type", 1, (e) => e.Populate(() => new ObjectTypeFn(), BuiltInAtomicType.STRING, ONE, 0)
                .Arg(0, AnyItemType.GetInstance(), ONE, null));
        }

        public override NamespaceUri GetNamespace()
        {
            return NamespaceUri.EXSLT_COMMON;
        }

        /// <summary>
        /// Implement exslt:node-set
        /// </summary>
        internal sealed class NodeSetFn : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                return arguments[0];
            }
        }

        /// <summary>
        /// Implement exslt:object-type
        /// </summary>
        internal sealed class ObjectTypeFn : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                TypeHierarchy th = context.GetConfiguration().GetTypeHierarchy();
                IItem value = arguments[0].Head();
                ItemType type = SequenceTool.GetItemType(value, th);
                if (th.IsSubType(type, AnyNodeTest.GetInstance()))
                {
                    return StringValue.Bmp("node-set");
                }
                else if (th.IsSubType(type, BuiltInAtomicType.STRING))
                {
                    return StringValue.Bmp("string");
                }
                else if (NumericType.IsNumericType(type))
                {
                    return StringValue.Bmp("number");
                }
                else if (th.IsSubType(type, BuiltInAtomicType.BOOLEAN))
                {
                    return StringValue.Bmp("boolean");
                }
                else
                {
                    return new StringValue(type.ToString());
                }
            }
        }
    }
}
