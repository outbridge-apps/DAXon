////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using System.Collections.Generic;

namespace OutSmart.DAXon.Tracing
{
    // Two listeners, both told of every event, as Saxon's: the port's stand-in kept the first listener only and never
    // removed one, so a second (configuration plus run) was ignored and a removed one went on tracing.
    internal sealed class TraceEventMulticaster : ITraceListener
    {
        private readonly ITraceListener a;
        private readonly ITraceListener b;

        private TraceEventMulticaster(ITraceListener a, ITraceListener b)
        {
            this.a = a;
            this.b = b;
        }

        public static ITraceListener Add(ITraceListener a, ITraceListener b)
        {
            if (a == null)
            {
                return b;
            }

            if (b == null)
            {
                return a;
            }

            return new TraceEventMulticaster(a, b);
        }

        public static bool Contains(ITraceListener l, ITraceListener x)
        {
            return l == x || l is TraceEventMulticaster m && (Contains(m.a, x) || Contains(m.b, x));
        }

        public static ITraceListener Remove(ITraceListener l, ITraceListener old)
        {
            if (l == old)
            {
                return null;
            }

            if (l is TraceEventMulticaster m)
            {
                ITraceListener a2 = Remove(m.a, old);
                ITraceListener b2 = Remove(m.b, old);
                return a2 == m.a && b2 == m.b ? m : Add(a2, b2);
            }

            return l;
        }

        public void SetOutputDestination(Logger stream)
        {
            a.SetOutputDestination(stream);
            b.SetOutputDestination(stream);
        }

        public void Open(Controller controller)
        {
            a.Open(controller);
            b.Open(controller);
        }

        public void Close()
        {
            a.Close();
            b.Close();
        }

        public void Enter(ITraceable instruction, Dictionary<string, object> properties, IXPathContext context)
        {
            a.Enter(instruction, properties, context);
            b.Enter(instruction, properties, context);
        }

        public void Leave(ITraceable instruction)
        {
            a.Leave(instruction);
            b.Leave(instruction);
        }

        public void StartCurrentItem(IItem currentItem)
        {
            a.StartCurrentItem(currentItem);
            b.StartCurrentItem(currentItem);
        }

        public void EndCurrentItem(IItem currentItem)
        {
            a.EndCurrentItem(currentItem);
            b.EndCurrentItem(currentItem);
        }

        public object Checkpoint()
        {
            return new[] { a.Checkpoint(), b.Checkpoint() };
        }

        public void Recover(object checkpoint, XPathException err)
        {
            object[] both = (object[])checkpoint;
            a.Recover(both[0], err);
            b.Recover(both[1], err);
        }

        public void StartRuleSearch()
        {
            a.StartRuleSearch();
            b.StartRuleSearch();
        }

        public void EndRuleSearch(object rule, Mode mode, IItem item)
        {
            a.EndRuleSearch(rule, mode, item);
            b.EndRuleSearch(rule, mode, item);
        }

        public void Dispose()
        {
            a.Dispose();
            b.Dispose();
        }
    }
}
