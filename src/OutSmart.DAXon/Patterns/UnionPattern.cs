////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Patterns
{
    /// <summary>
    /// A pattern formed as the union (or) of two other patterns
    /// </summary>
    internal sealed class UnionPattern : VennPattern
    {

        protected override string OperatorName => "union";
        public UnionPattern(Pattern p1, Pattern p2) : base(p1, p2)
        {

            // default is to take the priority from the component patterns
            SetPriority(double.NaN);
        }

        public override ItemType GetItemType()
        {
            ItemType t1 = p1.GetItemType();
            ItemType t2 = p2.GetItemType();
            return Types.Type.GetCommonSuperType(t1, t2);
        }

        public override UType GetUType()
        {
            return p1.GetUType().Union(p2.GetUType());
        }

        public override bool Matches(IItem item, IXPathContext context)
        {
            return p1.Matches(item, context) || p2.Matches(item, context);
        }

        public override bool MatchesBeneathAnchor(NodeInfo node, NodeInfo anchor, IXPathContext context)
        {
            return p1.MatchesBeneathAnchor(node, anchor, context) || p2.MatchesBeneathAnchor(node, anchor, context);
        }

        public override Pattern ConvertToTypedPattern(string val)
        {
            Pattern np1 = p1.ConvertToTypedPattern(val);
            Pattern np2 = p2.ConvertToTypedPattern(val);
            if (p1 == np1 && p2 == np2)
            {
                return this;
            }
            else
            {
                return new UnionPattern(np1, np2);
            }
        }

        public override bool Equals(object other)
        {
            if (other is UnionPattern)
            {
                HashSet<Pattern> s0 = new HashSet<Pattern>();
                GatherComponentPatterns(s0);
                HashSet<Pattern> s1 = new HashSet<Pattern>();
                ((UnionPattern)other).GatherComponentPatterns(s1);
                return s0.Equals(s1);
            }
            else
            {
                return false;
            }
        }

        protected override int ComputeHashCode()
        {
            return 0x3bd723a6 ^ p1.GetHashCode() ^ p2.GetHashCode();
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            UnionPattern n = new UnionPattern((Pattern)p1.Copy(rebindings), (Pattern)p2.Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, n);
            n.OriginalText = OriginalText;
            return n;
        }
    }
}