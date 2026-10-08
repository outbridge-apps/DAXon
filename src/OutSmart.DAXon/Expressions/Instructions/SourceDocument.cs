////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Accumulators;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Expressions.Parsing;
namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// Non-streamable implementation of the xsl:source-document instruction
    /// </summary>
    internal sealed class SourceDocument : Instruction
    {
        protected Operand hrefOp;
        protected Operand bodyOp;
        protected ParseOptions parseOptions;
        protected HashSet<Accumulator> accumulators;

        public override string ExpressionName => "xsl:source-document";

        public string ExportTag => "sourceDoc";

        public Expression Href
        {
            get => hrefOp.GetChildExpression(); set
            {
                hrefOp.SetChildExpression(value);
            }
        }

        public Expression Body
        {
            get => bodyOp.GetChildExpression(); set
            {
                bodyOp.SetChildExpression(value);
            }
        }
        public SourceDocument(Expression hrefExp, Expression body, ParseOptions options)
        {
            hrefOp = new Operand(this, hrefExp, OperandRole.SINGLE_ATOMIC);
            bodyOp = new Operand(this, body, new OperandRole(OperandRole.HAS_SPECIAL_FOCUS_RULES, OperandUsage.TRANSMISSION));
            this.parseOptions = options;
            this.accumulators = options.ApplicableAccumulators;
        }

        public override IEnumerable<Operand> Operands()
        {
            return OperandList(hrefOp, bodyOp);
        }

        public override bool AllowExtractingCommonSubexpressions()
        {
            return false;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            hrefOp.TypeCheck(visitor, contextInfo);
            Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, "xsl:stream/href", 0);
            TypeChecker tc = visitor.GetConfiguration().GetTypeChecker(false);
            hrefOp.SetChildExpression(tc.StaticTypeCheck(hrefOp.GetChildExpression(), SequenceType.SINGLE_STRING, role, visitor));
            ContextItemStaticInfo newType = GetConfiguration().MakeContextItemStaticInfo(NodeKindTest.DOCUMENT, false);
            newType.SetContextPostureStriding();
            bodyOp.TypeCheck(visitor, newType);
            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            ContextItemStaticInfo newType = GetConfiguration().MakeContextItemStaticInfo(NodeKindTest.DOCUMENT, false);
            newType.SetContextPostureStriding();
            hrefOp.Optimize(visitor, contextItemType);
            bodyOp.Optimize(visitor, newType);
            return this;
        }

        public override bool MayCreateNewNodes()
        {
            return !Body.HasSpecialProperty(StaticProperty.NO_NODES_NEWLY_CREATED);
        }

        public override int ComputeDependencies()
        {

            // Focus-dependency in the body is not relevant.
            int dependencies = 0;
            dependencies |= Href.Dependencies;
            dependencies |= Body.Dependencies & ~StaticProperty.DEPENDS_ON_FOCUS;
            return dependencies;
        }

        protected override int ComputeSpecialProperties()
        {

            // Not sure of the general rules here but we'll pick up some special cases which are useful for XQuery streaming
            // use cases written using saxon:stream() - where the body is always a call on snapshot()
            Expression body = Body;
            if ((body.GetSpecialProperties() & StaticProperty.ALL_NODES_NEWLY_CREATED) != 0)
            {
                return StaticProperty.ORDERED_NODESET | StaticProperty.PEER_NODESET;
            }

            return base.ComputeSpecialProperties();
        }

        public override void Export(ExpressionPresenter @out)
        {
            ExpressionPresenter.ExportOptions options = @out.GetOptions();
            @out.StartElement(ExportTag, this);
            int validation = parseOptions.GetSchemaValidationMode();
            if (validation != Validation.SKIP && validation != Validation.BY_TYPE)
            {
                @out.EmitAttribute("validation", validation + "");
            }

            ISchemaType schemaType = parseOptions.TopLevelType;
            if (schemaType != null)
            {
                @out.EmitAttribute("schemaType", schemaType.GetStructuredQName());
            }

            ISpaceStrippingRule xsltStripSpace = GetPackageData() is StylesheetPackage ? ((StylesheetPackage)GetPackageData()).SpaceStrippingRule : null;
            string flags = "";
            if (parseOptions.SpaceStrippingRule == xsltStripSpace)
            {
                flags += "s";
            }
            else if (parseOptions.SpaceStrippingRule == AllElementsSpaceStrippingRule.GetInstance())
            {
                flags += "S";
            }

            if (parseOptions.IsLineNumbering())
            {
                flags += "l";
            }

            if (parseOptions.IsExpandAttributeDefaults())
            {
                flags += "a";
            }

            if (parseOptions.DTDValidationMode == Validation.STRICT)
            {
                flags += "d";
            }

            if (parseOptions.IsXIncludeAware())
            {
                flags += "i";
            }

            @out.EmitAttribute("flags", flags);
            if (accumulators != null && accumulators.Count > 0)
            {
                StringBuilder fsb = new StringBuilder(256);
                foreach (Accumulator acc in accumulators)
                {
                    if (fsb.Length != 0)
                    {
                        fsb.Append(' ');
                    }

                    fsb.Append(acc.AccumulatorName.EQName);
                }

                @out.EmitAttribute("accum", fsb.ToString());
            }

            @out.SetChildRole("href");
            Href.Export(@out);
            @out.SetChildRole("body");
            Body.Export(@out);
            @out.EndElement();
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            SourceDocument exp = new SourceDocument(Href.Copy(rebindings), Body.Copy(rebindings), parseOptions);
            exp.SetRetainedStaticContext(GetRetainedStaticContext());
            ExpressionTool.CopyLocationInfo(this, exp);
            return exp;
        }

        public override Elaborator GetElaborator()
        {
            return new SourceDocumentElaborator();
        }

        private sealed class SourceDocumentElaborator : PushElaborator
        {
            public override IPushEvaluator ElaborateForPush()
            {
                SourceDocument expr = (SourceDocument)GetExpression();
                IStringEvaluator hrefEval = expr.Href.MakeElaborator().ElaborateForString(false);
                IPushEvaluator bodyPush = expr.Body.MakeElaborator().ElaborateForPush();
                return (output, context) =>
                {
                    string href = hrefEval.Eval(context);
                    NodeInfo doc = DocumentFn.MakeDoc(href, expr.StaticBaseURIString, expr.GetPackageData(), expr.parseOptions, context, expr.GetLocation(), false);
                    if (doc != null)
                    {
                        Controller controller = context.GetController();
                        if (expr.accumulators != null && controller is XsltController)
                        {
                            ((XsltController)controller).GetAccumulatorManager().SetApplicableAccumulators(doc.GetTreeInfo(), expr.accumulators);
                        }

                        IXPathContext c2 = context.NewMinorContext();
                        c2.SetCurrentIterator(new ManualIterator(doc));
                        return bodyPush.ProcessLeavingTail(output, c2);
                    }
                    else
                    {
                        return null;
                    }
                };
            }
        }
    }
}