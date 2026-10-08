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
    // Faithful runtime port of poc/output/full/AttributeGetter.cs (the real file is excluded because its
    // GetElaborator()/AttributeGetterElaborator : ItemElaborator drags the ItemElaborator compile cluster).
    // The prior stub overrode NEITHER EvaluateItem NOR Iterate and returned GetImplementationMethod()=>0, so it
    // inherited the mutually-recursive base Expression.Iterate<->EvaluateItem -> StackOverflow whenever the
    // optimizer atomized an attribute-axis @name/@id NameTest (Atomizer.cs:397-413) -- library cases 01 (xs:integer($o/@id))
    // and 04 (value-of @name). Fix: store the FingerprintedQName, return EVALUATE_METHOD, and implement the real
    // EvaluateItem fast-path (TinyElementImpl attribute value, else element attribute by name). Elaborator omitted
    // (String_1 pattern); never-hit XPDY0002 error branches dropped. Copy returns base Expression (covariant
    // AttributeGetter return would be CS8830 on net472).
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
