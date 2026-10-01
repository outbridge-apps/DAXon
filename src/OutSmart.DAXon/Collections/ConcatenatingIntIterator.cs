////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Functions;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Collections
{
    /// <summary>
    /// An iterator over nodes, that concatenates the nodes returned by two supplied iterators.
    /// </summary>
    internal sealed class ConcatenatingIntIterator : AbstractIntIterator
    {
        IIntIterator first;
        Func<IIntIterator> second;
        IIntIterator active;
        int lookahead;
        bool lookaheadFilled;
        public ConcatenatingIntIterator(IIntIterator first, Func<IIntIterator> second)
        {
            this.first = first;
            this.second = second;
            this.active = first;
        }

        public override bool HasNext()
        {
            if (lookaheadFilled)
            {
                return true;
            }

            if (active.MoveNext())
            {
                lookahead = active.Current;
                lookaheadFilled = true;
                return true;
            }
            else if (active == first)
            {
                first = null;
                active = second();
                if (active.MoveNext())
                {
                    lookahead = active.Current;
                    lookaheadFilled = true;
                    return true;
                }

                return false;
            }
            else
            {
                return false;
            }
        }

        public override int Next()
        {
            lookaheadFilled = false;
            return lookahead;
        }
    }
}