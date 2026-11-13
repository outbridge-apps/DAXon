////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Internal.Net;
using System;

namespace OutSmart.DAXon.Api
{
    /// <summary>
    /// A Destination in which an XdmNode is constructed to hold the output of a query or transformation.
    /// The result sequence is normalized (per the W3C Serialization sequence-normalization rules) into a
    /// single document node, retrievable via <see cref="GetXdmNode"/>.
    /// </summary>
    public class XdmDestination : AbstractDestination
    {
        internal TreeModel treeModel = TreeModel.TINY_TREE;
        internal Builder builder;

        public virtual URI BaseURI
        {
            get => DestinationBaseURI; set
            {
                ArgumentCheck.NotNull(value, nameof(value));
                if (!value.IsAbsolute())
                {
                    throw new ArgumentException("Supplied base URI must be absolute");
                }

                DestinationBaseURI = value;
            }
        }

        public XdmDestination()
        {
        }

        public virtual void SetTreeModel(TreeModel model)
        {
            this.treeModel = model;
        }

        public virtual TreeModel GetTreeModel()
        {
            return treeModel;
        }

        public override IReceiver GetReceiver(PipelineConfiguration pipe, SerializationProperties @params)
        {
            ArgumentCheck.NotNull(pipe, nameof(pipe));
            ArgumentCheck.NotNull(@params, nameof(@params));
            TreeModel model = treeModel;
            if (model == null)
            {
                int m = pipe.GetParseOptions().GetTreeModel();
                if (m != Builder.UNSPECIFIED_TREE_MODEL)
                {
                    model = TreeModel.GetTreeModel(m);
                }

                if (model == null)
                {
                    model = TreeModel.TINY_TREE;
                }
            }

            builder = model.MakeBuilder(pipe);
            string systemId = BaseURI == null ? null : BaseURI.ToASCIIString();
            if (systemId != null)
            {
                builder.SetUseEventLocation(false);
                builder.BaseURI = systemId;
            }

            SequenceNormalizer sn = @params.MakeSequenceNormalizer(builder);
            sn.SetSystemId(systemId);
            sn.OnClose(helper.ListenersOfRun());
            return sn;
        }

        public override void Close()
        {
            // no action
        }

        /// <summary>
        /// The root of the constructed tree (a document node), or null if nothing was written.
        /// </summary>
        public virtual XdmNode GetXdmNode()
        {
            if (builder == null)
            {
                throw new InvalidOperationException("The document has not yet been built");
            }

            NodeInfo node = builder.CurrentRoot;
            return node == null ? null : (XdmNode)XdmValue.Wrap(node);
        }

        public virtual void Reset()
        {
            builder = null;
        }
    }
}
