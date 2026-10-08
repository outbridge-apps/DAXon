////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Text;
using System.IO;
using OutSmart.DAXon.Serialization;
namespace OutSmart.DAXon.Lib
{
    public abstract class Logger
    {
        public const int INFO = 0;
        public const int WARNING = 1;
        public const int ERROR = 2;
        public const int DISASTER = 3;
        private bool unicodeAware = false;
        public virtual void Info(string message)
        {
            Println(message, INFO);
        }

        public virtual void Warning(string message)
        {
            Println(message, WARNING);
        }

        public virtual void Error(string message)
        {
            Println(message, ERROR);
        }

        public virtual void Disaster(string message)
        {
            Println(message, DISASTER);
        }

        public abstract void Println(string message, int severity);
        public virtual void Dispose()
        {
        }

        public virtual bool IsUnicodeAware()
        {
            return unicodeAware;
        }

        public virtual void SetUnicodeAware(bool aware)
        {
            unicodeAware = aware;
        }

        public virtual TextWriter AsWriter()
        {
            return new LoggingWriter(this);
        }

        public virtual StreamResult AsStreamResult()
        {
            return new StreamResult(AsWriter());
        }

        private sealed class LoggingWriter : TextWriter
        {
            private readonly StringBuilder builder = new StringBuilder();
            private readonly Logger logger;
            public override Encoding Encoding => Encoding.UTF8;
            public LoggingWriter(Logger logger)
            {
                this.logger = logger;
            }
            public override void Write(char c) { Write(new char[] { c }, 0, 1); }

            public override void Write(char[] cbuf, int off, int len)
            {
                for (int i = 0; i < len; i++)
                {
                    char ch = cbuf[off + i];
                    if (ch == '\n')
                    {
                        logger.Println(builder.ToString(), INFO);
                        builder.Length = 0;
                    }
                    else
                    {
                        builder.Append(ch);
                    }
                }
            }

            public override void Flush()
            {
                if (builder.Length > 0)
                {
                    logger.Println(builder.ToString(), INFO);
                    builder.Length = 0;
                }
            }

            protected override void Dispose(bool disposing)
            {
                Flush();
            }
        }
    }
}