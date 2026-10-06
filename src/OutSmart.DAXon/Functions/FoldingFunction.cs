////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Core;
namespace OutSmart.DAXon.Functions
{
    internal abstract class FoldingFunction : SystemFunction
    {

        public override string StreamerName => "IFold";
        public abstract IFold GetFold(IXPathContext context, params ISequence[] additionalArguments);
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return RunFold(GetFold(context, TailArguments(arguments)), arguments[0].Iterate());
        }

        protected static ISequence[] TailArguments(ISequence[] arguments)
        {
            ISequence[] additionalArgs = new ISequence[arguments.Length - 1];
            Array.Copy(arguments, 1, additionalArgs, 0, additionalArgs.Length);
            return additionalArgs;
        }

        protected static ISequence RunFold(IFold fold, ISequenceIterator iter)
        {
            try
            {
                int steps = 0;
                for (IItem item; (item = iter.Next()) != null;)
                {
                    // A stored input's iterator looks at no clock, and an item of a fold can be cheap.
                    if ((++steps & 1023) == 0)
                    {
                        Core.Controller.CheckActiveTimeout();
                    }

                    fold.ProcessItem(item);
                    if (fold.IsFinished())
                    {
                        break;
                    }
                }
            }
            catch (UncheckedXPathException e)
            {
                throw e.GetXPathException();
            }

            return fold.Result();
        }
    }
}