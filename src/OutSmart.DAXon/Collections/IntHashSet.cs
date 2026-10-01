////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Collections
{
    // Deliberate triplet with IntHashMap/IntToIntHashMap (C1): same Knuth open-addressing core,
    // kept as three copies because the empty-slot encoding differs per class — here the values
    // array doubles as the key array and emptiness is the configurable ndv sentinel. The probe
    // loops sit on hot paths and must stay monomorphic, without a shared dispatching core.
    internal sealed class IntHashSet : IntSet
    {
        private const int NBIT = 30; // MAX_SIZE = 2^NBIT
        private const int MAX_SIZE = 1 << NBIT; // maximum number of values held
        private readonly int ndv;
        private int _nmax; // 0 <= _nmax = 2^nbit <= 2^NBIT = MAX_SIZE
        private int _size; // 0 <= _size <= _nmax <= MAX_SIZE
        private int _nlo; // _nmax*_factor (_size<=_nlo, if possible)
        private int _nhi; //  MAX_SIZE*_factor (_size< _nhi, if possible)
        private int _shift; // _shift = 1 + NBIT - nbit (see function hash() below)
        private int _mask; // _mask = _nmax - 1
        private int[] _values; // array[_nmax] of values

        public int[] Values
        {
            get
            {
                int index = 0;
                int[] values = new int[_size];
                foreach (int _value in _values)
                {
                    if (_value != ndv)
                    {
                        values[index++] = _value;
                    }
                }

                return values;
            }
        }
        public IntHashSet() : this(8, int.MinValue)
        {
        }

        public IntHashSet(int capacity) : this(capacity, int.MinValue)
        {
        }

        public IntHashSet(int capacity, int noDataValue)
        {
            ndv = noDataValue;
            SetCapacity(capacity);
        }

        public override IntSet Copy()
        {
            if (_size == 0)
            {
                return IntEmptySet.GetInstance();
            }
            else
            {
                IntHashSet s = new IntHashSet(_size, ndv);
                s._nmax = _nmax;
                s._size = _size;
                s._nlo = _nlo;
                s._nhi = _nhi;
                s._shift = _shift;
                s._mask = _mask;
                s._values = new int[_values.Length];
                Array.Copy(_values, 0, s._values, 0, _values.Length);
                return s;
            }
        }

        public override IntSet MutableCopy()
        {
            return Copy();
        }

        public override void Clear()
        {
            _size = 0;
            for (int i = 0; i < _nmax; ++i)
            {
                _values[i] = ndv;
            }
        }

        public override int Size()
        {
            return _size;
        }

        public override bool IsEmpty()
        {
            return _size == 0;
        }

        public override bool Contains(int value)
        {
            return (_values[IndexOf(value)] != ndv);
        }

        public override bool Remove(int value)
        {

            // Knuth, v. 3, 527, Algorithm R.
            int i = IndexOf(value);
            if (_values[i] == ndv)
            {
                return false;
            }

            --_size;
            for (; ; )
            {
                _values[i] = ndv;
                int j = i;
                int r;
                do
                {
                    i = (i - 1) & _mask;
                    if (_values[i] == ndv)
                    {
                        return true;
                    }

                    r = Hash(_values[i]);
                }
                while ((i <= r && r < j) || (r < j && j < i) || (j < i && i <= r));
                _values[j] = _values[i];
            }
        }

        public override bool Add(int value)
        {
            if (value == ndv)
            {
                throw new ArgumentException("Can't add the 'no data' value");
            }

            int i = IndexOf(value);
            if (_values[i] == ndv)
            {
                ++_size;
                _values[i] = value;
                if (_size > MAX_SIZE)
                {
                    throw new InvalidOperationException("Too many elements (> " + MAX_SIZE + ')');
                }

                if (_nlo < _size && _size <= _nhi)
                {
                    SetCapacity(_size);
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        private int Hash(int key)
        {

            // Knuth, v. 3, 509-510. Randomize the 31 low-order bits of c*key
            // and return the highest nbits (where nbits <= 30) bits of these.
            // The constant c = 1327217885 approximates 2^31 * (sqrt(5)-1)/2.
            return ((1327217885 * key) >> _shift) & _mask;
        }

        private int IndexOf(int value)
        {
            int i = Hash(value);
            while (_values[i] != ndv)
            {
                if (_values[i] == value)
                {
                    return i;
                }

                i = (i - 1) & _mask;
            }

            return i;
        }

        private void SetCapacity(int capacity)
        {

            // Fixed load factor 0.25, kept in integer arithmetic (nmax < capacity * 4) —
            // unlike the IntHashMap/IntToIntHashMap siblings there is no configurable factor.
            if (capacity < _size)
            {
                capacity = _size;
            }

            int nbit, nmax;
            for (nbit = 1, nmax = 2; nmax < capacity * 4 && nmax < MAX_SIZE; ++nbit, nmax *= 2)
            {
            }

            int nold = _nmax;
            if (nmax == nold)
            {
                return;
            }

            _nmax = nmax;
            _nlo = nmax / 4;
            _nhi = MAX_SIZE / 4;
            _shift = 1 + NBIT - nbit;
            _mask = nmax - 1;
            _size = 0;
            int[] values = _values;
            _values = new int[nmax];
            ArrayTools.Fill(_values, ndv);
            if (values != null)
            {
                for (int i = 0; i < nold; ++i)
                {
                    int value = values[i];
                    if (value != ndv)
                    {

                        // Don't use add, because the capacity is necessarily large enough,
                        // and the value is necessarily unique (since in this set already)!
                        ++_size;
                        _values[IndexOf(value)] = value;
                    }
                }
            }
        }

        public override IIntIterator IIterator()
        {
            return new IntHashSetIterator(this);
        }

        public static bool ContainsSome(IntSet one, IntSet two)
        {
            if (two is IntEmptySet)
            {
                return false;
            }

            if (two is IntUniversalSet)
            {
                return !one.IsEmpty();
            }

            if (two is IntComplementSet)
            {
                return !((IntComplementSet)two).Exclusions.ContainsAll(one);
            }

            IIntIterator it = two.IIterator();
            while (it.MoveNext())
            {
                if (one.Contains(it.Current))
                {
                    return true;
                }
            }

            return false;
        }

        // Any IntSet implementation compares equal by contents (upstream casts to IntSet here;
        // narrowing to IntHashSet would throw on IntArraySet/IntRangeSet/... arguments).
        public override bool Equals(object other)
        {
            if (other is IntSet s)
            {
                return (Size() == s.Count && ContainsAll(s));
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {

            // Note, hashcodes are the same as those used by IntArraySet
            int h = 936247625;
            IIntIterator it = IIterator();
            while (it.MoveNext())
            {
                h += it.Current;
            }

            return h;
        }

        public override string ToString()
        {
            return Stringify(IIterator());
        }

        public static string Stringify(IIntIterator it)
        {
            StringBuilder sb = new StringBuilder(100);
            while (it.MoveNext())
            {
                if (sb.Length == 0)
                {
                    sb.Append(it.Current);
                }
                else
                {
                    sb.Append(' ').Append(it.Current);
                }
            }

            return sb.ToString();
        }

        public static IntHashSet Of(params int[] members)
        {
            IntHashSet @is = new IntHashSet(members.Length);
            foreach (int i in members)
            {
                @is.Add(i);
            }

            return @is;
        }

        private sealed class IntHashSetIterator : AbstractIntIterator
        {
            private readonly IntHashSet container;
            private int i;
            public IntHashSetIterator(IntHashSet container)
            {
                this.container = container;
                i = 0;
            }

            public override bool HasNext()
            {
                while (i < container._values.Length)
                {
                    if (container._values[i] != container.ndv)
                    {
                        return true;
                    }
                    else
                    {
                        i++;
                    }
                }

                return false;
            }

            public override int Next()
            {
                return container._values[i++];
            }
        }
    }
}
