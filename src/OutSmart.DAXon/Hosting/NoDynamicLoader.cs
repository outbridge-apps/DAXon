////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Transformation;

namespace OutSmart.DAXon.Lib
{
    // The loader of a configuration that was given none: a class named in a stylesheet or a
    // feature is reported as the error every caller already handles, not as a null reference.
    internal sealed class NoDynamicLoader : IIDynamicLoader
    {
        public static readonly NoDynamicLoader Instance = new NoDynamicLoader();

        private NoDynamicLoader()
        {
        }

        public System.Type GetType(string className, Logger traceOut)
        {
            throw NotLoadable(className);
        }

        public object GetInstance(string className)
        {
            throw NotLoadable(className);
        }

        public object GetInstance(string className, Logger traceOut)
        {
            throw NotLoadable(className);
        }

        public System.IO.Stream GetResourceAsStream(string name)
        {
            return null;
        }

        private static XPathException NotLoadable(string className)
        {
            return new XPathException("Failed to load " + className + ": classes are not loaded by name on this platform");
        }
    }
}
