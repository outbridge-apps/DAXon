////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Trees.Iterators;
namespace OutSmart.DAXon.Values
{
    public class ObjectValue<T> : IAnyExternalObject
    {
        private readonly T value;
        private readonly System.Type theInterface;

        public virtual System.Type Interface => theInterface;

        public virtual UnicodeString UnicodeStringValue => StringView.Of(value.ToString()).Tidy();

        public virtual object WrappedObject => value;
        public ObjectValue(T @object)
        {
            value = @object ?? throw new NullReferenceException("External object cannot wrap a Java null");
            theInterface = null;
        }

        public ObjectValue(T @object, System.Type theInterface)
        {
            value = @object ?? throw new NullReferenceException("External object cannot wrap a Java null");
            this.theInterface = theInterface ?? throw new NullReferenceException();
        }

        public virtual Genre GetGenre()
        {
            return Genre.EXTERNAL;
        }

        public virtual StringValue Atomize()
        {
            return new StringValue(UnicodeStringValue.Tidy());
        }

        public virtual ItemType GetItemType(TypeHierarchy th)
        {
            lock (th.GetConfiguration())
            {
                return JavaExternalObjectType.Of(value.GetType());
            }
        }

        public static string DisplayTypeName(object value)
        {
            return "java-type:" + value.GetType().FullName;
        }

        public virtual bool EffectiveBooleanValue()
        {
            return true;
        }

        public virtual T GetObject()
        {
            return value;
        }

        public override bool Equals(object other)
        {
            if (other is ObjectValue<object>)
            {
                object o = ((ObjectValue<object>)other).value;
                return value.Equals(o);
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public virtual string ToShortString()
        {
            string v = value.ToString();
            if (v.StartsWith(value.GetType().FullName, StringComparison.Ordinal))
            {
                return v;
            }
            else
            {
                return "(" + value.GetType().Name + ")" + Err.Truncate30(StringView.Tidy(value.ToString()));
            }
        }
        IAtomicSequence IItem.Atomize() => Atomize();
        // upstream Item interface defaults for a singleton value (were NIE stubs — MemoFunction's
        // cache walked GetLength() on a NodeSurrogate and died, function-1034)
        public virtual ISequenceIterator Iterate() => new SingletonIterator(this);
        public virtual IItem ItemAt(int arg0) => arg0 == 0 ? this : null;
        public virtual IItem Head() => this;
        public virtual IGroundedValue Subsequence(int start, int length) => start <= 0 && start + length > 0 ? (IGroundedValue)this : EmptySequence.GetInstance();
        public virtual int GetLength() => 1;
        public virtual string GetStringValue() => UnicodeStringValue.ToString();
        SingletonIterator IItem.Iterate() => new SingletonIterator(this);

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public virtual IGroundedValue Reduce() => this; // upstream GroundedValue default
        public virtual bool IsStreamed() => false; // upstream NodeInfo/Item default
        public virtual IGroundedValue Materialize() => this; // upstream GroundedValue default
        public virtual IEnumerable<IItem> AsIterable() => new IItem[] { this }; // singleton grounded value (upstream GroundedValue default for an Item)
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


