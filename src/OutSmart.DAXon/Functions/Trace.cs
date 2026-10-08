////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Functions
{
    internal sealed class Trace : SystemFunction, ITraceable
    {
        ILocation location = Loc.NONE;

        /// <summary>
        /// Tracing IIterator class
        /// </summary>
        public override string StreamerName => "Trace";
        public override int GetSpecialProperties(Expression[] arguments)
        {
            return arguments[0].GetSpecialProperties();
        }

        public override int GetCardinality(Expression[] arguments)
        {
            return arguments[0].GetCardinality();
        }

        public void NotifyListener(string label, ISequence val, IXPathContext context)
        {
            Dictionary<string, object> info = new Dictionary<string, object>();
            info["label"] = label;
            info["value"] = val;
            ITraceListener listener = context.GetController().GetTraceListener();
            listener.Enter(this, info, context);
            listener.Leave(this);
        }

        public override Expression MakeFunctionCall(params Expression[] arguments)
        {

            // Fix bug 2597
            Expression e = base.MakeFunctionCall(arguments);
            location = e.GetLocation();
            return e;
        }

        public static void TraceItem(IItem val, string label, Logger @out)
        {
            if (val == null)
            {
                @out.Info(label);
            }
            else
            {
                if (val is NodeInfo)
                {
                    @out.Info(label + ": " + Types.Type.DisplayTypeName(val) + ": " + Navigator.GetPath((NodeInfo)val));
                }
                else if (val is AtomicValue)
                {
                    @out.Info(label + ": " + Types.Type.DisplayTypeName(val) + ": " + val.UnicodeStringValue);
                }
                else if (val is ArrayItem || val is MapItem)
                {
                    @out.Info(label + ": " + val.ToShortString());
                }
                else if (val is IFunctionItem)
                {
                    StructuredQName name = ((IFunctionItem)val).GetFunctionName();
                    @out.Info(label + ": function " + (name == null ? "(anon)" : name.DisplayName) + "#" + ((IFunctionItem)val).GetArity());
                }
                else if (val.GetGenre() == Genre.EXTERNAL)
                {
                    object obj = ((ObjectValue<object>)val).GetObject();
                    @out.Info(label + ": " + obj.GetType().FullName + " = " + Err.Truncate30(StringView.Tidy(obj.ToString())));
                }
                else
                {
                    @out.Info(label + ": " + val.ToShortString());
                }
            }
        }

        public ILocation GetLocation()
        {
            return location;
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            Controller controller = context.GetController();
            string label = "*";
            if (arguments.Length > 1)
            {
                IItem labelArg = arguments[1].Head();
                if (labelArg != null)
                {
                    label = labelArg.GetStringValue();
                }
            }

            if (controller.IsTracing())
            {
                ISequence value = arguments[0].Materialize();
                NotifyListener(label, value, context);
                return value;
            }
            else
            {
                Logger @out = controller.TraceFunctionDestination;
                if (@out == null)
                {
                    return arguments[0];
                }
                else
                {
                    return SequenceTool.ToLazySequence(new TracingIterator(arguments[0].Iterate(), label, @out));
                }
            }
        }

        public StructuredQName GetObjectName()
        {
            return null;
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public void GatherProperties(Action<string, object> consumer) { } // upstream Traceable default: no properties

        /// <summary>
        /// Tracing IIterator class
        /// </summary>
        private sealed class TracingIterator : ISequenceIterator
        {
            private readonly ISequenceIterator @base;
            private readonly string label;
            private readonly Logger @out;
            private bool empty = true;
            private int position;
            public TracingIterator(ISequenceIterator @base, string label, Logger @out)
            {
                this.@base = @base;
                this.label = label;
                this.@out = @out;
            }

            public IItem Next()
            {
                IItem n = @base.Next();
                position++;
                if (n == null)
                {
                    if (empty)
                    {
                        TraceItem(null, label + ": empty sequence", @out);
                    }
                }
                else
                {
                    TraceItem(n, label + " [" + position + ']', @out);
                    empty = false;
                }

                return n;
            }

            public void Dispose()
            {
                @base.Dispose();
            }
        }
    }
}