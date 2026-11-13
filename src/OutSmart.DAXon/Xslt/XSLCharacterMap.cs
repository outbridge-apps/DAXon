////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:character-map declaration in the stylesheet. <br>
    /// </summary>
    internal sealed class XSLCharacterMap : StyleElement
    {
        string use;
        IList<XSLCharacterMap> characterMapElements;
        bool validated;

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


                CheckCircularity();
            }

            validated = true;
        }

        // Whether this map reaches itself through the maps it uses: one walk with a visited set. A recursion per path
        // overflowed the stack on a chain of 3000 maps and took exponential time on maps that share the ones they use.
        // Walked whole with no cycle found: no later walk need enter it (it cannot reach the map walking, or it would
        // reach itself). Each map walked every map it reached, so a chain of 10 000 took 24 s (XSLAttributeSet's twin).
        private bool acyclic;

        private void CheckCircularity()
        {
            var seen = new HashSet<XSLCharacterMap>();
            bool whole = true;
            var todo = new Stack<XSLCharacterMap>(characterMapElements);
            while (todo.Count > 0)
            {
                OutSmart.DAXon.Core.Controller.CheckActiveTimeout();
                XSLCharacterMap m = todo.Pop();
                if (m == this)
                {
                    CompileError("The definition of the character map is circular", "XTSE1600");
                    characterMapElements = null; // for error recovery
                    return;
                }

                // A map not validated yet is not walked: the last map of a cycle to be validated finds the cycle. What
                // it uses is not known yet, so the walk is not whole.
                if (!m.validated)
                {
                    whole = false;
                    continue;
                }

                if (m.acyclic || m.characterMapElements == null || !seen.Add(m))
                {
                    continue;
                }

                foreach (XSLCharacterMap used in m.characterMapElements)
                {
                    todo.Push(used);
                }
            }

            acyclic = whole;
        }

        // The characters of this map and of the maps it uses, a later mapping winning: the used maps in order, then this
        // map's own. Walked backwards keeping the first value met, which gives what expanding every path gives, and
        // a map met again then adds nothing: one visit per map, no recursion.
        // Every map is assembled, so each is assembled once, from the assembled maps it uses: a map walking all it reached
        // walked the rest of a chain for every map of it (10000 maps: 24 s). In post order and without recursion; the
        // first entry of a codepoint wins, its own (the last of them) before those of the maps used, the last used first.
        private IntHashMap<string> assembled;

        public IntHashMap<string> Assemble()
        {
            var todo = new Stack<XSLCharacterMap>();
            var expanded = new HashSet<XSLCharacterMap>();
            todo.Push(this);
            while (todo.Count > 0)
            {
                OutSmart.DAXon.Core.Controller.CheckActiveTimeout();
                XSLCharacterMap m = todo.Peek();
                if (m.assembled == null && expanded.Add(m) && m.characterMapElements != null)
                {
                    foreach (XSLCharacterMap used in m.characterMapElements)
                    {
                        if (used.assembled == null && !expanded.Contains(used))
                        {
                            todo.Push(used);
                        }
                    }

                    continue;
                }

                todo.Pop();
                m.assembled ??= m.Merged();
            }

            return assembled;
        }

        private IntHashMap<string> Merged()
        {
            var map = new IntHashMap<string>();
            var own = new List<NodeInfo>(Children());
            for (int i = own.Count - 1; i >= 0; i--)
            {
                // Any other child has been reported (XTSE0010) and is left out.
                if (own[i] is XSLOutputCharacter oc && map.Get(oc.CodePoint) == null)
                {
                    map.Put(oc.CodePoint, oc.ReplacementString);
                }
            }

            for (int u = (characterMapElements?.Count ?? 0) - 1; u >= 0; u--)
            {
                IntHashMap<string> used = characterMapElements[u].assembled;   // null only inside a cycle, an error already
                if (used == null)
                {
                    continue;
                }

                for (IIntIterator keys = used.KeyIterator(); keys.MoveNext();)
                {
                    if (map.Get(keys.Current) == null)
                    {
                        map.Put(keys.Current, used.Get(keys.Current));
                    }
                }
            }

            return map;
        }

        /* error path: see character-map-027 */
        public override void CompileDeclaration(Compilation compilation, ComponentDeclaration decl)
        {
        }
    }
}