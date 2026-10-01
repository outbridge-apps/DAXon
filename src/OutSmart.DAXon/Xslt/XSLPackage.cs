////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Transformation.Packages;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;

using OutSmart.DAXon.Api;
namespace OutSmart.DAXon.Xslt
{
    public class XSLPackage : XSLModuleRoot
    {
        private string nameAtt = null;
        private PackageVersion packageVersion = null;
        private bool declaredModes = true;
        private bool prepared = false;

        public virtual string Name
        {
            get
            {
                if (nameAtt == null)
                {
                    PrepareAttributes();
                }

                return nameAtt;
            }
        }

        public virtual VersionedPackageName NameAndVersion => new VersionedPackageName(Name, GetPackageVersion());
        public override void Initialise(INodeName elemName, ISchemaType elementType, IAttributeMap atts, NodeInfo parent, int sequenceNumber)
        {
            base.Initialise(elemName, elementType, atts, parent, sequenceNumber);
            ProcessDefaultCollationAttribute(); // Bug #5636
            declaredModes = GetLocalPart().Equals("package");
        }

        public virtual int GetVersion()
        {
            if (version == -1)
            {
                PrepareAttributes();
            }

            return version;
        }

        public virtual PackageVersion GetPackageVersion()
        {
            if (packageVersion == null)
            {
                PrepareAttributes();
            }

            return packageVersion;
        }

        public override void PrepareAttributes()
        {
            if (prepared)
            {

                // already done
                return;
            }

            prepared = true;
            string inputTypeAnnotationsAtt = null;
            string packageVersionAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string lexicalName = attName.DisplayName;
                string value = att.Value;
                if (lexicalName.Equals("name") && GetLocalPart().Equals("package"))
                {
                    nameAtt = Whitespace.Trim(value);
                }
                else if (lexicalName.Equals("id"))
                {
                }
                else if (lexicalName.Equals("version"))
                {
                    if (version == -1)
                    {
                        ProcessVersionAttribute(NamespaceUri.NULL);
                    }
                }
                else if (lexicalName.Equals("package-version") && GetLocalPart().Equals("package"))
                {
                    packageVersionAtt = Whitespace.Trim(value);
                }
                else if (lexicalName.Equals("declared-modes") && GetLocalPart().Equals("package"))
                {
                    declaredModes = ProcessBooleanAttribute("declared-modes", value);
                }
                else if (lexicalName.Equals("input-type-annotations"))
                {
                    inputTypeAnnotationsAtt = value;
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }

            if (packageVersionAtt == null)
            {
                packageVersion = PackageVersion.ONE;
            }
            else
            {
                try
                {
                    packageVersion = new PackageVersion(packageVersionAtt);
                }
                catch (XPathException ex)
                {
                    CompileErrorInAttribute(ex, "package-version");
                }
            }

            if (version == -1)
            {
                version = 30;
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
                        CompileError("Invalid value for input-type-annotations attribute. " + "Permitted values are (strip, preserve, unspecified)", "XTSE0020");
                        break;
                }
            }
        }

        // no action
        //
        public override bool IsDeclaredModes()
        {
            if (nameAtt == null)
            {
                PrepareAttributes();
            }

            return declaredModes;
        }

        public override void Validate(ComponentDeclaration decl)
        {
            foreach (NodeInfo child in Children())
            {
                int fp = child.Fingerprint;
                if (child.GetNodeKind() == Types.Type.TEXT || (child is StyleElement && ((StyleElement)child).IsDeclaration()) || child is DataElement)
                {
                }
                else if (child is StyleElement)
                {
                    if (GetLocalPart().Equals("package") && (fp == StandardNames.XSL_USE_PACKAGE || fp == StandardNames.XSL_EXPOSE))
                    {
                    }
                    else if (!((StyleElement)child).IsInXsltNamespace() && child.GetNamespaceUri() != "")
                    {
                    }
                    else if (child is AbsentExtensionElement && ((StyleElement)child).ForwardsCompatibleModeIsEnabled())
                    {
                    }
                    else if (((StyleElement)child).IsInXsltNamespace())
                    {
                        if (child is AbsentExtensionElement)
                        {
                        }
                        else
                        {
                            ((StyleElement)child).CompileError("Element " + child.DisplayName + " must not appear directly within " + DisplayName, "XTSE0010");
                        }
                    }
                    else
                    {
                        ((StyleElement)child).CompileError("Element " + child.DisplayName + " must not appear directly within " + DisplayName + " because it is not in a namespace", "XTSE0130");
                    }
                }
            }

            if (declaredModes)
            {
                string defaultMode = GetAttributeValue("default-mode");
                if (defaultMode != null && GetPrincipalStylesheetModule().GetRuleManager().ObtainMode(DefaultMode, false) == null)
                {
                    CompileError("The default mode " + defaultMode + " has not been declared in an xsl:mode declaration", "XTSE3085");
                }
            }
        }
    }
}