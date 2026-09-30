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
