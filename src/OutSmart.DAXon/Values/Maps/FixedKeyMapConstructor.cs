////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Operators;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Types;
// The engine XPathException: PreEvaluate's back-off catch used to bind to the (never-thrown)
// JAXP stub of the same name, so a fold-time error crashed compilation instead of backing off.
using XPathException = OutSmart.DAXon.Transformation.XPathException;
using System.Collections.Generic;
using System.Text;

namespace OutSmart.DAXon.Values.Maps
{
    /// <summary>
    /// Compiled form of a `map { "k1": v1, "k2": v2, ... }` constructor whose keys are all distinct
    /// xs:string literals. Instead of `map:merge((map:entry(k1,v1), ...))` -- which allocates one
    /// SingleEntryMap per entry plus a Block plus the merge machinery per record -- it builds the
    /// HashTrieMap directly with one InitialPut per pre-interned key. The key instances are shared
    /// across every evaluation (immutable), and building a HashTrieMap directly yields the identical
    /// hash-trie iteration order as the merge path (HAMT order is key-hash-determined, not
    /// insertion-order-determined), so serialization stays byte-for-byte identical.
    /// Restricted to distinct string-literal keys so duplicate/typed-key semantics stay on the old path.
    /// </summary>
    internal sealed class FixedKeyMapConstructor : Expression
    {
        private readonly string[] keys;
        private readonly StringValue[] keyValues;   // pre-built, shared across evaluations
        private readonly FixedShapeMap.Shape shape;  // interned key layout + HAMT iteration order, shared
        private readonly OperandArray operanda;

        public override string ExpressionName => "FixedKeyMapConstructor";
        public override int ImplementationMethod => EVALUATE_METHOD;

        public FixedKeyMapConstructor(string[] keys, IList<Expression> valueExprs)
        {
            this.keys = keys;
            keyValues = new StringValue[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                keyValues[i] = new StringValue(keys[i]);
            }

            shape = new FixedShapeMap.Shape(keyValues);

            Expression[] kids = new Expression[valueExprs.Count];
            for (int i = 0; i < valueExprs.Count; i++)
            {
                kids[i] = valueExprs[i];
                AdoptChildExpression(kids[i]);
            }

            operanda = new OperandArray(this, kids, OperandRole.NAVIGATE);
        }

        public override IEnumerable<Operand> Operands()
        {
            return operanda;
        }

        protected override int ComputeSpecialProperties()
        {
            return 0;
        }

        protected override int ComputeCardinality()
        {
            return StaticProperty.EXACTLY_ONE;   // a map is a single item
        }

        public override UType GetStaticUType(UType contextItemType)
        {
            return UType.FUNCTION;
        }

        public override ItemType GetItemType()
        {
            ItemType valueType = null;
            int valueCard = StaticProperty.EXACTLY_ONE;
            TypeHierarchy th = GetConfiguration().GetTypeHierarchy();
            foreach (Operand o in Operands())
            {
                Expression e = o.GetChildExpression();
                if (valueType == null)
                {
                    valueType = e.GetItemType();
                    valueCard = e.GetCardinality();
                }
                else
                {
                    valueType = Types.Type.GetCommonSuperType(valueType, e.GetItemType(), th);
                    valueCard = Cardinality.Union(valueCard, e.GetCardinality());
                }
            }

            if (valueType == null)
            {
                return MapType.EMPTY_MAP_TYPE;
            }

            return new MapType(BuiltInAtomicType.STRING, SequenceType.MakeSequenceType(valueType, valueCard));
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            IGroundedValue[] vals = new IGroundedValue[keyValues.Length];
            int i = 0;
            foreach (Operand o in Operands())
            {
                vals[i++] = ExpressionTool.EagerEvaluate(o.GetChildExpression(), context);
            }

            return new FixedShapeMap(shape, vals);
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Expression e = base.TypeCheck(visitor, contextInfo);
            return e != this ? e : PreEvaluate(visitor);
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Expression e = base.Optimize(visitor, contextInfo);
            return e != this ? e : PreEvaluate(visitor);
        }

        // All values literal -> the whole map is a compile-time constant.
        private Expression PreEvaluate(ExpressionVisitor visitor)
        {
            foreach (Operand o in Operands())
            {
                if (!(o.GetChildExpression() is Literal))
                {
                    return this;
                }
            }

            try
            {
                return Literal.MakeLiteral(EvaluateItem(visitor.MakeDynamicContext()), this);
            }
            catch (XPathException)
            {
                return this;
            }
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            IList<Expression> v2 = new List<Expression>(operanda.NumberOfOperands);
            foreach (Operand o in Operands())
            {
                v2.Add(o.GetChildExpression().Copy(rebindings));
            }

            FixedKeyMapConstructor c2 = new FixedKeyMapConstructor(keys, v2);
            ExpressionTool.CopyLocationInfo(this, c2);
            return c2;
        }

        public override Elaborator GetElaborator()
        {
            return new FixedKeyMapElaborator();
        }

        private sealed class FixedKeyMapElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                FixedKeyMapConstructor expr = (FixedKeyMapConstructor)GetExpression();
                FixedShapeMap.Shape shape = expr.shape;
                ISequenceEvaluator[] evals = new ISequenceEvaluator[expr.operanda.NumberOfOperands];
                int j = 0;
                foreach (Operand o in expr.Operands())
                {
                    evals[j++] = o.GetChildExpression().MakeElaborator().Eagerly();
                }

                return (context) =>
                {
                    IGroundedValue[] vals = new IGroundedValue[evals.Length];
                    for (int i = 0; i < evals.Length; i++)
                    {
                        vals[i] = evals[i].Evaluate(context).Materialize();
                    }

                    return new FixedShapeMap(shape, vals);
                };
            }
        }

        public override void Export(ExpressionPresenter @out)
        {
            // Export as the equivalent map:merge/entry shape so a round-trip stays spec-legible.
            @out.StartElement("map", this);
            int i = 0;
            foreach (Operand o in Operands())
            {
                @out.StartElement("entry");
                @out.EmitAttribute("key", keys[i++]);
                o.GetChildExpression().Export(@out);
                @out.EndElement();
            }

            @out.EndElement();
        }

        public override string ToShortString()
        {
            return "map{" + (keys.Length == 0 ? "" : keys[0] + ": ...") + "}";
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("map{");
            int i = 0;
            foreach (Operand o in Operands())
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append('"').Append(keys[i++]).Append("\": ").Append(o.GetChildExpression().ToString());
            }

            return sb.Append('}').ToString();
        }
    }
}
