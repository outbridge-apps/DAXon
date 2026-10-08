////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
namespace OutSmart.DAXon.Api
{
    // An error met while a result is read item by item: Java needed a second, unchecked type for what an iterator
    // throws. A DAXonApiException since 1.4, so one catch takes both and the error code is read the same way;
    // before, an Exception apart whose Message was the cause's whole ToString(), stack trace included.
    public class DAXonApiUncheckedException : DAXonApiException
    {
        public DAXonApiUncheckedException(Exception err) : base(err)
        {
        }
    }
}
