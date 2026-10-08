////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;

using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using System;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:next-iteration element in the stylesheet
    /// </summary>
    internal sealed class XSLNextIteration : XSLBreakOrContinue
    {
        public override void Validate(ComponentDeclaration decl)
        {
            ValidatePosition();
            if (xslIterate == null)
            {
                CompileError("xsl:next-iteration must be a descendant of an xsl:iterate instruction");
            }

            foreach (NodeInfo child in Children())
            {
                if (child is XSLWithParam)
                {
                    if (((XSLWithParam)child).IsTunnelParam())
                    {
                        CompileError("An xsl:with-param element within xsl:iterate must not specify tunnel='yes'", "XTSE0020");
                    }
                }
                else if (child.GetNodeKind() == Types.Type.TEXT)
                {

                    // with xml:space=preserve, white space nodes may still be there
                    if (!Whitespace.IsAllWhite(child.UnicodeStringValue))
                    {
                        CompileError("No character data is allowed within xsl:next-iteration", "XTSE0010");
                    }
                }
                else
                {
                    CompileError("Child element " + Err.Wrap(child.DisplayName, Err.ELEMENT) + " is not allowed as a child of xsl:next-iteration", "XTSE0010");
                }
            }
        }

        public override void PostValidate()
        {
            if (xslIterate == null)
            {
                return; // previous error already reported
            }


            // check that every supplied parameter is declared in the saxon:iterate instruction
            foreach (NodeInfo w in Children(new TypeIsInstancePredicate(typeof(XSLWithParam))))
            {
                XSLWithParam withParam = (XSLWithParam)w;
                IAxisIterator formalParams = xslIterate.IterateAxis(AxisInfo.CHILD);
                bool ok = false;
                NodeInfo param;
                while ((param = formalParams.Next()) != null)
                {
                    if (param is XSLLocalParam && ((XSLLocalParam)param).GetVariableQName().Equals(withParam.GetVariableQName()))
                    {
                        ok = true;
                        SequenceType required = ((XSLLocalParam)param).GetRequiredType();
                        withParam.CheckAgainstRequiredType(required);
                        break;
                    }
                }

                if (!ok)
                {
                    CompileError("Parameter " + withParam.GetVariableQName().DisplayName + " is not declared in the containing xsl:iterate instruction", "XTSE3130");
                }
            }
        }

        public SequenceType GetDeclaredParamType(StructuredQName name)
        {
            foreach (NodeInfo param in xslIterate.Children(NodeSelector.Of(new TypeIsInstancePredicate(typeof(XSLLocalParam)))))
            {
                if (((XSLLocalParam)param).GetVariableQName().Equals(name))
                {
                    return ((XSLLocalParam)param).GetRequiredType();
                }
            }

            return null;
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            NextIteration call = new NextIteration();
            call.SetRetainedStaticContext(MakeRetainedStaticContext());
            WithParam[] actualParams = GetWithParamInstructions(call, exec, decl, false);
            call.Parameters = actualParams;
            call.SetLocation(SaveLocation());

            // For all declared parameters of the xsl:iterate instruction that are not present in the
            // actual parameters of the xsl:next-iteration, add an implicit <xsl:with-param name="p" select="$p"/>
            if (xslIterate != null)
            {
                IAxisIterator declaredParams = xslIterate.IterateAxis(AxisInfo.CHILD);
                NodeInfo param;
                while ((param = declaredParams.Next()) != null)
                {
                    if (param is XSLLocalParam)
                    {
                        XSLLocalParam pdecl = (XSLLocalParam)param;
                        StructuredQName paramName = pdecl.GetVariableQName();
                        LocalParam lp = pdecl.CompiledParam;
                        bool found = false;
                        foreach (WithParam actualParam in actualParams)
                        {
                            if (paramName.Equals(actualParam.VariableQName))
                            {
                                actualParam.SlotNumber = lp.SlotNumber;
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                        {
                            WithParam wp = new WithParam();
                            wp.VariableQName = paramName;
                            VariableReference @ref = new LocalVariableReference(lp);
                            wp.SetSelectExpression(call, @ref);

                            wp.SlotNumber = lp.SlotNumber;
                            @ref.SetStaticType(pdecl.GetRequiredType(), null, 0);
                            WithParam[] p2 = new WithParam[actualParams.Length + 1];
                            p2[0] = wp;
                            Array.Copy(actualParams, 0, p2, 1, actualParams.Length);
                            actualParams = p2;
                        }
                    }
                }
            }

            return call;
        }
    }
}