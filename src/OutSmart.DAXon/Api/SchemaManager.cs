////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;namespace OutSmart.DAXon.Api
{
    public abstract class SchemaManager
    {
        public abstract string XsdVersion { get; set; }
        public abstract IErrorReporter ErrorReporter { get; set; }
        public abstract ISchemaURIResolver SchemaURIResolver { get; set; }
        public SchemaManager()
        {
        }
        public abstract void Load(ResolvedResource source);
        public virtual void Load(string file)
        {
            Load(new ResolvedResource { SystemId = file });
        }

        public abstract void ImportComponents(ResolvedResource source);
        public abstract void ExportComponents(IDestination destination);
        public abstract SchemaValidator NewSchemaValidator();
    }
}