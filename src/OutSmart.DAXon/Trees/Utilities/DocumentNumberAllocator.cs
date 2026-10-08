////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Trees.Utilities
{
    public class DocumentNumberAllocator
    {
        private readonly object syncLock = new object();
        // Changed to a long in Saxon 9.4, because a user reported an int overflowing
        // on a system that had been in live operation for several months. The effect wasn't fatal,
        // but could cause incorrect node identity tests.
        private long nextDocumentNumber;
        // Negative document numbers are used for streamed documents. This means that streamed
        // nodes always precede unstreamed nodes in document order. We take advantage of this
        // when sorting a sequence that contains both streamed and unstreamed nodes.
        private long nextStreamedDocumentNumber = -2; // -1 is special
        public virtual long AllocateDocumentNumber()
        {
            lock (syncLock)
            {
                return nextDocumentNumber++;
            }
        }

        public virtual long AllocateStreamedDocumentNumber()
        {
            lock (syncLock)
            {
                return nextStreamedDocumentNumber--;
            }
        }
    }
}