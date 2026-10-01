////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Ported from upstream net/sf/saxon/functions/MathFunctionSet.java (replaces the empty stub, which
// registered NO functions -> every math:* call was XPST0017 unresolved).

using System;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// The math: function namespace (math:pi, math:sin, math:pow, math:atan2, ...); registrations plus
    /// the concrete function implementations as nested classes.
    /// </summary>
    internal sealed class MathFunctionSet : BuiltInFunctionSet
    {
        private static readonly MathFunctionSet THE_INSTANCE = new MathFunctionSet();

        public override string ConventionalPrefix => "math";

        private MathFunctionSet()
        {
            Init();
        }

        public static MathFunctionSet GetInstance() => THE_INSTANCE;

        private void Reg1(string name, Func<double, double> method)
        {
            Register(name, 1, (e) => e.Populate(() => new TrigFn1(method), BuiltInAtomicType.DOUBLE, OPT, CARD0)
                .Arg(0, BuiltInAtomicType.DOUBLE, OPT, EMPTY));
        }

        private void Init()
        {
            // Arity 0
            Register("pi", 0, (e) => e.Populate(() => new PiFn(), BuiltInAtomicType.DOUBLE, ONE, 0));

            // Arity 1
            Reg1("sin", Math.Sin);
            Reg1("cos", Math.Cos);
            Reg1("tan", Math.Tan);
            Reg1("asin", Math.Asin);
            Reg1("acos", Math.Acos);
            Reg1("atan", Math.Atan);
            Reg1("sqrt", Math.Sqrt);
            Reg1("log", Math.Log);
            Reg1("log10", Math.Log10);
            Reg1("exp", Math.Exp);
            Reg1("exp10", (input) => Math.Pow(10, input));

            // Arity 2
            Register("pow", 2, (e) => e.Populate(() => new PowFn(), BuiltInAtomicType.DOUBLE, OPT, CARD0)
                .Arg(0, BuiltInAtomicType.DOUBLE, OPT, EMPTY)
                .Arg(1, BuiltInAtomicType.DOUBLE, ONE, null));

            Register("atan2", 2, (e) => e.Populate(() => new Atan2Fn(), BuiltInAtomicType.DOUBLE, ONE, 0)
                .Arg(0, BuiltInAtomicType.DOUBLE, ONE, null)
                .Arg(1, BuiltInAtomicType.DOUBLE, ONE, null));
        }

        public override NamespaceUri GetNamespace() => NamespaceUri.MATH;

        /// <summary>math:pi</summary>
        internal sealed class PiFn : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                return new DoubleValue(Math.PI);
            }
        }

        /// <summary>Generic superclass for the arity-1 trig functions.</summary>
        internal sealed class TrigFn1 : SystemFunction
        {
            private readonly Func<double, double> method;

            public TrigFn1(Func<double, double> method)
            {
                this.method = method;
            }

            public override ISequence Call(IXPathContext context, ISequence[] args)
            {
                // Arg type is xs:double? but a numeric literal (xs:integer) may reach here un-promoted;
                // read via NumericValue so integer/decimal/float inputs all yield the double value.
                NumericValue @in = (NumericValue)args[0].Head();
                if (@in == null)
                {
                    return EmptySequence.GetInstance();
                }
                else
                {
                    return new DoubleValue(method(@in.GetDoubleValue()));
                }
            }

            public override Elaborator GetElaborator()
            {
                return new TrigFn1Elaborator();
            }

            // The argument is consumed unconditionally, so it is read as an eager item instead of a
            // per-call LazySequence wrapper; null (empty) yields empty exactly like Call.
            private sealed class TrigFn1Elaborator : ItemElaborator
            {
                public override IItemEvaluator ElaborateForItem()
                {
                    SystemFunctionCall expr = (SystemFunctionCall)GetExpression();
                    TrigFn1 fn = (TrigFn1)expr.TargetFunction;
                    if (Cardinality.AllowsMany(expr.GetArg(0).GetCardinality()) || ErrorExpression.IsContainedIn(expr.GetArg(0)))
                    {
                        SystemFunctionCall.SystemFunctionCallElaborator generic = new SystemFunctionCall.SystemFunctionCallElaborator();
                        generic.SetExpression(expr);
                        return generic.ElaborateForItem();
                    }

                    IItemEvaluator argEval = expr.GetArg(0).MakeElaborator().ElaborateForItem();
                    return (context) =>
                    {
                        try
                        {
                            NumericValue @in = (NumericValue)argEval.Eval(context);
                            return @in == null ? null : (IItem)new DoubleValue(fn.method(@in.GetDoubleValue()));
                        }
                        catch (XPathException e)
                        {
                            throw e.MaybeWithLocation(expr.GetLocation()).MaybeWithContext(context);
                        }
                    };
                }
            }
        }

        /// <summary>math:pow</summary>
        internal sealed class PowFn : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] args)
            {
                NumericValue x = (NumericValue)args[0].Head();
                if (x == null)
                {
                    return EmptySequence.GetInstance();
                }

                double dx = x.GetDoubleValue();
                DoubleValue result;
                if (dx == 1)
                {
                    result = new DoubleValue(dx);
                }
                else
                {
                    NumericValue yv = (NumericValue)args[1].Head();
                    double dy = yv.GetDoubleValue();
                    if (dy == 0)
                    {
                        // F&O: math:pow($x, 0) is 1 for every $x, INCLUDING NaN. .NET Math.Pow(NaN, 0)
                        // returns NaN (unlike Java/IEEE-754 pow), so special-case it here.
                        result = new DoubleValue(1.0e0);
                    }
                    else if (dx == -1 && double.IsInfinity(dy))
                    {
                        result = new DoubleValue(1.0e0);
                    }
                    else if (dy == 0.5 && dx > 0)
                    {
                        // fdlibm (= Java Math.pow) special-cases y==0.5 for x>0 as sqrt(x); .NET's CRT
                        // pow runs the general (much slower) path. Same guard, same result.
                        result = new DoubleValue(Math.Sqrt(dx));
                    }
                    else
                    {
                        result = new DoubleValue(Math.Pow(dx, dy));
                    }
                }

                return result;
            }

            public override Elaborator GetElaborator()
            {
                return new PowFnElaborator();
            }

            // Arg 0 is consumed unconditionally; arg 1 only when $x != 1 — the fused path mirrors
            // that branch exactly (with lazy generic args, math:pow(1, ERR) never evaluates ERR).
            // A non-lazy argument (focus-dependent, constant-folded error) is evaluated up-front in
            // argument order, exactly where the generic path's Eagerly() evaluator runs it.
            private sealed class PowFnElaborator : ItemElaborator
            {
                public override IItemEvaluator ElaborateForItem()
                {
                    SystemFunctionCall expr = (SystemFunctionCall)GetExpression();
                    if (Cardinality.AllowsMany(expr.GetArg(0).GetCardinality()) || Cardinality.AllowsMany(expr.GetArg(1).GetCardinality())
                        || ErrorExpression.IsContainedIn(expr.GetArg(0)) || ErrorExpression.IsContainedIn(expr.GetArg(1)))
                    {
                        SystemFunctionCall.SystemFunctionCallElaborator generic = new SystemFunctionCall.SystemFunctionCallElaborator();
                        generic.SetExpression(expr);
                        return generic.ElaborateForItem();
                    }

                    bool eager0 = !expr.GetArg(0).SupportsLazyEvaluation();
                    bool eager1 = !expr.GetArg(1).SupportsLazyEvaluation();
                    IItemEvaluator xEval = expr.GetArg(0).MakeElaborator().ElaborateForItem();
                    IItemEvaluator yEval = expr.GetArg(1).MakeElaborator().ElaborateForItem();
                    return (context) =>
                    {
                        try
                        {
                            IItem pre0 = eager0 ? xEval.Eval(context) : null;
                            IItem pre1 = eager1 ? yEval.Eval(context) : null;
                            NumericValue x = (NumericValue)(eager0 ? pre0 : xEval.Eval(context));
                            if (x == null)
                            {
                                return null;
                            }

                            double dx = x.GetDoubleValue();
                            if (dx == 1)
                            {
                                return new DoubleValue(dx);
                            }

                            NumericValue yv = (NumericValue)(eager1 ? pre1 : yEval.Eval(context));
                            double dy = yv.GetDoubleValue();
                            if (dy == 0)
                            {
                                return new DoubleValue(1.0e0);
                            }

                            if (dx == -1 && double.IsInfinity(dy))
                            {
                                return new DoubleValue(1.0e0);
                            }

                            if (dy == 0.5 && dx > 0)
                            {
                                return new DoubleValue(Math.Sqrt(dx));
                            }

                            return new DoubleValue(Math.Pow(dx, dy));
                        }
                        catch (XPathException e)
                        {
                            throw e.MaybeWithLocation(expr.GetLocation()).MaybeWithContext(context);
                        }
                    };
                }
            }
        }

        /// <summary>math:atan2</summary>
        internal sealed class Atan2Fn : SystemFunction
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                NumericValue y = (NumericValue)arguments[0].Head();
                NumericValue x = (NumericValue)arguments[1].Head();
                double result = Math.Atan2(y.GetDoubleValue(), x.GetDoubleValue());
                return new DoubleValue(result);
            }

            public override Elaborator GetElaborator()
            {
                return new Atan2FnElaborator();
            }

            // Both arguments are consumed unconditionally; non-lazy args run up-front in argument
            // order, exactly where the generic path's Eagerly() evaluator runs them.
            private sealed class Atan2FnElaborator : ItemElaborator
            {
                public override IItemEvaluator ElaborateForItem()
                {
                    SystemFunctionCall expr = (SystemFunctionCall)GetExpression();
                    if (Cardinality.AllowsMany(expr.GetArg(0).GetCardinality()) || Cardinality.AllowsMany(expr.GetArg(1).GetCardinality())
                        || ErrorExpression.IsContainedIn(expr.GetArg(0)) || ErrorExpression.IsContainedIn(expr.GetArg(1)))
                    {
                        SystemFunctionCall.SystemFunctionCallElaborator generic = new SystemFunctionCall.SystemFunctionCallElaborator();
                        generic.SetExpression(expr);
                        return generic.ElaborateForItem();
                    }

                    bool eager0 = !expr.GetArg(0).SupportsLazyEvaluation();
                    bool eager1 = !expr.GetArg(1).SupportsLazyEvaluation();
                    IItemEvaluator yEval = expr.GetArg(0).MakeElaborator().ElaborateForItem();
                    IItemEvaluator xEval = expr.GetArg(1).MakeElaborator().ElaborateForItem();
                    return (context) =>
                    {
                        try
                        {
                            IItem pre0 = eager0 ? yEval.Eval(context) : null;
                            IItem pre1 = eager1 ? xEval.Eval(context) : null;
                            NumericValue y = (NumericValue)(eager0 ? pre0 : yEval.Eval(context));
                            NumericValue x = (NumericValue)(eager1 ? pre1 : xEval.Eval(context));
                            return new DoubleValue(Math.Atan2(y.GetDoubleValue(), x.GetDoubleValue()));
                        }
                        catch (XPathException e)
                        {
                            throw e.MaybeWithLocation(expr.GetLocation()).MaybeWithContext(context);
                        }
                    };
                }
            }
        }
    }
}
