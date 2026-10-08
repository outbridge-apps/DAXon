////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Types;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Xslt
{
    public class XSLStylesheet : XSLModuleRoot
    {
        public override void Initialise(INodeName elemName, ISchemaType elementType, IAttributeMap atts, NodeInfo parent, int sequenceNumber)
        {
            base.Initialise(elemName, elementType, atts, parent, sequenceNumber);
            ProcessDefaultCollationAttribute();
        }

        public override bool MayContainParam()
        {
            return true;
        }

        /// <summary>
        /// Prepare the attributes on the stylesheet element
        /// </summary>
        public override void PrepareAttributes()
        {
            ProcessDefaultCollationAttribute();
            ProcessDefaultMode();
            string inputTypeAnnotationsAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                switch (f)
                {
                    case "version":

                        // already processed
                        break;
                    case "id":

                        //
                        break;
                    case "extension-element-prefixes":

                        //
                        break;
                    case "exclude-result-prefixes":

                        //
                        break;
                    case "input-type-annotations":
                        inputTypeAnnotationsAtt = value;
                        break;
                    default:
                        CheckUnknownAttribute(attName);
                        break;
                }
            }

            if (version == -1 && (GetParent() == null || GetParent().GetNodeKind() == Types.Type.DOCUMENT))
            {
                ReportAbsence("version");
            }

            if (inputTypeAnnotationsAtt != null)
            {
                switch (inputTypeAnnotationsAtt)
                {
                    case "strip":

                        break;
                    case "preserve":

                        break;
                    case "unspecified":

                        //
                        break;
                    default:
                        InvalidAttribute("input-type-annotations", "strip|preserve|unspecified");
                        break;
                }
            }
        }

        /// <summary>
        /// Prepare the attributes on the stylesheet element
        /// </summary>
        //
        //
        //
        //
        public override void Validate(ComponentDeclaration decl)
        {
            if (validationError != null)
            {
                CompileError(validationError);
            }

            if (GetParent() != null && GetParent().GetNodeKind() != Types.Type.DOCUMENT)
            {
                CompileError(DisplayName + " must be the outermost element", "XTSE0010");
            }

            foreach (NodeInfo curr in Children())
            {
                if (curr.GetNodeKind() == Types.Type.TEXT || (curr is StyleElement && ((StyleElement)curr).IsDeclaration()) || curr is DataElement)
                {
                }
                else if (curr is StyleElement)
                {
                    if (!((StyleElement)curr).IsInXsltNamespace() && !((StyleElement)curr).GetNodeName().HasURI(NamespaceUri.NULL))
                    {
                    }
                    else if (curr is AbsentExtensionElement && ((StyleElement)curr).ForwardsCompatibleModeIsEnabled())
                    {
                    }
                    else if (((StyleElement)curr).IsInXsltNamespace())
                    {
                        ((StyleElement)curr).CompileError("Element " + curr.DisplayName + " must not appear directly within " + DisplayName, "XTSE0010");
                    }
                    else
                    {
                        ((StyleElement)curr).CompileError("Element " + curr.DisplayName + " must not appear directly within " + DisplayName + " because it is not in a namespace", "XTSE0130");
                    }
                }
            }
        }
    }
}