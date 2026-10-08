////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Linked;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Events
{
    public abstract class SequenceWriter : SequenceReceiver
    {
        private TreeModel treeModel = null;
        private Builder builder = null;
        private int level = 0;
        public SequenceWriter(PipelineConfiguration pipe) : base(pipe)
        {
        }

        public abstract void Write(IItem item);
        public override void StartDocument(int properties)
        {
            if (builder == null)
            {
                CreateTree(ReceiverOption.Contains(properties, ReceiverOption.MUTABLE_TREE));
            }

            if (level++ == 0)
            {
                builder.StartDocument(properties);
            }
        }

        public override void SetUnparsedEntity(string name, string systemID, string publicID)
        {
            if (builder != null)
            {
                builder.SetUnparsedEntity(name, systemID, publicID);
            }
        }

        private void CreateTree(bool mutable)
        {
            PipelineConfiguration pipe = GetPipelineConfiguration();
            if (treeModel != null)
            {
                builder = treeModel.MakeBuilder(pipe);
            }
            else if (pipe.GetController() != null)
            {
                if (mutable)
                {
                    TreeModel model = pipe.GetController().Model;
                    if (model.IsMutable())
                    {
                        builder = pipe.GetController().MakeBuilder();
                        builder.SetDurability(Durability.MUTABLE);
                    }
                    else
                    {
                        builder = new LinkedTreeBuilder(pipe, Durability.MUTABLE);
                    }
                }
                else
                {
                    builder = pipe.GetController().MakeBuilder();
                    builder.SetDurability(Durability.TEMPORARY);
                }
            }
            else
            {
                TreeModel model = GetConfiguration().GetParseOptions().Model;
                builder = model.MakeBuilder(pipe);
            }

            builder.SetPipelineConfiguration(pipe);
            builder.SetSystemId(systemId);
            builder.BaseURI = systemId;
            builder.SetTiming(false);
            builder.SetUseEventLocation(false);
            builder.Open();
        }

        public virtual TreeModel GetTreeModel()
        {
            return treeModel;
        }

        public virtual void SetTreeModel(TreeModel treeModel)
        {
            this.treeModel = treeModel;
        }

        public override void EndDocument()
        {
            if (--level == 0)
            {
                builder.EndDocument();
                NodeInfo doc = builder.CurrentRoot;

                // add the constructed document to the result sequence
                Append(doc, Loc.NONE, ReceiverOption.ALL_NAMESPACES);
                builder = null;
                systemId = null;
            }

            previousAtomic = false;
        }

        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            if (builder == null)
            {
                CreateTree(ReceiverOption.Contains(properties, ReceiverOption.MUTABLE_TREE));
            }


            builder.StartElement(elemName, type, attributes, namespaces, location, properties);
            level++;
            previousAtomic = false;
        }

        public override void EndElement()
        {

            builder.EndElement();
            if (--level == 0)
            {
                builder.Close();
                NodeInfo element = builder.CurrentRoot;
                Append(element, Loc.NONE, ReceiverOption.ALL_NAMESPACES);
                builder = null;
                systemId = null;
            }

            previousAtomic = false;
        }

        public override void Characters(UnicodeString s, ILocation locationId, int properties)
        {
            if (level == 0)
            {
                Orphan o = new Orphan(GetConfiguration());
                if (treeModel != null && treeModel.IsMutable())
                {
                    ((GenericTreeInfo)o.GetTreeInfo()).SetDurability(Durability.MUTABLE);
                }

                o.SetNodeKind(Types.Type.TEXT);
                o.SetStringValue(s.Tidy());
                Write(o);
            }
            else
            {
                if (!s.IsEmpty())
                {
                    builder.Characters(s, locationId, properties);
                }
            }

            previousAtomic = false;
        }

        public override void Comment(UnicodeString comment, ILocation locationId, int properties)
        {
            if (level == 0)
            {
                Orphan o = new Orphan(GetConfiguration());
                if (treeModel != null && treeModel.IsMutable())
                {
                    ((GenericTreeInfo)o.GetTreeInfo()).SetDurability(Durability.MUTABLE);
                }

                o.SetNodeKind(Types.Type.COMMENT);
                o.SetStringValue(comment.Tidy());
                Write(o);
            }
            else
            {
                builder.Comment(comment, locationId, properties);
            }

            previousAtomic = false;
        }

        public override void ProcessingInstruction(string target, UnicodeString data, ILocation locationId, int properties)
        {
            if (level == 0)
            {
                Orphan o = new Orphan(GetConfiguration());
                if (treeModel != null && treeModel.IsMutable())
                {
                    ((GenericTreeInfo)o.GetTreeInfo()).SetDurability(Durability.MUTABLE);
                }

                o.SetNodeName(new NoNamespaceName(target));
                o.SetNodeKind(Types.Type.PROCESSING_INSTRUCTION);
                o.SetStringValue(data.Tidy());
                Write(o);
            }
            else
            {
                builder.ProcessingInstruction(target, data, locationId, properties);
            }

            previousAtomic = false;
        }

        public override void Close()
        {
            previousAtomic = false;
            if (builder != null)
            {
                builder.Close();
            }
        }

        public override void Append(IItem item, ILocation locationId, int copyNamespaces)
        {
            if (item != null)
            {
                if (level == 0)
                {
                    Write(item);
                    previousAtomic = false;
                }
                else
                {
                    Decompose(item, locationId, copyNamespaces);
                }
            }
        }

        public override bool UsesTypeAnnotations()
        {
            return builder == null || builder.UsesTypeAnnotations();
        }
    }
}
