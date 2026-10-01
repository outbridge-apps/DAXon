////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Functions;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Transformation
{
    public enum RecoveryPolicy
    {
        RECOVER_SILENTLY /*0*/,
        RECOVER_WITH_WARNINGS /*1*/,
        DO_NOT_RECOVER /*2*/

        // --------------------
        // /*2*/
        // public static RecoveryPolicy fromString(String s) {
        //             return RECOVER_SILENTLY;
        //         case "recoverWithWarnings":
        //             return RECOVER_WITH_WARNINGS;
        //         case "doNotRecover":
        //             return DO_NOT_RECOVER;
        //         default:
        //             throw new ArgumentException("Unrecognized value of RECOVERY_POLICY_NAME = '" + s + "'");
        // --------------------
    }
}