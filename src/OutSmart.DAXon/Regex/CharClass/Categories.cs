////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Streams;
using System.IO;
namespace OutSmart.DAXon.Regex.CharClass
{
    internal sealed class Categories
    {

        public static readonly ICharacterClass ESCAPE_s = new IntSetCharacterClass(IntArraySet.Make(new int[] { 9, 10, 13, 32 }, 4));
        public static readonly ICharacterClass ESCAPE_S = new InverseCharacterClass(ESCAPE_s);
        public static readonly PredicateCharacterClass ESCAPE_i = new PredicateCharacterClass((value) => XMLCharacterData.IsNCNameStart11(value) || value == ':');
        public static readonly ICharacterClass ESCAPE_I = new InverseCharacterClass(ESCAPE_i);
        public static readonly PredicateCharacterClass ESCAPE_c = new PredicateCharacterClass((value) => XMLCharacterData.IsNCName11(value) || value == ':');
        public static readonly ICharacterClass ESCAPE_C = new InverseCharacterClass(ESCAPE_c);
        public static readonly Category ESCAPE_d = GetCategory("Nd");
        public static readonly ICharacterClass ESCAPE_D = new InverseCharacterClass(ESCAPE_d);
        static Category CATEGORY_P = GetCategory("P");
        static Category CATEGORY_Z = GetCategory("Z");
        static Category CATEGORY_C = GetCategory("C");
        public static readonly PredicateCharacterClass ESCAPE_w = new PredicateCharacterClass((value) => !(CATEGORY_P.Test(value) || CATEGORY_Z.Test(value) || CATEGORY_C.Test(value)));
        public static readonly ICharacterClass ESCAPE_W = new InverseCharacterClass(ESCAPE_w);

        private readonly Dictionary<string, Category> CATEGORIES = new Dictionary<string, Category>(30);
        private Categories()
        {
            // Runs inside a type initializer: see Controller.SuspendThreadDeadline
            Controller.DeadlineToken outer = Controller.SuspendThreadDeadline();
            try
            {
                Build();
            }
            finally
            {
                Controller.RestoreThreadDeadline(outer);
            }
        }

        private static Categories GetInstance()
        {
            return Holder.INSTANCE;
        }

        private void Build()
        {
            System.IO.Stream @in = Core.Version.platform.LocateResource("categories.xml", new List<string>());
            if (@in == null)
            {
                throw new InvalidOperationException("Unable to read categories.xml file");
            }

            Configuration config = new Configuration();
            ParseOptions options = new ParseOptions().WithSchemaValidationMode(Validation.SKIP).WithDTDValidationMode(Validation.SKIP).WithTreeModel(Builder.TINY_TREE).WithPleaseCloseAfterUse(true);
            NodeInfo doc;
            try
            {
                using (System.Xml.XmlReader reader = XmlReaderToReceiver.CreateXmlReader(null, @in, "categories.xml"))
                {
                    doc = config.BuildDocumentTree(reader, "categories.xml", options).GetRootNode();
                }
            }
            catch (XPathException e)
            {
                throw new InvalidOperationException("Failed to build categories.xml", e);
            }

            int fp_name = config.GetNamePool().AllocateFingerprint(NamespaceUri.NULL, "name");
            int fp_f = config.GetNamePool().AllocateFingerprint(NamespaceUri.NULL, "f");
            int fp_t = config.GetNamePool().AllocateFingerprint(NamespaceUri.NULL, "t");
            IAxisIterator iter = doc.IterateAxis(AxisInfo.DESCENDANT, new NameTest(Types.Type.ELEMENT, NamespaceUri.NULL, "cat", config.GetNamePool()));
            for (NodeInfo item; (item = iter.Next()) != null;)
            {
                string cat = ((TinyElementImpl)item).GetAttributeValue(fp_name);
                IntRangeSet irs = new IntRangeSet();
                foreach (NodeInfo r in item.Children(NodeKindTest.ELEMENT))
                {
                    string from = ((TinyElementImpl)r).GetAttributeValue(fp_f);
                    string to = ((TinyElementImpl)r).GetAttributeValue(fp_t);
                    irs.AddRange(Convert.ToInt32(from, 16), Convert.ToInt32(to, 16));
                }

                CATEGORIES[cat] = new Category(cat, new IntSetPredicate(irs));
            }

            string c = "CLMNPSZ";
            for (int i = 0; i < c.Length; i++)
            {
                char ch = c[i];
                IIntPredicateProxy ip = null;
                foreach (KeyValuePair<string, Category> entry in CATEGORIES)
                {
                    if (entry.Key[0] == ch)
                    {
                        ip = ip == null ? entry.Value : IntUnionPredicate.MakeUnion(ip, entry.Value);
                    }
                }

                string label = ch + "";
                CATEGORIES[label] = new Category(label, ip);
            }
        }
        public static Category GetCategory(string cat)
        {
            lock (typeof(Categories))
            {
                return GetInstance().CATEGORIES.GetOrDefault(cat);
            }
        }
        internal sealed class Category : ICharacterClass
        {
            private readonly string label;
            private readonly IIntPredicateProxy predicate;
            public Category(string label, IIntPredicateProxy predicate)
            {
                this.label = label;
                this.predicate = predicate;
            }

            public bool Test(int value)
            {
                return predicate.Test(value);
            }

            public bool IsDisjoint(ICharacterClass other)
            {
                if (other is Category)
                {
                    char majorCat0 = label[0];
                    string otherLabel = ((Category)other).label;
                    char majorCat1 = otherLabel[0];
                    return majorCat0 != majorCat1 || (label.Length > 1 && otherLabel.Length > 1 && !label.Equals(otherLabel));
                }
                else if (other is InverseCharacterClass)
                {
                    return other.IsDisjoint(this);
                }
                else if (other is SingletonCharacterClass)
                {
                    return !Test(((SingletonCharacterClass)other).Codepoint);
                }
                else if (other is IntSetCharacterClass)
                {
                    IntSet intSet = other.GetIntSet();
                    if (intSet.Count > 100)
                    {

                        // too expensive to test, and increasingly likely to be non-disjoint anyway
                        return false;
                    }

                    IIntIterator ii = intSet.IIterator();
                    while (ii.MoveNext())
                    {
                        if (Test(ii.Current))
                        {
                            return false;
                        }
                    }

                    return true;
                }
                else
                {
                    return false;
                }
            }

            public IntSet GetIntSet()
            {
                return Extent(predicate);
            }

            private static IntSet Extent(IIntPredicateProxy predicate)
            {
                if (predicate is IntSetPredicate)
                {
                    return ((IntSetPredicate)predicate).GetIntSet();
                }

                return null;
            }

            // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
            public IIntPredicateProxy Union(IIntPredicateProxy other) => IntPredicateLambda.Of((i) => Test(i) || other.Test(i));
        }

        private sealed class Holder
        {
            // See https://en.wikipedia.org/wiki/Initialization-on-demand_holder_idiom
            // The idea here is that the initialization occurs the first time getInstance() is called,
            // and it is automatically synchronized by virtue of the Java class loading rules.
            public static readonly Categories INSTANCE = new Categories();
        }
    }
}
