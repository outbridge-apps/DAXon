////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Functions
{
    internal abstract class SuperId : SystemFunction
    {
        public const int ID = 0;
        public const int ELEMENT_WITH_ID = 1;
        public abstract int Op { get; }
        public override int GetSpecialProperties(Expression[] arguments)
        {
            int prop = StaticProperty.ORDERED_NODESET | StaticProperty.SINGLE_DOCUMENT_NODESET | StaticProperty.NO_NODES_NEWLY_CREATED;
            if ((GetArity() == 1) || (arguments[1].GetSpecialProperties() & StaticProperty.CONTEXT_DOCUMENT_NODESET) != 0)
            {
                prop |= StaticProperty.CONTEXT_DOCUMENT_NODESET;
            }

            return prop;
        }

        public static ISequenceIterator GetIdSingle(ITreeInfo doc, UnicodeString idrefs, int operation)
        {
            if (Whitespace.ContainsWhitespace(idrefs.CodePoints()))
            {
                Whitespace.Tokenizer tokens = new Whitespace.Tokenizer(idrefs);
                IdMappingFunction map = new IdMappingFunction();
                map.document = doc;
                map.operation = operation;
                ISequenceIterator result = new Expressions.MappingIterator(tokens, map);
                return new DocumentOrderIterator(result, LocalOrderComparer.GetInstance());
            }
            else
            {
                return SingletonIterator.MakeIterator(doc.SelectID(idrefs.ToString(), operation == ELEMENT_WITH_ID));
            }
        }

        public static ISequenceIterator GetIdMultiple(ITreeInfo doc, ISequenceIterator idrefs, int operation)
        {
            IdMappingFunction map = new IdMappingFunction();
            map.document = doc;
            map.operation = operation;
            ISequenceIterator result = new Expressions.MappingIterator(idrefs, map);
            return new DocumentOrderIterator(result, LocalOrderComparer.GetInstance());
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            NodeInfo start = arguments.Length == 1 ? GetContextNode(context) : (NodeInfo)arguments[1].Head();
            NodeInfo arg1 = start.Root;
            if (arg1.GetNodeKind() != Types.Type.DOCUMENT)
            {
                throw new XPathException("In the " + GetFunctionName().GetLocalPart() + "() function," + " the tree being searched must be one whose root is a document node", "FODC0001", context);
            }

            ITreeInfo doc = arg1.GetTreeInfo();
            ISequenceIterator result;
            if (arguments[0] is AtomicValue)
            {
                result = GetIdSingle(doc, ((AtomicValue)arguments[0]).UnicodeStringValue, Op);
            }
            else
            {
                ISequenceIterator idrefs = arguments[0].Iterate();
                result = GetIdMultiple(doc, idrefs, Op);
            }

            return SequenceTool.ToLazySequence(result);
        }

        private sealed class IdMappingFunction : IMappingFunction
        {
            public ITreeInfo document;
            public int operation;
            public ISequenceIterator IMap(IItem item)
            {
                UnicodeString idrefs = Whitespace.Trim(item.UnicodeStringValue);

                // If this value contains a space, we need to break it up into its
                // separate tokens; if not, we can process it directly
                if (Whitespace.ContainsWhitespace(idrefs.CodePoints()))
                {
                    Whitespace.Tokenizer tokens = new Whitespace.Tokenizer(idrefs);
                    IdMappingFunction submap = new IdMappingFunction();
                    submap.document = document;
                    submap.operation = operation;
                    return new Expressions.MappingIterator(tokens, submap);
                }
                else
                {
                    return SingletonIterator.MakeIterator(document.SelectID(idrefs.ToString(), operation == ELEMENT_WITH_ID));
                }
            }
        }

        internal sealed class Id : SuperId
        {
            public override int Op => ID;
        }

        internal sealed class ElementWithId : SuperId
        {
            public override int Op => ELEMENT_WITH_ID;
        }
    }
}
