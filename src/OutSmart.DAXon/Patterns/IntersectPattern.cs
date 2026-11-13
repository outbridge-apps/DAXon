////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using System.Collections.Generic;

namespace OutSmart.DAXon.Patterns
{
    // Faithful port of net.sf.saxon.pattern.IntersectPattern (Saxon 12.9). Was a hollow stub whose implicit
    // conversion to Pattern returned NULL (not even a throw) — same silent-null family as ExceptPattern.
    // A pattern formed as the intersection of two other patterns.
    internal sealed class IntersectPattern : VennPattern
    {

        /// <summary>
        /// The default priority of an "intersect" pattern is the priority of the LH operand
        /// </summary>
        public override double DefaultPriority => p1.DefaultPriority;

        protected override string OperatorName => "intersect";
        public IntersectPattern(Pattern p1, Pattern p2) : base(p1, p2)
        {
        }

        // Left-deep chains recurse per operand: see UnionPattern.
        public override ItemType GetItemType()
        {
            OutSmart.DAXon.Internal.StackGuard.Probe();
            return p1.GetItemType();
        }

        public override UType GetUType()
        {
            OutSmart.DAXon.Internal.StackGuard.Probe();
            return p1.GetUType().Intersection(p2.GetUType());
        }

        public override bool Matches(IItem item, IXPathContext context)
        {
            OutSmart.DAXon.Internal.StackGuard.Probe();
            return p1.Matches(item, context) && p2.Matches(item, context);
        }

        public override bool MatchesBeneathAnchor(NodeInfo node, NodeInfo anchor, IXPathContext context)
        {
            OutSmart.DAXon.Internal.StackGuard.Probe();
            return p1.MatchesBeneathAnchor(node, anchor, context) && p2.MatchesBeneathAnchor(node, anchor, context);
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
                return new IntersectPattern(np1, np2);
            }
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            IntersectPattern n = new IntersectPattern((Pattern)p1.Copy(rebindings), (Pattern)p2.Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, n);
            n.OriginalText = OriginalText;
            return n;
        }
    }
}
