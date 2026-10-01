////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Streams;
using System.Globalization;
using System.IO;
namespace OutSmart.DAXon.Regex
{
    internal sealed class UnicodeBlocks
    {
        private readonly Dictionary<string, IntSet> blocks = new Dictionary<string, IntSet>(250);
        private UnicodeBlocks()
        {
            Build();
        }

        private static UnicodeBlocks GetInstance()
        {
            return Holder.INSTANCE;
        }

        public static IntSet GetBlock(string name)
        {
            UnicodeBlocks instance = GetInstance();
            IntSet cc = instance.blocks.GetOrDefault(name);
            if (cc != null)
            {
                return cc;
            }

            cc = instance.blocks.GetOrDefault(NormalizeBlockName(name));
            return cc;
        }

        private static string NormalizeBlockName(string name)
        {
            StringBuilder fsb = new StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                switch (c)
                {
                    case ' ':
                    case '\t':
                    case '\r':
                    case '\n':
                    case '_':

                        // no action
                        break;
                    default:
                        fsb.Append(c);
                        break;
                }
            }

            return fsb.ToString();
        }

        private void Build()
        {
            System.IO.Stream @in = Core.Version.platform.LocateResource("unicodeBlocks.xml", new List<string>());
            if (@in == null)
            {
                throw new RESyntaxException("Unable to read unicodeBlocks.xml file");
            }

            Configuration config = new Configuration();
            ParseOptions options = new ParseOptions().WithSchemaValidationMode(Validation.SKIP).WithDTDValidationMode(Validation.SKIP).WithSpaceStrippingRule(AllElementsSpaceStrippingRule.GetInstance()).WithPleaseCloseAfterUse(true);
            ITreeInfo doc;
            try
            {
                using (System.Xml.XmlReader reader = global::OutSmart.DAXon.Events.XmlReaderToReceiver.CreateXmlReader(null, @in, "unicodeBlocks.xml"))
                {
                    doc = config.BuildDocumentTree(reader, "unicodeBlocks.xml", options);
                }
            }
            catch (XPathException e)
            {
                throw new RESyntaxException("Failed to process unicodeBlocks.xml: " + e.Message);
            }

            IAxisIterator iter = doc.GetRootNode().IterateAxis(AxisInfo.DESCENDANT, new NameTest(Types.Type.ELEMENT, NamespaceUri.NULL, "block", config.GetNamePool()));
            while (true)
            {
                NodeInfo item = iter.Next();
                if (item == null)
                {
                    break;
                }

                string blockName = NormalizeBlockName(item.GetAttributeValue(NamespaceUri.NULL, "name"));
                IntSet range = null;
                foreach (NodeInfo rangeElement in item.Children(NodeKindTest.ELEMENT))
                {
                    int from = System.Convert.ToInt32(rangeElement.GetAttributeValue(NamespaceUri.NULL, "from").Substring(2), 16);
                    int to = System.Convert.ToInt32(rangeElement.GetAttributeValue(NamespaceUri.NULL, "to").Substring(2), 16);
                    IntSet cr = new IntBlockSet(from, to);
                    if (range == null)
                    {
                        range = cr;
                    }
                    else if (range is IntBlockSet)
                    {
                        range = range.MutableCopy().Union(cr);
                    }
                    else
                    {
                        range = range.Union(cr);
                    }
                }

                blocks[blockName] = range;
            }
        }

        private sealed class Holder
        {
            // See https://en.wikipedia.org/wiki/Initialization-on-demand_holder_idiom
            // The idea here is that the initialization occurs the first time getInstance() is called,
            // and it is automatically synchronized by virtue of the Java class loading rules.
            public static readonly UnicodeBlocks INSTANCE = new UnicodeBlocks();
        }
    }
}