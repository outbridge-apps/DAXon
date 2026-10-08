////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Operators;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Expressions
{
    /// <summary>
    /// Abstract superclass for calls to system-defined and user-defined functions
    /// </summary>
    public abstract class FunctionCall : Expression
    {
        private OperandArray operanda;

        public virtual Expression[] Arguments
        {
            get
            {
                Expression[] result = new Expression[GetArity()];
                int i = 0;
                foreach (Operand o in Operands())
                {
                    result[i++] = o.GetChildExpression();
                }

                return result;
            }
            set
            {
                SetOperanda(new OperandArray(this, value));
            }
        }

        public override int NetCost => 5;

        public override int ImplementationMethod => ITERATE_METHOD | EVALUATE_METHOD;

        public override string ExpressionName => "functionCall";

        public string DisplayName
        {
            get
            {
                StructuredQName fName = GetFunctionName();
                return fName == null ? "(anonymous)" : fName.DisplayName;
            }
        }
        protected virtual void SetOperanda(OperandArray operanda)
        {
            this.operanda = operanda;
        }

        public virtual OperandArray GetOperanda()
        {
            return operanda;
        }

        public override IEnumerable<Operand> Operands()
        {
            if (operanda != null)
            {
                return operanda;
            }
            else
            {

                // happens during expression tree construction
                return new List<Operand>();
            }
        }

        public abstract IFunctionItem GetTargetFunction(IXPathContext context);
        public abstract StructuredQName GetFunctionName();
        public int GetArity()
        {
            return GetOperanda().NumberOfOperands;
        }

        protected virtual void SetOperanda(Expression[] args, OperandRole[] roles)
        {
            SetOperanda(new OperandArray(this, args, roles));
        }

        public virtual Expression GetArg(int n)
        {
            return GetOperanda().GetOperandExpression(n);
        }

        public virtual void SetArg(int n, Expression child)
        {
            GetOperanda().SetOperand(n, child);
            AdoptChildExpression(child);
        }

        protected Expression SimplifyArguments(IStaticContext env)
        {
            for (int i = 0; i < Arguments.Length; i++)
            {
                Expression exp = GetArg(i).Simplify();
                if (exp != GetArg(i))
                {
                    AdoptChildExpression(exp);
                    SetArg(i, exp);
                }
            }

            return this;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            TypeCheckChildren(visitor, contextInfo);
            CheckArguments(visitor);
            return PreEvaluateIfConstant(visitor);
        }

        protected virtual Expression PreEvaluateIfConstant(ExpressionVisitor visitor)
        {
            Optimizer opt = visitor.ObtainOptimizer();
            if (opt.IsOptionSet(OptimizerOptions.CONSTANT_FOLDING))
            {
                bool @fixed = true;
                foreach (Operand o in Operands())
                {
                    if (!(o.GetChildExpression() is Literal))
                    {
                        @fixed = false;
                    }
                }

                if (@fixed)
                {
                    try
                    {
                        return PreEvaluate(visitor);
                    }
                    catch (NoDynamicContextException err)
                    {

                        // Early evaluation failed, typically because the implicit timezone is not yet known.
                        // Try again later at run-time.
                        return this;
                    }
                }
            }

            return this;
        }

        public virtual void CheckFunctionCall(IFunctionItem target, ExpressionVisitor visitor)
        {
            TypeChecker tc = visitor.GetConfiguration().GetTypeChecker(visitor.StaticContext.IsInBackwardsCompatibleMode());
            SequenceType[] argTypes = target.FunctionItemType.ArgumentTypes;
            IFunctionDefinition fd = null;
            if (target is IFunctionDefinition)
            {
                fd = (IFunctionDefinition)target;
            }

            if (target.IsSequenceVariadic() && GetArity() == 1)
            {
                string name = GetFunctionName() == null ? "" : GetFunctionName().DisplayName;
                Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.FUNCTION, name, 0);
                SetArg(0, tc.StaticTypeCheck(GetArg(0), new SequenceType(argTypes[0].PrimaryType, StaticProperty.ALLOWS_ZERO_OR_MORE), role, visitor));
            }
            else
            {
                int n = target.GetArity();
                for (int i = 0; i < n; i++)
                {
                    int pos = i;
                    Func<RoleDiagnostic> role = () =>
                    {
                        string name = GetFunctionName() == null ? "" : GetFunctionName().DisplayName;
                        return new RoleDiagnostic(RoleDiagnostic.FUNCTION, name, pos);
                    };
                    Expression arg = GetArg(i);

                    // Substitute default value expression for an argument marked for replacement. This is necessary
                    // because the default value expression was not necessarily available when the function call
                    // was first parsed, in the case where it is a forwards reference
                    if (arg is DefaultedArgumentExpression)
                    {
                        if (fd != null)
                        {

                            // A user-defined function with default argument values
                            if (i < fd.GetMinimumArity())
                            {

                                // This argument cannot be omitted
                                throw new XPathException("No value supplied for " + RoleDiagnostic.Ordinal(i + 1) + " parameter of function " + (GetFunctionName() == null ? "" : GetFunctionName().DisplayName), "XPST0141");
                            }

                            if (arg is DefaultedArgumentExpression.DefaultCollationArgument)
                            {
                                arg = new StringLiteral(visitor.StaticContext.GetDefaultCollationName());
                            }
                            else
                            {
                                Expression defaultValue = fd.GetDefaultValueExpression(i);
                                if (defaultValue == null)
                                {

                                    // This only happens if there's an error in the function definition
                                    throw new XPathException("No value or default available for " + RoleDiagnostic.Ordinal(i + 1) + " parameter of function " + (GetFunctionName() == null ? "" : GetFunctionName().DisplayName), "XPST0141");
                                }

                                arg = defaultValue.Copy(new RebindingMap());
                                AdoptChildExpression(arg);
                            }
                        }
                        else if (target is SystemFunction)
                        {

                            // A system function with default argument values
                            BuiltInFunctionSet.Entry details = ((SystemFunction)target).Details;
                            if (i < details.GetMinimumArity())
                            {

                                // This argument cannot be omitted
                                throw new XPathException("No value supplied for " + RoleDiagnostic.Ordinal(i + 1) + " parameter of function " + (GetFunctionName() == null ? "" : GetFunctionName().DisplayName), "XPST0141");
                            }


                            // For the moment, assume a default value of ()
                            arg = new Literal(EmptySequence.GetInstance());
                            AdoptChildExpression(arg);
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }

                    if (arg != null && !(arg is DefaultedArgumentExpression))
                    {
                        SetArg(i, tc.StaticTypeCheck(arg, argTypes[i], role, visitor));
                    }
                }
            }
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            OptimizeChildren(visitor, contextItemType);
            Optimizer opt = visitor.ObtainOptimizer();
            if (opt.IsOptionSet(OptimizerOptions.CONSTANT_FOLDING))
            {
                bool @fixed = true;
                foreach (Operand o in Operands())
                {
                    if (!(o.GetChildExpression() is Literal))
                    {
                        @fixed = false;
                        break;
                    }
                }

                if (@fixed)
                {
                    return PreEvaluate(visitor);
                }
            }

            return this;
        }

        public virtual Expression PreEvaluate(ExpressionVisitor visitor)
        {
            if ((IntrinsicDependencies & ~StaticProperty.DEPENDS_ON_STATIC_CONTEXT) != 0)
            {
                return this;
            }

            try
            {
                try
                {
                    Literal lit = Literal.MakeLiteral(SequenceTool.ToGroundedValue(Iterate(visitor.StaticContext.MakeEarlyEvaluationContext())), this);
                    Optimizer.Trace(visitor.GetConfiguration(), "Pre-evaluated function call " + ToShortString(), lit);
                    return lit;
                }
                catch (UncheckedXPathException e)
                {
                    throw e.GetXPathException();
                }
            }
            catch (NoDynamicContextException e)
            {

                // early evaluation failed, usually because implicit timezone required
                return this;
            }
            catch (NotSupportedException e)
            {

                if (e.InnerException is NoDynamicContextException)
                {
                    return this;
                }
                else
                {
                    throw;
                }
            }
        }

        protected virtual void CheckArguments(ExpressionVisitor visitor)
        {
        }

        protected virtual void CheckArgumentCount(int min, int max)
        {
            int numArgs = GetArity();
            string msg = null;
            if (min == max && numArgs != min)
            {
                msg = "Function call to " + DisplayName + " must supply " + Plural(min, "argument");
            }
            else if (numArgs < min)
            {
                msg = "Function call to " + DisplayName + " must supply at least " + Plural(min, "argument");
            }
            else if (numArgs > max)
            {
                msg = "Function call to " + DisplayName + " must supply no more than " + Plural(max, "argument");
            }

            if (msg != null)
            {
                throw new XPathException(msg, "XPST0017").AsStaticError().WithLocation(GetLocation());
            }
        }

        public static string Plural(int num, string thing)
        {
            switch (num)
            {
                case 0:
                    return "no " + thing + "s";
                case 1:
                    return "one " + thing;
                default:
                    return num + " " + thing + "s";
            }
        }

        public virtual PathMap.PathMapNodeSet AddExternalFunctionCallToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodes)
        {

            // Except in the case of system functions, we have no idea where a function call might
            // navigate, so we assume the worst, and register that the path has unknown dependencies
            PathMap.PathMapNodeSet result = new PathMap.PathMapNodeSet();
            foreach (Operand o in Operands())
            {
                result.AddNodeSet(o.GetChildExpression().AddToPathMap(pathMap, pathMapNodes));
            }

            result.SetHasUnknownDependencies();
            return result;
        }

        public override string ToString()
        {
            StringBuilder buff = new StringBuilder(64);
            StructuredQName fName = GetFunctionName();
            string f;
            if (fName == null)
            {
                f = "$anonymousFunction";
            }
            else if (fName.HasURI(NamespaceUri.FN))
            {
                f = fName.GetLocalPart();
            }
            else
            {
                f = fName.EQName;
            }

            buff.Append(f);
            bool first = true;
            foreach (Operand o in Operands())
            {
                buff.Append(first ? "(" : ", ");
                buff.Append(o.GetChildExpression().ToString());
                first = false;
            }

            buff.Append(first ? "()" : ")");
            return buff.ToString();
        }

        public override string ToShortString()
        {
            StructuredQName fName = GetFunctionName();
            return (fName == null ? "$anonFn" : fName.DisplayName) + "(" + (GetArity() == 0 ? "" : "...") + ")";
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("functionCall", this);
            if (GetFunctionName() == null)
            {
                throw new InvalidOperationException("Exporting call to anonymous function");
            }
            else
            {
                @out.EmitAttribute("name", GetFunctionName().DisplayName);
            }

            foreach (Operand o in Operands())
            {
                o.GetChildExpression().Export(@out);
            }

            @out.EndElement();
        }

        /// <summary>
        /// Determine whether two expressions are equivalent
        /// </summary>
        public override bool Equals(object o)
        {
            if (!(o is FunctionCall))
            {
                return false;
            }

            if (GetFunctionName() == null)
            {
                return this == o;
            }

            FunctionCall f = (FunctionCall)o;
            if (!GetFunctionName().Equals(f.GetFunctionName()))
            {
                return false;
            }

            if (GetArity() != f.GetArity())
            {
                return false;
            }

            for (int i = 0; i < GetArity(); i++)
            {
                if (!GetArg(i).IsEqual(f.GetArg(i)))
                {
                    return false;
                }
            }

            return true;
        }

        protected override int ComputeHashCode()
        {
            if (GetFunctionName() == null)
            {
                return base.ComputeHashCode();
            }

            int h = GetFunctionName().GetHashCode();
            for (int i = 0; i < GetArity(); i++)
            {
                h ^= GetArg(i).GetHashCode();
            }

            return h;
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return MakeElaborator().ElaborateForPull().Iterate(context);
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            return MakeElaborator().ElaborateForItem().Eval(context);
        }

        public virtual bool AdjustRequiredType(JavaExternalObjectType requiredType)
        {
            return false;
        }

        internal abstract class FunctionCallElaborator : PullElaborator
        {
            protected ISequenceEvaluator[] argumentEvaluators;
            protected virtual void AllocateArgumentEvaluators(FunctionCall expr, bool allowRepeatedUse)
            {
                int arity = expr.GetArity();
                argumentEvaluators = new ISequenceEvaluator[arity];
                for (int i = 0; i < arity; i++)
                {
                    argumentEvaluators[i] = expr.GetArg(i).MakeElaborator().Lazily(allowRepeatedUse, false);
                }
            }

            protected virtual ISequence[] EvaluateArguments(IXPathContext context)
            {
                ISequence[] args = new ISequence[argumentEvaluators.Length];
                for (int i = 0; i < argumentEvaluators.Length; i++)
                {
                    args[i] = argumentEvaluators[i].Evaluate(context);
                }

                return args;
            }
        }
    }
}
