////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Internal.Net;
using System;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Api
{
    public interface IDestination : IDisposable
    {
        URI DestinationBaseURI { get; set; }
        IReceiver GetReceiver(PipelineConfiguration pipe, SerializationProperties @params);
        void OnClose(IAction listener);
        void CloseAndNotify();
        void Close();
    }
}