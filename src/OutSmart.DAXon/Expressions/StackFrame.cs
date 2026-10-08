////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Instructions;

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Expressions
{
    public class StackFrame
    {
        public static readonly ISequence[] EMPTY_ARRAY_OF_SEQUENCE = new ISequence[0];
        public static readonly StackFrame EMPTY = new StackFrame(SlotManager.EMPTY, EMPTY_ARRAY_OF_SEQUENCE);
        public SlotManager map;
        public ISequence[] slots;
        protected Stack<ISequence> dynamicStack;

        public virtual ISequence[] StackFrameValues
        {
            get => slots; set
            {
                slots = value;
            }
        }
        public StackFrame(SlotManager map, ISequence[] slots)
        {
            this.map = map;
            this.slots = slots;
        }

        public virtual SlotManager GetStackFrameMap()
        {
            return map;
        }

        public virtual StackFrame Copy()
        {
            ISequence[] v2 = ArrayTools.CopyOf(slots, slots.Length);
            StackFrame s = new StackFrame(map, v2);
            if (dynamicStack != null)
            {
                s.dynamicStack = ShallowCopy(dynamicStack);
            }

            return s;
        }

        public virtual void PushDynamicValue(ISequence value)
        {
            if (this == StackFrame.EMPTY)
            {
                throw new InvalidOperationException("Immutable stack frame");
            }

            if (dynamicStack == null)
            {
                dynamicStack = NewStack();
            }

            dynamicStack.Push(value);
        }

        private Stack<ISequence> NewStack()
        {

            // Separate method for the benefit of C#
            return new Stack<ISequence>();
        }

        // Shallow-copy of a stack is tricky in C# because iteration reverses the order
        private Stack<ISequence> ShallowCopy(Stack<ISequence> old)
        {
            ISequence[] items = old.ToArray();
            Stack<ISequence> s2 = NewStack();
            for (int i = items.Length - 1; i >= 0; i--)
            {
                s2.Push(items[i]);
            }
            return s2;
        }

        public virtual ISequence PopDynamicValue()
        {
            return dynamicStack.Pop();
        }

        public virtual bool HoldsDynamicValue()
        {
            return dynamicStack != null && dynamicStack.Count > 0;
        }
    }
}