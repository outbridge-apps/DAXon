////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
// Hand-ported 1:1 from upstream ParameterSet.java (the JavaToCSharp converter crashes on it).
// NOT_SUPPLIED is 0, matching the real Java constant.

using System;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
using System.Collections.Generic;

namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// A ParameterSet is a set of parameters supplied when calling a template.
    /// It is a collection of name-value pairs.
    /// </summary>
    public class ParameterSet
    {

        public const int NOT_SUPPLIED = 0;
        public const int SUPPLIED = 1;
        public const int SUPPLIED_AND_CHECKED = 2;

        public static ParameterSet EMPTY_PARAMETER_SET = new ParameterSet(0);
        private StructuredQName[] keys;
        private ISequence[] values;
        private bool[] typeChecked;
        private int used = 0;

        public virtual StructuredQName[] ParameterNames => keys;

        public ParameterSet() : this(10)
        {
        }

        public ParameterSet(int capacity)
        {
            keys = new StructuredQName[capacity];
            values = new ISequence[capacity];
            typeChecked = new bool[capacity];
        }

        public ParameterSet(IDictionary<StructuredQName, ISequence> map) : this(map.Count)
        {
            int i = 0;
            foreach (KeyValuePair<StructuredQName, ISequence> entry in map)
            {
                keys[i] = entry.Key;
                values[i] = entry.Value;
                typeChecked[i++] = false;
            }

            used = i;
        }

        public ParameterSet(ParameterSet existing, int extra) : this(existing.used + extra)
        {
            for (int i = 0; i < existing.used; i++)
            {
                Put(existing.keys[i], existing.values[i], existing.typeChecked[i]);
            }
        }

        public virtual int Size()
        {
            return used;
        }

        public virtual void Put(StructuredQName id, ISequence value, bool @checked)
        {
            for (int i = 0; i < used; i++)
            {
                if (keys[i].Equals(id))
                {
                    values[i] = value;
                    typeChecked[i] = @checked;
                    return;
                }
            }

            if (used + 1 > keys.Length)
            {
                int newLength = used <= 5 ? 10 : used * 2;
                Array.Resize(ref values, newLength);
                Array.Resize(ref keys, newLength);
                Array.Resize(ref typeChecked, newLength);
            }

            keys[used] = id;
            typeChecked[used] = @checked;
            values[used++] = value;
        }

        public virtual int GetIndex(StructuredQName id)
        {
            for (int i = 0; i < used; i++)
            {
                if (keys[i].Equals(id))
                {
                    return i;
                }
            }

            return -1;
        }

        public virtual ISequence GetValue(int index)
        {
            return values[index];
        }

        public virtual bool IsTypeChecked(int index)
        {
            return typeChecked[index];
        }

        public virtual void Clear()
        {
            used = 0;
        }

        public virtual void MaterializeValues()
        {
            for (int i = 0; i < used; i++)
            {
                if (values[i] is Closure)
                {
                    values[i] = ((Closure)values[i]).Reduce();
                }
            }
        }
    }
}
