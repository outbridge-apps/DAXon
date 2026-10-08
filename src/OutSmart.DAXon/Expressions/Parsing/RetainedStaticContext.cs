////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Net;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Expressions.Parsing
{
    public class RetainedStaticContext : INamespaceResolver
    {
        private readonly Configuration config;
        private PackageData packageData;
        private URI staticBaseUri;
        private string staticBaseUriString;
        private string defaultCollationName;
        private INamespaceResolver namespaces; // Normally a NamespaceMap, except for the JAXP case
        private NamespaceUri defaultFunctionNamespace = NamespaceUri.FN;
        private NamespaceUri defaultElementNamespace;
        private DecimalFormatManager decimalFormatManager;
        private bool backwardsCompatibility;

        public virtual string StaticBaseUriString
        {
            get => staticBaseUriString; set
            {
                if (value != null)
                {
                    staticBaseUriString = value;
                    try
                    {
                        this.staticBaseUri = new URI(value);
                    }
                    catch (URISyntaxException)
                    {
                        staticBaseUri = null;
                    }
                }
            }
        }

        public virtual string DefaultCollationName
        {
            get => defaultCollationName; set
            {
                this.defaultCollationName = value;
            }
        }

        public virtual NamespaceUri DefaultFunctionNamespace
        {
            get => defaultFunctionNamespace; set
            {
                this.defaultFunctionNamespace = value;
            }
        }

        public virtual NamespaceUri DefaultElementNamespace
        {
            get => defaultElementNamespace == null ? NamespaceUri.NULL : defaultElementNamespace; set
            {
                defaultElementNamespace = value;
            }
        }
        public RetainedStaticContext(Configuration config)
        {
            this.config = config;
            packageData = new PackageData(config);
            namespaces = NamespaceMap.EmptyMap();
            defaultCollationName = NamespaceConstant.CODEPOINT_COLLATION_URI;
        }

        public RetainedStaticContext(IStaticContext sc)
        {
            this.config = sc.GetConfiguration();
            this.packageData = sc.GetPackageData();
            if (sc.StaticBaseURI != null)
            {
                staticBaseUriString = sc.StaticBaseURI;
                try
                {
                    this.staticBaseUri = ExpressionTool.GetBaseURI(sc, null, true);
                }
                catch (XPathException)
                {
                    staticBaseUri = null;
                }
            }

            this.defaultCollationName = sc.GetDefaultCollationName();
            this.decimalFormatManager = sc.GetDecimalFormatManager();
            this.defaultElementNamespace = sc.GetDefaultElementNamespace();
            defaultFunctionNamespace = sc.GetDefaultFunctionNamespace();
            backwardsCompatibility = sc.IsInBackwardsCompatibleMode();
            if (!Core.Version.platform.JAXPStaticContextCheck(this, sc))
            {
                namespaces = sc.GetNamespaceResolver();
            }
        }

        public virtual Configuration GetConfiguration()
        {
            return config;
        }

        public virtual void SetPackageData(PackageData packageData)
        {
            this.packageData = packageData;
        }

        public virtual PackageData GetPackageData()
        {
            return packageData;
        }

        public virtual URI GetStaticBaseUri()
        {
            if (staticBaseUri == null)
            {
                if (staticBaseUriString == null || (staticBaseUriString.Length == 0))
                {
                    return null;
                }
                else
                {
                    throw new XPathException("Supplied static base URI " + staticBaseUriString + " is not a valid URI");
                }
            }

            return staticBaseUri;
        }

        public virtual DecimalFormatManager GetDecimalFormatManager()
        {
            return decimalFormatManager;
        }

        public virtual void SetDecimalFormatManager(DecimalFormatManager decimalFormatManager)
        {
            this.decimalFormatManager = decimalFormatManager;
        }

        public virtual bool IsBackwardsCompatibility()
        {
            return backwardsCompatibility;
        }

        public virtual void SetBackwardsCompatibility(bool backwardsCompatibility)
        {
            this.backwardsCompatibility = backwardsCompatibility;
        }

        public virtual void DeclareNamespace(string prefix, NamespaceUri uri)
        {
            if (namespaces is NamespaceMap)
            {
                namespaces = ((NamespaceMap)namespaces).Put(prefix, uri);
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public virtual NamespaceUri GetURIForPrefix(string prefix, bool useDefault)
        {
            NamespaceUri uri = namespaces.GetURIForPrefix(prefix, useDefault);

            // For an unprefixed name resolved with useDefault, the "default namespace" is the default
            // element/type namespace. XSLT's xpath-default-namespace populates defaultElementNamespace but not
            // the namespace resolver's empty-prefix binding (unlike XQuery's `declare default element
            // namespace`), so fall back to it — otherwise xs:QName('ncname') gets no namespace in XSLT
            // (K2-SeqExprCast-201). XQuery already binds it, so this changes nothing there.
            if ((uri == null || uri.Equals(NamespaceUri.NULL)) && useDefault && string.IsNullOrEmpty(prefix)
                && defaultElementNamespace != null && !defaultElementNamespace.Equals(NamespaceUri.NULL))
            {
                return defaultElementNamespace;
            }

            return uri;
        }

        public virtual IEnumerator<string> IteratePrefixes()
        {
            return namespaces.IteratePrefixes();
        }

        public virtual bool DeclaresSameNamespaces(RetainedStaticContext other)
        {
            return namespaces.Equals(other.namespaces);
        }

        public override int GetHashCode()
        {
            int h = 0x2457cbce;
            if (staticBaseUriString != null)
            {
                h ^= staticBaseUriString.GetHashCode();
            }

            h ^= defaultCollationName.GetHashCode();
            h ^= defaultFunctionNamespace.GetHashCode();
            h ^= namespaces.GetHashCode();
            return h;
        }

        public override bool Equals(object other)
        {
            if (!(other is RetainedStaticContext))
            {
                return false;
            }

            RetainedStaticContext r = (RetainedStaticContext)other;
            return ExpressionTool.EqualOrNull(staticBaseUriString, r.staticBaseUriString) && defaultCollationName.Equals(r.defaultCollationName) && defaultFunctionNamespace.Equals(r.defaultFunctionNamespace) && namespaces.Equals(r.namespaces);
        }

        public virtual void SetNamespaces(INamespaceResolver namespaces)
        {
            this.namespaces = namespaces;
        }

        public virtual NamespaceMap GetNamespaceMap()
        {

            // This fails for a JAXP static context, whose namespaces cannot be enumerated
            return NamespaceMap.FromNamespaceResolver(namespaces);
        }
    }
}