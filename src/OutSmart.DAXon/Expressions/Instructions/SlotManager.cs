////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using System.Collections.Generic;
namespace OutSmart.DAXon.Expressions.Instructions
{
    public class SlotManager
    {
        /// <summary>
        /// An empty SlotManager
        /// </summary>
        public static SlotManager EMPTY = new SlotManager(0);
        private readonly List<StructuredQName> variableMap;
        // values are StructuredQName objects representing the variable names
        private int numberOfVariables;

        public virtual int NumberOfVariables
        {
            get => numberOfVariables; set
            {
                this.numberOfVariables = value;
                /* TrimToSize: noop on List<T> -- variableMap */
            }
        }

        public virtual IList<StructuredQName> VariableMap => variableMap;
        public SlotManager()
        {
            numberOfVariables = 0;
            variableMap = new List<StructuredQName>();
        }

        public SlotManager(int n)
        {
            numberOfVariables = n;
            variableMap = new List<StructuredQName>(n);
        }

        public virtual int AllocateSlotNumber(StructuredQName qName, ILocalBinding binding)
        {
            variableMap.Add(qName);
            return numberOfVariables++;
        }

        public virtual void ShowStackFrame(IXPathContext context, Logger logger)
        {
        }
    }
}
