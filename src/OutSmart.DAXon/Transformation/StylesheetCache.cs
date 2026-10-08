////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Internal.Collections;
using System.Collections.Generic;
namespace OutSmart.DAXon.Transformation
{
    /// <summary>
    /// A cache of the stylesheets (as XsltExecutables) used in calls to the fn:transform function, in a stylesheet or query.
    /// </summary>
    public class StylesheetCache
    {
        private readonly Dictionary<string, XsltExecutable> cacheByText = new Dictionary<string, XsltExecutable>();
        private readonly Dictionary<string, XsltExecutable> cacheByLocation = new Dictionary<string, XsltExecutable>();
        private readonly Dictionary<NodeInfo, XsltExecutable> cacheByNode = new Dictionary<NodeInfo, XsltExecutable>();
        public virtual XsltExecutable GetStylesheetByText(string style)
        {
            return cacheByText.GetOrDefault(style);
        }

        public virtual XsltExecutable GetStylesheetByLocation(string style)
        {
            return cacheByLocation.GetOrDefault(style);
        }

        public virtual XsltExecutable GetStylesheetByNode(NodeInfo style)
        {
            return cacheByNode.GetOrDefault(style);
        }

        public virtual void SetStylesheetByText(string style, XsltExecutable xsltExecutable)
        {
            cacheByText[style] = xsltExecutable;
        }

        public virtual void SetStylesheetByLocation(string style, XsltExecutable xsltExecutable)
        {
            cacheByLocation[style] = xsltExecutable;
        }

        public virtual void SetStylesheetByNode(NodeInfo style, XsltExecutable xsltExecutable)
        {
            cacheByNode[style] = xsltExecutable;
        }
    }
}