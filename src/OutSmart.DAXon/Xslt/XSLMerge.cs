////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using static OutSmart.DAXon.Expressions.Sorting.MergeInstr;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// Handler for xsl:merge elements in stylesheet. <br>
    /// </summary>
    internal sealed class XSLMerge : StyleElement
    {
        private int numberOfMergeSources = 0;
        public override bool IsInstruction()
        {
            return true;
        }

        protected override bool MayContainSequenceConstructor()
        {
            return false;
        }

        public override void PrepareAttributes()
        {
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                CheckUnknownAttribute(attName);
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            int childMask = 0;
            HashSet<string> mergeSourceNames = new HashSet<string>();
            foreach (NodeInfo child in Children())
            {
                if (child is XSLMergeSource)
                {
                    string name = ((XSLMergeSource)child).SourceName;
                    if (mergeSourceNames.Contains(name))
                    {
                        CompileError("Duplicate xsl:merge-source/@name", "XTSE3190");
                    }

                    mergeSourceNames.Add(name);
                    childMask = childMask | 1;
                    numberOfMergeSources++;
                }
                else if (child is XSLMergeAction)
                {
                    if ((childMask & 2) == 2)
                    {
                        CompileError("xsl:merge must have only one xsl:merge-action child element", "XTSE0010");
                    }

                    childMask = childMask | 2;
                }
                else if (child.GetNodeKind() == Types.Type.TEXT)
                {

                    // with xml:space=preserve, white space nodes may still be there
                    if (!Whitespace.IsAllWhite(child.UnicodeStringValue))
                    {
                        CompileError("No character data is allowed within xsl:merge", "XXXX");
                    }
                }
                else if (child is XSLFallback)
                {
                    if ((childMask & 2) == 0)
                    {
                        CompileError("xsl:fallback child of xsl:merge can appear only after xsl:merge-action", "XTSE0010");
                    }
                }
                else
                {
                    CompileError("Child element " + Err.Wrap(child.DisplayName, Err.ELEMENT) + " is not allowed as a child of xsl:merge", "XTSE0010");
                }
            }

            if (childMask == 1)
            {
                CompileError("xsl:merge element requires an xsl:merge-action", "XTSE0010");
            }
            else if (childMask == 2)
            {
                CompileError("xsl:merge element requires at least one xsl:merge-source child element", "XTSE0010");
            }
        }

        private void CheckCompatibleMergeKeys(MergeSource[] sources)
        {
            for (int i = 0; i < sources[0].mergeKeyDefinitions.Count; i++)
            {
                if (!sources[0].mergeKeyDefinitions.GetSortKeyDefinition(i).IsFixed())
                {
                    break;
                }

                for (int z = 1; z < sources.Length; z++)
                {
                    if (!sources[z].mergeKeyDefinitions.GetSortKeyDefinition(i).IsFixed())
                    {
                        break;
                    }


                    // Both definitions are fixed: compare them now
                    if (!CompareSortKeyDefinitions(sources[z].mergeKeyDefinitions.GetSortKeyDefinition(i), sources[0].mergeKeyDefinitions.GetSortKeyDefinition(i)))
                    {
                        CompileError("The " + RoleDiagnostic.Ordinal(i + 1) + " merge key definition of the " + RoleDiagnostic.Ordinal(z + 1) + " merge source is incompatible with the " + RoleDiagnostic.Ordinal(i + 1) + " merge key definition of the first merge source", "XTDE2210");
                    }
                }
            }
        }

        private bool CompareSortKeyDefinitions(SortKeyDefinition sd1, SortKeyDefinition sd2)
        {
            return SameFixedExpression(sd1.Language, sd2.Language) && SameFixedExpression(sd1.Order, sd2.Order) && SameFixedExpression(sd1.CollationNameExpression, sd2.CollationNameExpression) && SameFixedExpression(sd1.CaseOrder, sd2.CaseOrder) && SameFixedExpression(sd1.DataTypeExpression, sd2.DataTypeExpression);
        }

        private bool SameFixedExpression(Expression e1, Expression e2)
        {
            return (e1 == null && e2 == null) || (e1 != null && e1.Equals(e2));
        }

        public override Expression Compile(Compilation compilation, ComponentDeclaration decl)
        {
            MergeInstr merge = new MergeInstr();
            merge.SetLocation(SaveLocation());
            merge.SetRetainedStaticContext(MakeRetainedStaticContext());
            int entries = numberOfMergeSources;
            MergeSource[] sources = new MergeSource[entries];
            Expression action = Literal.MakeEmptySequence();
            int w = 0;
            int sortKeyDefLen = 0;
            foreach (NodeInfo node in Children())
            {
                if (node is XSLMergeSource)
                {
                    XSLMergeSource source = (XSLMergeSource)node;
                    SortKeyDefinitionList sortKeyDefs = source.MakeSortKeys(compilation, decl);
                    if (sortKeyDefLen == 0)
                    {
                        sortKeyDefLen = sortKeyDefs.Count;
                    }
                    else if (sortKeyDefLen != sortKeyDefs.Count)
                    {
                        CompileError("Each xsl:merge-source must have the same number of xsl:merge-key children", "XTSE2200");
                    }

                    Expression select = source.Select;
                    if (source.IsSortBeforeMerge())
                    {
                        select = new SortExpression(select, (SortKeyDefinitionList)(sortKeyDefs.Copy(new RebindingMap())));
                    }

                    MergeSource ms = source.MakeMergeSource(merge, select);
                    ms.mergeKeyDefinitions = sortKeyDefs;

                    sources[w++] = ms;
                }
                else if (node is XSLMergeAction)
                {
                    action = ((XSLMergeAction)node).CompileSequenceConstructor(compilation, decl, true);
                    if (action == null)
                    {
                        action = Literal.MakeEmptySequence();
                    }

                    try
                    {
                        action = action.Simplify();
                    }
                    catch (XPathException e)
                    {
                        CompileError(e);
                    }
                }
                else
                {
                }
            }

            CheckCompatibleMergeKeys(sources);
            merge.Init(sources, action);
            return merge;
        }
    }
}