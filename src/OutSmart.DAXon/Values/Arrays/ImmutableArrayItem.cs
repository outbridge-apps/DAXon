////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Collections.Zeno;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Values.Arrays
{
    internal sealed class ImmutableArrayItem : AbstractArrayItem
    {
        private readonly ZenoChain<IGroundedValue> vector;
        public ImmutableArrayItem(SimpleArrayItem other)
        {
            this.vector = new ZenoChain<IGroundedValue>().AddAll(other.GetMembers());
        }

        public ImmutableArrayItem(IEnumerable<IGroundedValue> members)
        {
            this.vector = new ZenoChain<IGroundedValue>().AddAll(members);
        }

        private ImmutableArrayItem(ZenoChain<IGroundedValue> vector)
        {
            this.vector = vector;
        }

        public static ImmutableArrayItem From(ISequenceIterator iter)
        {
            ZenoChain<IGroundedValue> content = new ZenoChain<IGroundedValue>();
            for (IItem item; (item = iter.Next()) != null;)
            {
                content = content.Add(item);
            }

            return new ImmutableArrayItem(content);
        }

        public override IGroundedValue Get(int index)
        {
            return vector[index];
        }

        public override ArrayItem Put(int index, IGroundedValue newValue)
        {
            ZenoChain<IGroundedValue> v2 = vector.Replace(index, newValue);
            return v2 == vector ? this : new ImmutableArrayItem(v2);
        }

        public override ArrayItem Insert(int position, IGroundedValue member)
        {
            ZenoChain<IGroundedValue> v2 = vector.Insert(position, member);
            return new ImmutableArrayItem(v2);
        }

        public override ArrayItem Append(IGroundedValue newMember)
        {
            ZenoChain<IGroundedValue> v2 = vector.Add(newMember);
            return new ImmutableArrayItem(v2);
        }

        public override int ArrayLength()
        {
            return vector.Size();
        }

        public override bool IsEmpty()
        {
            return vector.IsEmpty();
        }

        public override IEnumerable<IGroundedValue> Members()
        {
            return vector;
        }

        public override ArrayItem SubArray(int start, int end)
        {
            return new ImmutableArrayItem(vector.SubList(start, end));
        }

        public override ArrayItem Concat(ArrayItem other)
        {
            if (other.ArrayLength() == 0)
            {
                return this;
            }

            ZenoChain<IGroundedValue> otherChain;
            if (other is ImmutableArrayItem)
            {
                otherChain = ((ImmutableArrayItem)other).vector;
            }
            else
            {
                otherChain = new ImmutableArrayItem((SimpleArrayItem)other).vector;
            }

            ZenoChain<IGroundedValue> v2 = vector.AddAll(otherChain);
            return new ImmutableArrayItem(v2);
        }

        public override ArrayItem Remove(int index)
        {
            ZenoChain<IGroundedValue> v2 = vector.Remove(index);
            return v2 == vector ? this : new ImmutableArrayItem(v2);
        }

        public override ArrayItem RemoveSeveral(IntSet positions)
        {
            int[] p = new int[positions.Count];
            int i = 0;
            IIntIterator ii = positions.IIterator();
            while (ii.MoveNext())
            {
                p[i++] = ii.Current;
            }

            System.Array.Sort(p);
            ZenoChain<IGroundedValue> v2 = vector;
            for (int j = p.Length - 1; j >= 0; j--)
            {
                v2 = v2.Remove(p[j]);
            }

            return v2 == vector ? this : new ImmutableArrayItem(v2);
        }
    }
}