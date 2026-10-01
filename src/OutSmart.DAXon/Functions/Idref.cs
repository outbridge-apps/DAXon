////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implements the fn:idref function. Returns the nodes in a document that have an IDREF/IDREFS
    /// attribute (or element) referencing one of the supplied id values.
    /// </summary>
    internal sealed class Idref : SystemFunction
    {
        public override int GetSpecialProperties(Expression[] arguments)
        {
            int prop = StaticProperty.ORDERED_NODESET | StaticProperty.SINGLE_DOCUMENT_NODESET | StaticProperty.NO_NODES_NEWLY_CREATED;
            if ((GetArity() == 1) || (arguments[1].GetSpecialProperties() & StaticProperty.CONTEXT_DOCUMENT_NODESET) != 0)
            {
                prop |= StaticProperty.CONTEXT_DOCUMENT_NODESET;
            }

            return prop;
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            NodeInfo start = arguments.Length == 1 ? GetContextNode(context) : (NodeInfo)arguments[1].Head();
            NodeInfo arg2 = start.Root;
            if (arg2.GetNodeKind() != Types.Type.DOCUMENT)
            {
                throw new XPathException("In the idref() function," + " the tree being searched must be one whose root is a document node", "FODC0001", context);
            }

            KeyManager keyManager = GetRetainedStaticContext().GetPackageData().GetKeyManager();
            IdrefMappingFunction map = new IdrefMappingFunction();
            map.document = arg2.GetTreeInfo();
            map.keyContext = context;
            map.keyManager = keyManager;
            map.keySet = keyManager.GetKeyDefinitionSet(StandardNames.GetStructuredQName(StandardNames.XS_IDREFS));
            ISequenceIterator allValues = new Expressions.MappingIterator(arguments[0].Iterate(), map);
            ISequenceIterator result = new DocumentOrderIterator(allValues, LocalOrderComparer.GetInstance());
            return SequenceTool.ToLazySequence(result);
        }

        private sealed class IdrefMappingFunction : IMappingFunction
        {
            public ITreeInfo document;
            public IXPathContext keyContext;
            public KeyManager keyManager;
            public KeyDefinitionSet keySet;

            public ISequenceIterator IMap(IItem item)
            {
                return keyManager.SelectByKey(keySet, document, (StringValue)item, keyContext);
            }
        }
    }
}
