////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Serialization;
namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// An xsl:message or xsl:assert element in the stylesheet.
    /// </summary>
    internal sealed class MessageInstr : Instruction
    {
        private readonly Operand selectOp;
        private readonly Operand terminateOp;
        private readonly Operand errorCodeOp;
        private bool isAssert;

        public Expression Select
        {
            get => selectOp.GetChildExpression(); set
            {
                selectOp.SetChildExpression(value);
            }
        }

        public Expression Terminate
        {
            get => terminateOp.GetChildExpression(); set
            {
                terminateOp.SetChildExpression(value);
            }
        }

        public Expression ErrorCode
        {
            get => errorCodeOp.GetChildExpression(); set
            {
                errorCodeOp.SetChildExpression(value);
            }
        }

        public override int InstructionNameCode => isAssert ? StandardNames.XSL_ASSERT : StandardNames.XSL_MESSAGE;
        public MessageInstr(Expression select, Expression terminate, Expression errorCode)
        {
            if (errorCode == null)
            {
                errorCode = new StringLiteral(BMPString.Of("Q{" + NamespaceConstant.ERR + "}XTMM9000"));
            }

            selectOp = new Operand(this, select, OperandRole.SINGLE_ATOMIC);
            terminateOp = new Operand(this, terminate, OperandRole.SINGLE_ATOMIC);
            errorCodeOp = new Operand(this, errorCode, OperandRole.SINGLE_ATOMIC);
        }

        public override IEnumerable<Operand> Operands()
        {
            return OperandList(selectOp, terminateOp, errorCodeOp);
        }

        public void SetIsAssert(bool isAssert)
        {
            this.isAssert = isAssert;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            MessageInstr exp = new MessageInstr(Select.Copy(rebindings), Terminate.Copy(rebindings), ErrorCode.Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, exp);
            return exp;
        }

        public override Types.ItemType GetItemType()
        {
            return AnyItemType.GetInstance();
        }

        public override int GetCardinality()
        {
            return StaticProperty.ALLOWS_ZERO_OR_ONE;
        }

        public override bool MayCreateNewNodes()
        {
            return true;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            return base.Optimize(visitor, contextInfo);
        }

        private Message MakeMessage(bool abort, StructuredQName errorCode, NodeInfo content)
        {
            return new Message((XdmNode)XdmNode.Wrap(content), new QName(errorCode), abort, GetLocation());
        }

        private static IResultTarget StandardErrorResult()
        {
            return new StreamResult(Console.Error);
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("message", this);
            @out.SetChildRole("select");
            Select.Export(@out);
            @out.SetChildRole("terminate");
            Terminate.Export(@out);
            @out.SetChildRole("error");
            ErrorCode.Export(@out);
            @out.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new MessageInstrElaborator();
        }

        private sealed class MessageAdapter : ProxyOutputter
        {
            public MessageAdapter(Outputter next) : base(next)
            {
            }

            public override void Attribute(INodeName attName, ISimpleType typeCode, string value, ILocation location, int properties)
            {
                try
                {
                    base.Attribute(attName, typeCode, value, location, properties);
                }
                catch (XPathException e)
                {
                    Characters(StringView.Of(value), location, properties); //processingInstruction("attribute", StringView.of("name=\"" + attName.getDisplayName() + "\" value=\"" + value + "\""), location, ReceiverOption.NONE);
                }
            }

            public override void Namespace(string prefix, NamespaceUri namespaceUri, int properties)
            {
                try
                {
                    base.Namespace(prefix, namespaceUri, properties);
                }
                catch (XPathException e)
                {
                    Characters(namespaceUri.ToUnicodeString(), Loc.NONE, properties); //processingInstruction("namespace", StringView.of("prefix=\"" + prefix + "\" uri=\"" + namespaceUri + "\""), Loc.NONE, ReceiverOption.NONE);
                }
            }

            public override void Append(IItem item, ILocation locationId, int copyNamespaces)
            {
                if (item is NodeInfo)
                {
                    int kind = ((NodeInfo)item).GetNodeKind();
                    if (kind == Types.Type.ATTRIBUTE || kind == Types.Type.NAMESPACE)
                    {
                        Characters(item.UnicodeStringValue, locationId, ReceiverOption.NONE);
                        return;
                    }
                }
                else if (item is IFunctionItem && !((IFunctionItem)item).IsArray())
                {
                    string representation = ((IFunctionItem)item).IsMap() ? Err.Depict(item) : "Function " + Err.Depict(item);
                    Characters(StringView.Of(representation), locationId, ReceiverOption.NONE);
                    return;
                }

                NextOutputter.Append(item, locationId, copyNamespaces);
            }
        }

        private sealed class MessageInstrElaborator : PushElaborator
        {
            public override IPushEvaluator ElaborateForPush()
            {
                MessageInstr expr = (MessageInstr)GetExpression();
                IPushEvaluator select = expr.Select.MakeElaborator().ElaborateForPush();
                IStringEvaluator terminate = expr.Terminate.MakeElaborator().ElaborateForString(true);
                IStringEvaluator errorCodeEval = expr.ErrorCode.MakeElaborator().ElaborateForString(true);
                return (@out, context) =>
                {
                    if (!(context.GetController() is XsltController))
                    {

                        // fallback code allowing xsl:message to do something useful when called from
                        // the XSLT function library within free-standing XPath or XQuery, typically when debugging
                        ISequenceIterator iter = expr.Select.Iterate(context);
                        QueryResult.SerializeSequence(iter, context.GetConfiguration(), StandardErrorResult(), new Properties());
                        return null;
                    }

                    XsltController controller = (XsltController)context.GetController();
                    if (expr.isAssert && !controller.IsAssertionsEnabled())
                    {
                        return null;
                    }

                    bool abort = false;
                    string term = Whitespace.Trim(terminate.Eval(context));
                    switch (term)
                    {
                        case "no":
                        case "false":
                        case "0":

                            // no action
                            break;
                        case "yes":
                        case "true":
                        case "1":
                            abort = true;
                            break;
                        default:
                            throw new XPathException("The terminate attribute of xsl:message must be yes|true|1 or no|false|0").WithXPathContext(context).WithErrorCode("XTDE0030");
                    }

                    string code;
                    try
                    {
                        code = errorCodeEval.Eval(context);
                    }
                    catch (XPathException err)
                    {

                        // use the error code of the failure in place of the intended error code
                        code = err.ErrorCodeQName.EQName;
                    }

                    StructuredQName errorCode;
                    try
                    {
                        errorCode = StructuredQName.FromLexicalQName(code, false, true, expr.GetRetainedStaticContext());
                    }
                    catch (XPathException err)
                    {

                        // The spec says we fall back to XTMM9000
                        errorCode = new StructuredQName("err", NamespaceUri.ERR, "XTMM9000");
                    }

                    controller.IncrementMessageCounter(errorCode);
                    Builder builder = controller.MakeBuilder();
                    builder.SetDurability(Durability.TEMPORARY);
                    builder.SetTiming(false);
                    ComplexContentOutputter cco = new ComplexContentOutputter(builder);
                    Outputter rec = new MessageAdapter(cco);
                    rec.Open();
                    rec.StartDocument(abort ? ReceiverOption.TERMINATE : ReceiverOption.NONE);
                    try
                    {
                        try
                        {
                            ITailCall tc = select.ProcessLeavingTail(rec, context);
                            DispatchTailCall(tc);
                        }
                        catch (UncheckedXPathException e)
                        {
                            throw e.GetXPathException();
                        }
                    }
                    catch (XPathException e)
                    {
                        rec.Append(new StringValue("Error " + e.ShowErrorCode() + " while evaluating xsl:message at line " + expr.GetLocation().GetLineNumber() + " of " + expr.GetLocation().GetSystemId() + ": " + e.Message));
                    }

                    rec.EndDocument();
                    rec.Close();
                    builder.Close();
                    NodeInfo content = builder.CurrentRoot;
                    Message message = expr.MakeMessage(abort, errorCode, content);
                    try
                    {
                        controller.MessageHandler.Invoke(message);
                    }
                    catch (Exception e)
                    {
                    }

                    if (abort)
                    {
                        TerminationException te = new TerminationException("Processing terminated by " + StandardDiagnostics.GetInstructionNameDefault(expr) + " at line " + expr.GetLocation().GetLineNumber() + " in " + StandardDiagnostics.AbbreviateLocationURIDefault(expr.GetLocation().GetSystemId()));
                        te.SetLocation(expr.GetLocation());
                        te.ErrorCodeQName = errorCode;
                        te.ErrorObject = content;
                        throw te;
                    }

                    return null;
                };
            }
        }
    }
}
