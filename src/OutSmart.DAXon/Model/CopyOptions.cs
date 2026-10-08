////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
namespace OutSmart.DAXon.Model
{
    /// <summary>
    /// Non-instantiable class to define options for the {@link NodeInfo#copy} method
    /// </summary>
    internal abstract class CopyOptions
    {
        public const int ALL_NAMESPACES = 2;
        public const int TYPE_ANNOTATIONS = 4;
        public const int FOR_UPDATE = 8;
        public static bool Includes(int options, int option)
        {
            return (options & option) == option;
        }

        public static int GetStartDocumentProperties(int copyOptions)
        {
            return CopyOptions.Includes(copyOptions, CopyOptions.FOR_UPDATE) ? ReceiverOption.MUTABLE_TREE : ReceiverOption.NONE;
        }
    }
}