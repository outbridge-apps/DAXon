////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Patterns;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// Class representing xsl:stylesheet, xsl:transform, or xsl:package
    /// </summary>
    public abstract class XSLModuleRoot : StyleElement
    {
        public const int ANNOTATION_UNSPECIFIED = 0;
        public const int ANNOTATION_STRIP = 1;
        public const int ANNOTATION_PRESERVE = 2;

        public virtual int InputTypeAnnotationsAttribute
        {
            get
            {
                string inputTypeAnnotationsAtt = GetAttributeValue(NamespaceUri.NULL, "input-type-annotations");
                if (inputTypeAnnotationsAtt != null)
                {
                    switch (inputTypeAnnotationsAtt)
                    {
                        case "strip":
                            return ANNOTATION_STRIP;
                        case "preserve":
                            return ANNOTATION_PRESERVE;
                        case "unspecified":
                            return ANNOTATION_UNSPECIFIED;
                        default:
                            CompileError("Invalid value for input-type-annotations attribute. " + "Permitted values are (strip, preserve, unspecified)", "XTSE0020");
                            return ANNOTATION_UNSPECIFIED;
                    }
                }

                return -1;
            }
        }
        public virtual bool IsDeclaredModes()
        {
            return false;
        }

        public override void ProcessAllAttributes()
        {
            PrepareAttributes();
            foreach (NodeInfo node in Children(new TypeIsInstancePredicate(typeof(StyleElement))))
            {
                try
                {
                    ((StyleElement)node).ProcessAllAttributes();
                }
                catch (XPathException err)
                {
                    ((StyleElement)node).CompileError(err);
                }
            }
        }

        public override void Index(ComponentDeclaration decl, PrincipalStylesheetModule top)
        {
            CompileError(DisplayName + " can appear only as the outermost element", "XTSE0010");
        }
    }
}