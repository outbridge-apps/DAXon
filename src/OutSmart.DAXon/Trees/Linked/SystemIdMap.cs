////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
namespace OutSmart.DAXon.Trees.Linked
{
    internal sealed class SystemIdMap
    {
        private int[] sequenceNumbers;
        private string[] uris;
        private int allocated;
        public SystemIdMap()
        {
            sequenceNumbers = new int[4];
            uris = new string[4];
            allocated = 0;
        }

        public void SetSystemId(int sequence, string uri)
        {
            if (allocated > 0)
            {

                // ignore it if same as previous
                if (uri.Equals(uris[allocated - 1]))
                {
                    return;
                }

                if (sequence <= sequenceNumbers[allocated - 1])
                {
                    throw new ArgumentException("System IDs of nodes are immutable");
                }
            }

            if (sequenceNumbers.Length <= allocated + 1)
            {
                Array.Resize(ref sequenceNumbers, allocated * 2);
                Array.Resize(ref uris, allocated * 2);
            }

            sequenceNumbers[allocated] = sequence;
            uris[allocated] = uri;
            allocated++;
        }

        public string GetSystemId(int sequence)
        {
            if (allocated == 0)
            {
                return null;
            }


            // could use a binary chop, but it's not important
            for (int i = 1; i < allocated; i++)
            {
                if (sequenceNumbers[i] > sequence)
                {
                    return uris[i - 1];
                }
            }

            return uris[allocated - 1];
        }
    }
}