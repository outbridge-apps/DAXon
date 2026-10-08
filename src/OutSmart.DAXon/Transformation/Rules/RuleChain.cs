////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Transformation.Rules
{
    public class RuleChain
    {
        private Rule _head;
        public object optimizationData; // give this a better type
        /// <summary>
        /// Create an empty rule chain
        /// </summary>
        public RuleChain()
        {
            _head = null;
        }

        public RuleChain(Rule head)
        {
            this._head = head;
        }

        public virtual Rule Head()
        {
            return _head;
        }

        public virtual void SetHead(Rule head)
        {
            this._head = head;
        }

        public virtual int GetLength()
        {
            int i = 0;
            Rule r = Head();
            while (r != null)
            {
                i++;
                r = r.Next;
            }

            return i;
        }

        public virtual bool HasOptimizationData()
        {
            return optimizationData != null;
        }
    }
}