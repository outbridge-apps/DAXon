////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:output-character element in the stylesheet. <br>
    /// </summary>
    internal sealed class XSLOutputCharacter : StyleElement
    {
        private int codepoint = -1;
        private string replacementString = null;

        public int CodePoint => codepoint;

        public string ReplacementString => replacementString;
        public override void PrepareAttributes()
        {
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                if (f.Equals("character"))
                {
                    switch (value.Length)
                    {
                        case 0:
                            CompileError("character attribute must not be zero-length", "XTSE0020");
                            codepoint = 256; // for error recovery
                            break;
                        case 1:
                            codepoint = value[0];
                            break;
                        case 2:
                            if (UTF16CharacterSet.IsHighSurrogate(value[0]) && UTF16CharacterSet.IsLowSurrogate(value[1]))
                            {
                                codepoint = UTF16CharacterSet.CombinePair(value[0], value[1]);
                            }
                            else
                            {
                                CompileError("character attribute must be a single XML character", "XTSE0020");
                                codepoint = 256; // for error recovery
                            }

                            break;
                        default:
                            CompileError("character attribute must be a single XML character", "XTSE0020");
                            codepoint = 256; // for error recovery
                            break;
                    }
                }
                else if (f.Equals("string"))
                {
                    replacementString = value;
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }

            if (codepoint == -1)
            {
                ReportAbsence("character");
                codepoint = 256; // for error recovery
                return;
            }

            if (replacementString == null)
            {
                ReportAbsence("string");
                replacementString = ""; // for error recovery
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            if (!(GetParent() is XSLCharacterMap))
            {
                CompileError("xsl:output-character may appear only as a child of xsl:character-map", "XTSE0010");
            }
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            return null;
        }
    }
}