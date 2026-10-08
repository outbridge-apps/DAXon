////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Trees.Iterators;
namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// This class represents a function invoked using xsl:original from within an xs:override element.
    /// </summary>
    internal sealed class OriginalFunction : AbstractFunction, IFunctionItem, IContextOriginator
    {
        private readonly UserFunction userFunction;
        private readonly Component component;

        public override IFunctionItemType FunctionItemType => userFunction.FunctionItemType;

        public override string Description => userFunction.Description;
        public OriginalFunction(Component component)
        {
            this.component = component;
            this.userFunction = (UserFunction)component.GetActor();
        }

        public override ISequence Call(IXPathContext context, ISequence[] args)
        {
            XPathContextMajor c2 = userFunction.MakeNewContext(context, this);
            c2.SetCurrentComponent(component);
            return userFunction.Call(c2, args);
        }

        public override StructuredQName GetFunctionName()
        {
            return userFunction.GetFunctionName();
        }

        public override int GetArity()
        {
            return userFunction.GetArity();
        }

        public Component GetComponent()
        {
            return component;
        }

        public override void Export(ExpressionPresenter @out)
        {
            ExpressionPresenter.ExportOptions options = @out.GetOptions();
            @out.StartElement("origF");
            @out.EmitAttribute("name", GetFunctionName());
            @out.EmitAttribute("arity", "" + GetArity());
            @out.EmitAttribute("pack", options.packageMap.GetOrDefault(component.ContainingPackage) + "");
            @out.EndElement();
        }
        SingletonIterator IItem.Iterate() => new SingletonIterator(this);
    }
}

