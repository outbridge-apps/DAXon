////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Numerics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Internal.Streams;
using System.IO;
using OutSmart.DAXon.Serialization;
namespace OutSmart.DAXon.Lib
{
    internal sealed class StandardLogger : Logger
    {
        private TextWriter writer = Console.Error;
        private int threshold = Logger.INFO;
        private bool mustClose = false;

        public TextWriter PrintWriter
        {
            get => writer; set
            {
                this.writer = value;
            }
        }
        public StandardLogger()
        {
        }

        // IO-removal: StandardLogger(global::System.IO.TextWriter) dropped -- global::System.IO.TextWriter maps to System.IO.TextWriter, handled by StandardLogger(TextWriter).

        public StandardLogger(TextWriter writer)
        {
            PrintWriter = (TextWriter)writer;
        }

        public void SetPrintStream(TextWriter stream)
        {
            this.writer = stream;
        }

        public override StreamResult AsStreamResult()
        {
            return new StreamResult(writer);
        }

        public override void Println(string message, int severity)
        {
            if (severity >= threshold)
            {
                writer.Write(message + "\n");
                writer.Flush();
            }
        }

        /// <summary>
        /// Close the logger, indicating that no further messages will be written
        /// </summary>
        public override void Dispose()
        {
            if (mustClose)
            {
                writer.Dispose();
            }
        }
    }
}
