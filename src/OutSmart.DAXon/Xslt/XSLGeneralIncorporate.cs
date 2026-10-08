////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Linked;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Xslt
{
    internal abstract class XSLGeneralIncorporate : StyleElement
    {
        private string href;
        private DocumentImpl targetDoc;
        public override bool IsDeclaration()
        {
            return true;
        }

        public abstract bool IsImport();
        public override void PrepareAttributes()
        {
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                if (f.Equals("href"))
                {
                    href = Whitespace.Trim(value);
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }

            if (href == null)
            {
                ReportAbsence("href");
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            ValidateInstruction();
        }

        public virtual void ValidateInstruction()
        {
            CheckEmpty();
            CheckTopLevel(IsImport() ? "XTSE0190" : "XTSE0170", false);
        }

        public virtual StylesheetModule GetIncludedStylesheet(StylesheetModule importer, int precedence)
        {
            if (href == null)
            {

                // error already reported
                return null;
            }

            // Each level of xsl:include/xsl:import recurses through here into SpliceIncludes below.
            // The XTSE0180 cycle check only catches a repeated URI, so a server handing out a fresh
            // URI per level (or a generated chain) recurses without bound and overflows the
            // uncatchable .NET stack while compiling - ~900 levels on a 1 MB worker thread (AW).
            ProbeStylesheetDepth();

            try
            {
                PrincipalStylesheetModule psm = importer.GetPrincipalStylesheetModule();

                XSLStylesheet includedSheet;
                StylesheetModule incModule;
                DocumentKey key = DocumentFn.ComputeDocumentKey(href, GetBaseURI(), GetCompilation().GetPackageData(), false);
                includedSheet = (XSLStylesheet)psm.GetStylesheetDocument(key);
                if (includedSheet != null)
                {

                    // we already have the stylesheet document in cache; but we need to create a new module,
                    // because the import precedence might be different. See test impincl30.
                    incModule = new StylesheetModule(includedSheet, precedence);
                    incModule.Importer = importer;

                    // check for recursion
                    if (CheckForRecursion(importer, incModule.RootElement))
                    {
                        return null;
                    }
                }
                else
                {

                    //                DocumentImpl includedDoc = (DocumentImpl)map.get(key);
                    DocumentImpl includedDoc = targetDoc;
                    ElementImpl outermost = includedDoc.DocumentElement;
                    if (outermost is LiteralResultElement)
                    {
                        includedDoc = ((LiteralResultElement)outermost).MakeStylesheet(false);
                        outermost = includedDoc.DocumentElement;
                    }

                    if (!(outermost is XSLStylesheet))
                    {
                        string verb = this is XSLImport ? "Imported" : "Included";
                        CompileError(verb + " document " + href + " is not a stylesheet", "XTSE0165");
                        return null;
                    }

                    includedSheet = (XSLStylesheet)outermost;
                    psm.PutStylesheetDocument(key, includedSheet);
                    incModule = new StylesheetModule(includedSheet, precedence);
                    incModule.Importer = importer;
                    ComponentDeclaration decl = new ComponentDeclaration(incModule, includedSheet);
                    includedSheet.Validate(decl);
                    if (includedSheet.validationError != null)
                    {
                        if (reportingCircumstances == OnFailure.REPORT_ALWAYS)
                        {
                            includedSheet.CompileError(includedSheet.validationError);
                        }
                        else if (includedSheet.reportingCircumstances == OnFailure.REPORT_UNLESS_FORWARDS_COMPATIBLE)

                        // not sure if this can still happen
                        /*&& !incSheet.forwardsCompatibleModeIsEnabled()*/
                        {
                            includedSheet.CompileError(includedSheet.validationError);
                        }
                    }
                }

                incModule.SpliceIncludes(); // resolve any nested imports and includes;

                // Check the consistency of input-type-annotations
                //assert thisSheet != null;
                importer.InputTypeAnnotations = includedSheet.InputTypeAnnotationsAttribute | incModule.InputTypeAnnotations;
                return incModule;
            }
            catch (XPathException err)
            {
                CompileError(err.WithErrorCode("XTSE0165").AsStaticError());
                return null;
            }
        }

        /*&& !incSheet.forwardsCompatibleModeIsEnabled()*/
        public virtual void SetTargetDocument(DocumentImpl doc)
        {
            this.targetDoc = doc;
        }

        /*&& !incSheet.forwardsCompatibleModeIsEnabled()*/
        private bool CheckForRecursion(StylesheetModule importer, NodeInfo source)
        {
            StylesheetModule anc = importer;
            if (source.GetSystemId() != null)
            {
                while (anc != null)
                {
                    if (DocumentKey.NormalizeURI(source.GetSystemId()).Equals(DocumentKey.NormalizeURI(anc.RootElement.GetSystemId())))
                    {
                        CompileError("A stylesheet cannot " + GetLocalPart() + " itself", this is XSLInclude ? "XTSE0180" : "XTSE0210");
                        return true;
                    }

                    anc = anc.Importer;
                }
            }

            return false;
        }

        /*&& !incSheet.forwardsCompatibleModeIsEnabled()*/
        public override void CompileDeclaration(Compilation compilation, ComponentDeclaration decl)
        {
        }
    }
}