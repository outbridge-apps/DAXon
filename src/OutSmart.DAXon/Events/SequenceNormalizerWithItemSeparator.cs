////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Events
{
    /// <summary>
    /// Implement the "sequence normalization" logic as defined in the XSLT 3.0/XQuery 3.0
    /// serialization spec.
    ///
    /// <para>This class is used only if an ItemSeparator is specified. In the absence of an ItemSeparator,
    /// the insertion of a single space performed by the ComplexContentOutputter serves the purpose.</para>
    /// </summary>
    internal sealed class SequenceNormalizerWithItemSeparator : SequenceNormalizer
    {
        private readonly UnicodeString separator;
        private bool first = true;

        public SequenceNormalizerWithItemSeparator(IReceiver next, UnicodeString separator) : base(next)
        {
            this.separator = separator;
        }

        /// <summary>
        /// Start of event stream
        /// </summary>
        public override void Open()
        {
            first = true;
            base.Open();
        }

        /// <summary>
        /// Start of a document node.
        /// </summary>
        public override void StartDocument(int properties)
        {
            Sep();
            base.StartDocument(properties);
        }

        /// <summary>
        /// Notify the start of an element
        /// </summary>
        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            Sep();
            base.StartElement(elemName, type, attributes, namespaces, location, properties);
        }

        /// <summary>
        /// Character data
        /// </summary>
        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            Sep();
            base.Characters(chars, locationId, properties);
        }

        /// <summary>
        /// Processing Instruction
        /// </summary>
        public override void ProcessingInstruction(string target, UnicodeString data, ILocation locationId, int properties)
        {
            Sep();
            base.ProcessingInstruction(target, data, locationId, properties);
        }

        /// <summary>
        /// Output a comment
        /// </summary>
        public override void Comment(UnicodeString chars, ILocation locationId, int properties)
        {
            Sep();
            base.Comment(chars, locationId, properties);
        }

        /// <summary>
        /// Append an arbitrary item (node or atomic value) to the output
        /// </summary>
        /// <param name="item">the item to be appended</param>
        /// <param name="locationId">the location of the calling instruction, for diagnostics</param>
        /// <param name="copyNamespaces">if the item is an element node, this indicates whether its namespaces
        /// need to be copied. Values are <see cref="ReceiverOption.ALL_NAMESPACES"/>; the default (0) means no copying</param>
        public override void Append(IItem item, ILocation locationId, int copyNamespaces)
        {
            if (item is ArrayItem)
            {
                Flatten((ArrayItem)item, locationId, copyNamespaces);
            }
            else
            {
                if (item is AtomicValue)
                {
                    Sep();
                    NextReceiver.Characters(item.UnicodeStringValue, locationId, ReceiverOption.NONE);
                }
                else
                {
                    Decompose(item, locationId, copyNamespaces);
                }
            }
        }

        /// <summary>
        /// End of output. Note that closing this receiver also closes the rest of the
        /// pipeline.
        /// </summary>
        public override void Close()
        {
            base.Close();
        }

        /// <summary>
        /// Output the separator, assuming we are at the top level and not at the start
        /// </summary>
        private void Sep()
        {
            if (level == 0 && !first)
            {
                base.Characters(separator, Loc.NONE, ReceiverOption.NONE);
            }
            else
            {
                first = false;
            }
        }
    }
}
