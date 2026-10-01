////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Model
{
    internal sealed class One<T> : ZeroOrOne<T>
    {
        public One(T item) : base(item)
        {
            if (item == null)
            {
                throw new NullReferenceException();
            }
        }

        public static One<BooleanValue> Bool(bool value)
        {
            return new One<BooleanValue>(BooleanValue.Get(value));
        }

        public static One<StringValue> String(string value)
        {
            return new One<StringValue>(new StringValue(value));
        }

        public static One<StringValue> String(UnicodeString value)
        {
            return new One<StringValue>(new StringValue(value));
        }

        public static One<IntegerValue> Integer(long value)
        {
            return new One<IntegerValue>(new Int64Value(value));
        }

        public static One<DoubleValue> Dbl(double value)
        {
            return new One<DoubleValue>(new DoubleValue(value));
        }
    }
}