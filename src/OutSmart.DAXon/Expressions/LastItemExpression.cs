////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class LastItemExpression : SingleItemFilter
    {

        public override int ImplementationMethod => EVALUATE_METHOD;

        public override string ExpressionName => "lastOf";
        public LastItemExpression(Expression @base) : base(@base)
        {
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            LastItemExpression exp = new LastItemExpression(BaseExpression.Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, exp);
            return exp;
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            ISequenceIterator forwards = BaseExpression.Iterate(context);
            if (forwards is IReversibleIterator)
            {
                return ((IReversibleIterator)forwards).GetReverseIterator().Next();
            }
            else
            {
                IItem current = null;
                while (true)
                {
                    IItem item = forwards.Next();
                    if (item == null)
                    {
                        return current;
                    }

                    current = item;
                }
            }
        }

        public override Elaborator GetElaborator()
        {
            return new LastItemExprElaborator();
        }

        /// <summary>
        /// Elaborator for a "last item expression" (typically {@code SEQ[last()]})
        /// </summary>
        public static IItem GetLast(ISequenceIterator iter)
        {
            // Some IReversibleIterator impls (singleton/atomic/manual/empty) return null from
            // GetReverseIterator() to signal "reversal unsupported"; guard against it (was NRE on
            // SEQ[last()], e.g. functx:index-of-string-last) and fall through to a forward scan.
            ISequenceIterator rev = iter is IReversibleIterator ? ((IReversibleIterator)iter).GetReverseIterator() : null;
            if (rev != null)
            {
                return rev.Next();
            }
            else
            {
                IItem current = null;
                while (true)
                {
                    IItem item = iter.Next();
                    if (item == null)
                    {
                        return current;
                    }

                    current = item;
                }
            }
        }

        /// <summary>
        /// Elaborator for a "last item expression" (typically {@code SEQ[last()]})
        /// </summary>
        internal sealed class LastItemExprElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                LastItemExpression expr = (LastItemExpression)GetExpression();
                IPullEvaluator baseEval = expr.BaseExpression.MakeElaborator().ElaborateForPull();
                return (context) => GetLast(baseEval.Iterate(context));
            }
        }
    }
}