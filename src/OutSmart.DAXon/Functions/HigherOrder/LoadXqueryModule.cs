////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using S = OutSmart.DAXon.Api;
using System.Collections.Generic;

namespace OutSmart.DAXon.Functions.HigherOrder
{
    /// <summary>
    /// fn:load-xquery-module() — XPath 3.1. Officially a higher-order function requiring Saxon-PE+, and
    /// upstream StaticQueryContext.compileLibrary is EE-gated. This HE port implements it by compiling a
    /// synthetic main query that imports the requested module (reusing the working `import module` path),
    /// then reflecting the compiled library module's public functions and global variables into the
    /// map{'functions', 'variables'} result.
    /// </summary>
    internal sealed class LoadXqueryModule : SystemFunction, ICallable
    {
        public static OptionsParameter MakeOptionsParameter()
        {
            OptionsParameter op = new OptionsParameter();
            op.AddAllowedOption("xquery-version", SequenceType.SINGLE_DECIMAL);
            op.AddAllowedOption("location-hints", SequenceType.STRING_SEQUENCE);
            op.AddAllowedOption("context-item", SequenceType.OPTIONAL_ITEM);
            // Upstream types these as map(xs:QName, item()*): a non-map or a map whose keys aren't QNames must
            // fail XPTY0004 during option processing, not be silently ignored or InvalidCast'd downstream.
            SequenceType qnameKeyedMap = SequenceType.MakeSequenceType(new MapType(BuiltInAtomicType.QNAME, SequenceType.ANY_SEQUENCE), StaticProperty.EXACTLY_ONE);
            op.AddAllowedOption("variables", qnameKeyedMap);
            op.AddAllowedOption("vendor-options", qnameKeyedMap);
            return op;
        }

