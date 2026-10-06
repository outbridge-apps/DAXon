////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.ObjectModel;

namespace OutSmart.DAXon.Api
{
    /// <summary>
    /// The host rules of <see cref="ProcessorOptions"/>. Read-only once a Processor has taken the options.
    /// </summary>
    public sealed class HostRuleCollection : Collection<HostRule>
    {
        private volatile bool frozen;

        internal HostRuleCollection()
        {
        }

        internal void Freeze()
        {
            frozen = true;
        }

        protected override void InsertItem(int index, HostRule item)
        {
            ThrowIfFrozen();
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            base.InsertItem(index, item);
        }

        protected override void SetItem(int index, HostRule item)
        {
            ThrowIfFrozen();
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            base.SetItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            ThrowIfFrozen();
            base.RemoveItem(index);
        }

        protected override void ClearItems()
        {
            ThrowIfFrozen();
            base.ClearItems();
        }

        private void ThrowIfFrozen()
        {
            if (frozen)
            {
                throw new InvalidOperationException("These host rules belong to ProcessorOptions a Processor has taken and can no longer change.");
            }
        }
    }
}
