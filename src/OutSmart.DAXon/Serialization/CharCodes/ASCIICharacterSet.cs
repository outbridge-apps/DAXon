////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Serialization.CharCodes
{
    /// <summary>
    /// This class defines properties of the US-ASCII character set
    /// </summary>
    internal sealed class ASCIICharacterSet : ICharacterSet
    {
        public static readonly ASCIICharacterSet theInstance = new ASCIICharacterSet();

        public string CanonicalName => "US-ASCII";
        private ASCIICharacterSet()
        {
        }

        public static ASCIICharacterSet GetInstance()
        {
            return theInstance;
        }

        public bool InCharset(int c)
        {
            return c <= 0x7f;
        }
    }
}