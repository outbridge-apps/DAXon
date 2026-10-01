////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Streams;
namespace OutSmart.DAXon.Tracing
{
    public class ExpressionPresenter
    {
        private Configuration config;
        private IReceiver receiver;
        private ComplexContentOutputter cco;
        private int depth = 0;
        private bool inStartTag = false;
        private string nextRole = null;
        private readonly Stack<Expression> expressionStack = new Stack<Expression>();
        private readonly Stack<string> nameStack = new Stack<string>();
        private NamespaceMap namespaceMap = NamespaceMap.EmptyMap();
        private NamespaceUri defaultNamespace;
        private ExportOptions options = new ExportOptions();
        /// <summary>
        /// Make an uncommitted ExpressionPresenter. This must be followed by a call on init()
        /// </summary>
        public ExpressionPresenter()
        {
        }

        public ExpressionPresenter(Configuration config) : this(config, config.Logger)
        {
        }

        public ExpressionPresenter(Configuration config, StreamResult @out) : this(config, @out, false)
        {
        }

        public ExpressionPresenter(Builder builder) : this()
        {
            Init(builder.GetConfiguration(), builder, false);
        }

        public ExpressionPresenter(Configuration config, StreamResult @out, bool checksum)
        {
            Init(config, @out, checksum);
        }

        public ExpressionPresenter(Configuration config, Logger @out) : this(config, @out.AsStreamResult())
        {
        }

        public ExpressionPresenter(Configuration config, IReceiver receiver)
        {
            this.config = config;
            this.receiver = receiver;
            this.cco = new ComplexContentOutputter(receiver);
            try
            {
                cco.Open();
                cco.StartDocument(ReceiverOption.NONE);
            }
            catch (XPathException err)
            {
                err.ToString();
                throw new InvalidOperationException(err.Message);
            }
        }

        public virtual void Init(Configuration config, StreamResult @out, bool checksum)
        {
            SerializationProperties props = MakeDefaultProperties(config);
            if (config.XMLVersion == Configuration.XML11)
            {
                props.SetProperty(DAXonOutputKeys.VERSION, "1.1");
            }

            try
            {
                receiver = config.SerializerFactory.GetReceiver(@out, props);
                receiver = new NamespaceReducer(receiver);
                if (checksum)
                {
                    receiver = new CheckSumFilter(receiver);
                }

                cco = new ComplexContentOutputter(receiver);
            }
            catch (XPathException err)
            {
                err.ToString();
                throw new InvalidOperationException(err.Message);
            }

            this.config = config;
            try
            {
                cco.Open();
                cco.StartDocument(ReceiverOption.NONE);
            }
            catch (XPathException err)
            {
                err.ToString();
                throw new InvalidOperationException(err.Message);
            }
        }

        public virtual void Init(Configuration config, IReceiver @out, bool checksum)
        {
            receiver = @out;
            receiver = new NamespaceReducer(receiver);
            if (checksum)
            {
                receiver = new CheckSumFilter(receiver);
            }

            cco = new ComplexContentOutputter(receiver);
            this.config = config;
            try
            {
                cco.Open();
                cco.StartDocument(ReceiverOption.NONE);
            }
            catch (XPathException err)
            {
                err.ToString();
                throw new InvalidOperationException(err.Message);
            }
        }

        public virtual void SetDefaultNamespace(NamespaceUri @namespace)
        {
            defaultNamespace = @namespace;
            namespaceMap = namespaceMap.Put("", @namespace);
        }

        public virtual void SetOptions(ExportOptions options)
        {
            this.options = options;
        }

        public virtual ExportOptions GetOptions()
        {
            return options;
        }

        public virtual void SetRelocatable(bool relocatable)
        {
            this.options.relocatable = relocatable;
        }

        public static IReceiver DefaultDestination(Configuration config, Logger @out)
        {
            SerializationProperties props = MakeDefaultProperties(config);
            return config.SerializerFactory.GetReceiver(@out.AsStreamResult(), props);
        }

        public static SerializationProperties MakeDefaultProperties(Configuration config)
        {
            SerializationProperties props = new SerializationProperties();
            props.SetProperty(DAXonOutputKeys.METHOD, "xml");
            props.SetProperty(DAXonOutputKeys.INDENT, "yes");
            if (config.IsLicensedFeature(Configuration.LicenseFeature.PROFESSIONAL_EDITION))
            {
                props.SetProperty(DAXonOutputKeys.INDENT_SPACES, "1");
                props.SetProperty(DAXonOutputKeys.LINE_LENGTH, "4096");
            }

            props.SetProperty(DAXonOutputKeys.OMIT_XML_DECLARATION, "no");
            props.SetProperty(DAXonOutputKeys.ENCODING, "utf-8");
            props.SetProperty(DAXonOutputKeys.VERSION, "1.0");
            props.SetProperty(DAXonOutputKeys.SINGLE_QUOTES, "yes");
            return props;
        }

