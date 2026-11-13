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
            return DirectoryContents(dirFile, @params, context);
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
                catch (XPathException e) when (!e.IsRunLimit())
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

            string name = OutSmart.DAXon.Internal.Net.BidiUri.LocalPath(details.resourceUri);
            FileInfo file;
            try
            {
                file = new FileInfo(OutSmart.DAXon.Internal.Streams.FileNames.ForListing(name));
            }
            catch (Exception e) when (e is PathTooLongException || e is ArgumentException || e is NotSupportedException)
            {
                return properties;   // the .NET 4.5 mode takes no long path here: the member has only what its content says
            }

            if (!file.Exists)
            {
                return properties;
            }

            properties["path"] = StringValue.MakeStringValue(OutSmart.DAXon.Internal.Streams.FileNames.WithoutLongForm(file.FullName));
            properties["absolute-path"] = StringValue.MakeStringValue(OutSmart.DAXon.Internal.Streams.FileNames.WithoutLongForm(file.FullName));
            properties["canonical-path"] = StringValue.MakeStringValue(OutSmart.DAXon.Internal.Streams.FileNames.WithoutLongForm(file.FullName));
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
        private IEnumerator<string> DirectoryContents(DirectoryInfo directory, URIQueryParameters @params, IXPathContext context)
        {
            Func<string, string, bool> filter = null;
            bool recurse = false;
            Func<XPathException, bool> skipped = null;
            if (@params != null)
            {
                filter = @params.FilenameFilter;
                bool? r = @params.Recurse;
                if (r.HasValue)
                {
                    recurse = r.Value;
                }

                skipped = SubdirectoryFailures(@params.OnError, context);
            }

            return Walk(directory.FullName, recurse, filter, skipped);
        }

        // A directory below the one named that cannot be listed is a member that fails: on-error says whether it is
        // left out (true) or fails the collection (null or false). The directory named is the collection, and fails it.
        private static Func<XPathException, bool> SubdirectoryFailures(int? onError, IXPathContext context)
        {
            if (onError == URIQueryParameters.ON_ERROR_IGNORE)
            {
                return e => true;
            }

            if (onError == URIQueryParameters.ON_ERROR_WARNING)
            {
                Controller controller = context?.GetController();
                return e =>
                {
                    controller?.Warning("collection(): " + e.Message, e.ShowErrorCode(), null);
                    return true;
                };
            }

            return null;
        }

        private static List<OutSmart.DAXon.Internal.Streams.FileNames.Entry> EntriesOf(string directory)
        {
            try
            {
                return OutSmart.DAXon.Internal.Streams.FileNames.ListDirectory(directory);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException)
            {
                // Access denied is no IOException.
                throw new XPathException("The directory " + OutSmart.DAXon.Internal.Streams.FileNames.WithoutLongForm(directory) + " cannot be listed: " + e.Message, "FODC0002");
            }
        }

        // A directory being walked: its entries and the next one to look at.
        private sealed class Level
        {
            internal readonly string Shown;
            internal readonly string Prefix;
            internal readonly List<OutSmart.DAXon.Internal.Streams.FileNames.Entry> Entries;
            internal int Next;

            internal Level(string directory, List<OutSmart.DAXon.Internal.Streams.FileNames.Entry> entries)
            {
                Shown = OutSmart.DAXon.Internal.Streams.FileNames.WithoutLongForm(directory);
                Prefix = directory.EndsWith("\\", StringComparison.Ordinal) || directory.EndsWith("/", StringComparison.Ordinal) ? directory : directory + Path.DirectorySeparatorChar;
                Entries = entries;
            }
        }

        // Depth first, each directory in the system's order. The directories open are a stack of their own, not the thread's: a
        // tree thousands deep killed the process with a stack overflow, and each directory listed checks the run's limits.
        private static IEnumerator<string> Walk(string directory, bool recurse, Func<string, string, bool> filter, Func<XPathException, bool> skipped)
        {
            var open = new Stack<Level>();
            open.Push(new Level(directory, EntriesOf(directory)));
            while (open.Count > 0)
            {
                Level level = open.Peek();
                if (level.Next == level.Entries.Count)
                {
                    open.Pop();
                    continue;
                }

                OutSmart.DAXon.Internal.Streams.FileNames.Entry entry = level.Entries[level.Next++];
                // The filter of select= passes every directory; it is not asked, its look at the disk fails past MAX_PATH.
                if (filter != null && !entry.IsDirectory && !filter(level.Shown, entry.Name))
                {
                    continue;
                }

                string full = level.Prefix + entry.Name;
                if (entry.IsDirectory)
                {
                    // Do not descend into reparse points (junctions / directory symlinks): one
                    // pointing at an ancestor is a cycle, and this walk had no cycle detection, so
                    // it recursed until it exhausted the stack or hit MAX_PATH. Skipping them also
                    // matches the default "don't follow symlinks" of the reference implementation.
                    if (recurse && !entry.IsReparsePoint)
                    {
                        Controller.CheckActiveTimeoutNow();
                        List<OutSmart.DAXon.Internal.Streams.FileNames.Entry> entries;
                        try
                        {
                            entries = EntriesOf(full);
                        }
                        catch (XPathException e) when (skipped != null && skipped(e))
                        {
                            continue;
                        }

                        open.Push(new Level(full, entries));
                    }
                }
                else
                {
                    // A name on NTFS is any sixteen-bit units, half a surrogate pair among them, and no URI has
                    // that: System.Uri throws for it when its text is asked for (.NET Framework). U+FFFD stands for
                    // the half, as in any text from outside; the member is then a file that is not found, which
                    // on-error decides about.
                    yield return OutSmart.DAXon.Internal.Net.FileUris.Of(OutSmart.DAXon.Text.StringTool.WithoutHalfPairs(OutSmart.DAXon.Internal.Streams.FileNames.WithoutLongForm(full)));
                }
            }
        }
    }
}
