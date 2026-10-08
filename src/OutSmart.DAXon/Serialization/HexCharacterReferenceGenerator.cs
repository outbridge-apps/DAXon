////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Text;
using System.Globalization;
namespace OutSmart.DAXon.Serialization
{
    internal sealed class HexCharacterReferenceGenerator : ICharacterReferenceGenerator
    {
        public static readonly HexCharacterReferenceGenerator THE_INSTANCE = new HexCharacterReferenceGenerator();
        private HexCharacterReferenceGenerator()
        {
        }

        public void OutputCharacterReference(int charval, IUnicodeWriter writer)
        {
            writer.WriteCodePoint('&');
            writer.WriteCodePoint('#');
            writer.WriteCodePoint('x');
            writer.Write((charval).ToString("x", CultureInfo.InvariantCulture));
            writer.WriteCodePoint(';');
        }
    }
}