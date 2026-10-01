////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implements the XSLT function current-grouping-key()
    /// </summary>
    internal sealed class CurrentGroupingKeyCall : Expression, ICallable
    {
        public override Expression ScopingExpression => CurrentGroupCall.FindControllingInstruction(this);

        public override int ImplementationMethod => ITERATE_METHOD;

        /// <summary>
        /// Determine the dependencies
        /// </summary>
        public override int IntrinsicDependencies => StaticProperty.DEPENDS_ON_CURRENT_GROUP;

        protected override int ComputeCardinality()
        {

            // Can return an empty sequence in 2.0 mode
            return StaticProperty.ALLOWS_ZERO_OR_MORE;
        }

        public override ItemType GetItemType()
        {
            return BuiltInAtomicType.ANY_ATOMIC;
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("currentGroupingKey");
            @out.EndElement();
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            return new CurrentGroupingKeyCall();
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return MakeElaborator().ElaborateForPull().Iterate(context);
        }

        public ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return SequenceTool.ToLazySequence(Iterate(context));
        }

        public override Elaborator GetElaborator()
        {
            return new CurrentGroupingKeyCallElaborator();
        }

        private sealed class CurrentGroupingKeyCallElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                CurrentGroupingKeyCall expr = (CurrentGroupingKeyCall)GetExpression();
                return (context) =>
                {
                    IGroupIterator gi = context.GetCurrentGroupIterator();
                    IAtomicSequence result = gi == null ? null : gi.GetCurrentGroupingKey();
                    if (result == null)
                    {
                        throw new XPathException("There is no current grouping key", "XTDE1071").WithLocation(expr.GetLocation());
                    }

                    return result.Iterate();
                };
            }
        }
    }
}
