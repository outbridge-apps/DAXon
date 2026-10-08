////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Expressions.Instructions
{
    internal sealed class DocumentInstr : ParentNodeConstructor
    {
        private readonly bool textOnly;
        private readonly UnicodeString constantText;
        private Statistics treeStatistics = new Statistics();

        public override int ImplementationMethod => Expression.EVALUATE_METHOD;

        public Expression StringValueExpression
        {
            get
            {
                if (textOnly)
                {
                    if (constantText != null)
                    {
                        return new StringLiteral(StringValue.MakeUntypedAtomic(constantText));
                    }
                    else if (GetContentExpression() is ValueOf)
                    {
                        return ((ValueOf)GetContentExpression()).ConvertToCastAsString();
                    }
                    else
                    {
                        Expression fn = SystemFunction.MakeCall("string-join", GetRetainedStaticContext(), GetContentExpression(), new StringLiteral(StringValue.EMPTY_STRING));
                        CastExpression cast = new CastExpression(fn, BuiltInAtomicType.UNTYPED_ATOMIC, false);
                        ExpressionTool.CopyLocationInfo(this, cast);
                        return cast;
                    }
                }
                else
                {
                    throw new InvalidOperationException("getStringValueExpression() called on non-text-only document instruction");
                }
            }
        }

        public override int InstructionNameCode => StandardNames.XSL_DOCUMENT;

        public override string StreamerName => "DocumentInstr";
        public DocumentInstr(bool textOnly, UnicodeString constantText)
        {
            this.textOnly = textOnly;
            this.constantText = constantText;
        }

        public override IEnumerable<Operand> Operands()
        {
            return contentOp;
        }

        public bool IsTextOnly()
        {
            return textOnly;
        }

        protected override void CheckContentSequence(IStaticContext env)
        {
            CheckContentSequence(env, ContentOperand, ValidationOptions);
        }

        public static void CheckContentSequence(IStaticContext env, Operand content, ParseOptions validationOptions)
        {
            Operand[] components;
            if (content.GetChildExpression() is Block)
            {
                components = ((Block)content.GetChildExpression()).GetOperanda();
            }
            else
            {
                components = new Operand[]
                {
                    content
                };
            }

            int validation = validationOptions == null ? Validation.PRESERVE : validationOptions.GetSchemaValidationMode();
            ISchemaType type = validationOptions == null ? null : validationOptions.TopLevelType;
            int elementCount = 0;
            bool isXSLT = content.GetChildExpression().GetPackageData().IsXSLT();
            foreach (Operand o in components)
            {
                Expression component = o.GetChildExpression();
                Types.ItemType it = component.GetItemType();
                if (it is NodeTest)
                {
                    UType possibleNodeKinds = it.GetUType();
                    if (possibleNodeKinds.Equals(UType.ATTRIBUTE))
                    {
                        XPathException de = new XPathException("Cannot create an attribute node whose parent is a document node");
                        de.SetErrorCode(isXSLT ? "XTDE0420" : "XPTY0004");
                        de.SetLocator(component.GetLocation());
                        throw de;
                    }
                    else if (possibleNodeKinds.Equals(UType.NAMESPACE))
                    {
                        XPathException de = new XPathException("Cannot create a namespace node whose parent is a document node");
                        de.SetErrorCode(isXSLT ? "XTDE0420" : "XQTY0024");
                        de.SetLocator(component.GetLocation());
                        throw de;
                    }
                    else if (possibleNodeKinds.Equals(UType.ELEMENT))
                    {
                        elementCount++;
                        if (elementCount > 1 && (validation == Validation.STRICT || validation == Validation.LAX || type != null))
                        {
                            XPathException de = new XPathException("A valid document must have only one child element");
                            if (isXSLT)
                            {
                                de.SetErrorCode("XTTE1550");
                            }
                            else
                            {
                                de.SetErrorCode("XQDY0061");
                            }

                            de.SetLocator(component.GetLocation());
                            throw de;
                        }

                        if (validation == Validation.STRICT && component is FixedElement)
                        {
                            ISchemaDeclaration decl = env.GetConfiguration().GetElementDeclaration(((FixedElement)component).FixedElementName.Fingerprint);
                            if (decl != null)
                            {
                                ((FixedElement)component).GetContentExpression().CheckPermittedContents(decl.GetType(), true);
                            }
                        }
                    }
                }
            }
        }

        protected override int ComputeSpecialProperties()
        {
            int p = base.ComputeSpecialProperties();
            p |= StaticProperty.SINGLE_DOCUMENT_NODESET;
            if (GetValidationAction() == Validation.SKIP)
            {
                p |= StaticProperty.ALL_NODES_UNTYPED;
            }

            return p;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            DocumentInstr doc = new DocumentInstr(textOnly, constantText);
            ExpressionTool.CopyLocationInfo(this, doc);
            doc.SetContentExpression(GetContentExpression().Copy(rebindings));
            doc.SetValidationAction(GetValidationAction(), GetSchemaType());
            return doc;
        }

        public override Types.ItemType GetItemType()
        {
            return NodeKindTest.DOCUMENT;
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            return (NodeInfo)MakeElaborator().ElaborateForItem().Eval(context);
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("doc", this);
            if (!@out.GetOptions().relocatable)
            {
                @out.EmitAttribute("base", StaticBaseURIString);
            }

            string flags = "";
            if (textOnly)
            {
                flags += "t";
            }

            if (IsLocal())
            {
                flags += "l";
            }

            if (!(flags.Length == 0))
            {
                @out.EmitAttribute("flags", flags);
            }

            if (constantText != null)
            {
                @out.EmitAttribute("text", constantText.ToString());
            }

            if (GetValidationAction() != Validation.SKIP && GetValidationAction() != Validation.BY_TYPE)
            {
                @out.EmitAttribute("validation", Validation.Describe(GetValidationAction()));
            }

            ISchemaType schemaType = GetSchemaType();
            if (schemaType != null)
            {
                @out.EmitAttribute("type", schemaType.GetStructuredQName());
            }

            GetContentExpression().Export(@out);
            @out.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new DocumentInstrElaborator();
        }

        internal sealed class DocumentInstrElaborator : PushElaborator
        {
            public override IPushEvaluator ElaborateForPush()
            {
                DocumentInstr expr = (DocumentInstr)GetExpression();
                IPushEvaluator content = expr.GetContentExpression().MakeElaborator().ElaborateForPush();
                if (!expr.textOnly)
                {
                    if (expr.preservingTypes)
                    {
                        return (output, context) =>
                        {
                            output.SetSystemId(expr.StaticBaseURIString);
                            output.StartDocument(ReceiverOption.NONE);
                            ITailCall tc = content.ProcessLeavingTail(output, context);
                            DispatchTailCall(tc);
                            output.EndDocument();
                            return null;
                        };
                    }
                    else
                    {
                        return (output, context) =>
                        {
                            ParseOptions options = expr.ValidationOptions;
                            context.GetConfiguration().PrepareValidationReporting(context, options);
                            IReceiver validator = context.GetConfiguration().GetDocumentValidator(output, expr.StaticBaseURIString, options, expr.GetLocation());
                            ComplexContentOutputter outputter = new ComplexContentOutputter(validator);
                            outputter.StartDocument(ReceiverOption.NONE);
                            ITailCall tc = content.ProcessLeavingTail(outputter, context);
                            DispatchTailCall(tc);
                            outputter.EndDocument();
                            return null;
                        };
                    }
                }
                else
                {
                    IItemEvaluator evalAsItem = ElaborateForItem();
                    ILocation loc = expr.GetLocation();
                    return (output, context) =>
                    {
                        IItem item = evalAsItem.Eval(context);
                        if (item != null)
                        {
                            output.Append(item, loc, ReceiverOption.ALL_NAMESPACES);
                        }

                        return null;
                    };
                }
            }

            public override IItemEvaluator ElaborateForItem()
            {
                DocumentInstr expr = (DocumentInstr)GetExpression();
                string staticBaseUri = expr.StaticBaseURIString;
                if (expr.textOnly)
                {
                    if (expr.constantText != null)
                    {
                        UnicodeString text = expr.constantText;
                        return (context) => TextFragmentValue.MakeTextFragment(context.GetConfiguration(), text, staticBaseUri);
                    }
                    else
                    {
                        IPullEvaluator contentEval = expr.GetContentExpression().MakeElaborator().ElaborateForPull();
                        return (context) =>
                        {
                            UnicodeBuilder sb = new UnicodeBuilder();
                            ISequenceIterator iter = contentEval.Iterate(context);
                            for (IItem item; (item = iter.Next()) != null;)
                            {
                                sb.Accept(item.UnicodeStringValue);
                            }

                            return TextFragmentValue.MakeTextFragment(context.GetConfiguration(), sb.ToUnicodeString(), staticBaseUri);
                        };
                    }
                }
                else
                {
                    IPushEvaluator contentEval = expr.GetContentExpression().MakeElaborator().ElaborateForPush();
                    HostLanguage hostLanguage = expr.GetPackageData().GetHostLanguage();
                    return (context) =>
                    {
                        try
                        {
                            Controller controller = context.GetController();
                            PipelineConfiguration pipe = controller.MakePipelineConfiguration();
                            pipe.XPathContext = context;
                            Builder builder;
                            builder = controller.MakeBuilder();
                            builder.SetUseEventLocation(false);
                            builder.SetDurability(Durability.TEMPORARY);
                            if (builder is TinyBuilder)
                            {
                                ((TinyBuilder)builder).SetStatistics(expr.treeStatistics);
                            }

                            builder.BaseURI = staticBaseUri;
                            builder.SetTiming(false);
                            pipe.SetHostLanguage(hostLanguage);
                            builder.SetPipelineConfiguration(pipe);
                            ComplexContentOutputter @out = ComplexContentOutputter.MakeComplexContentReceiver(builder, expr.ValidationOptions);
                            @out.Open();
                            @out.StartDocument(ReceiverOption.NONE);
                            ITailCall tc = contentEval.ProcessLeavingTail(@out, context);
                            DispatchTailCall(tc);
                            @out.EndDocument();
                            @out.Close();
                            return builder.CurrentRoot;
                        }
                        catch (XPathException e) when (!(e is XPathException.StackOverflow))
                        {
                            // StackOverflow (SXLM0001) must fly through per-level decorating
                            // rethrows untouched: each catch-funclet rethrow on a 1000+-frame
                            // stack costs net stack, and the whole point of the stack guard is
                            // to survive with a fixed margin. The filter declines without
                            // running a funclet, so the deep unwind stays flat (round AQ).
                            throw e.MaybeWithLocation(expr.GetLocation()).MaybeWithContext(context);
                        }
                    };
                }
            }
        }
    }
}