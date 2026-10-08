////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
namespace OutSmart.DAXon.Expressions.Instructions
{
    public sealed class LocalParam : Instruction, ILocalBinding
    {
        private const int REQUIRED = 4;
        private const int TUNNEL = 8;
        private const int IMPLICITLY_REQUIRED = 16; // a parameter that is required because the fallback
        private Operand conversionOp = null;
        private int properties = 0;
        private Operand selectOp = null;
        private StructuredQName variableQName;
        private SequenceType requiredType;
        private int slotNumber = -999;
        private int referenceCount = 10;

        public Expression SelectExpression
        {
            get => selectOp == null ? null : selectOp.GetChildExpression(); set
            {
                if (value != null)
                {
                    if (selectOp == null)
                    {
                        selectOp = new Operand(this, value, OperandRole.NAVIGATE);
                    }
                    else
                    {
                        selectOp.SetChildExpression(value);
                    }
                }
                else
                {
                    selectOp = null;
                } //evaluator = null;
            }
        }

        public int LocalSlotNumber => slotNumber;

        public int SlotNumber { get => slotNumber; set => slotNumber = value; }

        public Expression Conversion
        {
            get => conversionOp == null ? null : conversionOp.GetChildExpression(); set
            {
                if (value != null)
                {
                    if (conversionOp == null)
                    {
                        conversionOp = new Operand(this, value, OperandRole.SINGLE_ATOMIC);
                    } //conversionEvaluator = Elaborator.makeElaborator(convertor).eagerly();
                }
                else
                {
                    conversionOp = null;
                }
            }
        }

        public override int InstructionNameCode => StandardNames.XSL_PARAM;

        public IntegerValue[] IntegerBoundsForVariable => null;

        public override string ExpressionName => "param";

        private string Flags
        {
            get
            {
                string flags = "";
                if (IsTunnelParam())
                {
                    flags += "t";
                }

                if (IsRequiredParam())
                {
                    flags += "r";
                }

                if (IsImplicitlyRequiredParam())
                {
                    flags += "i";
                }

                return flags;
            }
        }

        public void SetRequiredType(SequenceType required)
        {
            requiredType = required;
        }

        public SequenceType GetRequiredType()
        {
            return requiredType;
        }

        public void SetRequiredParam(bool requiredParam)
        {
            if (requiredParam)
            {
                properties |= REQUIRED;
            }
            else
            {
                properties &= ~REQUIRED;
            }
        }

        public void SetImplicitlyRequiredParam(bool requiredParam)
        {
            if (requiredParam)
            {
                properties |= IMPLICITLY_REQUIRED;
            }
            else
            {
                properties &= ~IMPLICITLY_REQUIRED;
            }
        }

        public void SetTunnel(bool tunnel)
        {
            if (tunnel)
            {
                properties |= TUNNEL;
            }
            else
            {
                properties &= ~TUNNEL;
            }
        }

        public void SetReferenceCount(int refCount)
        {
            referenceCount = refCount;
        }

        public override int GetCardinality()
        {
            return StaticProperty.EMPTY;
        }

        public bool IsAssignable()
        {
            return false;
        }

        public bool IsGlobal()
        {
            return false;
        }

        public bool IsRequiredParam()
        {
            return (properties & REQUIRED) != 0;
        }

        public bool IsImplicitlyRequiredParam()
        {
            return (properties & IMPLICITLY_REQUIRED) != 0;
        }

        public bool IsTunnelParam()
        {
            return (properties & TUNNEL) != 0;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            Expression e2 = base.TypeCheck(visitor, contextItemType);
            if (e2 != this)
            {
                return e2;
            }

            CheckAgainstRequiredType(visitor);
            return this;
        }

        public void ComputeEvaluationMode()
        {
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            LocalParam p2 = new LocalParam();
            ExpressionTool.CopyLocationInfo(this, p2);
            p2.SetLocation(GetLocation());
            if (conversionOp != null)
            {
                p2.Conversion = Conversion.Copy(rebindings);
            }


            //        p2.conversionEvaluator = conversionEvaluator;
            p2.properties = properties;
            if (selectOp != null)
            {
                p2.SelectExpression = SelectExpression.Copy(rebindings);
            }

            p2.variableQName = variableQName;
            p2.requiredType = requiredType;
            p2.slotNumber = slotNumber;
            p2.referenceCount = referenceCount;

            //        p2.evaluator = evaluator;
            return p2;
        }

        public void AddReference(VariableReference @ref, bool isLoopingReference)
        {
        }

        public void CheckAgainstRequiredType(ExpressionVisitor visitor)
        {

            // Note, in some cases we are doing this twice.
            Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.VARIABLE, variableQName.DisplayName, 0);
            SequenceType r = requiredType;
            Expression select = SelectExpression;
            if (r != null && select != null)
            {

                // check that the expression is consistent with the required type
                select = visitor.GetConfiguration().GetTypeChecker(false).StaticTypeCheck(select, requiredType, role, visitor);
            }
        }

        public void SetVariableQName(StructuredQName s)
        {
            variableQName = s;
        }

        public StructuredQName GetVariableQName()
        {
            return variableQName;
        }

        public override IEnumerable<Operand> Operands()
        {
            return OperandSparseList(selectOp, conversionOp);
        }

        public ISequence EvaluateVariable(IXPathContext c)
        {
            return c.EvaluateLocalVariable(slotNumber);
        }