        public virtual int StartElement(string name, Expression expr)
        {
            Expression parent = expressionStack.Count == 0 ? null : expressionStack.Peek();
            expressionStack.Push(expr);
            nameStack.Push("*" + name);
            int n = _startElement(name);
            if (parent == null || expr.GetRetainedStaticContext() != parent.GetRetainedStaticContext())
            {
                if (expr.GetRetainedStaticContext() == null)
                {
                }
                else
                {
                    EmitRetainedStaticContext(expr.GetRetainedStaticContext(), parent == null ? null : parent.GetRetainedStaticContext());
                }
            }

            string mod = expr.GetLocation().GetSystemId();
            if (mod != null && parent != null && (parent.GetLocation().GetSystemId() == null || !parent.GetLocation().GetSystemId().Equals(mod)))
            {
                EmitAttribute("module", TruncatedModuleName(mod));
            }

            int lineNr = expr.GetLocation().GetLineNumber();
            if (parent == null || (parent.GetLocation().GetLineNumber() != lineNr && lineNr != -1))
            {
                EmitAttribute("line", lineNr + "");
            }

            return n;
        }

        private string TruncatedModuleName(string module)
        {
            if (options.relocatable)
            {

                // If not exporting the base URI, cut the filename used for diagnostic location of errors down to its last component
                string[] parts = module.SplitRegex("/");
                for (int p = parts.Length - 1; p >= 0; p--)
                {
                    if (!(parts[p].Length == 0))
                    {
                        return parts[p];
                    }
                }
            }

            return module;
        }

        public virtual void EmitRetainedStaticContext(RetainedStaticContext sc, RetainedStaticContext parentSC)
        {
            try
            {
                if (!options.suppressStaticContext && !options.relocatable && sc.GetStaticBaseUri() != null && (parentSC == null || !sc.GetStaticBaseUri().Equals(parentSC.GetStaticBaseUri())))
                {
                    EmitAttribute("baseUri", sc.StaticBaseUriString);
                }

                if (!sc.DefaultCollationName.Equals(NamespaceConstant.CODEPOINT_COLLATION_URI) && (parentSC == null || !sc.DefaultCollationName.Equals(parentSC.DefaultCollationName)))
                {
                    EmitAttribute("defaultCollation", sc.DefaultCollationName);
                }

                if (!sc.DefaultElementNamespace.IsEmpty() && (parentSC == null || !sc.DefaultElementNamespace.Equals(parentSC.DefaultElementNamespace)))
                {
                    EmitAttribute("defaultElementNS", sc.DefaultElementNamespace.ToString());
                }

                string defaultFnNs = sc.DefaultFunctionNamespace.ToString();
                if (!NamespaceConstant.FN.Equals(defaultFnNs))
                {
                    EmitAttribute("defaultFunctionNS", defaultFnNs);
                }

                if (!options.suppressStaticContext && (parentSC == null || !sc.DeclaresSameNamespaces(parentSC)))
                {
                    bool includeXmlNamespace = "JS".Equals(GetOptions().target) && GetOptions().targetVersion >= 2;
                    EmitAttribute("ns", GetNamespacesAsString(sc.GetNamespaceMap(), includeXmlNamespace));
                }
            }
            catch (XPathException e)
            {
                throw new InvalidOperationException(e.Message, e);
            }
        }

        public static string GetNamespacesAsString(NamespaceMap sc, bool includeXmlNamespace)
        {

            // Note that this will throw an NotSupportedException if the context does
            // not allow namespace prefixes to be enumerated: that @is, if it is a JAXP static context.
            // Fortunately we don't need to serialize XPath expressions in that scenario.
            UnicodeBuilder ub = new UnicodeBuilder();
            IEnumerator<string> iter = sc.IteratePrefixes();
            while (iter.MoveNext())
            {
                string p = iter.Current;
                if (includeXmlNamespace || !p.Equals("xml"))
                {

                    //Bugs 6198, 6274
                    NamespaceUri uri = sc.GetURIForPrefix(p, true);
                    ub.Append(p);
                    ub.Append('=');
                    if (uri.Equals(NamespaceUri.GetUriForConventionalPrefix(p)))
                    {
                        ub.Append('~');
                    }
                    else
                    {
                        UnicodeString uUri = uri.ToUnicodeString();
                        if (Whitespace.ContainsWhitespace(uUri.CodePoints()))
                        {
                            throw new XPathException("Cannot export a stylesheet if namespaces contain whitespace: '" + uri + "'");
                        }

                        ub.Append(uUri);
                    }

                    ub.Append(' ');
                }
            }

            return ub.ToString().Trim();
        }

        public virtual int StartElement(string name)
        {
            nameStack.Push(name);
            return _startElement(name);
        }

        private int _startElement(string name)
        {

            try
            {
                if (inStartTag)
                {
                    cco.StartContent();
                    inStartTag = false;
                }

                INodeName nodeName;
                if (defaultNamespace == null)
                {
                    nodeName = new NoNamespaceName(name);
                }
                else
                {
                    nodeName = new FingerprintedQName("", defaultNamespace, name);
                }

                cco.StartElement(nodeName, Untyped.INSTANCE, Loc.NONE, ReceiverOption.NONE);
                if (nextRole != null)
                {
                    EmitAttribute("role", nextRole);
                    nextRole = null;
                }
            }
            catch (XPathException err)
            {
                err.ToString();
                throw new InvalidOperationException(err.Message);
            }

            inStartTag = true;
            return depth++;
        }

