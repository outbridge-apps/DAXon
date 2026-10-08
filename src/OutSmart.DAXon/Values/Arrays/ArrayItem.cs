////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Collections;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
namespace OutSmart.DAXon.Values.Arrays
{
    public abstract class ArrayItem : IFunctionItem
    {

        // PHASE7_INDEXER_ARRAYITEM
        public IGroundedValue this[int n] { get { return Get(n); } }
        public abstract IFunctionItemType FunctionItemType { get; }
        public virtual OperandRole[] OperandRoles => new[] { OperandRole.SINGLE_ATOMIC }; // upstream ArrayItem default
        public virtual string Description => "array"; // upstream ArrayItem default
        // Arrays are function items: no string value (FOTY0014), like AbstractFunction.
        public virtual UnicodeString UnicodeStringValue => throw new UncheckedXPathException(new XPathException("The string value of an array is not defined", "FOTY0014"));
        public bool IsArray()
        {
            return true;
        }

        public bool IsMap()
        {
            return false;
        }

        public abstract IGroundedValue Get(int index);
        public abstract ArrayItem Put(int index, IGroundedValue newValue);
        public abstract int ArrayLength();
        public virtual bool IsEmpty()
        {
            return ArrayLength() == 0;
        }

        public abstract IEnumerable<IGroundedValue> Members();
        public virtual ISequenceIterator Parcels()
        {
            return (ISequenceIterator)(new SequenceIteratorOverJavaIterator<IGroundedValue>(Members().GetEnumerator(), (member) => new Parcel()));
        }

        public abstract ArrayItem Append(IGroundedValue newMember);
        public abstract ArrayItem Concat(ArrayItem other);
        public abstract ArrayItem Remove(int index);
        public abstract ArrayItem RemoveSeveral(IntSet positions);
        public abstract ArrayItem SubArray(int start, int end);
        public abstract ArrayItem Insert(int position, IGroundedValue member);
        public abstract SequenceType GetMemberType(TypeHierarchy th);
        public virtual string ToShortString()
        {
            // This member walk recurses directly rather than through Err.Depict, so it carries
            // the depth cap itself (see Err.MaxDepictDepth).
            Err.EnterDepiction();
            try
            {
                if (Err.DepictionTooDeep)
                {
                    return "array{...}";
                }

                StringBuilder sb = new StringBuilder();
                sb.Append("array{");
                int count = 0;
                foreach (IGroundedValue member in Members())
                {
                    if (count++ > 2)
                    {
                        sb.Append(" ...");
                        break;
                    }

                    sb.Append(member.ToShortString());
                    sb.Append(", ");
                }

                sb.Append('}');
                return sb.ToString();
            }
            finally
            {
                Err.LeaveDepiction();
            }
        }

        public Genre GetGenre()
        {
            return Genre.ARRAY;
        }
        public abstract StructuredQName GetFunctionName();
        public abstract int GetArity();
        public abstract AnnotationList GetAnnotations();
        public abstract IXPathContext MakeNewContext(IXPathContext arg0, IContextOriginator arg1);
        public abstract bool DeepEquals(IFunctionItem arg0, IXPathContext arg1, IAtomicComparer arg2, int arg3);
        public abstract bool DeepEqual40(IFunctionItem arg0, IXPathContext arg1, DeepEqual.DeepEqualOptions arg2);
        public abstract void Export(ExpressionPresenter arg0);
        public abstract bool IsTrustedResultType();
        public abstract IAtomicSequence Atomize();
        public abstract ISequence Call(IXPathContext arg0, ISequence[] arg1);
        public virtual ISequenceIterator Iterate() => new SingletonIterator(this);
        // An array is a single item: as a grounded sequence it has length 1 (Head() == this), so ItemAt(0) is
        // the array itself and any other index is out of range. Was a throwing stub -> `array-expr[n]` NRE'd via
        // SubscriptExpression.GetItemAt (prod-ArrowPostfix). The array-MEMBER accessor is Get(int), not this.
        public virtual IItem ItemAt(int arg0) => arg0 == 0 ? (IItem)this : null;
        public virtual IItem Head() => this;
        // Singleton-sequence subsequence rule, same as ItemAt above.
        public virtual IGroundedValue Subsequence(int start, int length) => start <= 0 && (long)start + length > 0 ? (IGroundedValue)this : OutSmart.DAXon.Values.EmptySequence.GetInstance();
        public virtual int GetLength() => 1;
        // Mirrors UnicodeStringValue: arrays have no string value (FOTY0014).
        public virtual string GetStringValue() => throw new UncheckedXPathException(new XPathException("The string value of an array is not defined", "FOTY0014"));
        SingletonIterator IItem.Iterate() => default;

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public virtual bool IsSequenceVariadic() => false; // upstream FunctionItem default
        public virtual IGroundedValue Reduce() => this;
        public virtual bool IsStreamed() => false; // upstream NodeInfo/Item default
        public abstract bool EffectiveBooleanValue();
        public virtual IGroundedValue Materialize() => this;
        public virtual IEnumerable<IItem> AsIterable() => new IItem[] { this };
        public virtual bool ContainsNode(NodeInfo sought) => OutSmart.DAXon.Expressions.SingletonIntersectExpression.ContainsNode(((OutSmart.DAXon.Model.ISequence)this).Iterate(), sought); // upstream GroundedValue default
        public virtual IGroundedValue Concatenate(IGroundedValue[] others)
        {
            // upstream GroundedValue default: chain this value's items with the others
            var __chain = new OutSmart.DAXon.Collections.Zeno.ZenoChain<OutSmart.DAXon.Model.IItem>().AddAll(((OutSmart.DAXon.Model.IGroundedValue)this).AsIterable());
            foreach (OutSmart.DAXon.Model.IGroundedValue __v in others)
                __chain = __chain.AddAll(__v.AsIterable());
            return new OutSmart.DAXon.Collections.Zeno.ZenoSequence(__chain);
        }
        public virtual ISequence MakeRepeatable() => this; // upstream Sequence.makeRepeatable default
    }
}

