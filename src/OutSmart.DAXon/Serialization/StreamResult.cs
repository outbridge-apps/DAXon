////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
namespace OutSmart.DAXon.Serialization
{
    // Holder for a byte stream, character writer and/or system ID that serialized output
    // is written to.
    public class StreamResult : IResultTarget
    {
        public string SystemId { get; set; }
        public System.IO.Stream OutputStream { get; set; }
        public System.IO.TextWriter Writer { get; set; }
        public StreamResult() { }
        public StreamResult(string systemId) { SystemId = systemId; }
        public StreamResult(System.IO.Stream s) { OutputStream = s; }
        public StreamResult(System.IO.TextWriter w) { Writer = w; }
        public string GetSystemId() => SystemId;
        public void SetSystemId(string s) { SystemId = s; }
        public System.IO.Stream GetOutputStream() => OutputStream;
        public System.IO.TextWriter GetWriter() => Writer;
        public void SetWriter(System.IO.TextWriter w) { Writer = w; }
        public void SetOutputStream(System.IO.Stream s) { OutputStream = s; }
    }
}
