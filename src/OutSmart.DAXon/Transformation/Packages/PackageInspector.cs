////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Types;
using System;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Transformation.Packages
{
    internal sealed class PackageInspector : ProxyReceiver
    {
        private bool isSefFile;
        private string packageName;
        private string packageVersion = "1";
        private int elementCount;
        private string diagnostics;

        private VersionedPackageName NameAndVersion
        {
            get
            {
                if (packageName == null)
                {
                    return null;
                }

                try
                {
                    return new VersionedPackageName(packageName, packageVersion);
                }
                catch (XPathException)
                {
                    return null;
                }
            }
        }

        public string Diagnostics => diagnostics;
        public PackageInspector(PipelineConfiguration pipe) : base(new Sink(pipe))
        {
        }

        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            if (elementCount++ >= 1)
            {

                // abort the parse when the second start element tag is found
                throw new XPathException("#start#");
            }

            isSefFile = elemName.HasURI(NamespaceUri.SAXON_XSLT_EXPORT);
            if (attributes.Get(NamespaceUri.NULL, "name") == null)
            {
                diagnostics = "Top level element " + elemName.GetStructuredQName().EQName + " has no @name attribute";
            }
            else
            {
                packageName = attributes.Get(NamespaceUri.NULL, "name").Value;
            }

            if (attributes.Get(NamespaceUri.NULL, "package-version") != null)
            {
                packageVersion = attributes.Get(NamespaceUri.NULL, "package-version").Value;
            }

            if (attributes.Get(NamespaceUri.NULL, "packageVersion") != null)
            {
                packageVersion = attributes.Get(NamespaceUri.NULL, "packageVersion").Value;
            }

            AttributeInfo saxonVersion = attributes.Get(NamespaceUri.NULL, "saxonVersion");
            if (saxonVersion != null)
            {
                if (saxonVersion.Value.StartsWith("9", StringComparison.Ordinal))
                {
                    throw new XPathException("Saxon " + Core.Version.ProductVersion + " cannot load a SEF file created using version " + saxonVersion.Value);
                }
            }
        }

        public PackageDetails GetPackageDetails(string top, Configuration config)
        {
            try
            {
                ParseOptions options = new ParseOptions().WithDTDValidationMode(Validation.SKIP).WithSchemaValidationMode(Validation.SKIP);
                Sender.Send(new ResolvedResource { SystemId = top }, this, options);
            }
            catch (XPathException e)
            {

                // early exit is expected
                if (!e.Message.Equals("#start#"))
                {
                    throw;
                }
            }

            VersionedPackageName vp = NameAndVersion;
            if (vp == null)
            {
                return null;
            }
            else
            {
                PackageDetails details = new PackageDetails();
                details.nameAndVersion = vp;
                if (isSefFile)
                {
                    details.exportLocation = new ResolvedResource { SystemId = top };
                }
                else
                {
                    details.sourceLocation = new ResolvedResource { SystemId = top };
                }

                return details;
            }
        }
    }
}