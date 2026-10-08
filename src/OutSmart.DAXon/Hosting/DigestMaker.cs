////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Globalization;
using System.Text;
namespace OutSmart.DAXon.Lib
{
    internal sealed class DigestMaker
    {
        private string hexDigest = null;
        private readonly System.Security.Cryptography.SHA256 digest = System.Security.Cryptography.SHA256.Create();

        public string Digest
        {
            get
            {

                // The hash can only be finalized once
                if (hexDigest == null)
                {
                    digest.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                    // Java's String.format("%064x", new BigInteger(1, bytes)) == the 32 digest bytes as
                    // lowercase hex, zero-padded to 64. (C# String.Format has no %-conversions -- the
                    // literal translation returned the string "%064x" for every checksum.)
                    StringBuilder sb = new StringBuilder(64);
                    foreach (byte b in digest.Hash)
                    {
                        sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                    }
                    hexDigest = sb.ToString();
                }

                return hexDigest;
            }
        }

        public void Update(int value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(Convert.ToString(value, CultureInfo.InvariantCulture));
            digest.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }

        public void Update(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            digest.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }
    }
}