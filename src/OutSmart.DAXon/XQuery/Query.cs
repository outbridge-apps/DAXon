////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.IO;


namespace OutSmart.DAXon.XQuery
{
    // Real Query.cs (the XQuery command-line main) is excluded; XsltPackage.Save uses only
    // the static utility Query.CreateFileIfNecessary. Faithful re-impl (identical to Query.cs:1298).
    internal sealed class Query
    {
        // IO-removal: compat File eliminated -> path string + System.IO statics (faithful to Query.cs:1298).
        public static void CreateFileIfNecessary(string file)
        {
            if (!(File.Exists(file) || Directory.Exists(file)))
            {
                string directory = Path.GetDirectoryName(file);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                using (OutSmart.DAXon.Internal.Streams.FileNames.Create(file)) { }
            }
        }
    }
}
