////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Functions
{
    internal sealed class Adjust_2 : SystemFunction
    {
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            CalendarValue @in = (CalendarValue)arguments[0].Head();
            if (@in == null)
            {
                return EmptySequence.GetInstance();
            }
            else
            {
                DayTimeDurationValue tz = (DayTimeDurationValue)arguments[1].Head();
                if (tz == null)
                {
                    return @in.RemoveTimezone();
                }
                else
                {
                    return @in.AdjustTimezone(tz);
                }
            }
        }
    }
}
