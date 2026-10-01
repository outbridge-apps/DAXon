////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Functions;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Trees.Linked
{
    internal sealed class LineNumberMap
    {
        private readonly object syncLock = new object();
        private int[] sequenceNumbers;
        private int[] lineNumbers;
        private int[] columnNumbers;
        private int allocated;
        /// <summary>
        /// Create a LineNumberMap with an initial capacity of 200 nodes, which is expanded as necessary
        /// </summary>
        public LineNumberMap()
        {
            sequenceNumbers = new int[200];
            lineNumbers = new int[200];
            columnNumbers = new int[200];
            allocated = 0;
        }

        public void SetLineAndColumn(int sequence, int line, int column)
        {
            if (sequenceNumbers.Length <= allocated + 1)
            {
                Array.Resize(ref sequenceNumbers, allocated * 2);
                Array.Resize(ref lineNumbers, allocated * 2);
                Array.Resize(ref columnNumbers, allocated * 2);
            }

            sequenceNumbers[allocated] = sequence;
            lineNumbers[allocated] = line;
            columnNumbers[allocated] = column;
            allocated++;
        }

        public int GetLineNumber(int sequence)
        {
            if (sequenceNumbers.Length > allocated)
            {
                Condense();
            }

            int index = Array.BinarySearch(sequenceNumbers, sequence);
            if (index < 0)
            {
                index = -index - 1;
                if (index > lineNumbers.Length - 1)
                {
                    index = lineNumbers.Length - 1;
                }
            }

            return lineNumbers[index];
        }

        public int GetColumnNumber(int sequence)
        {
            if (sequenceNumbers.Length > allocated)
            {
                Condense();
            }

            int index = Array.BinarySearch(sequenceNumbers, sequence);
            if (index < 0)
            {
                index = -index - 1;
                if (index >= columnNumbers.Length)
                {
                    index = columnNumbers.Length - 1;
                }
            }

            return columnNumbers[index];
        }

        private void Condense()
        {
            lock (syncLock)
            {
                Array.Resize(ref sequenceNumbers, allocated);
                Array.Resize(ref lineNumbers, allocated);
                Array.Resize(ref columnNumbers, allocated);
            }
        }
    }
}