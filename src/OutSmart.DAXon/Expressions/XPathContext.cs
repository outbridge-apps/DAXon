////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Regex;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Transformation.Rules;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Expressions
{
    /// <summary>
    /// This class represents a context in which an XPath expression is evaluated.
    /// </summary>
    public interface IXPathContext
    {
        XPathContextMajor NewContext();
        XPathContextMajor NewCleanContext();
        XPathContextMinor NewMinorContext();
        ParameterSet GetLocalParameters();
        ParameterSet GetTunnelParameters();
        Controller GetController();
        Configuration GetConfiguration();
        NamePool GetNamePool();
        void SetCaller(IXPathContext caller);
        IXPathContext GetCaller();
        XPathContextMajor MajorContext { get; }
        IFocusIterator TrackFocus(ISequenceIterator iter);
        void SetCurrentIterator(IFocusIterator iter);
        IFocusIterator GetCurrentIterator();
        IItem GetContextItem();
        int GetLast();
        bool IsAtLast();
        IResourceResolver GetResourceResolver();
        IErrorReporter GetErrorReporter();
        Component GetCurrentComponent();
        int UseLocalParameter(StructuredQName parameterId, int slotNumber, bool isTunnel);
        StackFrame GetStackFrame();
        ISequence EvaluateLocalVariable(int slotnumber);
        void SetLocalVariable(int slotNumber, ISequence value);
        int TemporaryOutputState { get; set; }
        string CurrentOutputUri { get; set; }
        Component.M GetCurrentMode();
        Rule GetCurrentTemplateRule();
        IGroupIterator GetCurrentGroupIterator();
        IGroupIterator GetCurrentMergeGroupIterator();
        IRegexIterator GetCurrentRegexIterator();
        DateTimeValue GetCurrentDateTime();
        int GetImplicitTimezone();
        XPathException GetCurrentException();
        XPathContextMajor.ThreadManager GetThreadManager();
        void WaitForChildThreads();
        Component GetTargetComponent(int bindingSlot);
    }
}
