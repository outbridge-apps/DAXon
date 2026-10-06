////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using OutSmart.DAXon.Internal;
using SysRegex = System.Text.RegularExpressions;

namespace OutSmart.DAXon.Api
{
    /// <summary>
    /// Everything fixed when a <see cref="Processor"/> is created: the limits of an engine call, and what a stylesheet
    /// or query may reach through the engine's built-in resolvers - local files, the network, environment variables,
    /// file output. The defaults are the behaviour of 1.3.3: everything allowed, 150 MB of input, a minute a call.
    /// Resolvers and handlers the host installs are the host's own code and are not gated. A Processor takes the
    /// options: from then on a setter throws, and the same options can serve further processors.
    /// <para>
    /// A denied read fails the way a missing resource fails - the function's usual error code, with a message
    /// naming the missing permission or rule - before any file or network access; doc-available() and
    /// unparsed-text-available() return false. When anything is restricted, HTTP redirects are followed by the
    /// engine and every hop is checked like the first URI.
    /// </para>
    /// </summary>
    public sealed class ProcessorOptions
    {
        private static readonly IReadOnlyList<IPAddress> NoAddresses = new IPAddress[0];

        private TimeSpan? transformTimeout;
        private long maxInputBytes = Processor.DefaultMaxInputBytes;
        private int stackSizeThreshold = StackGuard.MinThreshold;
        private bool allowFileRead = true;
        private bool allowFileWrite = true;
        private bool allowNetwork = true;
        private bool allowEnvironmentVariables = true;
        private Func<Uri, ResourceKind, bool> readFilter;
        private Func<Uri, bool> writeFilter;
        private Func<string, bool> environmentVariableFilter;
        private volatile bool frozen;
        private bool unrestricted;

        /// <summary>Options that allow everything, with the limits of 1.3.3.</summary>
        public ProcessorOptions()
        {
            AllowedHosts = new HostRuleCollection();
            BlockedHosts = new HostRuleCollection();
        }

        /// <summary>Wall-clock limit per engine call; null for the default (1 minute), TimeSpan.Zero or negative for none.</summary>
        public TimeSpan? TransformTimeout
        {
            get => transformTimeout;
            set => Set(ref transformTimeout, value);
        }

        /// <summary>
        /// Largest input accepted, in bytes; <see cref="long.MaxValue"/> disables the cap. Not only what a stylesheet
        /// fetches: it also caps input the host passes in directly - DocumentBuilder, JsonBuilder, XsltCompiler,
        /// DocumentCache.
        /// </summary>
        public long MaxInputBytes
        {
            get => maxInputBytes;
            set
            {
                ThrowIfFrozen();
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                maxInputBytes = value;
            }
        }

        /// <summary>
        /// Bytes of the running thread's stack a recursion must leave free, or it stops with SXLM0001 (deep input with
        /// its own code: FOJS0001, XPST0003, ...); 128 KB by default and at least. Raise it when extension functions of
        /// the host's need much stack of their own: they can be called at the deepest level. How deep a recursion can
        /// go is set by the stack size of the thread. On Windows; elsewhere the runtime's own check stands in.
        /// </summary>
        public int StackSizeThreshold
        {
            get => stackSizeThreshold;
            set
            {
                ThrowIfFrozen();
                if (value < StackGuard.MinThreshold)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "The stack size threshold is at least "
                        + StackGuard.MinThreshold + " bytes (128 KB): with less an aborted recursion has no room to unwind, and the process dies of a stack overflow.");
                }

