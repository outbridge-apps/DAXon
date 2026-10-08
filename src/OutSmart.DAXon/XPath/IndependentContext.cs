////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.XPath
{
    public class IndependentContext : AbstractStaticContext, IXPathStaticContext, INamespaceResolver
    {
        protected Dictionary<string, NamespaceUri> namespaces = new Dictionary<string, NamespaceUri>(10);
        protected Dictionary<StructuredQName, XPathVariable> variables = new Dictionary<StructuredQName, XPathVariable>(20);
        protected INamespaceResolver externalResolver;
        protected Types.ItemType requiredContextItemType = AnyItemType.GetInstance();
        protected HashSet<NamespaceUri> importedSchemaNamespaces = new HashSet<NamespaceUri>();
        protected bool autoDeclare;
        protected Executable executable;
        protected RetainedStaticContext retainedStaticContext;
        protected OptimizerOptions optimizerOptions;
        protected bool parentlessContextItem;

        public virtual IEnumerable<XPathVariable> ExternalVariables => variables.Values;

        public virtual ICollection<XPathVariable> DeclaredVariables => variables.Values;
        public IndependentContext() : this(new Configuration())
        {
        }

        public IndependentContext(Configuration config)
        {
            SetConfiguration(config);
            ClearNamespaces();
            SetDefaultFunctionLibrary(31);
            SetDefaultCollationName(config.GetDefaultCollationName());
            SetOptimizerOptions(config.GetOptimizerOptions());
            PackageData pd = new PackageData(config);
            pd.SetHostLanguage(HostLanguage.XPATH, 31);
            pd.SetSchemaAware(false);
            SetPackageData(pd);
        }

        public IndependentContext(IndependentContext ic) : this(ic.GetConfiguration())
        {
            SetPackageData(ic.GetPackageData());
            SetBaseURI(ic.StaticBaseURI);
            SetContainingLocation(ic.GetContainingLocation());
            SetDefaultElementNamespace(ic.GetDefaultElementNamespace());
            SetDefaultFunctionNamespace(ic.GetDefaultFunctionNamespace());
            SetBackwardsCompatibilityMode(ic.IsInBackwardsCompatibleMode());
            namespaces = new Dictionary<string, NamespaceUri>(ic.namespaces);
            variables = new Dictionary<StructuredQName, XPathVariable>(10);
            FunctionLibraryList libList = (FunctionLibraryList)ic.GetFunctionLibrary();
            if (libList != null)
            {
                SetFunctionLibrary((FunctionLibraryList)libList.Copy());
            }

            SetDefaultCollationName(ic.GetDefaultCollationName());
            SetImportedSchemaNamespaces(ic.importedSchemaNamespaces);
            externalResolver = ic.externalResolver;
            autoDeclare = ic.autoDeclare;
            SetUnprefixedElementMatchingPolicy(ic.GetUnprefixedElementMatchingPolicy());
            SetXPathLanguageLevel(ic.GetXPathVersion());
            requiredContextItemType = ic.requiredContextItemType;
            SetExecutable(ic.GetExecutable());
            SetOptimizerOptions(ic.GetOptimizerOptions());
        }

        public override RetainedStaticContext MakeRetainedStaticContext()
        {
            if (retainedStaticContext == null)
            {
                retainedStaticContext = new RetainedStaticContext(this);
            }

            return retainedStaticContext;
        }

        public virtual void DeclareNamespace(string prefix, NamespaceUri uri)
        {
            if (prefix == null)
            {
                throw new NullReferenceException("Null prefix supplied to declareNamespace()");
            }

            if (uri == null)
            {
                throw new NullReferenceException("Null namespace URI supplied to declareNamespace()");
            }

            if (prefix == "")
            {
                SetDefaultElementNamespace(uri);
            }
            else
            {
                namespaces[prefix] = uri;
            }
        }

        public override void SetDefaultElementNamespace(NamespaceUri uri)
        {
            if (uri == null)
            {
                uri = NamespaceUri.NULL;
            }

            base.SetDefaultElementNamespace(uri);
            namespaces[""] = uri;
        }

        public virtual void ClearNamespaces()
        {
            namespaces.Clear();
            DeclareNamespace("xml", NamespaceUri.XML);
            DeclareNamespace("xsl", NamespaceUri.XSLT);
            DeclareNamespace("saxon", NamespaceUri.SAXON);
            DeclareNamespace("xs", NamespaceUri.SCHEMA);
            DeclareNamespace("", NamespaceUri.NULL);
        }

        public virtual void ClearAllNamespaces()
        {
            namespaces.Clear();
            DeclareNamespace("xml", NamespaceUri.XML);
            DeclareNamespace("", NamespaceUri.NULL);
        }

        public virtual void SetNamespaces(NodeInfo node)
        {
            namespaces.Clear();
            int kind = node.GetNodeKind();
            if (kind == Types.Type.ATTRIBUTE || kind == Types.Type.TEXT || kind == Types.Type.COMMENT || kind == Types.Type.PROCESSING_INSTRUCTION || kind == Types.Type.NAMESPACE)
            {
                node = node.GetParent();
            }

            if (node == null)
            {
                return;
            }

            IAxisIterator iter = node.IterateAxis(AxisInfo.NAMESPACE);
            while (true)
            {
                NodeInfo ns = iter.Next();
                if (ns == null)
                {
                    return;
                }

                string prefix = ns.GetLocalPart();
                if (prefix == "")
                {
                    SetDefaultElementNamespace(NamespaceUri.Of(ns.GetStringValue()));
                }
                else
                {
                    DeclareNamespace(ns.GetLocalPart(), NamespaceUri.Of(ns.GetStringValue()));
                }
            }
        }

        public void SetNamespaceResolver(INamespaceResolver resolver)
        {
            externalResolver = resolver;
        }

        public virtual void SetAllowUndeclaredVariables(bool allow)
        {
            autoDeclare = allow;
        }

        public virtual bool IsAllowUndeclaredVariables()
        {
            return autoDeclare;
        }

        public XPathVariable DeclareVariable(QNameValue qname)
        {
            return DeclareVariable(qname.GetStructuredQName());
        }

        public XPathVariable DeclareVariable(NamespaceUri namespaceURI, string localName)
        {
            StructuredQName qName = new StructuredQName("", namespaceURI == null ? NamespaceUri.NULL : namespaceURI, localName);
            return DeclareVariable(qName);
        }

        public virtual XPathVariable DeclareVariable(StructuredQName qName)
        {
            XPathVariable var = variables.GetOrDefault(qName);
            if (var != null)
            {
                return var;
            }
            else
            {
                var = XPathVariable.Make(qName);
                int slot = variables.Count;
                var.SetSlotNumber(slot);
                variables[qName] = var;
                return var;
            }
        }

        public virtual XPathVariable GetExternalVariable(StructuredQName qName)
        {
            return variables.GetOrDefault(qName);
        }

        public virtual int GetSlotNumber(QNameValue qname)
        {
            StructuredQName sq = qname.GetStructuredQName();
            XPathVariable var = variables.GetOrDefault(sq);
            if (var == null)
            {
                return -1;
            }

            return var.LocalSlotNumber;
        }

        public override INamespaceResolver GetNamespaceResolver()
        {
            if (externalResolver != null)
            {
                return externalResolver;
            }
            else
            {
                return this;
            }
        }

        public NamespaceUri GetURIForPrefix(string prefix, bool useDefault)
        {
            if (externalResolver != null)
            {
                return externalResolver.GetURIForPrefix(prefix, useDefault);
            }

            if ((prefix.Length == 0))
            {
                return useDefault ? GetDefaultElementNamespace() : NamespaceUri.NULL;
            }
            else
            {
                return namespaces.GetOrDefault(prefix);
            }
        }

        public IEnumerator<string> IteratePrefixes()
        {
            if (externalResolver != null)
            {
                return externalResolver.IteratePrefixes();
            }
            else
            {
                return namespaces.Keys.GetEnumerator();
            }
        }

        public override Expression BindVariable(StructuredQName qName)
        {
            XPathVariable var = variables.GetOrDefault(qName);
            if (var == null)
            {
                if (autoDeclare)
                {
                    return new LocalVariableReference(DeclareVariable(qName));
                }
                else
                {
                    throw new XPathException("Undeclared variable in XPath expression: $" + qName.ClarkName, "XPST0008");
                }
            }
            else
            {
                return new LocalVariableReference(var);
            }
        }

        public SlotManager GetStackFrameMap()
        {
            SlotManager map = GetConfiguration().MakeSlotManager();
            XPathVariable[] va = new XPathVariable[variables.Count];
            foreach (XPathVariable var in variables.Values)
            {
                va[var.LocalSlotNumber] = var;
            }

            foreach (XPathVariable v in va)
            {
                map.AllocateSlotNumber(v.GetVariableQName(), v);
            }

            return map;
        }

        public override bool IsImportedSchema(NamespaceUri @namespace)
        {
            return importedSchemaNamespaces.Contains(@namespace);
        }

        public override HashSet<NamespaceUri> GetImportedSchemaNamespaces()
        {
            return importedSchemaNamespaces;
        }

        public virtual void SetImportedSchemaNamespaces(HashSet<NamespaceUri> namespaces)
        {
            importedSchemaNamespaces = namespaces;
            if (namespaces.Count > 0)
            {
                SetSchemaAware(true);
            }
        }

        public virtual void SetRequiredContextItemType(Types.ItemType type)
        {
            requiredContextItemType = type;
        }

        public override Types.ItemType GetRequiredContextItemType()
        {
            return requiredContextItemType;
        }

        public virtual void SetOptimizerOptions(OptimizerOptions options)
        {
            this.optimizerOptions = options;
        }

        public override OptimizerOptions GetOptimizerOptions()
        {
            return this.optimizerOptions;
        }

        public virtual void SetExecutable(Executable exec)
        {
            executable = exec;
        }

        public virtual Executable GetExecutable()
        {
            return executable;
        }

        public virtual int GetColumnNumber()
        {
            return -1;
        }

        public virtual string GetPublicId()
        {
            return null;
        }

        public virtual int GetLineNumber()
        {
            return -1;
        }

        public bool IsContextItemParentless()
        {
            return parentlessContextItem;
        }

        public virtual void SetContextItemParentless(bool parentless)
        {
            parentlessContextItem = parentless;
        }
    }
}
