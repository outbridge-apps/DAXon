////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
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
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Streams;
using System.IO;
namespace OutSmart.DAXon.Regex
{
    internal sealed class CaseVariants
    {

        /// <summary>
        /// Get the case variants of roman letters (A-Z, a-z), other than the letters A-Z and a-z themselves
        /// </summary>
        public static int[] ROMAN_VARIANTS = new[]
        {
            0x0130,
            0x0131,
            0x212A,
            0x017F
        }; // The data file casevariants.xml was formed by applying the following query to the XML
        // Use one hashmap for characters with a single case variant, another for characters with multiple
        // case variants, to reduce the number of objects that need to be allocated
        private readonly IIntToIntMap monoVariants = new IntToIntHashMap(2500);
        private readonly IntHashMap<int[]> polyVariants = new IntHashMap<int[]>(100);
        private CaseVariants()
        {
            Build();
        }

        private static CaseVariants GetInstance()
        {
            return Holder.INSTANCE;
        }

        private void Build()
        {
            System.IO.Stream @in = Core.Version.platform.LocateResource("casevariants.xml", new List<string>());
            if (@in == null)
            {
                throw new InvalidOperationException("Unable to read casevariants.xml file");
            }

            Configuration config = new Configuration();
            ParseOptions options = new ParseOptions();
            options = options.WithSchemaValidationMode(Validation.SKIP);
            options = options.WithDTDValidationMode(Validation.SKIP);
            NodeInfo doc;
            try
            {
                using (global::System.Xml.XmlReader reader = global::OutSmart.DAXon.Events.XmlReaderToReceiver.CreateXmlReader(null, @in, "casevariants.xml"))
                {
                    doc = config.BuildDocumentTree(reader, "casevariants.xml", options).GetRootNode();
                }
            }
            catch (XPathException e)
            {
                throw new InvalidOperationException("Failed to build casevariants.xml", e);
            }

            IAxisIterator iter = doc.IterateAxis(AxisInfo.DESCENDANT, new NameTest(Types.Type.ELEMENT, NamespaceUri.NULL, "c", config.GetNamePool()));
            while (true)
            {
                NodeInfo item = iter.Next();
                if (item == null)
                {
                    break;
                }

                string code = item.GetAttributeValue(NamespaceUri.NULL, "n");
                int icode = Convert.ToInt32(code, 16);
                string variants = item.GetAttributeValue(NamespaceUri.NULL, "v");
                string[] vhex = variants.SplitRegex(",");
                int[] vint = new int[vhex.Length];
                for (int i = 0; i < vhex.Length; i++)
                {
                    vint[i] = Convert.ToInt32(vhex[i], 16);
                }

                if (vhex.Length == 1)
                {
                    monoVariants.Put(icode, vint[0]);
                }
                else
                {
                    polyVariants.Put(icode, vint);
                }
            }
        }

        public static int[] GetCaseVariants(int code)
        {
            CaseVariants variants = GetInstance();
            IIntToIntMap monoVariants = variants.monoVariants;
            int mono = monoVariants.Get(code);
            if (mono != monoVariants.DefaultValue)
            {
                return new int[]
                {
                    mono
                };
            }
            else
            {
                int[] result = variants.polyVariants[code];
                if (result == null)
                {
                    return IntArraySet.EMPTY_INT_ARRAY;
                }
                else
                {
                    return result;
                }
            }
        }

        private sealed class Holder
        {
            // See https://en.wikipedia.org/wiki/Initialization-on-demand_holder_idiom
            // The idea here is that the initialization occurs the first time getInstance() is called,
            // and it is automatically synchronized by virtue of the Java class loading rules.
            public static readonly CaseVariants INSTANCE = new CaseVariants();
        }
        // version of the Unicode database (for Saxon 9.6, the Unicode 6.2.0 version was used)
        //    declare namespace u = "http://www.unicode.org/ns/2003/ucd/1.0";
        //    <variants>{
        //    let $chars := doc('ucd.all.flat.xml')/ * / * /u:char[@suc!='#' or @slc!='#']
        //    for $c in $chars
        //    let $variants := ($chars[(@cp, @suc[.!='#']) = $c/(@cp, @suc[.!='#'])] |
        //                          $chars[(@cp, @slc[.!='#']) = $c/(@cp, @slc[.!='#'])]) except $c
        //    return
        //         if (count($variants) gt 0) then
        //           <c n="{$c/@cp}" v="{string-join($variants/@cp, ",")}"/>
        //         else ()
        //
        //    }</variants>
    }
}