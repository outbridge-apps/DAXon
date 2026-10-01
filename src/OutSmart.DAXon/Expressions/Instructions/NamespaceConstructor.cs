////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// A namespace constructor instruction. (xsl:namespace in XSLT 2.0, or namespace{}{} in XQuery 1.1)
    /// </summary>
    internal sealed class NamespaceConstructor : SimpleNodeConstructor
    {
        private readonly Operand nameOp;

        public Expression NameExp
        {
            get => nameOp.GetChildExpression(); set
            {
                nameOp.SetChildExpression(value);
            }
        }

        public override int InstructionNameCode => StandardNames.XSL_NAMESPACE;
        public NamespaceConstructor(Expression name)
        {
            nameOp = new Operand(this, name, OperandRole.SINGLE_ATOMIC);
        }

        public override IEnumerable<Operand> Operands()
        {
            return OperandList(selectOp, nameOp);
        }

        public override Types.ItemType GetItemType()
        {
            return NodeKindTest.NAMESPACE;
        }

        public override int GetCardinality()
        {
            return StaticProperty.EXACTLY_ONE;
        }

        public override void LocalTypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            IStaticContext env = visitor.StaticContext;
            nameOp.TypeCheck(visitor, contextItemType);
            Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, "namespace/name", 0);

            // See bug 2110. XQuery does not use the function conversion rules here, and disallows xs:anyURI.
            // In XSLT the name is an AVT so we automatically get a string; in XQuery we'll use the standard
            // mechanism to get an atomic value, and then check the type "by hand" at run time.
            NameExp = env.GetConfiguration().GetTypeChecker(false).StaticTypeCheck(NameExp, Values.SequenceType.OPTIONAL_ATOMIC, role, visitor);
            AdoptChildExpression(NameExp);

            // Do early checking of name if known statically
            if (NameExp is Literal)
            {
                EvaluatePrefix(env.MakeEarlyEvaluationContext());
            }
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            NamespaceConstructor exp = new NamespaceConstructor(NameExp.Copy(rebindings));
            exp.Select = Select.Copy(rebindings);
            ExpressionTool.CopyLocationInfo(this, exp);
            return exp;
        }

        public override INodeName EvaluateNodeName(IXPathContext context)
        {
            string prefix = EvaluatePrefix(context);
            return new NoNamespaceName(prefix);
        }

        private string EvaluatePrefix(IXPathContext context)
        {
            AtomicValue value = (AtomicValue)NameExp.EvaluateItem(context);
            if (value == null)
            {
                return "";
            }

            if (!(value is StringValue) || value is AnyURIValue)
            {

                // Can only happen in XQuery
                XPathException err = new XPathException("Namespace prefix is not an xs:string or xs:untypedAtomic", "XPTY0004", GetLocation());
                err.SetIsTypeError(true);
                throw DynamicError(GetLocation(), err, context);
            }

            string prefix = Whitespace.Trim(value.GetStringValue());
            return CheckPrefix(prefix, context);
        }

        public string CheckPrefix(string prefix, IXPathContext context)
        {
            prefix = Whitespace.Trim(prefix);
            if (!((prefix.Length == 0) || NameChecker.IsValidNCName(prefix)))
            {
                string errorCode = IsXSLT() ? "XTDE0920" : "XQDY0074";
                XPathException err = new XPathException("Namespace prefix is invalid: " + prefix, errorCode, GetLocation());
                throw DynamicError(GetLocation(), err, context);
            }

            if (prefix.Equals("xmlns"))
            {
                string errorCode = IsXSLT() ? "XTDE0920" : "XQDY0101";
                XPathException err = new XPathException("Namespace prefix 'xmlns' is not allowed", errorCode, GetLocation());
                throw DynamicError(GetLocation(), err, context);
            }

            return prefix;
        }

        public override void ProcessValue(UnicodeString value, Outputter output, IXPathContext context)
        {
            string prefix = EvaluatePrefix(context);
            string uri = value.ToString();
            CheckPrefixAndUri(prefix, uri, context);
            output.Namespace(prefix, NamespaceUri.Of(uri), ReceiverOption.REJECT_DUPLICATES);
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            NodeInfo node = (NodeInfo)base.EvaluateItem(context);
            string prefix = node.GetLocalPart();
            string uri = node.GetStringValue();
            CheckPrefixAndUri(prefix, uri, context);
            return node;
        }

        private void CheckPrefixAndUri(string prefix, string uri, IXPathContext context)
        {
            if (prefix.Equals("xml") != uri.Equals(NamespaceConstant.XML))
            {
                string errorCode = IsXSLT() ? "XTDE0925" : "XQDY0101";
                XPathException err = new XPathException("Namespace prefix 'xml' and namespace uri " + NamespaceConstant.XML + " must only be used together", errorCode, GetLocation());
                throw DynamicError(GetLocation(), err, context);
            }

            if ((uri.Length == 0))
            {
                string errorCode = IsXSLT() ? "XTDE0930" : "XQDY0101";
                XPathException err = new XPathException("Namespace URI is an empty string", errorCode, GetLocation());
                throw DynamicError(GetLocation(), err, context);
            }

            if (uri.Equals(NamespaceConstant.XMLNS))
            {
                string errorCode = IsXSLT() ? "XTDE0905" : "XQDY0101";
                XPathException err = new XPathException("A namespace node cannot have the reserved namespace " + NamespaceConstant.XMLNS, errorCode, GetLocation());
                throw DynamicError(GetLocation(), err, context);
            }

            if (context.GetConfiguration().XsdVersion == Configuration.XSD10 && !StandardURIChecker.GetInstance().IsValidURI(uri))
            {
                XPathException de = new XPathException("The string value of the constructed namespace node must be a valid URI", "XTDE0905", GetLocation());
                throw DynamicError(GetLocation(), de, context);
            }
        }

        //W3C bug 30180
        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("namespace", this);
            string flags = "";
            if (IsLocal())
            {
                flags += "l";
            }

            if (!(flags.Length == 0))
            {
                @out.EmitAttribute("flags", flags);
            }

            @out.SetChildRole("name");
            NameExp.Export(@out);
            @out.SetChildRole("select");
            Select.Export(@out);
            @out.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new NamespaceConstructorElaborator();
        }

        private sealed class NamespaceConstructorElaborator : SimpleNodePushElaborator
        {
            public override IPushEvaluator ElaborateForPush()
            {
                NamespaceConstructor expr = (NamespaceConstructor)GetExpression();
                ILocation loc = expr.GetLocation();
                string literalPrefix = expr.NameExp is StringLiteral ? ((StringLiteral)expr.NameExp).Stringify() : null;
                NamespaceUri literalUri = expr.Select is StringLiteral ? NamespaceUri.Of(((StringLiteral)expr.Select).Stringify()) : null;
                if (literalPrefix != null && literalUri != null)
                {
                    try
                    {
                        expr.CheckPrefix(literalPrefix, GetConfiguration().ConversionContext);
                        expr.CheckPrefixAndUri(literalPrefix, literalUri.ToString(), GetConfiguration().ConversionContext);
                    }
                    catch (XPathException e)
                    {
                        return (output, context) =>
                        {
                            throw e;
                        };
                    }

                    return (output, context) =>
                    {
                        output.Namespace(literalPrefix, literalUri, ReceiverOption.REJECT_DUPLICATES);
                        return null;
                    };
                }
                else
                {
                    IStringEvaluator nameEval = expr.NameExp.MakeElaborator().ElaborateForString(true);
                    IStringEvaluator contentEval = expr.Select.MakeElaborator().ElaborateForString(true);
                    return (output, context) =>
                    {
                        string prefix = nameEval.Eval(context);
                        string uri = contentEval.Eval(context);
                        expr.CheckPrefix(prefix, context);
                        expr.CheckPrefixAndUri(prefix, uri, context);
                        try
                        {
                            output.Namespace(prefix, NamespaceUri.Of(uri), ReceiverOption.REJECT_DUPLICATES);
                        }
                        catch (XPathException err)
                        {
                            throw Instruction.DynamicError(loc, err, context);
                        }

                        return null;
                    };
                }
            }

            public override IItemEvaluator ElaborateForItem()
            {
                NamespaceConstructor expr = (NamespaceConstructor)GetExpression();
                ILocation loc = expr.GetLocation();
                string literalPrefix = expr.NameExp is StringLiteral ? ((StringLiteral)expr.NameExp).Stringify() : null;
                NamespaceUri literalUri = expr.Select is StringLiteral ? NamespaceUri.Of(((StringLiteral)expr.Select).Stringify()) : null;
                if (literalPrefix != null && literalUri != null)
                {
                    try
                    {
                        expr.CheckPrefix(literalPrefix, GetConfiguration().ConversionContext);
                        expr.CheckPrefixAndUri(literalPrefix, literalUri.ToString(), GetConfiguration().ConversionContext);
                    }
                    catch (XPathException e)
                    {
                        return (context) =>
                        {
                            throw e;
                        };
                    }

                    return (context) =>
                    {
                        Orphan o = new Orphan(context.GetConfiguration());
                        o.SetNodeKind(Types.Type.NAMESPACE);
                        o.SetStringValue(literalUri.ToUnicodeString());
                        if (!(literalPrefix.Length == 0))
                        {
                            o.SetNodeName(new NoNamespaceName(literalPrefix));
                        }

                        return o;
                    };
                }
                else
                {
                    IStringEvaluator nameEval = expr.NameExp.MakeElaborator().ElaborateForString(true);
                    IUnicodeStringEvaluator contentEval = expr.Select.MakeElaborator().ElaborateForUnicodeString(true);
                    return (context) =>
                    {
                        string prefix = nameEval.Eval(context);
                        UnicodeString uri = contentEval.Eval(context);
                        expr.CheckPrefix(prefix, context);
                        expr.CheckPrefixAndUri(prefix, uri.ToString(), context);
                        Orphan o = new Orphan(context.GetConfiguration());
                        o.SetNodeKind(Types.Type.NAMESPACE);
                        o.SetStringValue(uri);
                        if (!(prefix.Length == 0))
                        {
                            o.SetNodeName(new NoNamespaceName(prefix));
                        }

                        return o;
                    };
                }
            }
        }
    }
}