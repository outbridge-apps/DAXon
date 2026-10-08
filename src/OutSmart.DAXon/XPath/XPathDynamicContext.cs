////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.XPath
{
    //@CSharpInjectMembers(code = {
    //        "    public void setErrorReporter(Action<OutSmart.DAXon.Api.IXmlProcessingError> reporter) {"
    //                + "        setErrorReporter(new Saxon.Impl.Helpers.ErrorReportingAction(reporter));"
    //                + "    }"
    //})
    public class XPathDynamicContext
    {
        private readonly ItemType contextItemType;
        private readonly XPathContextMajor contextObject;
        private readonly SlotManager stackFrameMap;

        public virtual IItem ContextItem
        {
            get => contextObject.GetContextItem(); set
            {
                if (value is NodeInfo)
                {
                    if (!((NodeInfo)value).GetConfiguration().IsCompatible(contextObject.GetConfiguration()))
                    {
                        throw new XPathException("Supplied node must be built using the same or a compatible Configuration", DAXonErrorCode.SXXP0004);
                    }
                }

                TypeHierarchy th = contextObject.GetConfiguration().GetTypeHierarchy();
                if (!contextItemType.Matches(value, th))
                {
                    throw new XPathException("Supplied context item does not match required context item type " + contextItemType);
                }

                ManualIterator iter = new ManualIterator(value);
                contextObject.SetCurrentIterator(iter);
                if (value is NodeInfo && ((NodeInfo)value).GetSystemId() != null)
                {
                    Controller controller = contextObject.GetController();
                    if (controller != null)
                    {
                        DocumentPool pool = controller.GetDocumentPool();
                        DocumentKey key = new DocumentKey(((NodeInfo)value).GetSystemId());
                        if (pool.Find(key) == null)
                        {
                            pool.Add(((NodeInfo)value).GetTreeInfo(), key);
                        }
                    }
                }
            }
        }

        public virtual IResourceResolver ResourceResolver
        {
            get => contextObject.GetResourceResolver(); set
            {
                contextObject.SetResourceResolver(value);
            }
        }

        public virtual IErrorReporter ErrorReporter
        {
            get => contextObject.GetErrorReporter(); set
            {
                contextObject.SetErrorReporter(value);
            }
        }

        public virtual IXPathContext XPathContextObject => contextObject;
        public XPathDynamicContext(ItemType contextItemType, XPathContextMajor contextObject, SlotManager stackFrameMap)
        {
            this.contextItemType = contextItemType;
            this.contextObject = contextObject;
            this.stackFrameMap = stackFrameMap;
        }

        public virtual void SetVariable(XPathVariable variable, ISequence value)
        {
            SequenceType requiredType = variable.GetRequiredType();
            if (requiredType != SequenceType.ANY_SEQUENCE)
            {
                XPathException err = TypeChecker.TestConformance(value, requiredType, contextObject);
                if (err != null)
                {
                    throw err;
                }
            }

            ISequenceIterator iter = value.Iterate();
            for (IItem item; (item = iter.Next()) != null;)
            {
                if (item is NodeInfo && !((NodeInfo)item).GetConfiguration().IsCompatible(contextObject.GetConfiguration()))
                {
                    throw new XPathException("Supplied node must be built using the same or a compatible Configuration", DAXonErrorCode.SXXP0004);
                }
            }

            int slot = variable.LocalSlotNumber;
            StructuredQName expectedName = slot >= stackFrameMap.NumberOfVariables ? null : stackFrameMap.VariableMap[slot];
            if (!variable.GetVariableQName().Equals(expectedName))
            {
                throw new XPathException("Supplied XPathVariable is bound to the wrong slot: perhaps it was created using a different static context");
            }

            contextObject.SetLocalVariable(slot, value);
        }

        public virtual ICollectionFinder GetCollectionFinder()
        {
            return contextObject.GetController().GetCollectionFinder();
        }

        public virtual void SetCollectionFinder(ICollectionFinder cf)
        {
            contextObject.GetController().SetCollectionFinder(cf);
        }

        public virtual void SetUnparsedTextURIResolver(IUnparsedTextURIResolver resolver)
        {
            contextObject.GetController().UnparsedTextURIResolver = resolver;
        }

        public virtual IUnparsedTextURIResolver GetUnparsedTextURIResolver()
        {
            return contextObject.GetController().UnparsedTextURIResolver;
        }

        public virtual void CheckExternalVariables(SlotManager stackFrameMap, int numberOfExternals)
        {
            ISequence[] stack = contextObject.GetStackFrame().StackFrameValues;
            for (int i = 0; i < numberOfExternals; i++)
            {
                if (stack[i] == null)
                {
                    StructuredQName qname = stackFrameMap.VariableMap[i];
                    throw new XPathException("No value has been supplied for variable $" + qname.DisplayName);
                }
            }
        }
    }
}