////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System.Collections.Generic;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implements the XSLT 3.0 function current-merge-group()
    /// </summary>
    internal sealed class CurrentMergeGroup : SystemFunction
    {
        private MergeInstr controllingInstruction; // may be unknown, when current group has dynamic scope
        private readonly HashSet<string> allowedNames = new HashSet<string>();

        public MergeInstr ControllingInstruction => controllingInstruction;

        public override ItemType ResultItemType => AnyItemType.GetInstance();

        public override string StreamerName => "CurrentMergeGroup";
        public void SetControllingInstruction(MergeInstr instruction, bool isInLoop)
        {
            this.controllingInstruction = instruction;
            foreach (MergeInstr.MergeSource m in instruction.MergeSources)
            {
                string name = m.sourceName;
                if (name != null)
                {
                    allowedNames.Add(name);
                }
            }
        }

        public override int GetSpecialProperties(Expression[] arguments)
        {
            return 0;
        }

        public override Expression MakeFunctionCall(params Expression[] arguments)
        {
            // Java: new SystemFunctionCall(this, arguments) { getScopingExpression() {...} } — the port's
            // old base(null, null) NRE'd in StaticFunctionCall's ctor, and GetScopingExpression was a
            // hide-not-override so scoping dispatch never reached it.
            return new AnonymousSystemFunctionCall(this, arguments);
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            string source = null;
            if (arguments.Length > 0)
            {
                source = arguments[0].Head().GetStringValue();
            }

            return SequenceTool.ToLazySequence(CurrentGroup(source, context));
        }

        private ISequenceIterator CurrentGroup(string source, IXPathContext c)
        {
            IGroupIterator gi = c.GetCurrentMergeGroupIterator();
            if (gi == null)
            {
                throw new XPathException("There is no current merge group", "XTDE3480");
            }

            if (source == null)
            {
                return gi.CurrentGroup().Iterate();
            }
            else
            {
                if (!allowedNames.Contains(source))
                {
                    throw new XPathException("Supplied argument (" + source + ") is not the name of any xsl:merge-source in the containing xsl:merge instruction", "XTDE3490");
                }

                return (ISequenceIterator)((MergeGroupingIterator)gi).IterateCurrentGroup(source);
            }
        }

        private sealed class AnonymousSystemFunctionCall : SystemFunctionCall
        {

            private readonly CurrentMergeGroup parent;
            public override Expression ScopingExpression => parent.ControllingInstruction;
            public AnonymousSystemFunctionCall(CurrentMergeGroup parent, Expression[] arguments) : base(parent, arguments)
            {
                this.parent = parent;
            }
        }
    }
}