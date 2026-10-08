////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
namespace OutSmart.DAXon.Transformation
{
    /// <summary>
    /// Exception thrown when there are problems with the license file
    /// </summary>
    internal sealed class LicenseException : Exception
    {
        public const int NOT_FOUND = 3;
        public const int WRONG_CONFIGURATION = 6;
        public LicenseException(string message) : base(message)
        {
        }
    }
}