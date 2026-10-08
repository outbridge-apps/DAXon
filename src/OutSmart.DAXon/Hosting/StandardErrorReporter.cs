////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees;
using System;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Functions;
namespace OutSmart.DAXon.Lib
{
    internal sealed class StandardErrorReporter : StandardDiagnostics, IErrorReporter
    {
        private int warningCount;
        private readonly int maximumNumberOfWarnings = 25;
        private int errorCount;
        private readonly int maximumNumberOfErrors = 1000;
        private readonly int maxOrdinaryCharacter = 255;
        private readonly int stackTraceDetail = 2;
        private readonly HashSet<string> warningsIssued = new HashSet<string>();
        protected Logger logger = new StandardLogger();
        private IXmlProcessingError latestError;
        private readonly bool outputErrorCodes = true;
        private readonly HashSet<StructuredQName> suppressedWarnings;

        public Logger Logger
        {
            get => logger; set
            {
                this.logger = value;
            }
        }

        public int NumberOfErrors => errorCount;
        public StandardErrorReporter()
        {
        }

        public bool IsSuppressedWarning(StructuredQName errorCode)
        {
            return suppressedWarnings != null && suppressedWarnings.Contains(errorCode);
        }

        // ONE instance of this reporter is shared by every XsltCompiler and every compilation made
        // from a Processor: CompilerInfo's copy constructor copies the reference, and both
        // `new XsltCompiler(...)` and each Compile() go through it. So everything below is
        // per-PROCESSOR state being used to make per-COMPILATION decisions, which is why the
        // counters are reset per episode (StartCompilationEpisode) and mutated under a lock.
        private readonly object counterLock = new object();

        /// <summary>
        /// Begin a new compilation episode: the error and warning budgets are per compilation,
        /// not per Processor lifetime.
        /// </summary>
        /// <remarks>
        /// Without this the budget accumulated forever on a shared reporter. Measured: one
        /// Processor, the same two-error stylesheet compiled repeatedly, and compile 501 (= 1000
        /// errors = maximumNumberOfErrors) got "Too many errors reported" attached to its FIRST
        /// error - which Compilation.ReportError turns into an XmlProcessingAbort, so the host was
        /// told "too many errors" about a stylesheet with two, and got one diagnostic instead of
        /// the full list. That undid what round C1 bought on any long-lived Processor.
        /// </remarks>
        internal void StartCompilationEpisode()
        {
            lock (counterLock)
            {
                errorCount = 0;
                warningCount = 0;
                latestError = null;
            }

            lock (warningsIssued)
            {
                warningsIssued.Clear();
            }
        }

        public void Report(IXmlProcessingError processingError)
        {
            // The dedup is "do not report the same object twice in a row"; on a shared reporter
            // another thread's report used to be able to sit between a repeat and its original.
            bool fresh;
            lock (counterLock)
            {
                fresh = processingError != latestError;
                if (fresh) { latestError = processingError; }
            }

            if (fresh)
            {
                if (processingError.IsWarning())
                {
                    if (processingError.GetErrorCode() == null || !IsSuppressedWarning(processingError.GetErrorCode().GetStructuredQName()))
                    {
                        Warning(processingError);
                    }
                }
                else
                {
                    Error(processingError);
                }
            }
        }

        protected void Warning(IXmlProcessingError error)
        {
            if (logger == null)
            {
                logger = new StandardLogger();
            }

            string message = ConstructMessage(error, "", "Warning ");

            // Locked (round BG-P3): concurrent compiles used to race a bare HashSet on the shared
            // reporter. Cold path - warnings are compile diagnostics.
            bool announceCap = false;
            bool display = false;
            lock (warningsIssued)
            {
                if (!warningsIssued.Contains(message))
                {
                    lock (counterLock)
                    {
                        if (warningCount > maximumNumberOfWarnings)
                        {
                            announceCap = warningCount == maximumNumberOfWarnings + 1;
                        }
                        else
                        {
                            display = true;
                        }

                        warningCount++;
                    }

                    // The set exists to dedup what is DISPLAYED. Since round 10 it is also cleared
                    // per compilation episode, so the growth this cap was added to bound is gone on
                    // the compile path; the cap stays as the backstop for any reporter that is
                    // never told an episode started.
                    if (warningCount <= maximumNumberOfWarnings + 1)
                    {
                        warningsIssued.Add(message);
                    }
                }
            }

            // Outside the lock: logger is host code and may block.
            if (announceCap) { logger.Info("No more warnings will be displayed"); }
            else if (display) { logger.Warning(message); }
        }

