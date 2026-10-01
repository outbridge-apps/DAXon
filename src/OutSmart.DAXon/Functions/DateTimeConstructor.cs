////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions;
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
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class supports the dateTime($date, $time) function
    /// </summary>
    internal sealed class DateTimeConstructor : SystemFunction
    {
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            DateValue arg0 = (DateValue)arguments[0].Head();
            TimeValue arg1 = (TimeValue)arguments[1].Head();
            if (arg0 == null || arg1 == null)
            {
                return EmptySequence.GetInstance();
            }

            return DateTimeValue.MakeDateTimeValue(arg0, arg1);
        }

        public override Elaborator GetElaborator()
        {
            return new DateTimeFnElaborator();
        }

        internal sealed class DateTimeFnElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                SystemFunctionCall sfc = (SystemFunctionCall)GetExpression();
                IItemEvaluator arg0eval = sfc.GetArg(0).MakeElaborator().ElaborateForItem();
                IItemEvaluator arg1eval = sfc.GetArg(1).MakeElaborator().ElaborateForItem();
                return (context) => DateTimeValue.MakeDateTimeValue((DateValue)arg0eval.Eval(context), (TimeValue)arg1eval.Eval(context));
            }
        }
    }
}
