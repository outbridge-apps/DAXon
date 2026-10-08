////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
namespace OutSmart.DAXon.Api.Push
{
    public interface IContainer : IDisposable
    {
        void SetDefaultNamespace(string uri);
        IElement Element(QName name);
        IElement Element(string name);
        IContainer Text(string value);
        IContainer Comment(string value);
        IContainer ProcessingInstruction(string name, string value);
        void Close();
    }
}