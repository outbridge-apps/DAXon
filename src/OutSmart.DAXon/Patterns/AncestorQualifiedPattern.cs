////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;


namespace OutSmart.DAXon.Patterns
{
    // Faithful port of net/sf/saxon/pattern/AncestorQualifiedPattern.java (runtime-matching members only).
    // Was a hollow stub (NOT : Pattern, implicit operator Pattern => null) which nulled every A/B path
    // match pattern -> NRE at PatternParser.ParsePattern:192. Now a real Pattern subclass; csproj keeps the
    // transpiled AncestorQualifiedPattern.cs excluded (re-including it cascades). Depends only on already-
    // compiled types. Construction sites (3-arg): SlashExpression:1383, PackageLoaderHE:6367.
    internal sealed class AncestorQualifiedPattern : Pattern
    {
        private Pattern basePattern;
        private Pattern upperPattern;
        private readonly int upwardsAxis = AxisInfo.PARENT;
        private bool testUpperPatternFirst;
        public int UpwardsAxis => upwardsAxis;

        public override int Dependencies => basePattern.Dependencies | upperPattern.Dependencies;
        public override int Fingerprint => basePattern.Fingerprint;

        public AncestorQualifiedPattern(Pattern @base, Pattern upper, int axis)
        {
            this.basePattern = @base;
            this.upperPattern = upper;
            this.upwardsAxis = axis;
            AdoptChildExpression(@base);
            AdoptChildExpression(upper);
        }

        public override IEnumerable<Operand> Operands()
        {
            return OperandList(
                new Operand(this, upperPattern, OperandRole.SAME_FOCUS_ACTION),
                new Operand(this, basePattern, OperandRole.SAME_FOCUS_ACTION));
        }

        public override bool IsMotionless() => basePattern.IsMotionless() && upperPattern.IsMotionless();
        public override bool MatchesCurrentGroup() => upperPattern.MatchesCurrentGroup();

        public override void BindCurrent(ILocalBinding binding)
        {
            basePattern.BindCurrent(binding);
            upperPattern.BindCurrent(binding);
        }

        public override Expression Simplify()
        {
            upperPattern = (Pattern)upperPattern.Simplify();
            basePattern = (Pattern)basePattern.Simplify();
            return this;
        }

        public override int AllocateSlots(SlotManager slotManager, int nextFree)
        {
            nextFree = upperPattern.AllocateSlots(slotManager, nextFree);
            nextFree = basePattern.AllocateSlots(slotManager, nextFree);
            return nextFree;
        }

        public override bool Matches(IItem item, IXPathContext context)
        {
            return item is NodeInfo && MatchesBeneathAnchor((NodeInfo)item, null, context);
        }

        public override bool MatchesBeneathAnchor(NodeInfo node, NodeInfo anchor, IXPathContext context)
        {
            if (testUpperPatternFirst)
            {
                bool ok;
                try { ok = MatchesUpperPattern(node, anchor, context); }
                catch (XPathException)
                {
                    if (basePattern.Matches(node, context))
                    {
                        throw;
                    }
                    return false;
                }
                return ok && basePattern.Matches(node, context);
            }
            else
            {
                bool ok;
                try { ok = basePattern.MatchesBeneathAnchor(node, anchor, context); }
                catch (XPathException)
                {
                    testUpperPatternFirst = true;
                    if (upperPattern.Matches(node, context))
                    {
                        throw;
                    }
                    return false;
                }
                return ok && MatchesUpperPattern(node, anchor, context);
            }
        }

        private bool MatchesUpperPattern(NodeInfo node, NodeInfo anchor, IXPathContext context)
        {
            switch (upwardsAxis)
            {
                case AxisInfo.SELF:
                    return upperPattern.MatchesBeneathAnchor(node, anchor, context);
                case AxisInfo.PARENT:
                    {
                        NodeInfo par = node.GetParent();
                        return par != null && upperPattern.MatchesBeneathAnchor(par, anchor, context);
                    }
                case AxisInfo.ANCESTOR:
                    return HasMatchingAncestor(anchor, node.GetParent(), context);
                case AxisInfo.ANCESTOR_OR_SELF:
                    return HasMatchingAncestor(anchor, node, context);
                default:
                    throw new XPathException("Unsupported axis in match pattern");
            }
        }

        private bool HasMatchingAncestor(NodeInfo anchor, NodeInfo anc, IXPathContext context)
        {
            while (anc != null)
            {
                if (upperPattern.MatchesBeneathAnchor(anc, anchor, context))
                {
                    return true;
                }
                if (anc.Equals(anchor))
                {
                    return false;
                }
                anc = anc.GetParent();
            }
            return false;
        }

        public override UType GetUType() => basePattern.GetUType();
        public override ItemType GetItemType() => basePattern.GetItemType();

        public override Pattern ConvertToTypedPattern(string val)
        {
            if (upperPattern.GetUType().Equals(UType.DOCUMENT))
            {
                Pattern b2 = basePattern.ConvertToTypedPattern(val);
                return b2 == basePattern ? (Pattern)this : new AncestorQualifiedPattern(b2, upperPattern, upwardsAxis);
            }
            else
            {
                Pattern u2 = upperPattern.ConvertToTypedPattern(val);
                return u2 == upperPattern ? (Pattern)this : new AncestorQualifiedPattern(basePattern, u2, upwardsAxis);
            }
        }

        public override string Reconstruct()
        {
            return upperPattern + (upwardsAxis == AxisInfo.PARENT ? "/" : "//") + basePattern;
        }

        public override void Export(ExpressionPresenter presenter)
        {
            presenter.StartElement("p.withUpper");
            presenter.EmitAttribute("axis", AxisInfo.axisName[UpwardsAxis]);
            basePattern.Export(presenter);
            upperPattern.Export(presenter);
            presenter.EndElement();
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            AncestorQualifiedPattern n = new AncestorQualifiedPattern(
                (Pattern)basePattern.Copy(rebindings),
                (Pattern)upperPattern.Copy(rebindings),
                upwardsAxis);
            ExpressionTool.CopyLocationInfo(this, n);
            n.OriginalText = OriginalText;
            n.testUpperPatternFirst = testUpperPatternFirst;
            return n;
        }
    }
}
