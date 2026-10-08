////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Lib
{
    /// <summary>
    /// Interface allowing localization modules for different languages to be dynamically loaded
    /// </summary>
    public abstract class LocalizerFactory
    {
        public virtual void SetLanguageProperties(string lang, Properties properties)
        {
        }

        public abstract INumberer GetNumberer(string language, string country);
        public virtual LocalizerFactory Copy()
        {
            return this;
        }
    }
}