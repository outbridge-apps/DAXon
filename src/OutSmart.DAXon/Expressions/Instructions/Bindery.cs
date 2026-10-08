////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions.Instructions
{
    public sealed class Bindery
    {
        internal readonly object syncLock = new object();
        private SlotManager slotManager;
        private IGroundedValue[] globals; // values of global variables and parameters

        public IGroundedValue[] GlobalVariables => globals;
        public Bindery(PackageData pack)
        {
            this.slotManager = pack.GlobalSlotManager;
            AllocateGlobals(slotManager);
        }

        private void AllocateGlobals(SlotManager map)
        {
            int n = map.NumberOfVariables + 1;
            globals = new IGroundedValue[n];
            for (int i = 0; i < n; i++)
            {
                globals[i] = null;
            }
        }

        public void SetGlobalVariable(GlobalVariable binding, IGroundedValue value)
        {
            globals[binding.BinderySlotNumber] = value;
        }

        public IGroundedValue SaveGlobalVariableValue(GlobalVariable binding, IGroundedValue value)
        {
            lock (syncLock)
            {
                int slot = binding.BinderySlotNumber;
                if (globals[slot] != null)
                {

                    // another thread has already evaluated the value
                    return globals[slot];
                }
                else
                {
                    globals[slot] = value;
                    return value;
                }
            }
        }

        public void SetGlobalVariableValue(int slot, IGroundedValue value)
        {
            globals[slot] = value;
        }

        public IGroundedValue GetGlobalVariableValue(GlobalVariable binding)
        {
            return globals[binding.BinderySlotNumber];
        }

        public IGroundedValue GetGlobalVariable(int slot)
        {
            return globals[slot];
        }

        internal sealed class FailureValue : ObjectValue<XPathException>
        {
            public FailureValue(XPathException err) : base(new XPathException(err?.Message))
            {
            }
        }
    }
}