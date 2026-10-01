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
    /// This class defines properties of the ISO-8859-1 character set
    /// </summary>
    internal sealed class ISO88591CharacterSet : ICharacterSet
    {
        private static readonly ISO88591CharacterSet theInstance = new ISO88591CharacterSet();

        public string CanonicalName => "ISO-8859-1";
        private ISO88591CharacterSet()
        {
        }

        public static ISO88591CharacterSet GetInstance()
        {
            return theInstance;
        }

        public bool InCharset(int c)
        {
            return c <= 0xff;
        }
    }
}