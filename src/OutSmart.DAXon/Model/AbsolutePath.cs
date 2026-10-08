////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Utilities;
using System.Collections.Generic;
using System.Text;
namespace OutSmart.DAXon.Model
{
    /// <summary>
    /// Represents the path from the root of an XDM tree to a specific node, as a sequence of (name, position) pairs
    /// </summary>
    public class AbsolutePath
    {
        private readonly IList<PathElement> path;
        private string systemId;

        public virtual string PathUsingPrefixes
        {
            get
            {
                StringBuilder fsb = new StringBuilder(256);
                foreach (PathElement pe in path)
                {
                    fsb.Append('/');
                    pe.AddToString(fsb, 'p');
                }

                return fsb.ToString();
            }
        }

        public virtual string PathUsingUris
        {
            get
            {
                StringBuilder fsb = new StringBuilder(256);
                foreach (PathElement pe in path)
                {
                    fsb.Append('/');
                    pe.AddToString(fsb, 'u');
                }

                return fsb.ToString();
            }
        }

        public virtual string PathUsingAbbreviatedUris
        {
            get
            {
                StringBuilder fsb = new StringBuilder(256);
                foreach (PathElement pe in path)
                {
                    fsb.Append('/');
                    pe.AddToString(fsb, 's');
                }

                return fsb.ToString();
            }
        }

        public virtual string SystemId
        {
            get => systemId; set
            {
                this.systemId = value;
            }
        }

        public virtual IList<PathElement> PathElements => path;
        public AbsolutePath(IEnumerable<PathElement> path)
        {
            this.path = new List<PathElement>(path);
        }

        public virtual void AppendAttributeName(INodeName attributeName)
        {
            if (path.Count > 0)
            {
                PathElement last = path[path.Count - 1];
                if (last.GetNodeKind() == Types.Type.ATTRIBUTE)
                {
                    path.RemoveAt(path.Count - 1);
                }
            }

            PathElement att = new PathElement(Types.Type.ATTRIBUTE, attributeName, 1);
            path.Add(att);
        }

        public static AbsolutePath PathToNode(NodeInfo node)
        {
            LinkedList<PathElement> list = new LinkedList<PathElement>();
            while (node != null && node.GetNodeKind() != Types.Type.DOCUMENT)
            {
                PathElement pe = new PathElement(node.GetNodeKind(), NameOfNode.MakeName(node), Navigator.GetNumberSimple(node, null));
                list.AddFirst(pe);
                node = node.GetParent();
            }

            return new AbsolutePath(list);
        }

        public override string ToString()
        {
            return PathUsingUris;
        }

        public override bool Equals(object obj)
        {
            return obj is AbsolutePath && obj.ToString().Equals(ToString());
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        /// <summary>
        /// Inner class representing one step in the path
        /// </summary>
        public class PathElement
        {
            int nodeKind;
            INodeName name;
            int index;

            public virtual INodeName Name => name;
            public PathElement(int nodeKind, INodeName name, int index)
            {
                this.nodeKind = nodeKind;
                this.name = name;
                this.index = index;
            }

            public virtual int GetNodeKind()
            {
                return nodeKind;
            }

            public virtual int GetIndex()
            {
                return index;
            }

            public virtual void AddToString(StringBuilder fsb, char option)
            {
                switch (nodeKind)
                {
                    case Types.Type.DOCUMENT:
                        fsb.Append("(/)");
                        break;
                    case Types.Type.ATTRIBUTE:
                        fsb.Append('@');
                        if (!name.GetNamespaceUri().IsEmpty())
                        {
                            if (option == 'u')
                            {
                                fsb.Append("Q{");
                                fsb.Append(name.GetNamespaceUri());
                                fsb.Append('}');
                            }
                            else if (option == 'p')
                            {
                                string prefix = name.GetPrefix();
                                if (!(prefix.Length == 0))
                                {
                                    fsb.Append(prefix);
                                    fsb.Append(':');
                                }
                            }
                            else if (option == 's')
                            {
                                fsb.Append("Q{");
                                fsb.Append(Err.AbbreviateURI(name.GetNamespaceUri()));
                                fsb.Append('}');
                            }
                        }

                        fsb.Append(Name.GetLocalPart());
                        break;
                    case Types.Type.ELEMENT:
                        if (option == 'u')
                        {
                            fsb.Append("Q{");
                            fsb.Append(name.GetNamespaceUri());
                            fsb.Append('}');
                        }
                        else if (option == 'p')
                        {
                            string prefix = name.GetPrefix();
                            if (!(prefix.Length == 0))
                            {
                                fsb.Append(prefix);
                                fsb.Append(':');
                            }
                        }
                        else if (option == 's')
                        {
                            if (!name.GetNamespaceUri().IsEmpty())
                            {
                                fsb.Append("Q{");
                                fsb.Append(Err.AbbreviateURI(name.GetNamespaceUri()));
                                fsb.Append('}');
                            }
                        }

                        fsb.Append(name.GetLocalPart());
                        AppendPredicate(fsb);
                        break;
                    case Types.Type.TEXT:
                        fsb.Append("text()");
                        break;
                    case Types.Type.COMMENT:
                        fsb.Append("comment()");
                        AppendPredicate(fsb);
                        break;
                    case Types.Type.PROCESSING_INSTRUCTION:
                        fsb.Append("processing-instruction(");
                        fsb.Append(name.GetLocalPart());
                        fsb.Append(')');
                        AppendPredicate(fsb);
                        break;
                    case Types.Type.NAMESPACE:
                        fsb.Append("namespace::");
                        if ((name.GetLocalPart().Length == 0))
                        {
                            fsb.Append("*[Q{" + NamespaceConstant.FN + "}local-name()=\"\"]");
                        }
                        else
                        {
                            fsb.Append(name.GetLocalPart());
                        }

                        break;
                    default:
                        break;
                }
            }

            private void AppendPredicate(StringBuilder fsb)
            {
                int index = GetIndex();
                if (index != -1)
                {
                    fsb.Append('[');
                    fsb.Append(GetIndex() + "");
                    fsb.Append(']');
                }
            }
        }
    }
}