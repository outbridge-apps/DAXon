////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Expressions
{
    // An attribute of the context element by name, read straight from the element (Saxon's AttributeGetter). It must
    // evaluate itself: Expression's own Iterate and EvaluateItem call each other.
    internal sealed class AttributeGetter : Expression
    {
        private readonly FingerprintedQName attributeName;
        public override int ImplementationMethod => EVALUATE_METHOD;
        public override int IntrinsicDependencies => StaticProperty.DEPENDS_ON_CONTEXT_ITEM;
        public AttributeGetter(object name) { attributeName = name as FingerprintedQName; }
        public override Expression Copy(RebindingMap r) => new AttributeGetter(attributeName);
        public override void Export(ExpressionPresenter @out) { }
        public override ItemType GetItemType() => BuiltInAtomicType.UNTYPED_ATOMIC;
        protected override int ComputeCardinality() => StaticProperty.ALLOWS_ZERO_OR_ONE;
        public override IItem EvaluateItem(IXPathContext context)
        {
            IItem item = context.GetContextItem();
            if (item is TinyElementImpl)
            {
                string val = ((TinyElementImpl)item).GetAttributeValue(attributeName.Fingerprint);
                return val == null ? null : StringValue.MakeUntypedAtomic(StringView.Tidy(val));
            }
            if (item is NodeInfo)
            {
                NodeInfo node = (NodeInfo)item;
                if (node.GetNodeKind() == Types.Type.ELEMENT)
                {
                    string val = node.GetAttributeValue(attributeName.GetNamespaceUri(), attributeName.GetLocalPart());
                    return val == null ? null : StringValue.MakeUntypedAtomic(StringView.Tidy(val));
                }
            }
            return null;
        }
    }
}