        protected void Error(IXmlProcessingError err)
        {
            int reported;
            lock (counterLock)
            {
                reported = errorCount++;
            }

            if (reported > maximumNumberOfErrors)
            {
                err.TerminationMessage = "Too many errors reported";
            }

            if (logger == null)
            {
                logger = new StandardLogger();
            }

            string message = DescribeError(err);
            logger.Error(message);
            if (err is XmlProcessingException)
            {
                XPathException exception = ((XmlProcessingException)err).GetXPathException();
                IXPathContext context = exception.XPathContext;
                if (context != null && !(context is EarlyEvaluationContext))
                {
                    OutputStackTrace(logger, context);
                }
            }
        }

        // The full two-line diagnostic the logger emits. Public because a failed compile must
        // be able to attach the same text to the exception it throws (round C1) - the logger's
        // own channel is Console.Error by default, which an embedded host cannot read.
        public string DescribeError(IXmlProcessingError err)
        {
            HostLanguage lang = err.GetHostLanguage();
            string langText = "";
            if (lang != HostLanguage.UNKNOWN)
            {
                switch (lang)
                {
                    case HostLanguage.XSLT:
                        break;
                    case HostLanguage.XQUERY:
                        langText = "in query ";
                        break;
                    case HostLanguage.XPATH:
                        langText = "in expression ";
                        break;
                    case HostLanguage.XML_SCHEMA:
                        langText = "in schema ";
                        break;
                    case HostLanguage.XSLT_PATTERN:
                        langText = "in pattern ";
                        break;
                }
            }

            string kind = "Error ";
            if (err.IsTypeError())
            {
                kind = "Type error ";
            }
            else if (err.IsStaticError())
            {
                kind = "Static error ";
            }

            return ConstructMessage(err, langText, kind);
        }

        public string ConstructMessage(IXmlProcessingError exception, string langText, string kind)
        {
            return ConstructFirstLine(exception, langText, kind) + "\n  " + ConstructSecondLine(exception);
        }

        public string ConstructFirstLine(IXmlProcessingError error, string langText, string kind)
        {
            ILocation locator = error.GetLocation();
            if (locator is AttributeLocation)
            {
                return kind + langText + GetLocationMessageText(locator);
            }
            else if (locator is XPathParser.NestedLocation)
            {
                XPathParser.NestedLocation nestedLoc = (XPathParser.NestedLocation)locator;
                ILocation outerLoc = nestedLoc.GetContainingLocation();
                int line = nestedLoc.LocalLineNumber + 1;
                int column = nestedLoc.GetColumnNumber() + 1;
                string lineInfo = line <= 1 ? "" : "on line " + line + ' ';
                string columnInfo = column <= 1 ? "" : "at " + (line <= 1 ? "char " : "column ") + column + ' ';
                string nearBy = nestedLoc.NearbyText;
                string extraContext = FormatExtraContext(error.GetFailingExpression(), nearBy);
                if (outerLoc is AttributeLocation)
                {

                    // Typical XSLT case
                    string innerLoc = lineInfo + extraContext + columnInfo;
                    return kind + innerLoc + langText + GetLocationMessageText(outerLoc);
                }
                else
                {

                    // Typical XQuery case; no extra information available from the outer location
                    string innerLoc = lineInfo + columnInfo;
                    if (outerLoc.GetLineNumber() > 1)
                    {
                        innerLoc += "(" + langText + "on line " + outerLoc.GetLineNumber() + ") ";
                    }

                    if (outerLoc.GetSystemId() != null)
                    {
                        innerLoc += "of " + outerLoc.GetSystemId() + " ";
                    }

                    return kind + extraContext + innerLoc;
                }
            }
            else
            {
                return kind + GetLocationMessage(error);
            }
        }

