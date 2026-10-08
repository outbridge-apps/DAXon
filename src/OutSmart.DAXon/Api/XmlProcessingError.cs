////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using System;
namespace OutSmart.DAXon.Api
{
    public interface IXmlProcessingError
    {
        HostLanguage GetHostLanguage();
        bool IsStaticError();
        bool IsTypeError();
        QName GetErrorCode();
        string GetMessage();
        ILocation GetLocation();
        string ModuleUri { get; }



        Expression GetFailingExpression();
        bool IsWarning();
        string GetPath();
        Exception GetCause();
        IXmlProcessingError AsWarning();
        string TerminationMessage { get; set; }
        bool IsAlreadyReported();
        void SetAlreadyReported(bool reported);
    }
}
