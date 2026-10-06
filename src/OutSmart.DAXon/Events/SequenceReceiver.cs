////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Events
{
    public abstract class SequenceReceiver : IReceiver
    {
        protected bool previousAtomic = false;
        protected PipelineConfiguration pipelineConfiguration;
        protected string systemId = null;

        protected virtual string ErrorCodeForDecomposingFunctionItems => GetPipelineConfiguration().IsXSLT() ? "XTDE0450" : "XQTY0105";
        public SequenceReceiver(PipelineConfiguration pipe)
        {
            this.pipelineConfiguration = pipe;
        }

        public PipelineConfiguration GetPipelineConfiguration()
        {
            return pipelineConfiguration;
        }

        public virtual void SetPipelineConfiguration(PipelineConfiguration pipelineConfiguration)
        {
            this.pipelineConfiguration = pipelineConfiguration;
        }

        public Configuration GetConfiguration()
        {
            return pipelineConfiguration.GetConfiguration();
        }

        public virtual void SetSystemId(string systemId)
        {
            this.systemId = systemId;
        }

        public virtual string GetSystemId()
        {
            return systemId;
        }

        public virtual void SetUnparsedEntity(string name, string systemID, string publicID)
        {
        }

        public virtual void Open()
        {
            previousAtomic = false;
        }

        public abstract void Append(IItem item, ILocation locationId, int properties);
        public virtual void Append(IItem item)
        {
            Append(item, Loc.NONE, ReceiverOption.ALL_NAMESPACES);
        }

        public virtual NamePool GetNamePool()
        {
            return pipelineConfiguration.GetConfiguration().GetNamePool();
        }

        protected virtual void Flatten(ArrayItem array, ILocation locationId, int copyNamespaces)
        {
            // Each nested array is a level of recursion: deep JSON in a result must stop with SXLM0001.
            StackGuard.Probe();
            try
            {
                foreach (ISequence member in array.Members())
                {
                    SequenceTool.Supply(member.Iterate(), (it) => Append(it, locationId, copyNamespaces));
                }
            }
            catch (RecursionDepthError e) when (!e.Described)
            {
                throw e.Describe("Too many nested arrays to flatten. The array is too deeply nested.", DAXonErrorCode.SXLM0001, locationId);
            }
        }

        protected virtual void Decompose(IItem item, ILocation locationId, int copyNamespaces)
        {
            if (item != null)
            {
                switch (item.GetGenre())
                {
                    case Genre.ATOMIC:
                    case Genre.EXTERNAL:
                        if (previousAtomic)
                        {
                            Characters(StringConstants.SINGLE_SPACE, locationId, ReceiverOption.NONE);
                        }

                        Characters(item.UnicodeStringValue, locationId, ReceiverOption.NONE);
                        previousAtomic = true;
                        break;
                    case Genre.ARRAY:
                        Flatten((ArrayItem)item, locationId, copyNamespaces);
                        break;
                    case Genre.MAP:
                    case Genre.FUNCTION:
                        string thing = item is MapItem ? "map" : "function item";
                        string errorCode = ErrorCodeForDecomposingFunctionItems;
                        if (errorCode.StartsWith("SENR", StringComparison.Ordinal))
                        {
                            throw new XPathException("Cannot serialize a " + thing + " using this output method", errorCode, locationId);
                        }
                        else
                        {
                            throw new XPathException("Cannot add a " + thing + " to an XDM node tree", errorCode, locationId);
                        }

                    case Genre.NODE:
                    default:
                        NodeInfo node = (NodeInfo)item;
                        int kind = node.GetNodeKind();
                        if (node is Orphan && ((Orphan)node).IsDisableOutputEscaping())
                        {

                            // see test case doe-0801, -2 -3 - needed for output buffered within try/catch, xsl:fork etc
                            Characters(item.UnicodeStringValue, locationId, ReceiverOption.DISABLE_ESCAPING);
                            previousAtomic = false;
                        }
                        else if (kind == Types.Type.DOCUMENT)
                        {
                            StartDocument(ReceiverOption.NONE); // needed to ensure that illegal namespaces or attributes in the content are caught
                            foreach (NodeInfo child in node.Children())
                            {
                                Append(child, locationId, copyNamespaces);
                            }

                            previousAtomic = false;
                            EndDocument();
                        }
                        else if (kind == Types.Type.ATTRIBUTE || kind == Types.Type.NAMESPACE)
                        {
                            string description = kind == Types.Type.ATTRIBUTE ? "attribute" : "namespace";
                            throw new XPathException("Sequence normalization: Cannot process free-standing " + description + " node (" + node.DisplayName + ")", "SENR0001", locationId);
                        }
                        else
                        {
                            int copyOptions = CopyOptions.TYPE_ANNOTATIONS;
                            if (ReceiverOption.Contains(copyNamespaces, ReceiverOption.ALL_NAMESPACES))
                            {
                                copyOptions |= CopyOptions.ALL_NAMESPACES;
                            }

                            ((NodeInfo)item).Copy(this, copyOptions, locationId);
                            previousAtomic = false;
                        }

                        break;
                }
            }
        }

        public virtual bool HandlesAppend()
        {
            return true;
        }
        public abstract void StartDocument(int arg0);
        public abstract void EndDocument();
        public abstract void StartElement(INodeName arg0, ISchemaType arg1, IAttributeMap arg2, NamespaceMap arg3, ILocation arg4, int arg5);
        public abstract void EndElement();
        public abstract void Characters(UnicodeString arg0, ILocation arg1, int arg2);
        public abstract void ProcessingInstruction(string arg0, UnicodeString arg1, ILocation arg2, int arg3);
        public abstract void Comment(UnicodeString arg0, ILocation arg1, int arg2);
        public abstract void Close();
        // Abort-path release (see IReceiver): no events, idempotent. Leaf receivers hold no resources.
        public virtual void Dispose()
        {
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public abstract bool UsesTypeAnnotations();
    }
}