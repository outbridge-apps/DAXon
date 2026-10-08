////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Xslt
{
    public class ComponentDeclaration
    {
        private readonly StyleElement sourceElement;
        private readonly StylesheetModule module;

        public virtual StylesheetModule Module => module;

        public virtual StyleElement SourceElement => sourceElement;

        public virtual int Precedence => module.Precedence;
        public ComponentDeclaration(StylesheetModule module, StyleElement source)
        {
            this.module = module;
            this.sourceElement = source;
        }
    }
}