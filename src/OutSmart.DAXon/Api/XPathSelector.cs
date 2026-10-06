////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Lib;
using OutSmart.DAXon.XPath;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Streams;
namespace OutSmart.DAXon.Api
{
    //@CSharpInjectMembers(code = {
    //        "    public void setErrorReporter(Action<OutSmart.DAXon.Api.IXmlProcessingError> reporter) {"
    //                + "        setErrorReporter(new Saxon.Impl.Helpers.ErrorReportingAction(reporter));"
    //                + "    }"
    //})
    public class XPathSelector : IEnumerable<XdmItem>
    {
        private readonly XPathExpression exp;
        private readonly XPathDynamicContext dynamicContext;
        private readonly Dictionary<StructuredQName, XPathVariable> declaredVariables;

        public virtual XPathDynamicContext UnderlyingXPathContext => dynamicContext;
        public XPathSelector(XPathExpression exp, Dictionary<StructuredQName, XPathVariable> declaredVariables)
        {
            this.exp = exp;
            this.declaredVariables = declaredVariables;
            dynamicContext = exp.CreateDynamicContext();
        }

        public virtual void SetContextItem(XdmItem item)
        {
            if (item == null)
            {
                throw new NullReferenceException("contextItem");
            }

            if (!exp.InternalExpression.GetPackageData().IsSchemaAware())
            {
                IItem it = item.UnderlyingValue.Head();
                if (it is NodeInfo && ((NodeInfo)it).GetTreeInfo().IsTyped())
                {
                    throw new DAXonApiException("The supplied node has been schema-validated, but the XPath expression was compiled without schema-awareness");
                }
            }

            try
            {
                dynamicContext.ContextItem = item.UnderlyingValue;
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (RecursionDepthError e)
            {
                throw new DAXonApiException(e.ToXPathException());
            }
        }

        public virtual XdmItem GetContextItem()
        {
            return XdmItem.WrapItem(dynamicContext.ContextItem);
        }

        public virtual void SetVariable(QName name, XdmValue value)
        {
            if (name == null)
                throw new NullReferenceException("name");
            if (value == null)
                throw new NullReferenceException("value");
            StructuredQName qn = name.GetStructuredQName();
            XPathVariable var = declaredVariables.GetOrDefault(qn);
            if (var == null)
            {
                throw new DAXonApiException(new XPathException("Variable has not been declared: " + name));
            }

            try
            {
                dynamicContext.SetVariable(var, (ISequence)(value.UnderlyingValue));
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (RecursionDepthError e)
            {
                throw new DAXonApiException(e.ToXPathException());
            }
            catch (UncheckedXPathException e)
            {
                throw new DAXonApiException(e);
            }
        }

        public virtual void SetResourceResolver(IResourceResolver resolver)
        {
            dynamicContext.ResourceResolver = resolver;
        }

        public virtual IResourceResolver GetResourceResolver()
        {
            return dynamicContext.ResourceResolver;
        }

        public virtual void SetUnparsedTextResolver(IUnparsedTextURIResolver resolver)
        {
            dynamicContext.SetUnparsedTextURIResolver(resolver);
        }

        public virtual IUnparsedTextURIResolver GetUnparsedTextURIResolver()
        {
            return dynamicContext.GetUnparsedTextURIResolver();
        }

        public virtual void SetErrorReporter(IErrorReporter reporter)
        {
            dynamicContext.ErrorReporter = reporter;
        }

        public virtual XdmValue Evaluate()
        {
            using RunResources run = RunResources.Enter();
            ISequence value;
            try
            {
                exp.ArmEvaluation(dynamicContext);
                value = SequenceTool.ToGroundedValue(exp.Iterate(dynamicContext));
            }
            catch (UncheckedXPathException uxe)
            {
                throw new DAXonApiException(uxe);
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (RecursionDepthError e)
            {
                // The stack-guard abort is deliberately not an XPathException (see
                // RecursionDepthError), so every API method that reports engine errors carries this
                // clause too. Converting HERE and nowhere deeper is the whole point: on its way up
                // the abort must not meet a handler that runs.
                throw new DAXonApiException(e.ToXPathException());
            }

            return XdmValue.Wrap(value);
        }

        public virtual XdmItem EvaluateSingle()
        {
            using RunResources run = RunResources.Enter();
            try
            {
                exp.ArmEvaluation(dynamicContext);
                IItem i = exp.EvaluateSingle(dynamicContext);
                if (i == null)
                {
                    return null;
                }

                return (XdmItem)XdmValue.Wrap(i);
            }
            catch (UncheckedXPathException uxe)
            {
                throw new DAXonApiException(uxe);   // an iterator's error, a limit among them, as Evaluate() reports it
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (RecursionDepthError e)
            {
                throw new DAXonApiException(e.ToXPathException());
            }
        }

        public virtual XdmSequenceIterator<XdmItem> IIterator()
        {
            RunResources scope = RunResources.Detached();
            RunResources saved = scope.Activate();
            try
            {
                exp.ArmEvaluation(dynamicContext);
                return new XdmSequenceIterator<XdmItem>(exp.Iterate(dynamicContext), scope);
            }
            catch (UncheckedXPathException e)
            {
                scope.CloseAll();
                throw new DAXonApiUncheckedException(e.GetXPathException());
            }
            catch (XPathException e)
            {
                scope.CloseAll();
                throw new DAXonApiUncheckedException(e);
            }
            catch (RecursionDepthError e)
            {
                scope.CloseAll();
                throw new DAXonApiUncheckedException(e.ToXPathException());
            }
            finally
            {
                RunResources.Restore(saved);
            }
        }

        public virtual bool EffectiveBooleanValue()
        {
            using RunResources run = RunResources.Enter();
            try
            {
                exp.ArmEvaluation(dynamicContext);
                return exp.EffectiveBooleanValue(dynamicContext);
            }
            catch (UncheckedXPathException uxe)
            {
                throw new DAXonApiException(uxe);
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (RecursionDepthError e)
            {
                throw new DAXonApiException(e.ToXPathException());
            }
        }
        // s9api XPathSelector is Iterable<XdmItem>: foreach over the selector evaluates it.
        public IEnumerator<XdmItem> GetEnumerator()
        {
            using (XdmSequenceIterator<XdmItem> it = IIterator())
            {
                while (it.HasNext())
                {
                    yield return it.Next();
                }
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
