////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions.Elaboration
{
    internal sealed class IndexedVariableEvaluator : ISequenceEvaluator
    {
        readonly IPullEvaluator puller;
        public IndexedVariableEvaluator(IPullEvaluator select)
        {
            this.puller = select;
        }

        public ISequence Evaluate(IXPathContext context)
        {
            try
            {
                return context.GetConfiguration().ObtainOptimizer().MakeIndexedValue(puller.Iterate(context));
            }
            catch (UncheckedXPathException e)
            {
                throw e.GetXPathException();
            }
        }
    }
}