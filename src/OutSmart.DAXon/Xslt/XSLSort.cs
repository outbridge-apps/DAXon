////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:sort element in the stylesheet. <br>
    /// </summary>
    internal sealed class XSLSort : XSLSortOrMergeKey
    {

        protected internal override Expression Stable => stable;
        public override void Validate(ComponentDeclaration decl)
        {
            base.Validate(decl);
            stable = TypeCheck("stable", stable);
            sortKeyDefinition.Stable = stable;
        }
    }
}