        public override ISequence Call(IXPathContext context, ISequence[] args)
        {
            IGroundedValue xqueryVersionOption = null;
            IGroundedValue locationHintsOption = null;
            IGroundedValue variablesOption = null;
            IGroundedValue contextItemOption = null;
            if (args.Length == 2)
            {
                MapItem suppliedOptions = (MapItem)args[1].Head();
                Dictionary<string, IGroundedValue> opts = Details.optionDetails.ProcessSuppliedOptions(suppliedOptions, context);
                opts.TryGetValue("xquery-version", out xqueryVersionOption);
                opts.TryGetValue("location-hints", out locationHintsOption);
                opts.TryGetValue("variables", out variablesOption);
                opts.TryGetValue("context-item", out contextItemOption);
            }

            if (xqueryVersionOption != null)
            {
                double vn = ((DecimalValue)xqueryVersionOption.Head()).GetDoubleValue();
                if (vn * 10 > 31 || !(vn == 1.0 || vn == 3.0 || vn == 3.1))
                {
                    throw new XPathException("Unsupported XQuery version " + vn, "FOQM0006");
                }
            }

            string moduleUriStr = args[0].Head().GetStringValue();
            if (moduleUriStr.Length == 0)
            {
                throw new XPathException("First argument of fn:load-xquery-module() must not be a zero length string", "FOQM0001");
            }

            NamespaceUri moduleUri = NamespaceUri.Of(moduleUriStr);
            List<string> locationHints = new List<string>();
            if (locationHintsOption != null)
            {
                ISequenceIterator it = locationHintsOption.Iterate();
                IItem hint;
                while ((hint = it.Next()) != null)
                {
                    locationHints.Add(hint.GetStringValue());
                }
            }

            Configuration config = context.GetConfiguration();
            string baseURI = GetRetainedStaticContext().StaticBaseUriString;

            // Build a synthetic main module that imports the target module, then compile it through the s9api
            // XQueryCompiler (which drives the working import-module resolution + library compilation).
            System.Text.StringBuilder q = new System.Text.StringBuilder();
            q.Append("import module namespace lxm=\"").Append(moduleUriStr.Replace("\"", "\"\"")).Append('"');
            for (int i = 0; i < locationHints.Count; i++)
            {
                q.Append(i == 0 ? " at " : ", ").Append('"').Append(locationHints[i].Replace("\"", "\"\"")).Append('"');
            }

            q.Append("; ()");

            S.Processor proc = new S.Processor(config);
            S.XQueryExecutable xqx;
            try
            {
                S.XQueryCompiler xqc = proc.NewXQueryCompiler();
                try { xqc.SetBaseURI(new OutSmart.DAXon.Internal.Net.URI(baseURI)); } catch { }
                if (config.GetModuleURIResolver() != null)
                {
                    xqc.SetModuleURIResolver(config.GetModuleURIResolver());
                }

                xqx = xqc.Compile(q.ToString());
            }
            catch (System.Exception e)
            {
                // Upstream maybeSetErrorCode("FOQM0002"): the resolver's XQST0059 propagates UNCHANGED —
                // verified live on Java-HE 12.5 (fn:load-xquery-module of an unresolvable URI raises
                // XQST0059, so Java itself fails qt3 fn-load-xquery-module-003/004 which demand the
                // F&O-spec FOQM0002; those are HE-parity exclusions). Only a code-less resolution
                // failure becomes FOQM0002; other static errors in a located module are FOQM0003.
                // Known deviation (R10-review): upstream compiles the located module in a SEPARATE stage
                // whose catch re-codes even a nested import-not-found XQST0059 to FOQM0003; this
                // synthetic-import structure cannot tell nested from top-level, so a nested XQST0059
                // escapes as XQST0059. No corpus coverage; a message-sniffing discriminator would be
                // more fragile than the deviation.
                // A limit of the run keeps its code too (SXTO0001, SXLM0003): the module had no static error.
                string code = ExtractCode(e);
                if (code == "XQST0059" || code == "FOQM0002" || code == DAXonErrorCode.SXTO0001 || code == DAXonErrorCode.SXLM0003)
                {
                    throw new XPathException(RootMessage(e), code);
                }

                throw new XPathException(RootMessage(e), "FOQM0003");
            }

            XQueryExpression xqe = xqx.UnderlyingCompiledQuery;
            Executable exec = xqe.MainModule.GetExecutable();
            IList<QueryModule> libs = exec.GetQueryLibraryModules(moduleUri);
            if (libs == null || libs.Count == 0)
            {
                throw new XPathException("The library module located does not have the expected namespace " + moduleUriStr, "FOQM0002");
            }

            QueryModule mainModule = xqe.MainModule;

            DynamicQueryContext dqc = new DynamicQueryContext(config);
            if (variablesOption != null)
            {
                MapItem extVariables = (MapItem)variablesOption.Head();
                foreach (OutSmart.DAXon.Values.Maps.KeyValuePair kv in extVariables.KeyValuePairs())
                {
                    if (kv.key is QNameValue qk)
                    {
                        dqc.SetParameter(qk.GetStructuredQName(), kv.value.Materialize());
                    }
                }
            }

            if (contextItemOption != null)
            {
                // Upstream checks the supplied context item against the module's declared context-item
                // type BEFORE setting it (LoadXqueryModule.java:185) — a mismatch is FOQM0005, not the
                // XPTY0004 the downstream type-check would raise (fn-load-xquery-module-060).
                IItem contextItem = contextItemOption.Head();
                GlobalContextRequirement gcr = mainModule.GetExecutable().GlobalContextRequirement;
                if (gcr != null)
                {
                    ItemType req = gcr.RequiredItemType;
                    if (req != null && !req.Matches(contextItem, context.GetConfiguration().GetTypeHierarchy()))
                    {
                        throw new XPathException("Required context item type is " + req, "FOQM0005");
                    }
                }
                dqc.ContextItem = contextItem;
            }

            Controller newController = xqe.NewController(dqc);
            // The loaded module runs on its own controller; make it share the caller's deadline so a
            // runaway module function cannot outlive the transformation that loaded it (nor reset the
            // clock). A caller with no deadline leaves the module unbounded, matching its choice.
            newController.InheritDeadlineFrom(context.GetController());
            IXPathContext newContext = newController.NewXPathContext();

            // Public global variables of the imported library module, evaluated. The main module's
            // GetImportedGlobalVariables() (libraryVariables) aggregates all imported modules' variables;
            // filter by the requested module namespace.
            MapItem variablesMap = new HashTrieMap();
            foreach (GlobalVariable var in mainModule.ImportedGlobalVariables)
            {
                QNameValue qn = new QNameValue(var.GetVariableQName(), BuiltInAtomicType.QNAME);
                if (qn.GetNamespaceURI().Equals(moduleUri) && !var.IsPrivate())
                {
                    IGroundedValue value;
                    try
                    {
                        value = var.EvaluateVariable(newContext);
                    }
                    catch (XPathException e)
                    {
                        e.SetIsGlobalError(false);
                        throw e.ReplacingErrorCode("XPTY0004", "FOQM0005");
                    }

                    variablesMap = variablesMap.AddEntry(qn, value);
                }
            }

            // Public functions of the target module, as bound function items keyed name -> {arity -> function}.
            MapItem functionsMap = new HashTrieMap();
            IExportAgent agent = new ThrowingExportAgent();
            XQueryFunctionLibrary functionLib = mainModule.GlobalFunctionLibrary;
            foreach (XQueryFunction function in functionLib.FunctionDefinitions)
            {
                QNameValue fqn = new QNameValue(function.GetFunctionName(), BuiltInAtomicType.QNAME);
                if (fqn.GetNamespaceURI().Equals(moduleUri) && !function.IsPrivate())
                {
                    UserFunction userFunction = function.GetUserFunction();
                    UserFunctionReference.BoundUserFunction buf =
                        new UserFunctionReference.BoundUserFunction(userFunction, userFunction.GetArity(), null, agent, newController);
                    IGroundedValue existing = functionsMap[fqn];
                    MapItem newMap;
                    if (existing is MapItem em)
                    {
                        newMap = em.AddEntry(Int64Value.MakeIntegerValue(function.NumberOfParameters), buf);
                    }
                    else
                    {
                        newMap = new SingleEntryMap(Int64Value.MakeIntegerValue(function.NumberOfParameters), buf);
                    }

                    functionsMap = functionsMap.AddEntry(fqn, newMap);
                }
            }

            DictionaryMap map = new DictionaryMap();
            map.InitialPut("variables", variablesMap);
            map.InitialPut("functions", functionsMap);
            return map;
        }

        private static string ExtractCode(System.Exception e)
        {
            for (System.Exception cur = e; cur != null; cur = cur.InnerException)
            {
                if (cur is XPathException xe)
                {
                    StructuredQName c = xe.ErrorCodeQName;
                    if (c != null)
                        return c.GetLocalPart();
                }
            }

            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(e.ToString(), @"\b([A-Z]{3,4}\d{4})\b");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string RootMessage(System.Exception e)
        {
            System.Exception cur = e;
            while (cur.InnerException != null)
                cur = cur.InnerException;
            return cur.Message;
        }

        // A function item returned from load-xquery-module() cannot be statically incorporated into an
        // exported package (upstream raises SXST0069 on export).
        private sealed class ThrowingExportAgent : IExportAgent
        {
            public void Export(OutSmart.DAXon.Tracing.ExpressionPresenter @out)
            {
                XPathException err = new XPathException("Cannot export a stylesheet that statically incorporates XQuery functions", DAXonErrorCode.SXST0069);
                err.SetIsStaticError(true);
                throw err;
            }
        }
    }
}
