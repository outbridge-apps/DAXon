////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Misc Java stdlib types Saxon references but we don't yet shim individually.

using System;
using System.Collections.Generic;
using System.IO;

namespace OutSmart.DAXon.Internal.Charsets
{
    // 2026-06-10: Java charset-decode exception hierarchy (UnparsedTextFunction.GetErrorCode FOUT1190/1200
    // discrimination). Java: CharacterCodingException extends IOException; Malformed/Unmappable extend it.
    // IO-removal: compat IO.IOException eliminated -> extend System.IO.IOException (Java CharacterCodingException
    // extends IOException; Malformed/Unmappable extend this). Faithful: keeps the FOUT1190/1200 discrimination chain.
    internal class CharacterCodingException : IOException
    {
        public CharacterCodingException() : base("character coding error") { }
    }
}
