////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;

namespace OutSmart.DAXon.Api
{
    /// <summary>
    /// Settings fixed when a <see cref="Processor"/> is created. The processor freezes the options
    /// (and their <see cref="Resources"/>); frozen options can be passed to further processors.
    /// </summary>
    public sealed class ProcessorOptions
    {
        private TimeSpan? transformTimeout;
        private ResourceAccessPolicy resources;
        private volatile bool frozen;

        /// <summary>Wall-clock limit per engine call; null for the default (1 minute), TimeSpan.Zero or negative for none.</summary>
        public TimeSpan? TransformTimeout
        {
            get => transformTimeout;
            set
            {
                ThrowIfFrozen();
                transformTimeout = value;
            }
        }

        /// <summary>What stylesheets and queries may reach; null for a default policy, which allows everything.</summary>
        public ResourceAccessPolicy Resources
        {
            get => resources;
            set
            {
                ThrowIfFrozen();
                resources = value;
            }
        }

        /// <summary>True once a Processor has taken the options (or <see cref="Freeze"/> was called); setters then throw.</summary>
        public bool IsFrozen => frozen;

        /// <summary>Makes the options and their policy immutable; a null <see cref="Resources"/> becomes the default policy.</summary>
        public void Freeze()
        {
            if (frozen)
            {
                return;
            }

            if (resources == null)
            {
                resources = new ResourceAccessPolicy();
            }

            resources.Freeze();
            frozen = true;
        }

        private void ThrowIfFrozen()
        {
            if (frozen)
            {
                throw new InvalidOperationException("The ProcessorOptions are frozen: a Processor uses them, so they can no longer change.");
            }
        }
    }
}
