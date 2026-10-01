////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class MappingIterator : ISequenceIterator
    {
        private readonly ISequenceIterator @base;
        private readonly IMappingFunction action;
        private readonly OutSmart.DAXon.Core.Controller controller;   // null: no deadline check
        private ISequenceIterator results = null;
        public MappingIterator(ISequenceIterator @base, IMappingFunction action)
        {
            this.@base = @base;
            this.action = action;
        }

        // Overload used by producers of potentially unbounded mapped sequences (e.g. the pull form
        // of a 'for' expression): the controller lets the pull loop honour the transformation's
        // cooperative deadline.
        public MappingIterator(ISequenceIterator @base, IMappingFunction action, OutSmart.DAXon.Core.Controller controller) : this(@base, action)
        {
            this.controller = controller;
        }

        public static MappingIterator IMap(ISequenceIterator @base, SequenceMapper.ILambda mappingExpression)
        {
            return new MappingIterator(@base, SequenceMapper.Of(mappingExpression));
        }

        public IItem Next()
        {
            try
            {
                IItem nextItem;
                while (true)
                {
                    controller?.CheckTimeout();
                    if (results != null)
                    {
                        nextItem = results.Next();
                        if (nextItem != null)
                        {
                            break;
                        }
                        else
                        {
                            results = null;
                        }
                    }

                    IItem nextSource = @base.Next();
                    if (nextSource != null)
                    {

                        // Call the supplied mapping function
                        ISequenceIterator obj = action.IMap(nextSource);

                        // The result may be null (representing an empty sequence),
                        //  or a ISequenceIterator (any sequence)
                        if (obj != null)
                        {
                            results = obj;
                            nextItem = results.Next();
                            if (nextItem == null)
                            {
                                results = null;
                            }
                            else
                            {
                                break;
                            }
                        } // now go round the loop to get the next item from the base sequence
                    }
                    else
                    {
                        results = null;
                        return null;
                    }
                }

                return nextItem;
            }
            catch (XPathException e)
            {
                throw new UncheckedXPathException(e);
            }
        }

        public void Dispose()
        {
            if (results != null)
            {
                results.Dispose();
            }

            @base.Dispose();
        }
    }
}