        public string FormatExtraContext(Expression failingExpression, string nearBy)
        {
            if (failingExpression != null)
            {
                if (failingExpression.IsCallOn(typeof(Error)))
                {
                    return "signaled by call to error() ";
                }
                else
                {
                    return "evaluating (" + failingExpression.ToShortString() + ") ";
                }
            }
            else if (nearBy != null && !(nearBy.Length == 0))
            {
                return (nearBy.StartsWith("...", StringComparison.Ordinal) ? "near" : "in") + ' ' + Err.Wrap(nearBy) + " ";
            }
            else
            {
                return "";
            }
        }

        public string ConstructSecondLine(IXmlProcessingError err)
        {
            return ExpandSpecialCharacters(WordWrap(GetExpandedMessage(err)));
        }

        protected string GetLocationMessage(IXmlProcessingError err)
        {
            ILocation loc = err.GetLocation();
            return GetLocationMessageText(loc);
        }

        public string GetExpandedMessage(IXmlProcessingError err)
        {
            string message = FormatErrorCode(err) + " " + err.GetMessage();
            message = FormatNestedMessages(err, message);
            return message;
        }

        public string FormatNestedMessages(IXmlProcessingError err, string message)
        {
            StringBuilder sb = new StringBuilder(message);
            Exception e = err.GetCause();
            for (int depth = 0; e != null && depth < 16; depth++)
            {
                // Frames only for a defect, as Java prints them for a RuntimeException alone; a cause the message
                // already names (an I/O failure, a parser error) adds nothing.
                string next = e.Message;
                bool defect = IsDefect(e);
                if (defect || (next != null && !message.Contains(next)))
                {
                    sb.Append(sb.Length > 0 && sb[sb.Length - 1] == '.' ? " Caused by " : ". Caused by ").Append(e.GetType().FullName).Append(": ").Append(next);
                    if (defect)
                    {
                        sb.Append('\n').Append(e.StackTrace);
                    }
                }

                e = e.InnerException;
            }

            return sb.ToString();
        }

        // The .NET counterparts of Java's RuntimeException: a fault in code rather than in the input or the
        // environment (WebException is an InvalidOperationException, a decoding failure or a refused path an
        // ArgumentException; NotSupportedException is left out, an unknown URI scheme raises it too).
        private static bool IsDefect(Exception e)
        {
            if (DAXonApiException.IsIO(e))
            {
                return false;
            }

            return e is NullReferenceException || e is InvalidCastException || e is IndexOutOfRangeException
                || e is KeyNotFoundException || e is NotImplementedException || e is ArithmeticException
                || (e is InvalidOperationException && !(e is System.Net.WebException))
                || (e is ArgumentException && !(e is DecoderFallbackException) && !(e is EncoderFallbackException));
        }


        public string FormatErrorCode(IXmlProcessingError err)
        {
            if (outputErrorCodes)
            {
                QName qCode = err.GetErrorCode();
                if (qCode != null)
                {
                    if (qCode.GetNamespaceUri().Equals(NamespaceUri.ERR))
                    {
                        return qCode.LocalName + " ";
                    }
                    else
                    {
                        return qCode.ToString() + " ";
                    }
                }
            }

            return "";
        }

        public string ExpandSpecialCharacters(string @in)
        {
            if (logger.IsUnicodeAware())
            {
                return @in;
            }
            else
            {
                return ExpandSpecialCharacters(@in, maxOrdinaryCharacter);
            }
        }

        protected void OutputStackTrace(Logger @out, IXPathContext context)
        {
            LogStackTrace(context, @out, stackTraceDetail);
        }
    }
}