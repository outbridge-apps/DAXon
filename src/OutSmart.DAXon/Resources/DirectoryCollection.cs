////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.IO;

namespace OutSmart.DAXon.Resources
{
    // Faithful port of net.sf.saxon.resource.DirectoryCollection (Saxon 12.9). Was missing entirely, so
    // collection('some-directory/') — the standard multi-document input pattern (xsl:merge log-files
    // tests etc.) — raised FODC0002 from the finder.
    // A resource collection containing all, or selected, files within a filestore directory.
    internal sealed class DirectoryCollection : AbstractResourceCollection
    {
        private readonly DirectoryInfo dirFile;
        private ISpaceStrippingRule whitespaceRules;

        public DirectoryCollection(Configuration config, string collectionURI, DirectoryInfo file, URIQueryParameters @params) : base(config)
        {
            if (collectionURI == null)
            {
                throw new ArgumentNullException(nameof(collectionURI));
            }

            this.collectionURI = collectionURI;
            dirFile = file;
            if (@params == null)
            {
                this.@params = new URIQueryParameters("", config);
            }
            else
            {
                this.@params = @params;
            }
        }

        public override bool StripWhitespace(ISpaceStrippingRule rules)
        {
            this.whitespaceRules = rules;
            return true;
        }

        public override IEnumerator<string> GetResourceURIs(IXPathContext context)
        {
            return DirectoryContents(dirFile, @params);
        }

        public override IEnumerator<IResource> GetResources(IXPathContext context)
        {
            ParseOptions options = OptionsFromQueryParameters(@params, context).WithSpaceStrippingRule(whitespaceRules);
            bool metadata = @params.MetaData == true;
            IEnumerator<string> resourceURIs = GetResourceURIs(context);
            while (resourceURIs.MoveNext())
            {
                string @in = resourceURIs.Current;
                IResource resource;
                try
                {
                    InputDetails details = GetInputDetails(@in);
                    details.resourceUri = @in;
                    details.parseOptions = options;
                    if (@params.ContentType != null)
                    {
                        details.contentType = @params.ContentType;
                    }

                    resource = MakeResource(context, details);
                    if (metadata)
                    {
                        resource = new MetadataResource(resource.ResourceURI, resource, FileProperties(details));
                    }
                }
                catch (XPathException e)
                {
                    int? onError = @params.OnError;
                    if (onError == URIQueryParameters.ON_ERROR_FAIL)
                    {
                        resource = new FailedResource(@in, e);
                    }
                    else if (onError == URIQueryParameters.ON_ERROR_WARNING)
                    {
                        context.GetController().Warning("collection(): failed to parse " + @in + ": " + e.Message, e.ShowErrorCode(), null);
                        continue;
                    }
                    else
                    {
                        continue;
                    }
                }

                if (resource != null)
                {
                    yield return resource;
                }
            }
        }

        // Upstream's java.io.File properties of a member; canonical-path does not resolve links here.
        private static IDictionary<string, IGroundedValue> FileProperties(InputDetails details)
        {
            var properties = new Dictionary<string, IGroundedValue>();
            if (details.contentType != null)
            {
                properties["content-type"] = StringValue.MakeStringValue(details.contentType);
            }

            if (details.encoding != null)
            {
                properties["encoding"] = StringValue.MakeStringValue(details.encoding);
            }

            var file = new FileInfo(new Uri(details.resourceUri).LocalPath);
            if (!file.Exists)
            {
                return properties;
            }

            properties["path"] = StringValue.MakeStringValue(file.FullName);
            properties["absolute-path"] = StringValue.MakeStringValue(file.FullName);
            properties["canonical-path"] = StringValue.MakeStringValue(file.FullName);
            properties["can-read"] = BooleanValue.Get(CanRead(file));
            properties["can-write"] = BooleanValue.Get((file.Attributes & FileAttributes.ReadOnly) == 0);
            properties["can-execute"] = BooleanValue.Get(CanExecute(file));
            properties["is-hidden"] = BooleanValue.Get((file.Attributes & FileAttributes.Hidden) != 0);
            properties["last-modified"] = DateTimeValue.FromJavaTime(new DateTimeOffset(file.LastWriteTimeUtc).ToUnixTimeMilliseconds());
            properties["length"] = new Int64Value(file.Length);
            return properties;
        }

        // A permission, as Java's canRead: a file another process holds locked still counts as readable.
        private static bool CanRead(FileInfo file)
        {
            try
            {
                using (file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    return true;
                }
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (IOException)
            {
                return true;
            }
        }

        // Java on Windows reports every existing file executable; elsewhere it is the owner's x bit.
        private static bool CanExecute(FileInfo file)
        {
#if NET
            if (!OperatingSystem.IsWindows())
            {
                return (File.GetUnixFileMode(file.FullName) & UnixFileMode.UserExecute) != 0;
            }
#endif
            return true;
        }

        /// <summary>
        /// Return the contents of a collection that maps to a directory in filestore
        /// </summary>
        protected IEnumerator<string> DirectoryContents(DirectoryInfo directory, URIQueryParameters @params)
        {
            Func<string, string, bool> filter = null;
            bool recurse = false;
            if (@params != null)
            {
                filter = @params.FilenameFilter;
                bool? r = @params.Recurse;
                if (r.HasValue)
                {
                    recurse = r.Value;
                }
            }

            return Walk(directory, recurse, filter);
        }

        private static IEnumerator<string> Walk(DirectoryInfo directory, bool recurse, Func<string, string, bool> filter)
        {
            foreach (FileSystemInfo entry in directory.GetFileSystemInfos())
            {
                if (filter != null && !filter(directory.FullName, entry.Name))
                {
                    continue;
                }

                if (entry is DirectoryInfo)
                {
                    // Do not descend into reparse points (junctions / directory symlinks): one
                    // pointing at an ancestor is a cycle, and this walk had no cycle detection, so
                    // it recursed until it exhausted the stack or hit MAX_PATH. Skipping them also
                    // matches the default "don't follow symlinks" of the reference implementation.
                    if (recurse && (entry.Attributes & FileAttributes.ReparsePoint) == 0)
                    {
                        IEnumerator<string> inner = Walk((DirectoryInfo)entry, true, filter);
                        while (inner.MoveNext())
                        {
                            yield return inner.Current;
                        }
                    }
                }
                else
                {
                    // A name on NTFS is any sixteen-bit units, half a surrogate pair among them, and no URI has
                    // that: System.Uri throws for it when its text is asked for (.NET Framework). U+FFFD stands for
                    // the half, as in any text from outside; the member is then a file that is not found, which
                    // on-error decides about.
                    yield return new Uri(OutSmart.DAXon.Text.StringTool.WithoutHalfPairs(entry.FullName)).AbsoluteUri;
                }
            }
        }
    }
}
