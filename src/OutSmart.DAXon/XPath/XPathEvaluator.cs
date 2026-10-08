////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.XPath
{
    internal sealed class XPathEvaluator
    {
        private IXPathStaticContext staticContext;

        public IXPathStaticContext StaticContext
        {
            get => staticContext; set
            {
                staticContext = value;
            }
        }
        public XPathEvaluator(Configuration config)
        {
            staticContext = new IndependentContext(config);
        }

        public Configuration GetConfiguration()
        {
            return staticContext.GetConfiguration();
        }

        public XPathExpression CreateExpression(string expression)
        {
            using RunResources run = RunResources.Enter();
            Configuration config = GetConfiguration();
            Executable exec = new Executable(config);
            exec.TopLevelPackage = staticContext.GetPackageData();
            exec.SetSchemaAware(staticContext.GetPackageData().IsSchemaAware());
            exec.SetHostLanguage(HostLanguage.XPATH);

            // Make the function library that's available at run-time (e.g. for saxon:evaluate() and function-lookup()).
            // This includes all user-defined functions regardless of which module they are in
            IFunctionLibrary userlib = exec.FunctionLibrary;
            FunctionLibraryList lib = new FunctionLibraryList();
            lib.AddFunctionLibrary(XPath31FunctionSet.GetInstance());
            lib.AddFunctionLibrary(config.GetBuiltInExtensionLibraryList(31));
            lib.AddFunctionLibrary(new ConstructorFunctionLibrary(config));
            lib.AddFunctionLibrary(config.GetIntegratedFunctionLibrary());
            config.AddExtensionBinders(lib);
            if (userlib != null)
            {
                lib.AddFunctionLibrary(userlib);
            }

            exec.FunctionLibrary = lib;
            Optimizer opt = config.ObtainOptimizer();
            Expression exp = ExpressionTool.Make(expression, staticContext, 0, -1, null);
            RetainedStaticContext rsc = staticContext.MakeRetainedStaticContext();
            exp.SetRetainedStaticContext(rsc);
            ExpressionVisitor visitor = ExpressionVisitor.Make(staticContext);
            Types.ItemType contextItemType = staticContext.GetRequiredContextItemType();
            ContextItemStaticInfo cit = config.MakeContextItemStaticInfo(contextItemType, true);
            cit.SetParentless(staticContext.IsContextItemParentless());
            exp = exp.TypeCheck(visitor, cit);
            if (opt.IsOptionSet(OptimizerOptions.MISCELLANEOUS))
            {
                exp = exp.Optimize(visitor, cit);
            }

            if (opt.IsOptionSet(OptimizerOptions.LOOP_LIFTING))
            {
                exp.ParentExpression = null;
                exp = LoopLifter.Process(exp);
            }

            exp = PostProcess(exp);
            exp.SetRetainedStaticContext(rsc);
            SlotManager map = staticContext.GetStackFrameMap();
            int numberOfExternalVariables = map.NumberOfVariables;
            ExpressionTool.AllocateSlots(exp, numberOfExternalVariables, map);
            XPathExpression xpe = new XPathExpression(staticContext, exp, exec);
            xpe.SetStackFrameMap(map, numberOfExternalVariables);
            return xpe;
        }

        protected Expression PostProcess(Expression exp)
        {
            return exp;
        }

        public XPathExpression CreatePattern(string pattern)
        {
            Configuration config = GetConfiguration();
            Executable exec = new Executable(config);
            Patterns.Pattern pat = Patterns.Pattern.Make(pattern, staticContext, new PackageData(config));
            ExpressionVisitor visitor = ExpressionVisitor.Make(staticContext);
            pat.TypeCheck(visitor, config.MakeContextItemStaticInfo(Types.Type.NODE_TYPE, true));
            SlotManager map = staticContext.GetStackFrameMap();
            int slots = map.NumberOfVariables;
            slots = pat.AllocateSlots(map, slots);

            //PatternSponsor sponsor = new PatternSponsor(pat);
            XPathExpression xpe = new XPathExpression(staticContext, pat, exec);
            xpe.SetStackFrameMap(map, slots);
            return xpe;
        }
    }
}