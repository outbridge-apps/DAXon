////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Core;

namespace OutSmart.DAXon.Internal
{
    /// <summary>
    /// The one place the engine's built-in resolvers ask the Processor's options what they may reach.
    /// Read and write answer null when allowed, otherwise the denial text for the caller's error.
    /// Options that allow everything (the default) answer at once without parsing anything; a
    /// filter of the host's that throws counts as a denial.
    /// </summary>
    /// <summary>
    /// A resource-policy denial. Its own type lets a call site give it that site's retrieval code
    /// (fn:transform: FOXT0002) without also catching unrelated errors from host resolvers.
    /// </summary>
    internal sealed class ResourceDeniedException : OutSmart.DAXon.Transformation.XPathException
    {
        public ResourceDeniedException(string message)
            : base(message)
        {
        }

        public ResourceDeniedException(string message, string errorCode)
            : base(message, errorCode)
        {
        }
    }

    internal static class ResourceGate
    {
        public static string CheckRead(Configuration config, string absoluteUri, ResourceKind kind)
        {
            ProcessorOptions options = config?.ProcessorOptions;
            if (options == null || options.IsUnrestricted)
            {
                return null;
            }

            Uri uri;
            if (!Uri.TryCreate(absoluteUri, UriKind.Absolute, out uri))
            {
                return "Access to " + absoluteUri + " is denied by the resource-access policy: the URI cannot be classified";
            }

            try
            {
                return options.PermitsRead(uri, kind)
                    ? null
                    : options.DescribeDenial(uri, kind) ?? "Access to " + uri + " is denied by the resource-access policy";
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                return "Access to " + uri + " is denied: the resource-access policy failed (" + e.Message + ")";
            }
        }

        // A member of an archive whose own URI CheckRead has passed: only the host's ReadFilter is left to ask, with the
        // member's jar: URI (the flags and host rules would take that scheme for the network).
        public static string CheckMember(Configuration config, string memberUri, ResourceKind kind)
        {
            ProcessorOptions options = config?.ProcessorOptions;
            if (options == null || options.IsUnrestricted)
            {
                return null;
            }

            Uri uri;
            if (!Uri.TryCreate(memberUri, UriKind.Absolute, out uri))
            {
                return "Access to " + memberUri + " is denied by the resource-access policy: the URI cannot be classified";
            }

            try
            {
                return options.PermitsMember(uri, kind) ? null : options.DescribeMemberDenial(uri, kind);
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                return "Access to " + uri + " is denied: the resource-access policy failed (" + e.Message + ")";
            }
        }

        public static string CheckWrite(Configuration config, string absoluteUri)
        {
            ProcessorOptions options = config?.ProcessorOptions;
            if (options == null || options.IsUnrestricted)
            {
                return null;
            }

            Uri uri;
            if (!Uri.TryCreate(absoluteUri, UriKind.Absolute, out uri))
            {
                return "Writing to " + absoluteUri + " is denied by the resource-access policy: the URI cannot be classified";
            }

            try
            {
                return options.PermitsWrite(uri) ? null : options.DescribeWriteDenial(uri);
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                return "Writing to " + uri + " is denied: the resource-access policy failed (" + e.Message + ")";
            }
        }

        // Any options but the unrestricted default: the built-in HTTP fetchers then follow redirects
        // themselves and check every hop.
        public static bool IsRestricted(Configuration config)
        {
            ProcessorOptions options = config?.ProcessorOptions;
            return options != null && !options.IsUnrestricted;
        }

        // A denial carrying the code a missing resource of this kind gets; query modules and
        // external entities are coded by their callers.
        public static ResourceDeniedException Denied(string text, ResourceKind kind)
        {
            string code = kind == ResourceKind.Text ? "FOUT1170"
                : kind == ResourceKind.Document || kind == ResourceKind.Collection ? "FODC0002"
                : kind == ResourceKind.StylesheetModule ? "XTSE0165"
                : null;
            return code == null ? new ResourceDeniedException(text) : new ResourceDeniedException(text, code);
        }

        // For the built-in result-document resolvers: gates a write only when href names a target.
        public static string CheckOutput(Configuration config, string href, string baseUri)
        {
            ProcessorOptions options = config?.ProcessorOptions;
            if (options == null || options.IsUnrestricted)
            {
                return null;
            }

            string target = OutputTarget(href, baseUri);
            return target == null ? null : CheckWrite(config, target);
        }

        // The absolute URI a built-in output resolver writes for href, resolved with the same URI
        // class and steps they use; null when it does not resolve (the resolver then raises its own
        // error). An empty href is the principal output, chosen by the host, and is not gated.
        public static string OutputTarget(string href, string baseUri)
        {
            if (string.IsNullOrEmpty(href))
            {
                return null;
            }

            try
            {
                OutSmart.DAXon.Internal.Net.URI absolute = new OutSmart.DAXon.Internal.Net.URI(href);
                if (!absolute.IsAbsolute())
                {
                    if (baseUri == null)
                    {
                        return null;
                    }

                    absolute = new OutSmart.DAXon.Internal.Net.URI(baseUri).Resolve(href);
                }

                return absolute.ToString();
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                return null;
            }
        }

        public static ResourceKind KindOfNature(string nature)
        {
            switch (nature)
            {
                case OutSmart.DAXon.Lib.ResourceRequest.TEXT_NATURE:
                case OutSmart.DAXon.Lib.ResourceRequest.BINARY_NATURE:
                    return ResourceKind.Text;
                case OutSmart.DAXon.Lib.ResourceRequest.XQUERY_NATURE:
                    return ResourceKind.QueryModule;
                case OutSmart.DAXon.Lib.ResourceRequest.DTD_NATURE:
                case OutSmart.DAXon.Lib.ResourceRequest.EXTERNAL_ENTITY_NATURE:
                    return ResourceKind.ExternalEntity;
                default:
                    return nature != null && nature == OutSmart.DAXon.Lib.ResourceRequest.XSLT_NATURE
                        ? ResourceKind.StylesheetModule
                        : ResourceKind.Document;
            }
        }

        public static bool PermitsEnvironment(Configuration config, string name)
        {
            ProcessorOptions options = config?.ProcessorOptions;
            if (options == null || options.IsUnrestricted)
            {
                return true;
            }

            try
            {
                return options.PermitsEnvironmentVariable(name);
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                return false;
            }
        }
    }
}