        public virtual void SetChildRole(string role)
        {
            nextRole = role;
        }

        public virtual void EmitAttribute(string name, string value)
        {
            if (value != null)
            {
                if (name.Equals("module"))
                {
                    value = TruncatedModuleName(value);
                }

                try
                {
                    cco.Attribute(new NoNamespaceName(name), BuiltInAtomicType.UNTYPED_ATOMIC, value, Loc.NONE, ReceiverOption.NONE);
                }
                catch (XPathException err)
                {
                    err.ToString();
                    throw new InvalidOperationException(err.Message);
                }
            }
        }

        public virtual void EmitAttribute(string name, StructuredQName value)
        {
            string attVal = value.EQName;
            try
            {
                cco.Attribute(new NoNamespaceName(name), BuiltInAtomicType.UNTYPED_ATOMIC, attVal, Loc.NONE, ReceiverOption.NONE);
            }
            catch (XPathException err)
            {
                err.ToString();
                throw new InvalidOperationException(err.Message);
            }
        }

        public virtual void Namespace(string prefix, NamespaceUri uri)
        {
            try
            {
                cco.Namespace(prefix, uri, ReceiverOption.NONE);
            }
            catch (XPathException e)
            {
                e.ToString();
                throw new InvalidOperationException(e.Message);
            }
        }

        public virtual int EndElement()
        {

            try
            {
                if (inStartTag)
                {
                    cco.StartContent();
                    inStartTag = false;
                }

                cco.EndElement();
            }
            catch (XPathException err)
            {
                err.ToString();
                throw new InvalidOperationException(err.Message);
            }

            string name = nameStack.Pop();
            if (name.StartsWith("*", StringComparison.Ordinal))
            {
                expressionStack.Pop();
            }

            return --depth;
        }

        public virtual void StartSubsidiaryElement(string name)
        {
            StartElement(name);
        }

        /// <summary>
        /// End a child element in the output
        /// </summary>
        public virtual void EndSubsidiaryElement()
        {
            EndElement();
        }

        /// <summary>
        /// Close the output
        /// </summary>
        public virtual void Dispose()
        {
            try
            {
                if (receiver is CheckSumFilter)
                {
                    int c = ((CheckSumFilter)receiver).Checksum;
                    cco.ProcessingInstruction(CheckSumFilter.SIGMA, BMPString.Of(((int)(c)).ToString("x")), Loc.NONE, ReceiverOption.NONE);
                    string digest = ((CheckSumFilter)receiver).Digest;
                    cco.ProcessingInstruction(CheckSumFilter.SIGMA2, BMPString.Of(digest), Loc.NONE, ReceiverOption.NONE);
                }

                cco.EndDocument();
                cco.Close();
            }
            catch (XPathException err)
            {
                err.ToString();
                throw new InvalidOperationException(err.Message);
            }
        }

        public virtual Configuration GetConfiguration()
        {
            return config;
        }

        public virtual NamePool GetNamePool()
        {
            return config.GetNamePool();
        }

        public virtual TypeHierarchy GetTypeHierarchy()
        {
            return config.GetTypeHierarchy();
        }

        public static string JsEscape(string @in)
        {
            StringBuilder @out = new StringBuilder(@in.Length);
            for (int i = 0; i < @in.Length; i++)
            {
                char c = @in[i];
                switch (c)
                {
                    case '\'':
                        @out.Append("\\'");
                        break;
                    case '"':
                        @out.Append("\\\"");
                        break;
                    case '\b':
                        @out.Append("\\b");
                        break;
                    case '\f':
                        @out.Append("\\f");
                        break;
                    case '\n':
                        @out.Append("\\n");
                        break;
                    case '\r':
                        @out.Append("\\r");
                        break;
                    case '\t':
                        @out.Append("\\t");
                        break;
                    case '\\':
                        @out.Append("\\\\");
                        break;
                    default:
                        if (c < 32 || (c > 127 && c < 160) || c > UTF16CharacterSet.SURROGATE1_MIN)
                        {
                            @out.Append("\\u");
                            StringBuilder hex = new StringBuilder(((int)(c)).ToString("x").ToUpperInvariant());
                            while (hex.Length < 4)
                            {
                                hex.Insert(0, "0");
                            }

                            @out.Append(hex);
                        }
                        else
                        {
                            @out.Append(c);
                        }

                        break;
                }
            }

            return @out.ToString();
        }

        public class ExportOptions
        {
            public string target = "";
            public int targetVersion = 0;
            public bool relocatable = false;
            public StylesheetPackage rootPackage;
            public Dictionary<Component, int> componentMap;
            public Dictionary<StylesheetPackage, int> packageMap;
            public bool explaining;
            public bool suppressStaticContext;
        }
    }
}