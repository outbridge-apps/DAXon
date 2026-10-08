////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;

namespace OutSmart.DAXon.Functions.Registry
{
    // ParamKeywords -- function parameter naming maps (used by BuiltInFunctionSet.GetParamMap).
    internal static class ParamKeywords
    {
        public static readonly Dictionary<string, string> fnParamNames = new Dictionary<string, string>();
        public static readonly Dictionary<string, string> mapParamNames = new Dictionary<string, string>();
        public static readonly Dictionary<string, string> arrayParamNames = new Dictionary<string, string>();
        // BuiltInFunctionSet references mathParamNames.
        public static readonly Dictionary<string, string> mathParamNames = new Dictionary<string, string>();
    }
}
