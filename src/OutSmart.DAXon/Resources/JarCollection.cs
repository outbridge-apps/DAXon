////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Streams;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace OutSmart.DAXon.Resources
{
    // Port of net.sf.saxon.resource.JarCollection (Saxon 12.9): the files of a ZIP archive (.zip, .jar,
    // .docx, .xlsx, or a jar: URI) as a collection, each member typed like a file in a directory and
    // named jar:<archive>!/<entry>. The archive and every member it inflates are held to the policy's
    // MaxInputBytes, so a small archive cannot inflate past the cap, and its ReadFilter is asked about
    // every member by that name. Upstream's URI listing read every other entry (it advanced twice per
    // loop); here every file entry is listed.
    internal sealed class JarCollection : AbstractResourceCollection
    {
        private readonly string archiveURI;
        private readonly string entryPrefix;
        private ISpaceStrippingRule whitespaceRules;

        public JarCollection(IXPathContext context, string collectionURI, URIQueryParameters @params) : base(context.GetConfiguration())
        {
            this.collectionURI = collectionURI;
            this.@params = @params;
            string archive = collectionURI;
            string inner = "";
            if (archive.StartsWith("jar:", StringComparison.Ordinal))
            {
                archive = archive.Substring(4);
                int bang = archive.IndexOf("!/", StringComparison.Ordinal);
                if (bang >= 0)
                {
                    inner = archive.Substring(bang + 2);
                    archive = archive.Substring(0, bang);
                }
            }

            archiveURI = archive;
            entryPrefix = inner;
        }

        public override bool StripWhitespace(ISpaceStrippingRule rules)
        {
            this.whitespaceRules = rules;
            return true;
        }

        public override IEnumerator<string> GetResourceURIs(IXPathContext context)
        {
            using (ZipArchive zip = OpenArchive())
            {
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    if (Selected(entry))
                    {
                        yield return MemberURI(entry);
                    }
                }
            }
        }

        public override IEnumerator<IResource> GetResources(IXPathContext context)
        {
            ParseOptions options = OptionsFromQueryParameters(@params, context).WithSpaceStrippingRule(whitespaceRules);
            bool metadata = @params?.MetaData == true;
            long max = InputSizeLimit.MaxFor(config);
            using (ZipArchive zip = OpenArchive())
            {
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    if (!Selected(entry))
                    {
                        continue;
                    }

                    string uri = MemberURI(entry);
                    IResource resource;
                    try
                    {
                        // Every member, as a directory collection asks about every file.
                        string denied = ResourceGate.CheckMember(config, uri, Api.ResourceKind.Collection);
                        if (denied != null)
                        {
                            throw new XPathException(denied, "FODC0002");
                        }

                        var details = new InputDetails();
                        using (Stream member = InputSizeLimit.Apply(entry.Open(), max, uri, "FODC0002"))
                        {
                            details.binaryContent = BinaryResource.ReadBinaryFromStream(member, uri);
                        }

                        details.contentType = @params?.ContentType ?? GuessContentTypeFromName(entry.FullName)
                            ?? Internal.Net.URLConnection.GuessContentTypeFromBytes(details.binaryContent, details.binaryContent.Length);
                        details.parseOptions = options;
                        details.resourceUri = uri;
                        if (@params?.OnError != null)
                        {
                            details.onError = @params.OnError.Value;
                        }

                        resource = MakeResource(context, details);
                        if (metadata)
                        {
                            resource = new MetadataResource(uri, resource, EntryProperties(entry));
                        }
                    }
                    catch (Exception e) when (e is XPathException || e is InvalidDataException || e is IOException)
                    {
                        XPathException err = e as XPathException ?? new XPathException(e.Message, "FODC0002");
                        int onError = @params?.OnError ?? URIQueryParameters.ON_ERROR_FAIL;
                        if (onError == URIQueryParameters.ON_ERROR_FAIL)
                        {
                            resource = new FailedResource(uri, err);
                        }
                        else
                        {
                            if (onError == URIQueryParameters.ON_ERROR_WARNING)
                            {
                                context.GetController()?.Warning("collection(): failed to read " + uri + ": " + err.Message, err.ShowErrorCode(), null);
                            }

                            continue;
                        }
                    }

                    yield return resource;
                }
            }
        }

        private ZipArchive OpenArchive()
        {
            string denied = ResourceGate.CheckRead(config, archiveURI, Api.ResourceKind.Collection);
            if (denied != null)
            {
                throw new XPathException(denied, "FODC0002");
            }

            // Read whole and closed at once: a member that fails ends the caller's iteration without
            // disposing it, and an open archive would stay locked until finalization.
            try
            {
                byte[] bytes;
                using (Stream stream = InputSizeLimit.Apply(ResourceLoader.UrlStream(config, archiveURI, Api.ResourceKind.Collection),
                    InputSizeLimit.MaxFor(config), archiveURI, "FODC0002"))
                {
                    bytes = BinaryResource.ReadBinaryFromStream(stream, archiveURI);
                }

                return new ZipArchive(new MemoryStream(bytes, false), ZipArchiveMode.Read);
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is UriFormatException)
            {
                throw new XPathException("Unable to read JAR/ZIP file " + collectionURI + ": " + e.Message, "FODC0002");
            }
        }

        // A file entry under the jar: URI's inner path, accepted by ?select= / ?match= if given.
        private bool Selected(ZipArchiveEntry entry)
        {
            string name = entry.FullName;
            if (name.EndsWith("/", StringComparison.Ordinal) || !name.StartsWith(entryPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            Func<string, string, bool> filter = @params?.FilenameFilter;
            return filter == null || filter(null, name);
        }

        private string MemberURI(ZipArchiveEntry entry)
        {
            return "jar:" + archiveURI + "!/" + entry.FullName;
        }

        // Upstream's entry properties as far as .NET exposes them: crc and comment from .NET 7 on,
        // extra and compression-method nowhere.
        private static IDictionary<string, IGroundedValue> EntryProperties(ZipArchiveEntry entry)
        {
            var properties = new Dictionary<string, IGroundedValue>
            {
                ["entry-name"] = StringValue.MakeStringValue(entry.FullName),
                ["size"] = new Int64Value(entry.Length),
                ["compressed-size"] = new Int64Value(entry.CompressedLength),
                ["last-modified"] = DateTimeValue.FromJavaTime(entry.LastWriteTime.ToUnixTimeMilliseconds()),
            };
#if NET
            properties["crc"] = new Int64Value(entry.Crc32);
            properties["comment"] = StringValue.MakeStringValue(entry.Comment ?? "");
#endif
            return properties;
        }
    }
}
