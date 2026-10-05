////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Internal.Collections;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Values
{
    public class NestedIntegerValue : IComparable<NestedIntegerValue>
    {
        public static NestedIntegerValue ONE = new NestedIntegerValue(new int[] { 1 });
        public static NestedIntegerValue TWO = new NestedIntegerValue(new int[] { 2 });
        int[] value;

        public virtual NestedIntegerValue Stem
        {
            get
            {
                if (value.Length == 0)
                {
                    return null;
                }
                else
                {
                    int[] v = new int[value.Length - 1];
                    Array.Copy(value, 0, v, 0, v.Length);
                    return new NestedIntegerValue(v);
                }
            }
        }

        public virtual int Depth => value.Length;

        public virtual int Leaf
        {
            get
            {
                if (value.Length == 0)
                {
                    return -1;
                }
                else
                {
                    return value[value.Length - 1];
                }
            }
        }
        public NestedIntegerValue(string v)
        {
            Parse(v);
        }

        public NestedIntegerValue(int[] val)
        {
            value = val;
        }

        public static NestedIntegerValue Parse(string v)
        {
            string[] parts = v.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            int[] valuei = new int[parts.Length];
            try
            {
                for (int i = 0; i < parts.Length; i++)
                {
                    valuei[i] = int.Parse(parts[i], CultureInfo.InvariantCulture);
                }
            }
            catch (Exception exc) when (exc is FormatException || exc is OverflowException)
            {
                throw new XPathException("Nested integer value has incorrect format: " + v);
            }

            return new NestedIntegerValue(valuei);
        }

        public virtual NestedIntegerValue Append(int leaf)
        {
            int[] v = new int[value.Length + 1];
            Array.Copy(value, 0, v, 0, value.Length);
            v[value.Length] = leaf;
            return new NestedIntegerValue(v);
        }

        public override bool Equals(object o)
        {
            return (o is NestedIntegerValue) && ArrayTools.Equals(value, ((NestedIntegerValue)o).value);
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public override int GetHashCode()
        {
            return ArrayTools.GetHashCode(value);
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public virtual int CompareTo(NestedIntegerValue other)
        {
            NestedIntegerValue v2 = (NestedIntegerValue)other;
            for (int i = 0; i < value.Length && i < v2.value.Length; i++)
            {
                if (value[i] != v2.value[i])
                {
                    if (value[i] < v2.value[i])
                    {
                        return -1;
                    }
                    else
                    {
                        return 1;
                    }
                }
            }

            return Math.Sign(value.Length - v2.value.Length);
        }
    }
}