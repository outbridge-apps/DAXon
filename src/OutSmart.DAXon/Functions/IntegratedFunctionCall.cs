////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Functions
{
    internal sealed class IntegratedFunctionCall : FunctionCall, ICallable
    {
        private readonly StructuredQName name;
        private readonly ExtensionFunctionCall function;
        private SequenceType resultType = SequenceType.ANY_SEQUENCE;
        private int state;

        public override int IntrinsicDependencies
        {
            get
            {
                ExtensionFunctionDefinition definition = function.Definition;
                return definition.DependsOnFocus() ? StaticProperty.DEPENDS_ON_FOCUS : 0;
            }
        }
        public IntegratedFunctionCall(StructuredQName name, ExtensionFunctionCall function)
        {
            this.name = name;
            this.function = function;
        }

        public override StructuredQName GetFunctionName()
        {
            return name;
        }

        public override IFunctionItem GetTargetFunction(IXPathContext context)
        {
            return null;
        }

        protected override void CheckArguments(ExpressionVisitor visitor)
        {
            ExtensionFunctionDefinition definition = function.Definition;
            CheckArgumentCount(definition.MinimumNumberOfArguments, definition.MaximumNumberOfArguments);
            int args = GetArity();
            SequenceType[] declaredArgumentTypes = definition.ArgumentTypes;
            if (declaredArgumentTypes == null || (args != 0 && declaredArgumentTypes.Length == 0))
            {
                throw new XPathException("Integrated function " + DisplayName + " failed to declare its argument types");
            }

            SequenceType[] actualArgumentTypes = new SequenceType[args];
            TypeChecker tc = visitor.GetConfiguration().GetTypeChecker(false);
            for (int i = 0; i < args; i++)
            {
                int pos = i;
                Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.FUNCTION, GetFunctionName().DisplayName, pos);
                SetArg(i, tc.StaticTypeCheck(GetArg(i), i < declaredArgumentTypes.Length ? declaredArgumentTypes[i] : declaredArgumentTypes[declaredArgumentTypes.Length - 1], role, visitor));
                actualArgumentTypes[i] = SequenceType.MakeSequenceType(GetArg(i).GetItemType(), GetArg(i).GetCardinality());
            }

            resultType = definition.GetResultType(actualArgumentTypes);
            if (state == 0)
            {
                function.SupplyStaticContext(visitor.StaticContext, 0, Arguments);
            }

            state++;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Expression exp = base.TypeCheck(visitor, contextInfo);
            if (exp is IntegratedFunctionCall)
            {
                Expression exp2 = ((IntegratedFunctionCall)exp).function.Rewrite(visitor.StaticContext, Arguments);
                if (exp2 == null)
                {
                    return exp;
                }
                else
                {
                    ExpressionTool.CopyLocationInfo(this, exp2);
                    return exp2.Simplify().TypeCheck(visitor, contextInfo).Optimize(visitor, contextInfo);
                }
            }

            return exp;
        }

        public override Expression PreEvaluate(ExpressionVisitor visitor)
        {
            return this;
        }

        public override ItemType GetItemType()
        {
            if (function.Definition.TrustResultType())
            {
                return resultType.PrimaryType;
            }
            else
            {
                return AnyItemType.GetInstance();
            }
        }

        protected override int ComputeCardinality()
        {
            if (function.Definition.TrustResultType())
            {
                return resultType.GetCardinality();
            }
            else
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
        }

        protected override int ComputeSpecialProperties()
        {
            ExtensionFunctionDefinition definition = function.Definition;
            return definition.HasSideEffects() ? StaticProperty.HAS_SIDE_EFFECTS : StaticProperty.NO_NODES_NEWLY_CREATED;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            IntegratedFunctionCall copy = new IntegratedFunctionCall(GetFunctionName(), function);
            Expression[] args = new Expression[GetArity()];
            for (int i = 0; i < args.Length; i++)
            {
                args[i] = GetArg(i).Copy(rebindings);
            }

            copy.Arguments = args;
            copy.resultType = resultType;
            copy.state = state;
            ExpressionTool.CopyLocationInfo(this, copy);
            return copy;
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("ifCall", this);
            @out.EmitAttribute("name", GetFunctionName());
            @out.EmitAttribute("type", resultType.ToAlphaCode());
            foreach (Operand o in Operands())
            {
                o.GetChildExpression().Export(@out);
            }

            @out.EndElement();
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            ExtensionFunctionDefinition definition = function.Definition;
            ISequence[] argValues = new ISequence[GetArity()];
            for (int i = 0; i < argValues.Length; i++)
            {
                argValues[i] = SequenceTool.ToLazySequence(GetArg(i).Iterate(context));
            }

            Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.FUNCTION_RESULT, GetFunctionName().DisplayName, 0);
            Configuration config = context.GetConfiguration();
            TypeHierarchy th = config.GetTypeHierarchy();
            ISequenceIterator result;
            try
            {
                result = function.Call(context, argValues).Iterate();
            }
            catch (XPathException e)
            {
                throw e.MaybeWithLocation(GetLocation());
            }

            if (!definition.TrustResultType())
            {
                int card = resultType.GetCardinality();
                if (card != StaticProperty.ALLOWS_ZERO_OR_MORE)
                {
                    result = new CardinalityCheckingIterator(result, card, role, GetLocation());
                }

                ItemType type = resultType.PrimaryType;
                if (type != AnyItemType.GetInstance())
                {
                    result = new ItemMappingIterator(result, ItemMapper.Of((item) =>
                    {
                        if (!type.Matches(item, th))
                        {
                            string msg = role().ComposeErrorMessage(type, item, th);
                            throw new XPathException(msg, "XPTY0004").WithLocation(GetLocation());
                        }

                        return item;
                    }), true);
                }

                if (th.Relationship(type, AnyNodeTest.GetInstance()) != Affinity.DISJOINT)
                {
                    result = new ItemMappingIterator(result, new ConfigurationCheckingFunction(context.GetConfiguration()), true);
                }
            }

            return result;
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            ExtensionFunctionDefinition definition = function.Definition;
            ISequence[] argValues = new ISequence[GetArity()];
            for (int i = 0; i < argValues.Length; i++)
            {
                argValues[i] = SequenceTool.ToLazySequence(GetArg(i).Iterate(context));
            }

            RoleDiagnostic role = new RoleDiagnostic(RoleDiagnostic.FUNCTION_RESULT, GetFunctionName().DisplayName, 0);
            Configuration config = context.GetConfiguration();
            TypeHierarchy th = config.GetTypeHierarchy();
            IItem result;
            try
            {
                result = function.Call(context, argValues).Head();
            }
            catch (XPathException e)
            {
                throw e.MaybeWithLocation(GetLocation());
            }

            if (!definition.TrustResultType())
            {
                ItemType type = resultType.PrimaryType;
                if (result == null ? !Cardinality.AllowsZero(resultType.GetCardinality()) : !type.Matches(result, th))
                {
                    string msg = role.ComposeErrorMessage(type, result, th);
                    throw new XPathException(msg, "XPTY0004").WithLocation(GetLocation());
                }

                if (result is NodeInfo && !config.IsCompatible(((NodeInfo)result).GetConfiguration()))
                {
                    throw new XPathException("Node returned by extension function was created with an incompatible Configuration", DAXonErrorCode.SXXP0004);
                }
            }

            return result;
        }

        public override bool EffectiveBooleanValue(IXPathContext context)
        {
            ISequence[] argValues = new ISequence[GetArity()];
            for (int i = 0; i < argValues.Length; i++)
            {
                argValues[i] = SequenceTool.ToLazySequence(GetArg(i).Iterate(context));
            }

            try
            {
                return function.EffectiveBooleanValue(context, argValues);
            }
            catch (XPathException e)
            {
                throw e.MaybeWithLocation(GetLocation());
            }
        }

        public ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return function.Call(context, arguments);
        }

        internal sealed class ConfigurationCheckingFunction : IItemMappingFunction
        {
            private readonly Configuration config;
            public ConfigurationCheckingFunction(Configuration config)
            {
                this.config = config;
            }

            public IItem MapItem(IItem item)
            {
                if (item is NodeInfo && !config.IsCompatible(((NodeInfo)item).GetConfiguration()))
                {
                    throw new XPathException("Node returned by extension function was created with an incompatible Configuration", DAXonErrorCode.SXXP0004);
                }

                return item;
            }
        }
    }
}