////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:context-item element in the stylesheet. <br>
    /// </summary>
    internal class XSLContextItem : StyleElement
    {
        private ItemType requiredType = AnyItemType.GetInstance();
        private bool mayBeOmitted = true;
        private bool absentFocus = false;
        public override void PrepareAttributes()
        {
            string asAtt = null;
            string useAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                switch (f)
                {
                    case "as":
                        asAtt = Whitespace.Trim(value);
                        break;
                    case "use":
                        useAtt = Whitespace.Trim(value);
                        break;
                    default:
                        CheckUnknownAttribute(attName);
                        break;
                }
            }

            if (asAtt != null)
            {
                SequenceType st;
                try
                {
                    st = MakeSequenceType(asAtt);
                }
                catch (XPathException e)
                {
                    st = SequenceType.SINGLE_ITEM;
                    CompileErrorInAttribute(e, "as");
                }

                if (st.GetCardinality() != StaticProperty.EXACTLY_ONE)
                {
                    CompileError("The xsl:context-item/@use attribute must be an item type (no occurrence indicator allowed)", "XTSE0020");
                    return;
                }

                requiredType = st.PrimaryType;
            }

            if (useAtt != null)
            {
                switch (useAtt)
                {
                    case "required":
                        mayBeOmitted = false;
                        break;
                    case "optional":

                        // no action, this is the default
                        break;
                    case "absent":
                        absentFocus = true;
                        break;
                    default:
                        InvalidAttribute("use", "required|optional|absent");
                        break;
                }
            }

            if (asAtt != null && absentFocus)
            {
                CompileError("The 'as' attribute must be omitted when use='absent' is specified", this is XSLGlobalContextItem ? "XTSE3089" : "XTSE3088");
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            if (!(GetParent() is XSLTemplate))
            {
                CompileError("xsl:context-item can appear only as a child of xsl:template");
                return;
            }

            if (mayBeOmitted && ((XSLTemplate)GetParent()).TemplateName == null)
            {
                CompileError("xsl:context-item appearing in an xsl:template declaration with no name attribute must specify use=required", "XTSE0020");
            }

            ((XSLTemplate)GetParent()).SetContextItemRequirements(requiredType, mayBeOmitted, absentFocus);
            SequenceTool.Supply(IterateAxis(AxisInfo.PRECEDING_SIBLING), (prec) =>
            {
                if (((NodeInfo)prec).GetNodeKind() != Types.Type.TEXT || !Whitespace.IsAllWhite(prec.UnicodeStringValue))
                {
                    CompileError("xsl:context-item must be the first child of xsl:template");
                }
            });
        }

        public virtual ItemType GetRequiredContextItemType()
        {
            return requiredType;
        }

        public virtual bool IsMayBeOmitted()
        {
            return mayBeOmitted;
        }

        public virtual bool IsAbsentFocus()
        {
            return absentFocus;
        }
    }
}