        public bool IsCompatible(LocalParam other)
        {
            return GetVariableQName().Equals(other.GetVariableQName()) && GetRequiredType().Equals(other.GetRequiredType()) && IsTunnelParam() == other.IsTunnelParam();
        }

        public override bool IsLiftable(bool forStreaming)
        {
            return false;
        }

        public override bool HasVariableBinding(IBinding binding)
        {
            return this == binding;
        }

        public override ItemType GetItemType()
        {
            return ErrorType.GetInstance();
        }

        protected override int ComputeCardinality()
        {
            return StaticProperty.ALLOWS_ZERO_OR_MORE;
        }

        protected override int ComputeSpecialProperties()
        {
            return StaticProperty.HAS_SIDE_EFFECTS;
        }

        public override bool MayCreateNewNodes()
        {
            return false;
        }

        public override string ToShortString()
        {
            return "$" + GetVariableQName().DisplayName;
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("param", this);
            @out.EmitAttribute("name", GetVariableQName());
            @out.EmitAttribute("slot", "" + SlotNumber);
            string flags = Flags;
            if (!(flags.Length == 0))
            {
                @out.EmitAttribute("flags", flags);
            }

            if (GetRequiredType() != SequenceType.ANY_SEQUENCE)
            {
                @out.EmitAttribute("as", GetRequiredType().ToAlphaCode());
            }

            if (SelectExpression != null)
            {
                @out.SetChildRole("select");
                SelectExpression.Export(@out);
            }

            Expression conversion = Conversion;
            if (conversion != null)
            {
                @out.SetChildRole("conversion");
                conversion.Export(@out);
            }

            @out.EndElement();
        }

        public void SetIndexedVariable()
        {
        }

        public bool IsIndexedVariable()
        {
            return false;
        }

        public override Elaborator GetElaborator()
        {
            return new LocalParamElaborator();
        }

        /// <summary>
        /// The Elaborator for this kind of expression
        /// </summary>
        internal sealed class LocalParamElaborator : PushElaborator
        {
            public override IPushEvaluator ElaborateForPush()
            {
                LocalParam expr = (LocalParam)GetExpression();
                ISequenceEvaluator selectEvaluator;
                if (expr.SelectExpression == null)
                {
                    selectEvaluator = null;
                }
                else if (expr.referenceCount == FilterExpression.FILTERED)
                {
                    IPullEvaluator pullEval = expr.SelectExpression.MakeElaborator().ElaborateForPull();
                    selectEvaluator = new IndexedVariableEvaluator(pullEval);
                }
                else
                {
                    Expression select = expr.SelectExpression;
                    selectEvaluator = new LearningEvaluator(select, select.MakeElaborator().Lazily(true, false)); //selectEvaluator = select.makeElaborator().lazily(true);
                }

                ISequenceEvaluator conversionEval = (expr.Conversion == null) ? null : expr.Conversion.MakeElaborator().Eagerly();
                return (@out, context) =>
                {
                    int wasSupplied = context.UseLocalParameter(expr.variableQName, expr.slotNumber, expr.IsTunnelParam());
                    switch (wasSupplied)
                    {
                        case ParameterSet.SUPPLIED_AND_CHECKED:

                            // No action needed
                            break;
                        case ParameterSet.SUPPLIED:

                            // if a parameter was supplied by the caller, with no type-checking by the caller,
                            // then we may need to convert it to the type required
                            if (conversionEval != null)
                            {
                                context.SetLocalVariable(expr.slotNumber, conversionEval.Evaluate(context)); // We do an eager evaluation here for safety, because the result of the
                                // type conversion overwrites the slot where the actual supplied parameter
                                // is contained.
                            }

                            break;
                        case ParameterSet.NOT_SUPPLIED:
                            if (expr.IsRequiredParam() || expr.IsImplicitlyRequiredParam())
                            {
                                string name = "$" + expr.GetVariableQName().DisplayName;
                                int suppliedAsTunnel = context.UseLocalParameter(expr.variableQName, expr.slotNumber, !expr.IsTunnelParam());
                                string message = "No value supplied for required parameter " + name;
                                if (expr.IsImplicitlyRequiredParam())
                                {
                                    message += ". A value is required because " + "the default value is not a valid instance of the required type";
                                }

                                if (suppliedAsTunnel != ParameterSet.NOT_SUPPLIED)
                                {
                                    if (expr.IsTunnelParam())
                                    {
                                        message += ". A non-tunnel parameter with this name was supplied, but a tunnel parameter is required";
                                    }
                                    else
                                    {
                                        message += ". A tunnel parameter with this name was supplied, but a non-tunnel parameter is required";
                                    }
                                }

                                throw new XPathException(message).WithXPathContext(context).WithErrorCode("XTDE0700");
                            }

                            if (selectEvaluator == null)
                            {
                                throw new InvalidOperationException("Internal error: No select expression");
                            }
                            else
                            {

                                // There is a select attribute: do a lazy evaluation of the expression,
                                // which will already contain any code to force conversion to the required type.
                                int savedOutputState = context.TemporaryOutputState;
                                context.TemporaryOutputState = StandardNames.XSL_WITH_PARAM;
                                ISequence result = selectEvaluator.Evaluate(context);
                                context.SetLocalVariable(expr.slotNumber, result);
                                context.TemporaryOutputState = savedOutputState;
                                return null;
                            }
                    }

                    return null;
                };
            }
        }
    }
}
