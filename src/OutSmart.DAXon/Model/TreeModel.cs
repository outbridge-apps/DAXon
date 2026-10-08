////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Trees.Linked;
using OutSmart.DAXon.Trees.Tiny;
using System;
namespace OutSmart.DAXon.Model
{
    public abstract class TreeModel
    {
        public static readonly TreeModel TINY_TREE = new TinyTree();
        public static readonly TreeModel TINY_TREE_CONDENSED = new TinyTreeCondensed();
        public static readonly TreeModel LINKED_TREE = new LinkedTree();
        public static readonly TreeModel IMMUTABLE_LINKED_TREE = new LinkedTree(false);
        public virtual int SymbolicValue => Builder.UNSPECIFIED_TREE_MODEL;

        public virtual string Name => ToString();
        public abstract Builder MakeBuilder(PipelineConfiguration pipe);

        public static TreeModel GetTreeModel(int symbolicValue)
        {
            switch (symbolicValue)
            {
                case Builder.TINY_TREE:
                    return TreeModel.TINY_TREE;
                case Builder.TINY_TREE_CONDENSED:
                    return TreeModel.TINY_TREE_CONDENSED;
                case Builder.LINKED_TREE:
                    return TreeModel.LINKED_TREE;
                default:
                    throw new ArgumentException("tree model " + symbolicValue);
            }
        }

        public virtual bool IsMutable()
        {
            return false;
        }

        public virtual bool IsSchemaAware()
        {
            return false;
        }

        private sealed class TinyTree : TreeModel
        {

            public override int SymbolicValue => Builder.TINY_TREE;

            public override string Name => "TinyTree";
            public override Builder MakeBuilder(PipelineConfiguration pipe)
            {
                TinyBuilder builder = new TinyBuilder(pipe);
                builder.SetStatistics(pipe.GetConfiguration().GetTreeStatistics().SOURCE_DOCUMENT_STATISTICS);
                return builder;
            }

            public override bool IsSchemaAware()
            {
                return true;
            }
        }

        private sealed class TinyTreeCondensed : TreeModel
        {

            public override int SymbolicValue => Builder.TINY_TREE_CONDENSED;

            public override string Name => "TinyTreeCondensed";
            public override Builder MakeBuilder(PipelineConfiguration pipe)
            {
                TinyBuilderCondensed tbc = new TinyBuilderCondensed(pipe);
                tbc.SetStatistics(pipe.GetConfiguration().GetTreeStatistics().SOURCE_DOCUMENT_STATISTICS);
                return tbc;
            }

            public override bool IsSchemaAware()
            {
                return true;
            }
        }

        private sealed class LinkedTree : TreeModel
        {
            private readonly bool mutable;

            public override int SymbolicValue => Builder.LINKED_TREE;

            public override string Name => "LinkedTree";
            public LinkedTree()
            {
                this.mutable = true;
            }

            public LinkedTree(bool mutable)
            {
                this.mutable = mutable;
            }

            public override Builder MakeBuilder(PipelineConfiguration pipe)
            {
                return new LinkedTreeBuilder(pipe, mutable ? Durability.MUTABLE : Durability.LASTING);
            }

            public override bool IsSchemaAware()
            {
                return true;
            }

            public override bool IsMutable()
            {
                return mutable;
            }
        }
    }
}