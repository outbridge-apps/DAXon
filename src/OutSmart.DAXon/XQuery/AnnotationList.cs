////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using System.Collections.Generic;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.XQuery
{
    /// <summary>
    /// An immutable list of function or variable annotations, or of annotation assertions
    /// </summary>
    public class AnnotationList : IEnumerable<Annotation>
    {
        public static AnnotationList EMPTY = new AnnotationList(new List<Annotation>());
        private readonly IList<Annotation> list;
        public AnnotationList(IList<Annotation> list)
        {
            this.list = list;
        }

        public static AnnotationList Singleton(Annotation ann)
        {
            return new AnnotationList(new List<Annotation>(1) { ann });
        }

        public virtual void Check(Configuration config, string where)
        {
            Dictionary<NamespaceUri, IList<Annotation>> map = GroupByNamespace();
            foreach (KeyValuePair<NamespaceUri, IList<Annotation>> entry in map)
            {
                IFunctionAnnotationHandler handler = config.GetFunctionAnnotationHandler(entry.Key);
                if (handler != null)
                {
                    handler.Check(new AnnotationList(entry.Value), where);
                }
            }
        }

        private Dictionary<NamespaceUri, IList<Annotation>> GroupByNamespace()
        {
            Dictionary<NamespaceUri, IList<Annotation>> result = new Dictionary<NamespaceUri, IList<Annotation>>();
            foreach (Annotation ann in list)
            {
                NamespaceUri ns = ann.AnnotationQName.GetNamespaceUri();
                if (result.ContainsKey(ns))
                {
                    result.GetOrDefault(ns).Add(ann);
                }
                else
                {
                    IList<Annotation> list = new List<Annotation>();
                    list.Add(ann);
                    result[ns] = list;
                }
            }

            return result;
        }

        public virtual AnnotationList FilterByNamespace(NamespaceUri ns)
        {
            IList<Annotation> @out = new List<Annotation>();
            foreach (Annotation ann in list)
            {
                if (ann.AnnotationQName.HasURI(ns))
                {
                    @out.Add(ann);
                }
            }

            return new AnnotationList(@out);
        }

        public virtual IEnumerator<Annotation> IIterator()
        {
            return list.GetEnumerator();
        }

        public virtual bool IsEmpty()
        {
            return list.Count == 0;
        }

        public virtual int Size()
        {
            return list.Count;
        }

        public virtual Annotation Get(int i)
        {
            return list[i];
        }

        public virtual bool Includes(StructuredQName name)
        {
            foreach (Annotation a in list)
            {
                if (a.AnnotationQName.Equals(name))
                {
                    return true;
                }
            }

            return false;
        }

        public virtual bool Includes(string localName)
        {
            foreach (Annotation a in list)
            {
                if (a.AnnotationQName.GetLocalPart().Equals(localName))
                {
                    return true;
                }
            }

            return false;
        }

        public override bool Equals(object other)
        {

            // treat the annotation list as ordered
            return other is AnnotationList && list.Equals(((AnnotationList)other).list);
        }

        public override int GetHashCode()
        {
            return list.GetHashCode();
        }
        public IEnumerator<Annotation> GetEnumerator() => list.GetEnumerator(); // StubGen NIE -> real: delegate to the backing list
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => list.GetEnumerator();
    }
}
