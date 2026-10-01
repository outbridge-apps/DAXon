////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions
{
    internal abstract class TreatFn : SystemFunction, ICallable
    {
        public abstract override string ErrorCodeForTypeErrors { get; }
        public abstract int RequiredCardinality { get; }

        public override string StreamerName => "TreatFn";
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            ISequenceIterator iterator = arguments[0].Iterate();
            int card = RequiredCardinality;

            iterator = new CardinalityCheckingIterator(iterator, card, () => MakeRoleDiagnostic(), null);
            return new LazySequence(iterator);
        }

        public virtual RoleDiagnostic MakeRoleDiagnostic()
        {
            return new RoleDiagnostic(RoleDiagnostic.FUNCTION, GetFunctionName().DisplayName, 0, ErrorCodeForTypeErrors);
        }

        internal sealed class ExactlyOne : TreatFn
        {
            public override int RequiredCardinality => StaticProperty.EXACTLY_ONE;

            public override string ErrorCodeForTypeErrors => "FORG0005";
        }

        internal sealed class OneOrMore : TreatFn
        {
            public override int RequiredCardinality => StaticProperty.ALLOWS_ONE_OR_MORE;

            public override string ErrorCodeForTypeErrors => "FORG0004";
        }

        internal sealed class ZeroOrOne : TreatFn
        {
            public override int RequiredCardinality => StaticProperty.ALLOWS_ZERO_OR_ONE;

            public override string ErrorCodeForTypeErrors => "FORG0003";
        }
    }
}
