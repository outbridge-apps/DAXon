////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees;
using OutSmart.DAXon.Types;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Xslt
{
    public class ExpressionContext : IStaticContext
    {
        private readonly StyleElement element;
        private readonly StructuredQName attributeName;
        private ILocation containingLocation;
        private RetainedStaticContext retainedStaticContext;

        public virtual StructuredQName AttributeName => attributeName;

        public virtual string StaticBaseURI => element.GetBaseURI();
        public ExpressionContext(StyleElement styleElement, StructuredQName attributeName)
        {
            element = styleElement;
            this.attributeName = attributeName;
        }

        public virtual Configuration GetConfiguration()
        {
            return element.GetConfiguration();
        }

        public virtual StylesheetPackage GetPackageData()
        {
            return element.GetPackageData();
        }

        public virtual bool IsSchemaAware()
        {
            return element.IsSchemaAware();
        }

        public virtual IXPathContext MakeEarlyEvaluationContext()
        {
            return new EarlyEvaluationContext(GetConfiguration());
        }

        public virtual RetainedStaticContext MakeRetainedStaticContext()
        {
            if (retainedStaticContext == null)
            {
                if (element.ChangesRetainedStaticContext() || !(element.GetParent() is StyleElement))
                {
                    retainedStaticContext = new RetainedStaticContext(this);
                }
                else
                {
                    StyleElement.ResolveAncestorsFirst((StyleElement)element.GetParent(), s => s.GetStaticContext().retainedStaticContext == null, s => s.GetStaticContext().MakeRetainedStaticContext());
                    retainedStaticContext = ((StyleElement)element.GetParent()).GetStaticContext().MakeRetainedStaticContext();
                }
            }

            return retainedStaticContext;
        }

        public virtual ILocation GetContainingLocation()
        {
            if (containingLocation == null)
            {
                if (attributeName == null)
                {
                    containingLocation = element;
                }
                else
                {
                    containingLocation = new AttributeLocation(element, attributeName);
                }
            }

            return containingLocation;
        }

        /// <summary>
        /// Issue a compile-time warning
        /// </summary>
        public virtual void IssueWarning(string s, string errorCode, ILocation locator)
        {
            element.IssueWarning(s, errorCode, locator);
        }

        public virtual string GetSystemId()
        {
            return element.GetSystemId();
        }

        public virtual INamespaceResolver GetNamespaceResolver()
        {
            return element.AllNamespaces;
        }

        public virtual Types.ItemType GetRequiredContextItemType()
        {
            return AnyItemType.GetInstance();
        }

        public virtual DecimalFormatManager GetDecimalFormatManager()
        {
            return element.GetCompilation().GetPrincipalStylesheetModule().GetDecimalFormatManager();
        }

        public virtual OptimizerOptions GetOptimizerOptions()
        {
            return element.GetCompilation().GetCompilerInfo().GetOptimizerOptions();
        }

        public virtual Expression BindVariable(StructuredQName qName)
        {
            SourceBinding sourceBinding = element.BindVariable(qName, attributeName);
            if (sourceBinding == null)
            {
                if (qName.HasURI(NamespaceUri.XSLT) && qName.GetLocalPart().Equals("original"))
                {
                    // $xsl:original in an overriding xsl:variable: the by-QName reference stays unbound and
                    // resolves at slot-allocation time to the hidden copy of the overridden component
                    // (Actor.ProcessComponentReference -> StylesheetPackage.GetOverriddenComponent).
                    element.GetXslOriginal(StandardNames.XSL_VARIABLE);
                    return new GlobalVariableReference(qName);
                }


                // it might have been declared in an imported package or query
                SymbolicName sn = new SymbolicName(StandardNames.XSL_VARIABLE, qName);
                Component comp = element.GetCompilation().GetPrincipalStylesheetModule().GetComponent(sn);
                if (comp != null)
                {

                    // test variable-0118
                    // See tests variable-0118 and variable-0120
                    SequenceTool.Supply(element.IterateAxis(AxisInfo.ANCESTOR_OR_SELF), (parent) =>
                    {
                        if (parent is XSLGlobalVariable && ((XSLGlobalVariable)parent).GetVariableQName().Equals(qName))
                        {
                            XPathException err = new XPathException("Variable $" + qName.DisplayName + " cannot be used within its own declaration", "XPST0008");
                            err.SetIsStaticError(true);
                            throw err;
                        }
                    });
                    GlobalVariable globalVar = (GlobalVariable)comp.GetActor();
                    GlobalVariableReference vref = new GlobalVariableReference(globalVar);
                    vref.SetStaticType(globalVar.GetRequiredType(), null, 0);
                    return vref;
                }


                // it might be an implicit error variable in try/catch
                if (GetXPathVersion() >= 30 && qName.HasURI(NamespaceUri.ERR))
                {
                    StyleElement catcher = null;
                    for (NodeInfo anc = element; anc != null; anc = anc.GetParent())
                    {
                        if (anc is XSLCatch)
                        {
                            catcher = (StyleElement)anc;
                            break;
                        }
                    }
                    if (catcher != null)
                    {
                        foreach (StructuredQName errorVariable in StandardNames.errorVariables)
                        {
                            if (errorVariable.GetLocalPart().Equals(qName.GetLocalPart()))
                            {
                                SystemFunction f = VendorFunctionSetHE.GetInstance().MakeFunction("dynamic-error-info", 1);
                                return f.MakeFunctionCall(new StringLiteral(qName.GetLocalPart()));
                            }
                        }
                    }
                }

                XPathException error = new XPathException("Variable $" + qName.DisplayName + " has not been declared (or its declaration is not in scope)", "XPST0008");
                error.SetIsStaticError(true);
                throw error;
            }

            VariableReference var;
            if (sourceBinding.HasProperty(SourceBinding.BindingProperty.IMPLICITLY_DECLARED))
            {

                // Used for the $value variable in xsl:accumulator-rule
                SuppliedParameterReference supRef = new SuppliedParameterReference(0);
                supRef.SetSuppliedType(sourceBinding.DeclaredType);
                return supRef;
            }

            if (sourceBinding.HasProperty(SourceBinding.BindingProperty.GLOBAL))
            {
                var = new GlobalVariableReference(qName);

                GlobalVariable compiledVar = ((XSLGlobalVariable)sourceBinding.SourceElement).CompiledVariable;
                if (compiledVar != null && element.GetCompilation().GetCompilerInfo().IsJustInTimeCompilation())
                {
                    var.Fixup(compiledVar);
                    var.SetStaticType(compiledVar.GetRequiredType(), sourceBinding.ConstantValue, 0);
                }
                else
                {
                    sourceBinding.RegisterReference(var);
                }

                return var;
            }
            else
            {
                var = new LocalVariableReference(qName);
                sourceBinding.RegisterReference(var);
                return var;
            }
        }

        public virtual IFunctionLibrary GetFunctionLibrary()
        {

            // Note, the xsl:original library is now present in the package function library unconditionally
            return element.ContainingPackage.GetFunctionLibrary();
        }

        /// <summary>
        /// Get the default collation. Return null if no default collation has been defined
        /// </summary>
        public virtual string GetDefaultCollationName()
        {
            return element.GetDefaultCollationName();
        }

        /// <summary>
        /// Get the default collation. Return null if no default collation has been defined
        /// </summary>
        public virtual NamespaceUri GetDefaultElementNamespace()
        {
            return element.DefaultXPathNamespace;
        }

        /// <summary>
        /// Get the default function @namespace
        /// </summary>
        public virtual NamespaceUri GetDefaultFunctionNamespace()
        {
            return NamespaceUri.FN;
        }

        public virtual bool IsInBackwardsCompatibleMode()
        {
            return element.XPath10ModeIsEnabled();
        }

        public virtual int GetXPathVersion()
        {
            if (element.GetCompilation().GetCompilerInfo().XsltVersion == 40 || GetConfiguration().GetBooleanProperty(Feature<bool>.ALLOW_SYNTAX_EXTENSIONS))
            {
                return 40;
            }


            //        if ((element.getEffectiveVersion() == 40
            //                || element.getCompilation().getCompilerInfo().getXsltVersion() == 40
            //                || (attributeName != null && attributeName.hasURI(NamespaceUri.SAXON)))
            return GetConfiguration().GetConfigurationProperty(Feature<int>.XPATH_VERSION_FOR_XSLT); //        }
        }

        public virtual bool IsImportedSchema(NamespaceUri @namespace)
        {

            //if (Configuration.USE_PACKAGE_BINDING) {
            return element.GetPrincipalStylesheetModule().IsImportedSchema(@namespace); //} else {
            //    return getConfiguration().
            //}
        }

        public virtual HashSet<NamespaceUri> GetImportedSchemaNamespaces()
        {
            return element.GetPrincipalStylesheetModule().ImportedSchemaTable;
        }

        public virtual KeyManager GetKeyManager()
        {
            return element.GetCompilation().GetPrincipalStylesheetModule().GetKeyManager();
        }

        public virtual StyleElement GetStyleElement()
        {
            return element;
        }

        public virtual Types.ItemType ResolveTypeAlias(StructuredQName typeName)
        {
            return GetPackageData().ObtainTypeAliasManager().GetItemType(typeName);
        }

        public virtual UnprefixedElementMatchingPolicy GetUnprefixedElementMatchingPolicy()
        {
            return element.GetCompilation().GetCompilerInfo().GetUnprefixedElementMatchingPolicy();
        }
        PackageData IStaticContext.GetPackageData() => GetPackageData();
    }
}

