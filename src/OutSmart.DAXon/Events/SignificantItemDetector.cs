////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Api;

using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Events
{
    internal sealed class SignificantItemDetector : ProxyOutputter
    {
        private int level = 0;
        private bool empty = true;
        private readonly IAction trigger;
        public SignificantItemDetector(Outputter next, IAction trigger) : base(next)
        {
            this.trigger = trigger;
        }

        private void Start()
        {
            if (empty)
            {
                trigger.DoAction();
                empty = false;
            }
        }

        /*level==0 && */
        /// <summary>
        /// Start of a document node.
        /// </summary>
        public override void StartDocument(int properties)
        {
            if (level++ != 0)
            {
                NextOutputter.StartDocument(properties);
            }
        }

        /*level==0 && */
        /// <summary>
        /// Start of a document node.
        /// </summary>
        public override void StartElement(INodeName elemName, ISchemaType type, ILocation location, int properties)
        {
            Start();
            level++;
            NextOutputter.StartElement(elemName, type, location, properties);
        }

        /*level==0 && */
        /// <summary>
        /// Notify the start of an element, supplying all attributes and namespaces
        /// </summary>
        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            Start();
            level++;
            NextOutputter.StartElement(elemName, type, attributes, namespaces, location, properties);
        }

        /*level==0 && */
        /// <summary>
        /// Notify a namespace binding.
        /// </summary>
        public override void Namespace(string prefix, NamespaceUri namespaceUri, int properties)
        {
            Start();
            NextOutputter.Namespace(prefix, namespaceUri, properties);
        }

        /*level==0 && */
        /// <summary>
        /// Notify an attribute.
        /// </summary>
        public override void Attribute(INodeName attName, ISimpleType typeCode, string value, ILocation location, int properties)
        {
            Start();
            NextOutputter.Attribute(attName, typeCode, value, location, properties);
        }

        /*level==0 && */
        public override void StartContent()
        {
            NextOutputter.StartContent();
        }

        /*level==0 && */
        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            if (!chars.IsEmpty())
            {
                Start();
            }

            NextOutputter.Characters(chars, locationId, properties);
        }

        /*level==0 && */
        public override void ProcessingInstruction(string target, UnicodeString data, ILocation locationId, int properties)
        {
            Start();
            NextOutputter.ProcessingInstruction(target, data, locationId, properties);
        }

        /*level==0 && */
        public override void Comment(UnicodeString chars, ILocation locationId, int properties)
        {
            Start();
            NextOutputter.Comment(chars, locationId, properties);
        }

        /*level==0 && */
        public static bool IsSignificant(IItem item)
        {
            if (item is NodeInfo)
            {
                NodeInfo node = (NodeInfo)item;
                return (node.GetNodeKind() != Types.Type.TEXT || !node.UnicodeStringValue.IsEmpty()) && (node.GetNodeKind() != Types.Type.DOCUMENT || node.HasChildNodes());
            }
            else if (item is AtomicValue)
            {
                return !item.UnicodeStringValue.IsEmpty();
            }
            else if (item is ArrayItem)
            {
                if (((ArrayItem)item).IsEmpty())
                {
                    return true;
                }
                else
                {
                    // Each nested array is a level of recursion: the depth is the value's.
                    StackGuard.Probe();
                    try
                    {
                        foreach (ISequence mem in ((ArrayItem)item).Members())
                        {
                            try
                            {
                                ISequenceIterator memIter = mem.Iterate();
                                IItem it;
                                while ((it = memIter.Next()) != null)
                                {
                                    if (IsSignificant(it))
                                    {
                                        return true;
                                    }
                                }
                            }
                            catch (UncheckedXPathException)
                            {
                                return true;
                            }
                        }
                    }
                    catch (RecursionDepthError e) when (!e.Described)
                    {
                        throw e.Describe("Too many nested arrays to tell whether content is empty (xsl:on-empty, xsl:on-non-empty). The array is too deeply nested.", DAXonErrorCode.SXLM0001, null);
                    }

                    return false;
                }
            }

            return true;
        }

        /*level==0 && */
        public override void Append(IItem item, ILocation locationId, int copyNamespaces)
        {
            if (IsSignificant(item))
            {
                Start();
            }

            base.Append(item, locationId, copyNamespaces);
        }

        /*level==0 && */
        public override void Append(IItem item)
        {
            if (IsSignificant(item))
            {
                Start();
            }

            base.Append(item);
        }

        /*level==0 && */
        /// <summary>
        /// Notify the end of a document node
        /// </summary>
        public override void EndDocument()
        {
            if (--level != 0)
            {
                NextOutputter.EndDocument();
            }
        }

        /*level==0 && */
        /// <summary>
        /// End of element
        /// </summary>
        public override void EndElement()
        {
            level--;
            NextOutputter.EndElement();
        }

        /*level==0 && */
        /// <summary>
        /// End of element
        /// </summary>
        public bool IsEmpty()
        {
            return empty;
        }
    }
}
