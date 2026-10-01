////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions.HigherOrder;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Regex;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.XPath;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions
{
    internal sealed class RegexGroup : ContextAccessorFunction
    {
        public override IFunctionItem BindContext(IXPathContext context)
        {
            IFunctionItem alwaysEmptyFunction = new CallableFunction(1, new AlwaysEmpty(), new SpecificFunctionType(new SequenceType[] { SequenceType.SINGLE_INTEGER }, SequenceType.SINGLE_STRING));
            if (GetRetainedStaticContext().GetPackageData().HostLanguageVersion < 40)
            {
                return alwaysEmptyFunction;
            }

            IRegexIterator ri = context.GetCurrentRegexIterator();
            if (ri == null)
            {
                throw new XPathException("There is no current group", "XTDE1061");
            }

            int groups = ri.NumberOfGroups;
            MapItem map = new HashTrieMap();
            for (int i = 0; i <= groups; i++)
            {
                map = map.AddEntry(Int64Value.MakeIntegerValue(i), new StringValue(ri.GetRegexGroup(i)));
            }

            StructuredQName mapGetName = new StructuredQName("map", NamespaceUri.MAP_FUNCTIONS, "get");
            BuiltInFunctionSet lib = MapFunctionSet.GetInstance(40);
            SymbolicName.F symbolicName = new SymbolicName.F(mapGetName, 3);
            IFunctionItem mapGet3 = lib.GetFunctionItem(symbolicName, new IndependentContext(context.GetConfiguration()));
            return (IFunctionItem)new CurriedFunction(mapGet3, new ISequence[] { map, null, alwaysEmptyFunction });
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments) /*Java covariant StringValue widened (C# 7.3)*/
        {
            IRegexIterator iter = context.GetCurrentRegexIterator();
            if (iter == null)
            {
                return StringValue.EMPTY_STRING;
            }

            NumericValue gp0 = (NumericValue)arguments[0].Head();
            UnicodeString s = iter.GetRegexGroup((int)gp0.LongValue());
            return StringValue.MakeUStringValue(s);
        }

        private sealed class AlwaysEmpty : ICallable
        {
            public ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                return StringValue.EMPTY_STRING;
            }
        }
    }
}
