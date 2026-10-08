////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Values;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Xslt
{
    internal sealed class XSLImportSchema : StyleElement
    {
        public override bool IsDeclaration()
        {
            return true;
        }

        public override void PrepareAttributes()
        {
            string @namespace = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string value = att.Value;
                string f = attName.DisplayName;
                if (f.Equals("schema-location"))
                {
                }
                else if (f.Equals("namespace"))
                {
                    @namespace = Whitespace.Trim(value);
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }

            if (@namespace == "")
            {
                CompileError("The zero-length string is not a valid namespace URI. " + "For a schema with no @namespace, omit the namespace attribute");
            }
        }

        //
        public override void Validate(ComponentDeclaration decl)
        {
            CheckTopLevel("XTSE0010", false);
        }

        //
        public override void Index(ComponentDeclaration decl, PrincipalStylesheetModule top)
        {
        }

        //
        //
        public override void CompileDeclaration(Compilation compilation, ComponentDeclaration decl)
        {
        }
    }
}