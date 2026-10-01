////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions.Elaboration
{
    internal sealed class MemoClosureEvaluator : ISequenceEvaluator
    {
        readonly Expression input;
        readonly IPullEvaluator inputEvaluator;
        readonly bool singleton;
        public MemoClosureEvaluator(Expression input, IPullEvaluator inputEvaluator)
        {
            this.input = input;
            this.inputEvaluator = inputEvaluator;
            this.singleton = !Cardinality.AllowsMany(input.GetCardinality());
        }

        public ISequence Evaluate(IXPathContext context)
        {
            try
            {
                return singleton ? new SingletonClosure(input, inputEvaluator, context) : new MemoClosure(input, inputEvaluator, context);
            }
            catch (UncheckedXPathException e)
            {
                throw e.GetXPathException();
            }
        }
    }
}