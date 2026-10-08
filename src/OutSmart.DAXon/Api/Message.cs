////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Api
{
    public class Message
    {
        private readonly XdmNode _content;
        private readonly QName _errorCode;
        private readonly bool _terminate;
        private readonly ILocation _location;

        public virtual XdmNode Content => _content;
        public Message(XdmNode content, QName errorCode, bool terminate, ILocation location)
        {
            this._content = content;
            this._errorCode = errorCode;
            this._terminate = terminate;
            this._location = location;
        }

        public virtual string GetStringValue()
        {
            return _content.GetStringValue();
        }

        public override string ToString()
        {
            return _content.ToString();
        }

        public virtual QName GetErrorCode()
        {
            return _errorCode;
        }

        public virtual bool IsTerminate()
        {
            return _terminate;
        }

        public virtual ILocation GetLocation()
        {
            return _location;
        }
    }
}