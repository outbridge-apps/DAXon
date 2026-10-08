////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Lib
{
    public class InvalidityReportGenerator : StandardInvalidityHandler
    {
        public static readonly NamespaceUri REPORT_NS = NamespaceUri.Of("http://saxon.sf.net/ns/validation");

        public virtual int ErrorCount => 0;

        public virtual int WarningCount => 0;
        public InvalidityReportGenerator(Configuration config) : base(config)
        {
        }

        public InvalidityReportGenerator(Configuration config, Outputter receiver) : base(config)
        {
        }

        public virtual void SetReceiver(Outputter receiver)
        {
        }

        public virtual void SetSystemId(string id)
        {
        }

        public virtual void SetSchemaName(string name)
        {
        }

        public virtual void SetXsdVersion(string version)
        {
        }

        //}
        public override void ReportInvalidity(IInvalidity failure)
        {
        }

        public override void StartReporting(string systemId)
        {
        }

        //}
        // no action
        public override ISequence EndReporting()
        {
            return null;
        }

        public virtual void CreateMetaData()
        {
        }
    }
}