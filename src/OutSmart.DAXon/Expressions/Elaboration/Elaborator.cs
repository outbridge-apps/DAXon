////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Values;
using System;
namespace OutSmart.DAXon.Expressions.Elaboration
{
    public abstract class Elaborator
    {
        private Expression expression;

        // Expression.MakeElaborator's stack-probe verdict: the probing wrapper once the expression is
        // known to be deep, else the root found when it was last judged shallow.
        internal object ProbeVerdict;
        public Elaborator()
        {
        }

        public virtual Expression GetExpression()
        {
            return expression;
        }

        public virtual void SetExpression(Expression expr)
        {
            this.expression = expr;
        }

        protected virtual Configuration GetConfiguration()
        {
            return expression.GetConfiguration();
        }

        public virtual ISequenceEvaluator Eagerly()
        {
            Expression expr = GetExpression();
            int m = expr.ImplementationMethod;
            if ((m & Expression.EVALUATE_METHOD) != 0 && !Cardinality.AllowsMany(expr.GetCardinality()))
            {
                IItemEvaluator itemEvaluator = ElaborateForItem();
                return new SingleItemEvaluator(itemEvaluator);
            }
            else if ((m & Expression.ITERATE_METHOD) != 0)
            {
                IPullEvaluator pullEvaluator = ElaborateForPull();
                return new EagerPullEvaluator(pullEvaluator);
            }
            else
            {
                IPushEvaluator pushEvaluator = ElaborateForPush();
                return new EagerPushEvaluator(pushEvaluator);
            }
        }

        public virtual ISequenceEvaluator Lazily(bool repeatable, bool lazyEvaluationRequired)
        {
            Expression expr = GetExpression();
            if (lazyEvaluationRequired)
            {
                return new MemoClosureEvaluator(expr, ElaborateForPull());
            }
            else if (!expr.SupportsLazyEvaluation())
            {
                return Eagerly();
            }
            else if (repeatable)
            {
                return new LearningEvaluator(expr, new MemoClosureEvaluator(expr, ElaborateForPull()));
            }
            else
            {
                IPullEvaluator pullEvaluator = ElaborateForPull();
                return new LazyPullEvaluator(pullEvaluator);
            }
        }

        public abstract IPullEvaluator ElaborateForPull();
        public abstract IPushEvaluator ElaborateForPush();
        public abstract IItemEvaluator ElaborateForItem();
        public abstract IBooleanEvaluator ElaborateForBoolean();
        public abstract IUnicodeStringEvaluator ElaborateForUnicodeString(bool zeroLengthWhenAbsent);
        public virtual IStringEvaluator ElaborateForString(bool zeroLengthWhenAbsent)
        {
            IUnicodeStringEvaluator evaluator = ElaborateForUnicodeString(zeroLengthWhenAbsent);
            return (context) =>
            {
                UnicodeString u = evaluator.Eval(context);
                return u == null ? HandleNullString(zeroLengthWhenAbsent) : u.ToString();
            };
        }

        protected UnicodeString HandleNullUnicodeString(bool zeroLengthWhenAbsent)
        {
            return zeroLengthWhenAbsent ? EmptyUnicodeString.GetInstance() : null;
        }

        protected UnicodeString HandlePossiblyNullUnicodeString(UnicodeString str, bool zeroLengthWhenAbsent)
        {
            if (str == null && zeroLengthWhenAbsent)
            {
                return EmptyUnicodeString.GetInstance();
            }
            else
            {
                return str;
            }
        }

        protected string HandleNullString(bool zeroLengthWhenAbsent)
        {
            return zeroLengthWhenAbsent ? "" : null;
        }

        protected string HandlePossiblyNullString(string str, bool zeroLengthWhenAbsent)
        {
            if (str == null && zeroLengthWhenAbsent)
            {
                return "";
            }
            else
            {
                return str;
            }
        }

        public virtual IUpdateEvaluator ElaborateForUpdate()
        {
            throw new NotSupportedException("Update not supported");
        }
    }
}