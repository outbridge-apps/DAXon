////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2013-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Tracing;
using System.Collections.Generic;
namespace OutSmart.DAXon.Expressions.Sorting
{
    public class SortKeyDefinitionList : PseudoExpression, IEnumerable<SortKeyDefinition>
    {
        private readonly SortKeyDefinition[] sortKeyDefinitions;

        // PHASE7_SKDL_COUNT: shadow LINQ Count extension
        public int Count => Size();

        public override int ImplementationMethod => 0;
        public SortKeyDefinitionList(SortKeyDefinition[] sortKeyDefinitions)
        {
            this.sortKeyDefinitions = sortKeyDefinitions;
        }

        public override IEnumerable<Operand> Operands()
        {
            IList<Operand> list = new List<Operand>(Size());
            foreach (SortKeyDefinition skd in sortKeyDefinitions)
            {
                list.Add(new Operand(this, skd, OperandRole.INSPECT));
            }

            return list;
        }

        public override bool IsLiftable(bool forStreaming)
        {
            return false;
        }
        public virtual int Size()
        {
            return sortKeyDefinitions.Length;
        }

        public virtual SortKeyDefinition GetSortKeyDefinition(int i)
        {
            return sortKeyDefinitions[i];
        }

        public IEnumerator<SortKeyDefinition> IIterator()
        {
            return ((IEnumerable<SortKeyDefinition>)sortKeyDefinitions).GetEnumerator();
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            SortKeyDefinition[] s2 = new SortKeyDefinition[sortKeyDefinitions.Length];
            for (int i = 0; i < sortKeyDefinitions.Length; i++)
            {
                s2[i] = (SortKeyDefinition)sortKeyDefinitions[i].Copy(rebindings);
            }

            return new SortKeyDefinitionList(s2);
        }

        public override void Export(ExpressionPresenter @out)
        {
            foreach (SortKeyDefinition skd in sortKeyDefinitions)
            {
                skd.Export(@out);
            }
        }
        public IEnumerator<SortKeyDefinition> GetEnumerator() => IIterator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}