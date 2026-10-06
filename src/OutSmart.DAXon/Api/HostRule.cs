////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using SysRegex = System.Text.RegularExpressions;

namespace OutSmart.DAXon.Api
{
    /// <summary>
    /// A test on the host of a URI, for <see cref="ProcessorOptions.AllowedHosts"/> and
    /// <see cref="ProcessorOptions.BlockedHosts"/>. The factories validate their argument, so a
    /// bad rule fails when it is created, not at the first fetch. Hosts are compared in canonical
    /// form: IDN host (punycode), lower case, no trailing dot; an IPv4-mapped IPv6 address counts
    /// as the IPv4 address.
    /// </summary>
    public abstract class HostRule
    {
        /// <summary>For a custom rule: derive and implement <see cref="Matches"/>.</summary>
        protected HostRule()
        {
        }

        /// <summary>One host name or one IP address, case-insensitive; Unicode and punycode spellings are equal.</summary>
        public static HostRule Exact(string host)
        {
            return new ExactRule(host);
        }

        /// <summary><c>*.example.com</c>: every subdomain of example.com, not example.com itself.</summary>
        public static HostRule Wildcard(string pattern)
        {
            return new WildcardRule(pattern);
        }

        /// <summary>
        /// A .NET regular expression over the whole canonical host (anchored, case-insensitive,
        /// culture-invariant, with a match timeout; a timeout counts as a denial).
        /// </summary>
        public static HostRule Regex(string pattern)
        {
            return new RegexRule(pattern);
        }

        /// <summary>
        /// An IPv4 or IPv6 range in CIDR form (<c>10.0.0.0/8</c>, <c>fd00::/8</c>; a bare address is
        /// a single-address range). Tested against a literal IP in the URI, or against the addresses
        /// a host name resolves to.
        /// <para>
        /// For a host name the addresses are resolved before the request, within the run's deadline,
        /// and the HTTP stack resolves the name again when it connects; a DNS answer that changes in
        /// between is not seen. Host-name rules (<see cref="Exact"/>, <see cref="Wildcard"/>) are the
        /// reliable form of an allow-list; IP ranges are an additional layer, typically for blocking
        /// internal networks.
        /// </para>
        /// </summary>
        public static HostRule IpRange(string cidr)
        {
            return new IpRangeRule(cidr);
        }

        /// <summary>
        /// True when the rule matches the host of <paramref name="uri"/>. <paramref name="resolved"/>
        /// holds the addresses the host name resolved to (empty when it was not resolved); only
        /// address rules use it, and they match a name only when every address is in range. The
        /// policy blocks a name when any address is in a blocking range, and allows it when every
        /// address is in one of the allowing ranges.
        /// </summary>
        public abstract bool Matches(Uri uri, IReadOnlyList<IPAddress> resolved);

        // Only address ranges need DNS; the policy resolves a name only when one is present.
        internal virtual bool NeedsResolution => false;

        internal static string CanonicalHost(Uri uri)
        {
            string host = uri.IdnHost;
            if (host.Length > 1 && host[0] == '[' && host[host.Length - 1] == ']')
            {
                host = host.Substring(1, host.Length - 2);
            }

            return host.TrimEnd('.').ToLowerInvariant();
        }

        internal static IPAddress LiteralAddress(Uri uri)
        {
            if (uri.HostNameType != UriHostNameType.IPv4 && uri.HostNameType != UriHostNameType.IPv6)
            {
                return null;
            }

            IPAddress address;
            return IPAddress.TryParse(uri.DnsSafeHost, out address) ? Normalize(address) : null;
        }

        internal static IPAddress Normalize(IPAddress address)
        {
            return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        }

        private static string CanonicalName(string host, string paramName)
        {
            string h = host.Trim().TrimEnd('.');
            if (h.Length == 0)
            {
                throw new ArgumentException("A host name is required.", paramName);
            }

            try
            {
                h = new IdnMapping().GetAscii(h).ToLowerInvariant();
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException("'" + host + "' is not a valid host name: " + e.Message, paramName, e);
            }

            foreach (char c in h)
            {
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '-' && c != '.' && c != '_')
                {
                    throw new ArgumentException("'" + host + "' is not a valid host name.", paramName);
                }
            }

            return h;
        }

        private static string StripBrackets(string s)
        {
            return s.Length > 1 && s[0] == '[' && s[s.Length - 1] == ']' ? s.Substring(1, s.Length - 2) : s;
        }

        private sealed class ExactRule : HostRule
        {
            private readonly string text;
            private readonly string name;
            private readonly IPAddress address;

            internal ExactRule(string host)
            {
                if (host == null)
                {
                    throw new ArgumentNullException(nameof(host));
                }

                text = host;
                IPAddress a;
                if (IPAddress.TryParse(StripBrackets(host.Trim()), out a))
                {
                    address = Normalize(a);
                }
                else
                {
                    name = CanonicalName(host, nameof(host));
                }
            }

