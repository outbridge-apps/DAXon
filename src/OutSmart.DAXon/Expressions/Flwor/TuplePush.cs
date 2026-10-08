////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
namespace OutSmart.DAXon.Expressions.Flwor
{
    public abstract class TuplePush
    {
        private readonly Outputter outputter;
        protected TuplePush(Outputter outputter)
        {
            this.outputter = outputter;
        }

        protected virtual Outputter GetOutputter()
        {
            return outputter;
        }

        public abstract void ProcessTuple(IXPathContext context);
        public virtual void Dispose()
        {
        }
    }
}