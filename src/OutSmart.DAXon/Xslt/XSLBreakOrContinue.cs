////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// Abstract class containing functionality common to xsl:break and xsl:next-iteration
    /// </summary>
    internal abstract class XSLBreakOrContinue : StyleElement
    {
        protected XSLIterate xslIterate;
        public override bool IsInstruction()
        {
            return true;
        }

        public override void PrepareAttributes()
        {
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                CheckUnknownAttribute(attName);
            }
        }

        /// <summary>
        /// Test that this xsl:next-iteration or xsl:break instruction appears in a valid position
        /// </summary>
        protected virtual void ValidatePosition()
        {
            NodeInfo inst = this;
            bool isLast = true;
            while (true)
            {
                if (!(inst is XSLWhen))
                {
                    IAxisIterator sibs = inst.IterateAxis(AxisInfo.FOLLOWING_SIBLING);
                    while (true)
                    {
                        NodeInfo sib = sibs.Next();
                        if (sib == null)
                        {
                            break;
                        }

                        if (sib is XSLFallback || sib is XSLCatch)
                        {
                            continue;
                        }

                        isLast = false;
                    }
                }

                inst = inst.GetParent();
                if (inst is XSLIterate)
                {
                    xslIterate = (XSLIterate)inst;
                    break;
                }
                else if (inst is XSLTry || inst is XSLCatch)
                {
                }
                else if (inst is XSLWhen || inst is XSLOtherwise || inst is XSLIf || inst is XSLChooseOrSwitch)
                {
                }
                else if (inst == null)
                {
                    CompileError(DisplayName + " is not allowed at outermost level", "XTSE3120");
                    return;
                }
                else
                {
                    CompileError(DisplayName + " is not allowed within " + inst.DisplayName, "XTSE3120");
                    return;
                }
            }

            if (!isLast)
            {
                CompileError(DisplayName + " must be the last instruction in the xsl:iterate loop", "XTSE3120");
            }
        }
    }
}