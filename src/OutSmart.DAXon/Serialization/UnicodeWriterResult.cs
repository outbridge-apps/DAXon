////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Text;

namespace OutSmart.DAXon.Serialization
{
    // Runtime: functional — Serialize wraps a UnicodeBuilder in this and SerializerFactory.GetReceiver
    // (SerializerFactory.cs:145/147) does `result is UnicodeWriterResult` -> GetUnicodeWriter() to obtain the
    // output sink. Implements IResultTarget (GetSystemId/SetSystemId only). Mirrors the excluded
    // real UnicodeWriterResult.cs (kept excluded to avoid the dual-type CS0101 with this stub).
    internal sealed class UnicodeWriterResult : IResultTarget
    {
        private readonly IUnicodeWriter _writer;
        private string _systemId;
        public IUnicodeWriter UnicodeWriter => _writer;
        public UnicodeWriterResult(IUnicodeWriter unicodeWriter, string systemId) { _writer = unicodeWriter; _systemId = systemId; }
        public string GetSystemId() => _systemId;
        public void SetSystemId(string systemId) { _systemId = systemId; }
    }
}
