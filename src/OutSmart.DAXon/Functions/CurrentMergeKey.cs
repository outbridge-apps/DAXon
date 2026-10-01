////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implements the XSLT function current-grouping-key()
    /// </summary>
    internal sealed class CurrentMergeKey : SystemFunction, ICallable
    {
        private MergeInstr controllingInstruction = null; // may be unknown, when current group has dynamic scope

        public MergeInstr ControllingInstruction
        {
            get => controllingInstruction; set
            {
                this.controllingInstruction = value;
            }
        }

        public override Expression MakeFunctionCall(params Expression[] arguments)
        {
            // Java: new SystemFunctionCall(this, arguments) — old base(null, null) NRE'd in the
            // StaticFunctionCall ctor; GetScopingExpression was a hide-not-override.
            return new AnonymousSystemFunctionCall(this, arguments);
        }

        /// <summary>
        /// Evaluate the expression
        /// </summary>
        public ISequenceIterator Iterate(IXPathContext c)
        {
            IGroupIterator gi = c.GetCurrentMergeGroupIterator();
            if (gi == null)
            {
                throw new XPathException("There is no current merge key", "XTDE3510");
            }

            IAtomicSequence keySequence = gi.GetCurrentGroupingKey();

            // Bug 6639 - remove any null entries
            IList<AtomicValue> items = new List<AtomicValue>(keySequence.GetLength());
            foreach (AtomicValue item in keySequence)
            {
                if (item != null)
                {
                    items.Add(item);
                }
            }

            return new ListIterator.Of<AtomicValue>(items);
        }

        /// <summary>
        /// Evaluate the expression
        /// </summary>
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return SequenceTool.ToLazySequence(Iterate(context));
        }

        private sealed class AnonymousSystemFunctionCall : SystemFunctionCall
        {

            private readonly CurrentMergeKey parent;
            public override Expression ScopingExpression => parent.ControllingInstruction;
            public AnonymousSystemFunctionCall(CurrentMergeKey parent, Expression[] arguments) : base(parent, arguments)
            {
                this.parent = parent;
            }
        }
    }
}