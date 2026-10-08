////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;

using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System.Collections.Generic;
namespace OutSmart.DAXon.Events
{
    public abstract class SequenceNormalizer : ProxyReceiver
    {
        protected int level = 0;
        private IList<IAction> actionList;
        private bool failed = false;
        public SequenceNormalizer(IReceiver next) : base(next)
        {
        }

        /// <summary>
        /// Start of event stream
        /// </summary>
        public override void Open()
        {
            level = 0;
            previousAtomic = false;
            base.Open();
            NextReceiver.StartDocument(ReceiverOption.NONE);
        }

        /// <summary>
        /// Start of a document node.
        /// </summary>
        public override void StartDocument(int properties)
        {
            level++;
            previousAtomic = false;
        }

        /// <summary>
        /// Notify the end of a document node
        /// </summary>
        public override void EndDocument()
        {
            level--;
            previousAtomic = false;
        }

        /// <summary>
        /// Notify the start of an element
        /// </summary>
        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            try
            {
                level++;
                base.StartElement(elemName, type, attributes, namespaces, location, properties);
                previousAtomic = false;
            }
            catch (XPathException e)
            {
                failed = true;
                throw;
            }
        }

        /// <summary>
        /// Character data
        /// </summary>
        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            try
            {
                base.Characters(chars, locationId, properties);
                previousAtomic = false;
            }
            catch (XPathException e)
            {
                failed = true;
                throw;
            }
        }

        /// <summary>
        /// Processing Instruction
        /// </summary>
        public override void ProcessingInstruction(string target, UnicodeString data, ILocation locationId, int properties)
        {
            try
            {
                base.ProcessingInstruction(target, data, locationId, properties);
                previousAtomic = false;
            }
            catch (XPathException e)
            {
                failed = true;
                throw;
            }
        }

        /// <summary>
        /// Output a comment
        /// </summary>
        public override void Comment(UnicodeString chars, ILocation locationId, int properties)
        {
            try
            {
                base.Comment(chars, locationId, properties);
                previousAtomic = false;
            }
            catch (XPathException e)
            {
                failed = true;
                throw;
            }
        }

        public override void EndElement()
        {
            try
            {
                level--;
                base.EndElement();
                previousAtomic = false;
            }
            catch (XPathException e)
            {
                failed = true;
                throw;
            }
        }

        public override void Close()
        {
            if (failed)
            {
                base.Close();
            }
            else
            {
                NextReceiver.EndDocument();
                base.Close();
                try
                {
                    if (actionList != null)
                    {
                        foreach (IAction action in actionList)
                        {
                            action.Act();
                        }
                    }
                }
                catch (DAXonApiException e)
                {
                    throw XPathException.MakeXPathException(e);
                }
            }
        }

        public virtual void OnClose(IList<IAction> actionList)
        {
            this.actionList = actionList;
        }

        public virtual void OnClose(IAction action)
        {
            if (actionList == null)
            {
                actionList = new List<IAction>();
            }

            actionList.Add(action);
        }
    }
}