                stackSizeThreshold = value;
            }
        }

        /// <summary>Read local <c>file:</c> resources. A <c>file:</c> URI with a host (UNC) also needs <see cref="AllowNetwork"/>.</summary>
        public bool AllowFileRead
        {
            get => allowFileRead;
            set => Set(ref allowFileRead, value);
        }

        /// <summary>Write files through xsl:result-document and the built-in output resolvers.</summary>
        public bool AllowFileWrite
        {
            get => allowFileWrite;
            set => Set(ref allowFileWrite, value);
        }

        /// <summary>Fetch any URI that is not a local file or <c>data:</c>; host rules then apply.</summary>
        public bool AllowNetwork
        {
            get => allowNetwork;
            set => Set(ref allowNetwork, value);
        }

        /// <summary>environment-variable(), available-environment-variables(), system-property() for a name without a namespace.</summary>
        public bool AllowEnvironmentVariables
        {
            get => allowEnvironmentVariables;
            set => Set(ref allowEnvironmentVariables, value);
        }

        /// <summary>When not empty, a network host must match one of these rules.</summary>
        public HostRuleCollection AllowedHosts { get; }

        /// <summary>A network host matching any of these rules is denied; checked before <see cref="AllowedHosts"/>.</summary>
        public HostRuleCollection BlockedHosts { get; }

        /// <summary>
        /// A rule of the host's own, asked about every read the flags and host rules allow (a <c>data:</c> URI too);
        /// false denies it. It runs on the threads that run stylesheets, so it must be thread-safe; if it throws, the
        /// read is denied.
        /// </summary>
        public Func<Uri, ResourceKind, bool> ReadFilter
        {
            get => readFilter;
            set => Set(ref readFilter, value);
        }

        /// <summary>As <see cref="ReadFilter"/>, for every file the built-in output resolvers would write.</summary>
        public Func<Uri, bool> WriteFilter
        {
            get => writeFilter;
            set => Set(ref writeFilter, value);
        }

        /// <summary>As <see cref="ReadFilter"/>, for every environment variable a stylesheet asks for: a denied one reads as unset.</summary>
        public Func<string, bool> EnvironmentVariableFilter
        {
            get => environmentVariableFilter;
            set => Set(ref environmentVariableFilter, value);
        }

        // True once a Processor has taken the options; the setters then throw.
        internal bool IsFrozen => frozen;

        // Makes the options and their rule lists immutable. Called by the Processor; idempotent.
        internal void Freeze()
        {
            if (frozen)
            {
                return;
            }

            AllowedHosts.Freeze();
            BlockedHosts.Freeze();
            unrestricted = allowFileRead && allowFileWrite && allowNetwork && allowEnvironmentVariables
                && AllowedHosts.Count == 0 && BlockedHosts.Count == 0
                && readFilter == null && writeFilter == null && environmentVariableFilter == null;
            frozen = true;
        }

        // Frozen and allowing everything: the gates return at once, as if they were not there.
        internal bool IsUnrestricted => frozen && unrestricted;

        // Whether a stylesheet or query may read uri (absolute).
        internal bool PermitsRead(Uri uri, ResourceKind kind)
        {
            return Reason(uri, false) == null && (readFilter == null || readFilter(uri, kind));
        }

        // Whether the built-in output resolvers may write uri (absolute).
        internal bool PermitsWrite(Uri uri)
        {
            return Reason(uri, true) == null && (writeFilter == null || writeFilter(uri));
        }

        // Whether the built-in resolver may read the variable: a denied one reads as unset, and
        // available-environment-variables() lists only the permitted names.
        internal bool PermitsEnvironmentVariable(string name)
        {
            return allowEnvironmentVariables && (environmentVariableFilter == null || environmentVariableFilter(name));
        }

        // The error text for a denied read: the URI and the missing permission, the matching rule, or the filter.
        internal string DescribeDenial(Uri uri, ResourceKind kind)
        {
            return "Access to " + uri + " (" + KindText(kind) + ") is denied by the resource-access policy: "
                + (Reason(uri, false) ?? "ReadFilter refused it");
        }

        internal string DescribeWriteDenial(Uri uri)
        {
            return "Writing to " + uri + " is denied by the resource-access policy: "
                + (Reason(uri, true) ?? "WriteFilter refused it");
        }

        // null when the flags and host rules allow; otherwise the missing permission or the rule, in words
        private string Reason(Uri uri, bool write)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            if (!uri.IsAbsoluteUri)
            {
                return "the URI is not absolute";
            }

            string scheme = uri.Scheme;
            if (!write && (scheme == "data" || scheme == "classpath"))
            {
                // inline content, or the host-installed class loader
                return null;
            }

            if (scheme == Uri.UriSchemeFile)
            {
                if (write ? !allowFileWrite : !allowFileRead)
                {
                    return write ? "AllowFileWrite is false" : "AllowFileRead is false";
                }

                string host = uri.Host;
                if (host.Length == 0 || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                if (!allowNetwork)
                {
                    return "AllowNetwork is false (a file: URI with a host reaches another machine)";
                }

                return HostReason(uri);
            }

            if (!allowNetwork)
            {
                return "AllowNetwork is false";
            }

            return HostReason(uri);
        }

        private string HostReason(Uri uri)
        {
            if (AllowedHosts.Count == 0 && BlockedHosts.Count == 0)
            {
                return null;
            }

            string host = HostRule.CanonicalHost(uri);
            IReadOnlyList<IPAddress> resolved = Resolve(uri);
            if (resolved == null)
            {
                // A blocking range cannot be ruled out, and the fetch would resolve the name itself.
                if (HasAddressRules(BlockedHosts))
                {
                    return "host '" + host + "' could not be resolved to check the BlockedHosts IP ranges";
                }

                resolved = NoAddresses;
            }

            try
            {
                foreach (HostRule rule in BlockedHosts)
                {
                    if (MatchesAny(rule, uri, resolved))
                    {
                        return "host '" + host + "' matches BlockedHosts rule " + rule;
                    }
                }

                if (AllowedHosts.Count == 0)
                {
                    return null;
                }

                foreach (HostRule rule in AllowedHosts)
                {
                    if (rule.Matches(uri, resolved))
                    {
                        return null;
                    }
                }

                // Several allowing ranges together: every address must be in one of them.
                if (resolved.Count > 1 && CoveredByAllowed(uri, resolved))
                {
                    return null;
                }

                return "host '" + host + "' matches no AllowedHosts rule";
            }
            catch (SysRegex.RegexMatchTimeoutException)
            {
                return "a host rule timed out on host '" + host + "'";
            }
        }

        // A blocking rule matches a name if ANY of its addresses matches; Matches asks for all.
        private static bool MatchesAny(HostRule rule, Uri uri, IReadOnlyList<IPAddress> resolved)
        {
            if (rule.Matches(uri, resolved))
            {
                return true;
            }

            if (resolved.Count > 1)
            {
                foreach (IPAddress address in resolved)
                {
                    if (rule.Matches(uri, new[] { address }))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool CoveredByAllowed(Uri uri, IReadOnlyList<IPAddress> resolved)
        {
            foreach (IPAddress address in resolved)
            {
                bool covered = false;
                foreach (HostRule rule in AllowedHosts)
                {
                    if (rule.Matches(uri, new[] { address }))
                    {
                        covered = true;
                        break;
                    }
                }

                if (!covered)
                {
                    return false;
                }
            }

            return true;
        }

        // The addresses a host name resolves to, only when an IP-range rule needs them; empty for a
        // literal address or when no range rule exists; null when the lookup failed or ran past the
        // run's deadline (the run itself then fails on its own deadline check).
        private IReadOnlyList<IPAddress> Resolve(Uri uri)
        {
            if (HostRule.LiteralAddress(uri) != null || uri.DnsSafeHost.Length == 0
                || !(HasAddressRules(BlockedHosts) || HasAddressRules(AllowedHosts)))
            {
                return NoAddresses;
            }

            try
            {
                Task<IPAddress[]> lookup = Dns.GetHostAddressesAsync(uri.DnsSafeHost);
                int remaining = OutSmart.DAXon.Core.Controller.RemainingMillis();
                if (!lookup.Wait(remaining < 0 ? Timeout.Infinite : remaining))
                {
                    return null;
                }

                IPAddress[] addresses = lookup.Result;
                for (int i = 0; i < addresses.Length; i++)
                {
                    addresses[i] = HostRule.Normalize(addresses[i]);
                }

                return addresses;
            }
            catch (AggregateException)
            {
                return null;
            }
            catch (SocketException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static bool HasAddressRules(HostRuleCollection rules)
        {
            foreach (HostRule rule in rules)
            {
                if (rule.NeedsResolution)
                {
                    return true;
                }
            }

            return false;
        }

        private static string KindText(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Document:
                    return "document";
                case ResourceKind.Text:
                    return "text";
                case ResourceKind.Collection:
                    return "collection";
                case ResourceKind.StylesheetModule:
                    return "stylesheet module";
                case ResourceKind.QueryModule:
                    return "query module";
                case ResourceKind.ExternalEntity:
                    return "external entity";
                default:
                    return kind.ToString();
            }
        }

        private void Set<T>(ref T field, T value)
        {
            ThrowIfFrozen();
            field = value;
        }

        private void ThrowIfFrozen()
        {
            if (frozen)
            {
                throw new InvalidOperationException("These ProcessorOptions belong to a Processor and can no longer change.");
            }
        }
    }
}
