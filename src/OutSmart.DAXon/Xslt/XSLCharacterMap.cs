////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:character-map declaration in the stylesheet. <br>
    /// </summary>
    internal sealed class XSLCharacterMap : StyleElement
    {
        string use;
        IList<XSLCharacterMap> characterMapElements = null;
        bool validated = false;
        bool redundant = false;

        public StructuredQName CharacterMapName
        {
            get
            {
                StructuredQName name = GetObjectName();
                if (name == null)
                {
                    return MakeQName(GetAttributeValue(NamespaceUri.NULL, "name"), null, "name");
                }

                return name;
            }
        }
        public override bool IsDeclaration()
        {
            return true;
        }

        public override void PrepareAttributes()
        {
            string name = null;
            use = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string value = att.Value;
                string f = attName.DisplayName;
                if (f.Equals("name"))
                {
                    name = Whitespace.Trim(value);
                }
                else if (f.Equals("use-character-maps"))
                {
                    use = value;
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }

            if (name == null)
            {
                ReportAbsence("name");
                name = "unnamedCharacterMap_" + GetHashCode();
            }

            SetObjectName(MakeQName(name, null, "name"));
        }

        public override void Validate(ComponentDeclaration decl)
        {
            if (validated)
            {
                return;
            }


            // check that this is a top-level declaration
            CheckTopLevel("XTSE0010", false);

            // check that the only children are xsl:output-character elements
            foreach (NodeInfo child in Children())
            {
                if (!(child is XSLOutputCharacter))
                {
                    CompileError("Only xsl:output-character is allowed within xsl:character-map", "XTSE0010");
                }
            }


            // check that there isn't another character-map with the same name and import
            // precedence
            PrincipalStylesheetModule psm = GetPrincipalStylesheetModule();
            ComponentDeclaration other = psm.GetCharacterMap(GetObjectName());
            if (other != null && other.SourceElement != this)
            {
                if (decl.Precedence == other.Precedence)
                {
                    CompileError("There are two character-maps with the same name and import precedence", "XTSE1580");
                }
                else if (decl.Precedence < other.Precedence)
                {
                    redundant = true;
                }
            }


            // validate the use-character-maps attribute
            if (use != null)
            {

                // identify any character maps that this one refers to
                characterMapElements = new List<XSLCharacterMap>(5);
                foreach (string displayname in use.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    try
                    {
                        string[] parts = NameChecker.GetQNameParts(displayname);
                        NamespaceUri uri = GetURIForPrefix(parts[0], false);
                        if (uri == null)
                        {
                            CompileError("Undeclared namespace prefix " + Err.Wrap(parts[0]) + " in character map name", "XTSE0280");
                        }

                        StructuredQName qn = new StructuredQName(parts[0], uri, parts[1]);
                        ComponentDeclaration charMapDecl = psm.GetCharacterMap(qn);
                        if (charMapDecl == null)
                        {
                            CompileError("No character-map named '" + displayname + "' has been defined", "XTSE1590");
                        }
                        else
                        {
                            XSLCharacterMap @ref = (XSLCharacterMap)charMapDecl.SourceElement;
                            characterMapElements.Add(@ref);
                        }
                    }
                    catch (QNameException err)
                    {
                        CompileError("Invalid character-map name. " + err.GetMessage(), "XTSE1590");
                    }
                }


                // check for circularity
                foreach (object characterMapElement in characterMapElements)
                {
                    ((XSLCharacterMap)characterMapElement).CheckCircularity(this);
                }
            }

            validated = true;
        }

        /* error path: see character-map-027 */
        private void CheckCircularity(XSLCharacterMap origin)
        {
            if (this == origin)
            {
                CompileError("The definition of the character map is circular", "XTSE1600");
                characterMapElements = null; // for error recovery
            }
            else
            {
                if (!validated)
                {

                    // if this attribute set isn't validated yet, we don't check it.
                    // The circularity will be detected when the last attribute set in the cycle
                    // gets validated
                    return;
                }

                if (characterMapElements != null)
                {
                    foreach (object characterMapElement in characterMapElements)
                    {
                        ((XSLCharacterMap)characterMapElement).CheckCircularity(origin);
                    }
                }
            }
        }

        /* error path: see character-map-027 */
        public void Assemble(IntHashMap<string> map)
        {
            if (characterMapElements != null)
            {
                foreach (XSLCharacterMap charmap in characterMapElements)
                {
                    charmap.Assemble(map);
                }
            }

            foreach (NodeInfo child in Children())
            {
                XSLOutputCharacter oc = (XSLOutputCharacter)child;
                map.Put(oc.CodePoint, oc.ReplacementString);
            }
        }

        /* error path: see character-map-027 */
        public override void CompileDeclaration(Compilation compilation, ComponentDeclaration decl)
        {
        }
    }
}