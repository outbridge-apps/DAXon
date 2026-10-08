////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Lib
{
    public interface ITraceListener : IDisposable
    {
        void SetOutputDestination(Logger stream);


        void Open(Controller controller);


        void Close();


        void Enter(ITraceable instruction, Dictionary<string, object> properties, IXPathContext context);


        void Leave(ITraceable instruction);


        void StartCurrentItem(IItem currentItem);


        void EndCurrentItem(IItem currentItem);


        object Checkpoint();



        void Recover(object checkpoint, XPathException err);


        void StartRuleSearch();


        void EndRuleSearch(object rule, Mode mode, IItem item);

    }
}
