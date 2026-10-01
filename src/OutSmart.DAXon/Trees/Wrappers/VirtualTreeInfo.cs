////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Model;
using System;
using System.Collections.Generic;

namespace OutSmart.DAXon.Trees.Wrappers
{
    /// <summary>
    /// Implementation of TreeInfo for a Virtual Copy tree
    /// </summary>
    internal sealed class VirtualTreeInfo : GenericTreeInfo
    {
        private bool copyAccumulators;

        public override IEnumerator<string> UnparsedEntityNames => ((VirtualCopy)GetRootNode()).OriginalNode.GetTreeInfo().UnparsedEntityNames;

        public VirtualTreeInfo(Configuration config) : base(config)
        {
        }

        public VirtualTreeInfo(Configuration config, VirtualCopy vc) : base(config, vc)
        {
        }

        public void SetCopyAccumulators(bool copy)
        {
            this.copyAccumulators = copy;
        }

        public bool IsCopyAccumulators() => copyAccumulators;

        public override String[] GetUnparsedEntity(string name)
        {
            return ((VirtualCopy)GetRootNode()).OriginalNode.GetTreeInfo().GetUnparsedEntity(name);
        }
    }
}
