////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Expressions.Instructions
{
    public abstract class Instruction : Expression
    {

        public override int ImplementationMethod => Expression.PROCESS_METHOD;

        public virtual int InstructionNameCode => -1;

        public override string ExpressionName
        {
            get
            {
                int code = InstructionNameCode;
                if (code >= 0 & code < 1024)
                {
                    return StandardNames.GetDisplayName(code);
                }
                else
                {
                    return base.ExpressionName;
                }
            }
        }

        public override int NetCost => 20;
        public Instruction()
        {
        }

        public override bool IsInstruction()
        {
            return true;
        }

        public override Types.ItemType GetItemType()
        {
            return Types.Type.ITEM_TYPE;
        }

        protected override int ComputeCardinality()
        {
            return StaticProperty.ALLOWS_ZERO_OR_MORE;
        }

        public abstract override IEnumerable<Operand> Operands();
        public override void Process(Outputter output, IXPathContext context)
        {
            try
            {
                ITailCall tc = MakeElaborator().ElaborateForPush().ProcessLeavingTail(output, context);
                DispatchTailCall(tc);
            }
            catch (XPathException err) when (!(err is XPathException.StackOverflow))
            {
                // Filtered: this sits under EVERY instruction, so on a recursive template it is
                // one catch-and-rethrow per level - the most expensive shape there is for a
                // stack-guard abort, which must reach the host instead of being re-decorated.
                throw err.MaybeWithFailingExpression(this).MaybeWithContext(context);
            }
        }

        public virtual ILocation GetSourceLocator()
        {
            return GetLocation();
        }

        protected static XPathException DynamicError(ILocation loc, XPathException error, IXPathContext context)
        {
            if (error is TerminationException)
            {
                return error;
            }

            return error.MaybeWithLocation(loc).MaybeWithContext(context);
        }

        public static ParameterSet AssembleParams(IXPathContext context, WithParam[] actualParams)
        {
            if (actualParams == null || actualParams.Length == 0)
            {
                return null;
            }

            ParameterSet @params = new ParameterSet(actualParams.Length);
            foreach (WithParam actualParam in actualParams)
            {
                @params.Put(actualParam.VariableQName, actualParam.GetSelectValue(context), actualParam.IsTypeChecked());
            }

            return @params;
        }

        public static ParameterSet AssembleTunnelParams(IXPathContext context, WithParam[] actualParams)
        {
            ParameterSet existingParams = context.GetTunnelParameters();
            if (existingParams == null)
            {
                return AssembleParams(context, actualParams);
            }

            if (actualParams == null || actualParams.Length == 0)
            {
                return existingParams;
            }

            ParameterSet newParams = new ParameterSet(existingParams, actualParams.Length);
            foreach (WithParam actualParam in actualParams)
            {
                newParams.Put(actualParam.VariableQName, actualParam.GetSelectValue(context), false);
            }

            return newParams;
        }

        protected override int ComputeSpecialProperties()
        {
            int p = base.ComputeSpecialProperties();
            if (AlwaysCreatesNewNodes())
            {
                p |= StaticProperty.ALL_NODES_NEWLY_CREATED;
            }

            if (MayCreateNewNodes())
            {
                return p;
            }
            else
            {
                return p | StaticProperty.NO_NODES_NEWLY_CREATED;
            }
        }

        public virtual bool MayCreateNewNodes()
        {
            return false;
        }

        public virtual bool AlwaysCreatesNewNodes()
        {
            return false;
        }

        protected bool SomeOperandCreatesNewNodes()
        {
            foreach (Operand o in Operands())
            {
                Expression child = o.GetChildExpression();
                int props = child.GetSpecialProperties();
                if ((props & StaticProperty.NO_NODES_NEWLY_CREATED) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            return MakeElaborator().ElaborateForItem().Eval(context); //        int m = getImplementationMethod();
            //        if ((m & EVALUATE_METHOD) != 0) {
            //            throw new AssertionError(
            //                    "evaluateItem() is not implemented in the subclass " + getClass());
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            Elaborator elaborator = MakeElaborator();
            if (StackProbingElaborator.Unwrap(elaborator) is FallbackElaborator)
            {
                if ((ImplementationMethod & PROCESS_METHOD) != 0)
                {
                    return ExpressionTool.GetIteratorFromProcessMethod(this, context);
                }

                throw new InvalidOperationException("No iterate() method available for expression " + ToShortString());
            }

            return elaborator.ElaborateForPull().Iterate(context); //        int m = getImplementationMethod();
            //                return EmptyIterator.emptyIterator();
        }

        public override UnicodeString EvaluateAsString(IXPathContext context)
        {
            IItem item = EvaluateItem(context);
            if (item == null)
            {
                return EmptyUnicodeString.GetInstance();
            }
            else
            {
                return item.UnicodeStringValue;
            }
        }

        public virtual bool IsXSLT()
        {
            return GetPackageData().IsXSLT();
        }
    }
}