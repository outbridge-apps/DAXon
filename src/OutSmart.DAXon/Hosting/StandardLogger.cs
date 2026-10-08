////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.IO;
using OutSmart.DAXon.Serialization;
namespace OutSmart.DAXon.Lib
{
    internal sealed class StandardLogger : Logger
    {
        private TextWriter writer = Console.Error;
        private readonly int threshold = Logger.INFO;
        private readonly bool mustClose = false;

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

        // IO-removal: StandardLogger(TextWriter) dropped -- TextWriter maps to System.IO.TextWriter, handled by StandardLogger(TextWriter).

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
