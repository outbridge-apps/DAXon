////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Api
{
    // A location is a native Saxon concept: it does NOT extend the JAXP javax.xml.transform.SourceLocator
    // nor the SAX org.xml.sax.Locator (both removed for the de-Java effort). It declares its own accessors.
    public interface ILocation
    {
        string GetSystemId();
        string GetPublicId();
        int GetLineNumber();
        int GetColumnNumber();
        ILocation SaveLocation();
    }
}