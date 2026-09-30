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
    /// The one place the engine's built-in resolvers ask the Processor's ResourceAccessPolicy.
    /// Read and write answer null when allowed, otherwise the denial text for the caller's error.
    /// An unrestricted policy (the default) answers at once without parsing anything; a policy
    /// that throws counts as a denial.
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
            ResourceAccessPolicy policy = config?.ResourcePolicy;
            if (policy == null || policy.IsUnrestricted)
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
                return policy.PermitsRead(uri, kind)
                    ? null
                    : policy.DescribeDenial(uri, kind) ?? "Access to " + uri + " is denied by the resource-access policy";
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                return "Access to " + uri + " is denied: the resource-access policy failed (" + e.Message + ")";
            }
        }

        public static string CheckWrite(Configuration config, string absoluteUri)
        {
            ResourceAccessPolicy policy = config?.ResourcePolicy;
            if (policy == null || policy.IsUnrestricted)
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
                return policy.PermitsWrite(uri) ? null : policy.DescribeWriteDenial(uri);
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                return "Writing to " + uri + " is denied: the resource-access policy failed (" + e.Message + ")";
            }
        }

        // For the built-in result-document resolvers: gates a write only when href names a target.
        public static string CheckOutput(Configuration config, string href, string baseUri)
        {
            ResourceAccessPolicy policy = config?.ResourcePolicy;
            if (policy == null || policy.IsUnrestricted)
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
            ResourceAccessPolicy policy = config?.ResourcePolicy;
            if (policy == null || policy.IsUnrestricted)
            {
                return true;
            }

            try
            {
                return policy.PermitsEnvironmentVariable(name);
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                return false;
            }
        }
    }
}
