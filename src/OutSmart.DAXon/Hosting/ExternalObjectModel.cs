////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;

using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Serialization;
namespace OutSmart.DAXon.Lib
{
    public interface IExternalObjectModel
    {
        string DocumentClassName { get; }
        string IdentifyingURI { get; }
        PJConverter GetPJConverter(System.Type targetClass);
        JPConverter GetJPConverter(System.Type sourceClass, Configuration config);
        PJConverter GetNodeListCreator(object node);
        IReceiver GetDocumentBuilder(IResultTarget result);
    }
}
