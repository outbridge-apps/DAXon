////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal;
using System.IO;
namespace OutSmart.DAXon.Api
{
    public class XsltPackage
    {
        private readonly XsltCompiler compiler;
        private readonly StylesheetPackage stylesheetPackage;

        public virtual string Name => stylesheetPackage.PackageName;

        public virtual StylesheetPackage UnderlyingPreparedPackage => stylesheetPackage;
        public XsltPackage(XsltCompiler compiler, StylesheetPackage pp)
        {
            this.compiler = compiler;
            this.stylesheetPackage = pp;
        }

        public virtual Processor GetProcessor()
        {
            return compiler.GetProcessor();
        }

        public virtual string GetVersion()
        {
            return stylesheetPackage.GetPackageVersion().ToString();
        }

        public virtual PackageVersion GetPackageVersion()
        {
            return stylesheetPackage.GetPackageVersion();
        }

        public virtual WhitespaceStrippingPolicy GetWhitespaceStrippingPolicy()
        {
            return new WhitespaceStrippingPolicy(stylesheetPackage);
        }

        public virtual XsltExecutable Link()
        {
            try
            {
                Configuration config = GetProcessor().UnderlyingConfiguration;
                CompilerInfo info = compiler.UnderlyingCompilerInfo;
                Compilation compilation = new Compilation(config, info);
                compilation.SetPackageData(stylesheetPackage);
                stylesheetPackage.CheckForAbstractComponents();
                PreparedStylesheet pss = new PreparedStylesheet(compilation);
                stylesheetPackage.UpdatePreparedStylesheet(pss);
                pss.AddPackage(stylesheetPackage);
                return new XsltExecutable(GetProcessor(), pss);
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (RecursionDepthError e)
            {
                throw new DAXonApiException(e.ToXPathException());
            }
        }

        public virtual void Save(string file)
        {
            string target = stylesheetPackage.TargetEdition;
            if (target == null)
            {
                target = GetProcessor().DAXonEdition;
            }

            Save(file, target);
        }

        public virtual void Save(string file, string target)
        {
            try
            {
                XQuery.Query.CreateFileIfNecessary(file);
                ExpressionPresenter presenter = GetProcessor().UnderlyingConfiguration.NewExpressionExporter(target, OutSmart.DAXon.Internal.Streams.FileNames.Create(file), stylesheetPackage);
                presenter.GetOptions().relocatable = stylesheetPackage.IsRelocatable();
                stylesheetPackage.Export(presenter);
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (RecursionDepthError e)
            {
                throw new DAXonApiException(e.ToXPathException());
            }
            catch (IOException e)
            {
                throw new DAXonApiException(e);
            }
        }
    }
}