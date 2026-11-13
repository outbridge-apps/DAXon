////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
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

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            IRegexIterator iter = context.GetCurrentRegexIterator();
            if (iter == null)
            {
                return StringValue.EMPTY_STRING;
            }

            NumericValue gp0 = (NumericValue)arguments[0].Head();
            UnicodeString s = iter.GetRegexGroup(GroupNumber(gp0));
            return StringValue.MakeUStringValue(s);
        }

        // A number past int is no group (-1 gives ""); (int)LongValue() wrapped 4294967297 to group 1.
        private static int GroupNumber(NumericValue n) => n.Signum() == 0 ? 0 : n.AsSubscript();

        public override Elaborator GetElaborator()
        {
            return new RegexGroupElaborator();
        }

        // regex-group(N): the group's text straight from the current regex iterator, N read once when it is a literal; no
        // argument array and, for a string consumer such as xsl:value-of, no StringValue.
        private sealed class RegexGroupElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                Func<IXPathContext, UnicodeString> group = Group();
                return context => StringValue.MakeUStringValue(group(context));
            }

            public override IUnicodeStringEvaluator ElaborateForUnicodeString(bool zeroLengthWhenAbsent)
            {
                Func<IXPathContext, UnicodeString> group = Group();
                return context => group(context) ?? EmptyUnicodeString.GetInstance();
            }

            private Func<IXPathContext, UnicodeString> Group()
            {
                Expression arg = ((SystemFunctionCall)GetExpression()).GetArg(0);
                if (arg is Literal literal && literal.GroundedValue is NumericValue n)
                {
                    int number = GroupNumber(n);
                    return context => context.GetCurrentRegexIterator()?.GetRegexGroup(number);
                }

                IItemEvaluator argEval = arg.MakeElaborator().ElaborateForItem();
                return context =>
                {
                    IRegexIterator iter = context.GetCurrentRegexIterator();
                    return iter?.GetRegexGroup(GroupNumber((NumericValue)argEval.Eval(context)));
                };
            }
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
