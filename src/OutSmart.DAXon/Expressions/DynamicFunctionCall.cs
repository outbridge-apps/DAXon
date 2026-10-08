////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Operators;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class DynamicFunctionCall : Expression
    {
        private readonly Operand targetFunction;
        private readonly OperandArray suppliedArguments;

        public override int ImplementationMethod => ITERATE_METHOD;
        public DynamicFunctionCall(Expression fn, IList<Expression> args)
        {
            targetFunction = new Operand(this, fn, OperandRole.INSPECT);
            suppliedArguments = new OperandArray(this, args.ToArray());
        }

        public override ItemType GetItemType()
        {

            // IItem type of the result is the same as that of the supplied function
            ItemType fnType = targetFunction.GetChildExpression().GetItemType();
            if (fnType is MapType)
            {
                return ((MapType)fnType).ValueType.PrimaryType;
            }
            else if (fnType is ArrayItemType)
            {
                return ((ArrayItemType)fnType).MemberType.PrimaryType;
            }
            else if (fnType is IFunctionItemType)
            {
                return ((IFunctionItemType)fnType).ResultType.PrimaryType;
            }
            else if (fnType is AnyFunctionType)
            {
                return AnyItemType.GetInstance();
            }
            else
            {
                return AnyItemType.GetInstance();
            }
        }

        protected override int ComputeCardinality()
        {
            ItemType fnType = targetFunction.GetChildExpression().GetItemType();
            if (fnType is MapType)
            {
                return Cardinality.Union(((MapType)fnType).ValueType.GetCardinality(), StaticProperty.ALLOWS_ZERO);
            }
            else if (fnType is ArrayItemType)
            {
                return ((ArrayItemType)fnType).MemberType.GetCardinality();
            }
            else if (fnType is IFunctionItemType)
            {
                return ((IFunctionItemType)fnType).ResultType.GetCardinality();
            }
            else
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
        }

        public int GetArity()
        {
            return suppliedArguments.NumberOfOperands;
        }

        public override IEnumerable<Operand> Operands()
        {
            IList<Operand> allOperands = new List<Operand>(GetArity() + 1);
            allOperands.Add(targetFunction);
            for (int i = 0; i < GetArity(); i++)
            {
                allOperands.Add(suppliedArguments.GetOperand(i));
            }

            return allOperands;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            TypeCheckChildren(visitor, contextInfo);
            TypeChecker tc = visitor.GetConfiguration().GetTypeChecker(false);
            Func<RoleDiagnostic> roleSupplier0 = () => new RoleDiagnostic(RoleDiagnostic.DYNAMIC_FUNCTION, targetFunction.GetChildExpression().ToShortString(), 0);
            targetFunction.SetChildExpression(tc.StaticTypeCheck(targetFunction.GetChildExpression(), SequenceType.SINGLE_FUNCTION, roleSupplier0, visitor));
            if (GetArity() == 1)
            {
                Expression target = targetFunction.GetChildExpression();
                if (target.GetItemType() is MapType)
                {

                    // Convert $map($key) to map:get($map, $key)
                    // This improves streamability analysis - see accumulator-053
                    return MakeGetCall(visitor, MapFunctionSet.GetInstance(31), contextInfo);
                }
                else if (target.GetItemType() is ArrayItemType)
                {

                    // Convert $array($key) to array:get($array, $key)
                    return MakeGetCall(visitor, ArrayFunctionSet.GetInstance(31), contextInfo);
                }
            }

            return this;
        }

        private Expression MakeGetCall(ExpressionVisitor visitor, BuiltInFunctionSet fnSet, ContextItemStaticInfo contextInfo)
        {
            Expression target = targetFunction.GetChildExpression();
            Expression key = suppliedArguments.GetOperandExpression(0);
            Expression getter = fnSet.MakeFunction("get", 2).MakeFunctionCall(target, key);
            getter.SetRetainedStaticContext(target.GetRetainedStaticContext());

            // Use custom diagnostics for type errors on the argument of the call (bug 4772)
            TypeChecker tc = visitor.GetConfiguration().GetTypeChecker(visitor.StaticContext.IsInBackwardsCompatibleMode());
            if (fnSet == MapFunctionSet.GetInstance(31))
            {
                Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.MISC, "key value supplied when calling a map as a function", 0);
                ((SystemFunctionCall)getter).SetArg(1, tc.StaticTypeCheck(key, SequenceType.SINGLE_ATOMIC, role, visitor));
            }
            else
            {
                Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.MISC, "subscript supplied when calling an array as a function", 0);
                ((SystemFunctionCall)getter).SetArg(1, tc.StaticTypeCheck(key, SequenceType.SINGLE_INTEGER, role, visitor));
            }

            return getter.TypeCheck(visitor, contextInfo);
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return MakeElaborator().ElaborateForPull().Iterate(context);
        }

        public override void Export(ExpressionPresenter @out)
        {
            if (@out.GetOptions().target == "JS" && @out.GetOptions().targetVersion == 2)
            {

                // for backwards compatibility, output a call on saxon:apply
                @out.StartElement("ifCall", this);
                @out.EmitAttribute("name", "Q{http://saxon.sf.net/}apply");
                @out.EmitAttribute("type", "*");
                if (targetFunction.GetChildExpression() is Literal)
                {
                    IFunctionItem f = (IFunctionItem)(((Literal)targetFunction.GetChildExpression()).GroundedValue);
                    if (f.GetFunctionName() != null)
                    {
                        @out.EmitAttribute("dyn", f.GetFunctionName().EQName + "#" + f.GetArity());
                    }
                }

                targetFunction.GetChildExpression().Export(@out);
                @out.StartSubsidiaryElement("arrayBlock");
                foreach (Operand o in suppliedArguments)
                {
                    o.GetChildExpression().Export(@out);
                }

                @out.EndSubsidiaryElement();
                @out.EndElement();
            }
            else
            {
                @out.StartElement("dynCall", this);
                targetFunction.GetChildExpression().Export(@out);
                foreach (Operand o in suppliedArguments)
                {
                    o.GetChildExpression().Export(@out);
                }

                @out.EndElement();
            }
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            Expression f = targetFunction.GetChildExpression().Copy(rebindings);
            List<Expression> args = new List<Expression>(GetArity());
            foreach (Operand o in suppliedArguments)
            {
                args.Add(o.GetChildExpression().Copy(rebindings));
            }

            return new DynamicFunctionCall(f, args);
        }

        public override void GatherProperties(Action<string, object> consumer)
        {
        }

        public override Elaborator GetElaborator()
        {
            return new DynamicFunctionCallElaborator();
        }

        public override string ToShortString()
        {
            StringBuilder sb = new StringBuilder(targetFunction.GetChildExpression().ToShortString()).Append('(');
            foreach (Operand op in suppliedArguments)
            {
                sb.Append(op.GetChildExpression().ToShortString()).Append(',');
            }

            sb[sb.Length - 1] = ')';
            return sb.ToString();
        }

        private sealed class DynamicFunctionCallElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                DynamicFunctionCall expr = (DynamicFunctionCall)GetExpression();
                TypeHierarchy th = GetConfiguration().GetTypeHierarchy();
                ISequenceEvaluator[] argEvaluators = new ISequenceEvaluator[expr.GetArity()];
                for (int i = 0; i < argEvaluators.Length; i++)
                {
                    Expression arg = expr.suppliedArguments.GetOperand(i).GetChildExpression();
                    argEvaluators[i] = new LearningEvaluator(arg, arg.MakeElaborator().Lazily(true, false));
                }

                Expression body = expr.targetFunction.GetChildExpression();
                IItemEvaluator functionEvaluator = body.MakeElaborator().ElaborateForItem();
                bool is40 = expr.GetRetainedStaticContext().GetPackageData().HostLanguageVersion >= 40;
                return (context) =>
                {
                    IFunctionItem fn = (IFunctionItem)functionEvaluator.Eval(context);
                    IFunctionItemType fit = fn.FunctionItemType;
                    if (fn.GetArity() != argEvaluators.Length)
                    {
                        string errorCode = "XPTY0004";
                        throw new XPathException("Number of arguments required for dynamic call to " + fn.Description + " is " + fn.GetArity() + "; number supplied = " + argEvaluators.Length, errorCode).AsTypeError().WithXPathContext(context).WithLocation(expr.GetLocation());
                    }

                    ISequence[] argValues = new ISequence[argEvaluators.Length];
                    if (fit == AnyFunctionType.ANY_FUNCTION)
                    {
                        for (int i = 0; i < argEvaluators.Length; i++)
                        {
                            argValues[i] = argEvaluators[i].Evaluate(context);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < argEvaluators.Length; i++)
                        {
                            SequenceType expected = fit.ArgumentTypes[i];
                            ISequence actual = argEvaluators[i].Evaluate(context);
                            if (!expected.Equals(SequenceType.ANY_SEQUENCE))
                            {
                                Func<RoleDiagnostic> role;
                                role = () => new RoleDiagnostic(RoleDiagnostic.FUNCTION, fn.Description, 0);
                                actual = th.ApplyFunctionConversionRules(actual, expected, role, Loc.NONE);
                            }

                            argValues[i] = actual;
                        }
                    }

                    IXPathContext c2 = fn.MakeNewContext(context, null);
                    if (!is40)
                    {
                        c2.CurrentOutputUri = null;
                        if (c2 is XPathContextMajor)
                        {
                            ((XPathContextMajor)c2).SetCurrentRegexIterator(null);
                        }
                    }

                    ISequence rawResult = fn.Call(c2, argValues);
                    if (fn.IsTrustedResultType())
                    {

                        // trust system functions to return a result of the correct type
                        return rawResult.Iterate();
                    }
                    else
                    {

                        // Check the result of the function
                        Func<RoleDiagnostic> resultRole = () => new RoleDiagnostic(RoleDiagnostic.FUNCTION_RESULT, "fn:apply", -1);
                        return th.ApplyFunctionConversionRules(rawResult, fit.ResultType, resultRole, Loc.NONE).Iterate();
                    }
                };
            }
        }
    }
}