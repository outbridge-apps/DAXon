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
using SysRegex = System.Text.RegularExpressions;

namespace OutSmart.DAXon.Api
{
    /// <summary>
    /// What a stylesheet or query may reach through the engine's built-in resolvers: local files,
    /// the network, environment variables, and file output. The defaults allow everything, exactly
    /// as before this type existed. Resolvers and handlers the host installs are the host's own
    /// code and are not gated. A policy is frozen when a <see cref="Processor"/> takes it; a frozen
    /// policy is immutable and can serve any number of processors.
    /// </summary>
    public class ResourceAccessPolicy
    {
        private static readonly IReadOnlyList<IPAddress> NoAddresses = new IPAddress[0];

        private bool allowFileRead = true;
        private bool allowFileWrite = true;
        private bool allowNetwork = true;
        private bool allowEnvironmentVariables = true;
        private long maxInputBytes = Processor.DefaultMaxInputBytes;
        private volatile bool frozen;
        private bool unrestricted;

        public ResourceAccessPolicy()
        {
            AllowedHosts = new HostRuleCollection();
            BlockedHosts = new HostRuleCollection();
        }

        /// <summary>Read local <c>file:</c> resources. A <c>file:</c> URI with a host (UNC) also needs <see cref="AllowNetwork"/>.</summary>
        public bool AllowFileRead
        {
            get => allowFileRead;
            set
            {
                ThrowIfFrozen();
                allowFileRead = value;
            }
        }

        /// <summary>Write files through xsl:result-document and the built-in output resolvers.</summary>
        public bool AllowFileWrite
        {
            get => allowFileWrite;
            set
            {
                ThrowIfFrozen();
                allowFileWrite = value;
            }
        }

        /// <summary>Fetch any URI that is not a local file or <c>data:</c>; host rules then apply.</summary>
        public bool AllowNetwork
        {
            get => allowNetwork;
            set
            {
                ThrowIfFrozen();
                allowNetwork = value;
            }
        }

        /// <summary>environment-variable(), available-environment-variables(), system-property() for a name without a namespace.</summary>
        public bool AllowEnvironmentVariables
        {
            get => allowEnvironmentVariables;
            set
            {
                ThrowIfFrozen();
                allowEnvironmentVariables = value;
            }
        }

        /// <summary>Largest input accepted, in bytes; <see cref="long.MaxValue"/> disables the cap.</summary>
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

        /// <summary>When not empty, a network host must match one of these rules.</summary>
        public HostRuleCollection AllowedHosts { get; }

        /// <summary>A network host matching any of these rules is denied; checked before <see cref="AllowedHosts"/>.</summary>
        public HostRuleCollection BlockedHosts { get; }

        public bool IsFrozen => frozen;

        public void Freeze()
        {
            if (frozen)
            {
                return;
            }

            AllowedHosts.Freeze();
            BlockedHosts.Freeze();

            // A subclass may override the Permits* methods, so only the base type can take the fast path.
            unrestricted = GetType() == typeof(ResourceAccessPolicy)
                && allowFileRead && allowFileWrite && allowNetwork && allowEnvironmentVariables
                && AllowedHosts.Count == 0 && BlockedHosts.Count == 0;
            frozen = true;
        }

        // Frozen and allowing everything: the gates return at once, as if they were not there.
        internal bool IsUnrestricted => frozen && unrestricted;

        /// <summary>Whether a stylesheet or query may read <paramref name="uri"/> (absolute).</summary>
        public virtual bool PermitsRead(Uri uri, ResourceKind kind)
        {
            return Reason(uri, false) == null;
        }

        /// <summary>Whether the built-in output resolvers may write <paramref name="uri"/> (absolute).</summary>
        public virtual bool PermitsWrite(Uri uri)
        {
            return Reason(uri, true) == null;
        }

        public virtual bool PermitsEnvironmentVariable(string name)
        {
            return allowEnvironmentVariables;
        }

        /// <summary>The error text for a denied read: the URI and the missing permission or the matching rule.</summary>
        public virtual string DescribeDenial(Uri uri, ResourceKind kind)
        {
            return "Access to " + uri + " (" + KindText(kind) + ") is denied by the resource-access policy: "
                + (Reason(uri, false) ?? "the policy does not permit it");
        }

        internal string DescribeWriteDenial(Uri uri)
        {
            return "Writing to " + uri + " is denied by the resource-access policy: "
                + (Reason(uri, true) ?? "the policy does not permit it");
        }

        // null when allowed; otherwise the missing permission or the rule, in words
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

        private void ThrowIfFrozen()
        {
            if (frozen)
            {
                throw new InvalidOperationException("The ResourceAccessPolicy is frozen: a Processor uses it, so it can no longer change.");
            }
        }
    }
}
