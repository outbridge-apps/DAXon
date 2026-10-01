////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Trees.Tiny;

namespace OutSmart.DAXon.Model
{
    // Upstream condenses duplicate text/attribute values to save memory; this port builds a regular
    // tiny tree (functionally identical, no condensation). It USED to be a bare stub whose implicit
    // conversion to Builder THREW - selecting the TINY_TREE_CONDENSED tree model crashed every build.
    internal sealed class TinyBuilderCondensed : TinyBuilder
    {
        public TinyBuilderCondensed(PipelineConfiguration pipe) : base(pipe)
        {
        }
    }
}
