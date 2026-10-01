////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Api;using OutSmart.DAXon.Functions;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Core;
namespace OutSmart.DAXon.Expressions.Parsing
{
    internal sealed class Loc : ILocation
    {
        public static Loc NONE = new Loc(null, -1, -1);
        private readonly string systemId;
        private readonly int lineNumber;
        private readonly int columnNumber;
        public Loc(ILocation loc)
        {
            systemId = loc.GetSystemId();
            lineNumber = loc.GetLineNumber();
            columnNumber = loc.GetColumnNumber();
        }

        public Loc(string systemId, int lineNumber, int columnNumber)
        {
            this.systemId = systemId;
            this.lineNumber = lineNumber;
            this.columnNumber = columnNumber;
        }

        public string GetSystemId()
        {
            return systemId;
        }

        public string GetPublicId()
        {
            return null;
        }

        public int GetLineNumber()
        {
            return lineNumber;
        }

        public int GetColumnNumber()
        {
            return columnNumber;
        }

        public ILocation SaveLocation()
        {
            return this;
        }
    }
}