////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// A function item that wraps a ICallable
    /// </summary>
    internal sealed class CallableFunction : AbstractFunction
    {
        private ICallable callable;
        private readonly SymbolicName.F name;
        private IFunctionItemType type;
        private AnnotationList annotations;

        public ICallable Callable
        {
            get => callable; set
            {
                this.callable = value;
            }
        }

        public override IFunctionItemType FunctionItemType
        {
            get
            {
                if (type == AnyFunctionType.GetInstance() && callable is XQueryFunctionLibrary.UnresolvedCallable)
                {
                    UserFunction uf = ((XQueryFunctionLibrary.UnresolvedCallable)callable).GetFunction();
                    if (uf != null)
                    {

                        // the previously unresolved function reference is now resolved
                        type = uf.FunctionItemType;
                    }
                }

                return type;
            }
        }

        public override string Description => callable.ToString();
        public CallableFunction(SymbolicName.F name, ICallable callable, IFunctionItemType type)
        {
            this.name = name;
            this.callable = callable;
            this.type = type;
        }

        public CallableFunction(int arity, ICallable callable, IFunctionItemType type)
        {
            this.name = new SymbolicName.F(NamespaceUri.ANONYMOUS.QName("anon"), arity);
            this.callable = callable;
            this.type = type;
        }

        public override StructuredQName GetFunctionName()
        {
            return name.ComponentName;
        }

        public override int GetArity()
        {
            return name.GetArity();
        }

        public override AnnotationList GetAnnotations()
        {
            return annotations;
        }

        public override ISequence Call(IXPathContext context, ISequence[] args)
        {
            return callable.Call(context, args);
        }

        public override void Export(ExpressionPresenter @out)
        {
            throw new NotSupportedException("A CallableFunction is a transient value that cannot be exported");
        }
    }
}
