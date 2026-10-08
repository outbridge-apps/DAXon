////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Text;
namespace OutSmart.DAXon.Collections
{
    // Deliberate triplet with IntHashMap/IntHashSet (C1): same Knuth open-addressing core, kept
    // as three copies because the empty-slot encoding differs per class — here any int value is
    // valid, so occupancy needs the explicit _filled[] array. The probe loops sit on hot paths
    // and must stay monomorphic, without a shared dispatching core.
    internal sealed class IntToIntHashMap : IIntToIntMap
    {
        private const int NBIT = 30; // NMAX = 2^NBIT
        private const int NMAX = 1 << NBIT; // maximum number of keys mapped
        private readonly double _factor; // 0.0 <= _factor <= 1.0
        private int _defaultValue = int.MaxValue;
        private int _nmax; // 0 <= _nmax = 2^nbit <= 2^NBIT = NMAX
        private int _n; // 0 <= _n <= _nmax <= NMAX
        private int _nlo; // _nmax*_factor (_n<=_nlo, if possible)
        private int _nhi; //  NMAX*_factor (_n< _nhi, if possible)
        private int _shift; // _shift = 1 + NBIT - nbit (see function hash() below)
        private int _mask; // _mask = _nmax - 1
        private int[] _key; // array[_nmax] of keys
        private int[] _value; // array[_nmax] of values
        private bool[] _filled; // _filled[i]==true iff _key[i] is mapped

        public int this[int key]
        {
            get { return Get(key); }
            set { Put(key, value); }
        }

        public int DefaultValue
        {
            get => _defaultValue; set
            {
                _defaultValue = value;
            }
        }

        public IntToIntHashMap() : this(8, 0.25)
        {
        }

        public IntToIntHashMap(int capacity) : this(capacity, 0.25)
        {
        }

        public IntToIntHashMap(int capacity, double factor)
        {
            _factor = factor;
            SetCapacity(capacity);
        }

        public void Clear()
        {
            _n = 0;
            for (int i = 0; i < _nmax; ++i)
            {
                _filled[i] = false;
            }
        }

        public bool Contains(int key)
        {
            return _filled[IndexOf(key)];
        }

        public int Get(int key)
        {
            int i = IndexOf(key);
            return _filled[i] ? _value[i] : _defaultValue;
        }

        public int Size()
        {
            return _n;
        }

        public bool Remove(int key)
        {

            // Knuth, v. 3, 527, Algorithm R.
            int i = IndexOf(key);
            if (!_filled[i])
            {
                return false;
            }

            --_n;
            for (; ; )
            {
                _filled[i] = false;
                int j = i;
                int r;
                do
                {
                    i = (i - 1) & _mask;
                    if (!_filled[i])
                    {
                        return true;
                    }

                    r = Hash(_key[i]);
                }
                while ((i <= r && r < j) || (r < j && j < i) || (j < i && i <= r));
                _key[j] = _key[i];
                _value[j] = _value[i];
                _filled[j] = _filled[i];
            }
        }

        public void Put(int key, int value)
        {
            int i = IndexOf(key);
            if (_filled[i])
            {
                _value[i] = value;
            }
            else
            {
                _key[i] = key;
                _value[i] = value;
                _filled[i] = true;
                Grow();
            }
        }

        public IIntIterator KeyIterator()
        {
            return new IntToIntHashMapKeyIterator(this);
        }

        private int Hash(int key)
        {

            // Knuth, v. 3, 509-510. Randomize the 31 low-order bits of c*key
            // and return the highest nbits (where nbits <= 30) bits of these.
            // The constant c = 1327217885 approximates 2^31 * (sqrt(5)-1)/2.
            return ((1327217885 * key) >> _shift) & _mask;
        }

        private int IndexOf(int key)
        {
            int i = Hash(key);
            while (_filled[i])
            {
                if (_key[i] == key)
                {
                    return i;
                }

                i = (i - 1) & _mask;
            }

            return i;
        }

        private void Grow()
        {
            ++_n;
            if (_n > NMAX)
            {
                throw new InvalidOperationException("number of keys mapped exceeds " + NMAX);
            }

            if (_nlo < _n && _n <= _nhi)
            {
                SetCapacity(_n);
            }
        }

        private void SetCapacity(int capacity)
        {
            if (capacity < _n)
            {
                capacity = _n;
            }

            double factor = (_factor < 0.01) ? 0.01 : (_factor > 0.99) ? 0.99 : _factor;
            int nbit, nmax;
            for (nbit = 1, nmax = 2; nmax * factor < capacity && nmax < NMAX; ++nbit, nmax *= 2)
            {
            }

            int nold = _nmax;
            if (nmax == nold)
            {
                return;
            }

            _nmax = nmax;
            _nlo = (int)(nmax * factor);
            _nhi = (int)(NMAX * factor);
            _shift = 1 + NBIT - nbit;
            _mask = nmax - 1;
            int[] key = _key;
            int[] value = _value;
            bool[] filled = _filled;
            _n = 0;
            _key = new int[nmax];
            _value = new int[nmax];
            _filled = new bool[nmax];
            if (key != null)
            {
                for (int i = 0; i < nold; ++i)
                {
                    if (filled[i])
                    {
                        Put(key[i], value[i]);
                    }
                }
            }
        }

        // Diagnostics only; capped at ~100 entries.
        public override string ToString()
        {
            StringBuilder buffer = new StringBuilder(256);
            buffer.Append('{');
            IIntIterator keys = KeyIterator();
            int count = 0;
            while (keys.MoveNext())
            {
                int k = keys.Current;
                int v = Get(k);
                buffer.Append(" " + k + ":" + v + ",");
                if (count++ >= 100)
                {
                    buffer.Append("....");
                    break;
                }
            }

            buffer[buffer.Length - 1] = '}';
            return buffer.ToString();
        }

        private sealed class IntToIntHashMapKeyIterator : AbstractIntIterator
        {
            private readonly IntToIntHashMap map;
            private int i = 0;
            public IntToIntHashMapKeyIterator(IntToIntHashMap map)
            {
                this.map = map;
                i = 0;
            }

            public override bool HasNext()
            {
                while (i < map._key.Length)
                {
                    if (map._filled[i])
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
                return map._key[i++];
            }
        }
    }
}