            public override bool Matches(Uri uri, IReadOnlyList<IPAddress> resolved)
            {
                if (address != null)
                {
                    IPAddress literal = LiteralAddress(uri);
                    return literal != null && literal.Equals(address);
                }

                return string.Equals(CanonicalHost(uri), name, StringComparison.Ordinal);
            }

            public override string ToString()
            {
                return "Exact(" + text + ")";
            }
        }

        private sealed class WildcardRule : HostRule
        {
            private readonly string text;
            private readonly string suffix;

            internal WildcardRule(string pattern)
            {
                if (pattern == null)
                {
                    throw new ArgumentNullException(nameof(pattern));
                }

                string p = pattern.Trim();
                if (!p.StartsWith("*.", StringComparison.Ordinal) || p.IndexOf('*', 1) >= 0)
                {
                    throw new ArgumentException("A wildcard rule has the form '*.example.com'; use Exact for one host.", nameof(pattern));
                }

                text = pattern;
                suffix = "." + CanonicalName(p.Substring(2), nameof(pattern));
            }

            public override bool Matches(Uri uri, IReadOnlyList<IPAddress> resolved)
            {
                string host = CanonicalHost(uri);
                return host.Length > suffix.Length && host.EndsWith(suffix, StringComparison.Ordinal);
            }

            public override string ToString()
            {
                return "Wildcard(" + text + ")";
            }
        }

        private sealed class RegexRule : HostRule
        {
            // Hosts are at most 253 characters; a sane pattern matches in microseconds.
            private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

            private readonly string text;
            private readonly SysRegex.Regex regex;

            internal RegexRule(string pattern)
            {
                if (pattern == null)
                {
                    throw new ArgumentNullException(nameof(pattern));
                }

                const SysRegex.RegexOptions options = SysRegex.RegexOptions.IgnoreCase | SysRegex.RegexOptions.CultureInvariant;

                // Parsed alone first: a pattern like "a)|.*|(b" is only balanced inside the anchor
                // group and would otherwise escape it.
                _ = new SysRegex.Regex(pattern, options, MatchTimeout);
                text = pattern;
                regex = new SysRegex.Regex("\\A(?:" + pattern + ")\\z", options, MatchTimeout);
            }

            public override bool Matches(Uri uri, IReadOnlyList<IPAddress> resolved)
            {
                return regex.IsMatch(CanonicalHost(uri));
            }

            public override string ToString()
            {
                return "Regex(" + text + ")";
            }
        }

        private sealed class IpRangeRule : HostRule
        {
            private readonly string text;
            private readonly byte[] network;
            private readonly AddressFamily family;
            private readonly int prefix;

            internal IpRangeRule(string cidr)
            {
                if (cidr == null)
                {
                    throw new ArgumentNullException(nameof(cidr));
                }

                string s = cidr.Trim();
                int slash = s.IndexOf('/');
                IPAddress a;
                if (!IPAddress.TryParse(StripBrackets(slash < 0 ? s : s.Substring(0, slash)), out a))
                {
                    throw new ArgumentException("'" + cidr + "' is not an IP range in CIDR form.", nameof(cidr));
                }

                int bits = a.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
                int length = bits;
                if (slash >= 0 && (!int.TryParse(s.Substring(slash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out length) || length > bits))
                {
                    throw new ArgumentException("'" + cidr + "' has an invalid prefix length.", nameof(cidr));
                }

                if (a.IsIPv4MappedToIPv6)
                {
                    if (length < 96)
                    {
                        throw new ArgumentException("'" + cidr + "' mixes IPv4-mapped and native IPv6 space.", nameof(cidr));
                    }

                    a = a.MapToIPv4();
                    length -= 96;
                }

                text = cidr;
                family = a.AddressFamily;
                prefix = length;
                network = Mask(a.GetAddressBytes(), prefix);
            }

            internal override bool NeedsResolution => true;

            public override bool Matches(Uri uri, IReadOnlyList<IPAddress> resolved)
            {
                IPAddress literal = LiteralAddress(uri);
                if (literal != null)
                {
                    return Contains(literal);
                }

                if (resolved == null || resolved.Count == 0)
                {
                    return false;
                }

                foreach (IPAddress address in resolved)
                {
                    if (address == null || !Contains(address))
                    {
                        return false;
                    }
                }

                return true;
            }

            public override string ToString()
            {
                return "IpRange(" + text + ")";
            }

            private bool Contains(IPAddress address)
            {
                IPAddress a = Normalize(address);
                if (a.AddressFamily != family)
                {
                    return false;
                }

                byte[] masked = Mask(a.GetAddressBytes(), prefix);
                for (int i = 0; i < masked.Length; i++)
                {
                    if (masked[i] != network[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            private static byte[] Mask(byte[] bytes, int length)
            {
                for (int i = 0; i < bytes.Length; i++)
                {
                    int keep = Math.Max(0, Math.Min(8, length - i * 8));
                    bytes[i] &= (byte)(0xFF << (8 - keep));
                }

                return bytes;
            }
        }
    }
}
