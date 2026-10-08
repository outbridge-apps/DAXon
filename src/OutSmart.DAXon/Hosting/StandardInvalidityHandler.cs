////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Lib
{
    public class StandardInvalidityHandler : StandardDiagnostics, IInvalidityHandler
    {
        private readonly Configuration config;
        private Logger logger;

        public virtual Logger Logger
        {
            get => logger; set
            {
                this.logger = value;
            }
        }
        public StandardInvalidityHandler(Configuration config)
        {
            this.config = config;
            this.logger = config.Logger;
        }

        public virtual Configuration GetConfiguration()
        {
            return config;
        }

        public virtual void StartReporting(string systemId)
        {
        }

        public virtual void ReportInvalidity(IInvalidity failure)
        {
            Logger localLogger = logger;
            if (localLogger == null)
            {
                localLogger = config.Logger;
            }

            string explanation = GetExpandedMessage(failure);
            string constraintReference = GetConstraintReferenceMessage(failure);
            string contextLocation = ((ValidationFailure)failure).ContextLocationText;
            string finalMessage = "Validation error " + GetLocationMessage(failure) + "\n  " + WordWrap(explanation) + WordWrap((contextLocation.Length == 0) ? "" : "\n  " + contextLocation) + WordWrap(constraintReference == null ? "" : "\n  " + constraintReference) + FormatListOfOffendingNodes((ValidationFailure)failure);
            localLogger.Error(finalMessage);
        }

        public virtual string GetLocationMessage(IInvalidity err)
        {
            string locMessage = "";
            string systemId;
            NodeInfo node = err.GetInvalidNode();
            AbsolutePath path;
            string nodeMessage = null;
            int lineNumber = err.GetLineNumber();
            if (err is DOMLocator)
            {
                nodeMessage = "at " + ((DOMLocator)err).OriginatingNode.GetNodeName() + ' ';
            }

            if (nodeMessage == null)
            {
                if (lineNumber == -1 && (path = err.GetPath()) != null)
                {
                    nodeMessage = "at " + path + ' ';
                }
                else if (node != null)
                {
                    nodeMessage = "at " + Navigator.GetPath(node) + ' ';
                }
            }

            bool containsLineNumber = lineNumber != -1;
            if (nodeMessage != null)
            {
                locMessage += nodeMessage;
            }

            if (containsLineNumber)
            {
                locMessage += "on line " + lineNumber + ' ';
                if (err.GetColumnNumber() != -1)
                {
                    locMessage += "column " + err.GetColumnNumber() + ' ';
                }
            }

            systemId = err.GetSystemId();
            if (systemId != null && systemId.Length != 0)
            {
                locMessage += (containsLineNumber ? "of " : "in ") + AbbreviateLocationURI(systemId) + ':';
            }

            return locMessage;
        }

        public virtual string GetExpandedMessage(IInvalidity err)
        {
            string code = err.GetErrorCode();
            return (code == null ? "" : code + ": ") + err.GetMessage();
        }

        public virtual string GetConstraintReferenceMessage(IInvalidity err)
        {
            if (err.SchemaPart == -1)
            {
                return null;
            }

            return "See https://www.w3.org/TR/xmlschema11-" + err.SchemaPart + "/#" + err.ConstraintName + " clause " + err.ConstraintClauseNumber;
        }

        public virtual ISequence EndReporting()
        {
            return EmptySequence.GetInstance();
        }
    }
}