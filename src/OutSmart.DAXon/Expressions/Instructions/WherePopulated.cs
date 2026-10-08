////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// A compiled xsl:where-populated instruction (formerly xsl:conditional-content).
    /// </summary>
    internal sealed class WherePopulated : UnaryExpression, IItemMappingFunction
    {

        public override int ImplementationMethod => ITERATE_METHOD | PROCESS_METHOD;

        public override string ExpressionName => "wherePop";

        public override string StreamerName => "WherePopulated";
        public WherePopulated(Expression @base) : base(@base)
        {
        }

        public override bool IsInstruction()
        {
            return true;
        }

        protected override OperandRole GetOperandRole()
        {
            return new OperandRole(0, OperandUsage.TRANSMISSION);
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            return new WherePopulated(BaseExpression.Copy(rebindings));
        }

        protected override int ComputeCardinality()
        {
            return BaseExpression.GetCardinality() | StaticProperty.ALLOWS_ZERO;
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return new ItemMappingIterator(BaseExpression.Iterate(context), this);
        }

        public override void Process(Outputter output, IXPathContext context)
        {
            DispatchTailCall(MakeElaborator().ElaborateForPush().ProcessLeavingTail(output, context));
        }

        public IItem MapItem(IItem item)
        {
            return IsDeemedEmpty(item) ? null : item;
        }

        public static bool IsDeemedEmpty(IItem item)
        {
            if (item is NodeInfo)
            {
                int kind = ((NodeInfo)item).GetNodeKind();
                switch (kind)
                {
                    case Types.Type.DOCUMENT:
                    case Types.Type.ELEMENT:
                        return !((NodeInfo)item).HasChildNodes();
                    default:
                        return item.UnicodeStringValue.Length() == 0;
                }
            }
            else if (item is StringValue || item is HexBinaryValue || item is Base64BinaryValue)
            {
                return item.UnicodeStringValue.Length() == 0;
            }
            else if (item is MapItem)
            {
                return ((MapItem)item).IsEmpty();
            }
            else if (item is ArrayItem)
            {
                // Each nested array is a level of recursion: the depth is the value's.
                StackGuard.Probe();
                try
                {
                    foreach (IGroundedValue value in ((ArrayItem)item).Members())
                    {
                        foreach (IItem it in value.AsIterable())
                        {
                            if (!IsDeemedEmpty(it))
                            {
                                return false;
                            }
                        }
                    }
                }
                catch (RecursionDepthError e) when (!e.Described)
                {
                    throw e.Describe("Too many nested arrays to tell whether xsl:where-populated content is empty. The array is too deeply nested.", DAXonErrorCode.SXLM0001, null);
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("condCont", this);
            BaseExpression.Export(@out);
            @out.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new WherePopulatedElaborator();
        }

        private sealed class WherePopulatedElaborator : PushElaborator
        {
            public override IPushEvaluator ElaborateForPush()
            {
                WherePopulated expr = (WherePopulated)GetExpression();
                IPushEvaluator basePush = expr.BaseExpression.MakeElaborator().ElaborateForPush();
                return (output, context) =>
                {
                    WherePopulatedOutputter filter = new WherePopulatedOutputter(output);
                    ITailCall tc = basePush.ProcessLeavingTail(filter, context);
                    DispatchTailCall(tc);
                    return null;
                };
            }

            public override IPullEvaluator ElaborateForPull()
            {
                WherePopulated expr = (WherePopulated)GetExpression();
                IPullEvaluator basePull = expr.BaseExpression.MakeElaborator().ElaborateForPull();
                return (context) => new ItemMappingIterator(basePull.Iterate(context), expr);
            }
        }
    }
}
