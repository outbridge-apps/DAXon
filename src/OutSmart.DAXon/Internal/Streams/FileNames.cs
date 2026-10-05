using System;
using System.IO;

namespace OutSmart.DAXon.Internal.Streams
{
    /// <summary>
    /// The names Windows keeps for what is no file. A device - CON, NUL, COM1, \\.\pipe\x - is opened by .NET
    /// Framework only to be refused, with an exception of its own; later runtimes open it and read: a console or a
    /// port is a wait no time limit ends. A name with a colon past the drive is a stream of another file: output
    /// goes where no listing shows it, and "x.config::$DATA" reads x.config past a check of how the name ends.
    /// Every file the engine opens by a name goes through here. Such a name is an IOException before anything is
    /// opened; what the name did not show (CONIN$) is one as soon as it is open and turns out to be no file.
    /// Elsewhere than on Windows every name is a file's.
    /// </summary>
    internal static class FileNames
    {
        private static readonly bool Windows = Path.DirectorySeparatorChar == '\\';

        public static FileStream OpenRead(string path)
        {
            return OpenRead(path, 4096);
        }

        public static FileStream OpenRead(string path, int bufferSize)
        {
            Check(path);
            return OnDisk(Open(path, FileMode.Open, FileAccess.Read, bufferSize), path);
        }

        public static FileStream Create(string path)
        {
            Check(path);
            return OnDisk(Open(path, FileMode.Create, FileAccess.Write, 4096), path);
        }

        private static FileStream Open(string path, FileMode mode, FileAccess access, int bufferSize)
        {
            try
            {
                return new FileStream(path, mode, access, FileShare.Read, bufferSize);
            }
            catch (NotSupportedException e)
            {
                // .NET Framework: opened, found to be no file on a disk, closed - or a form of path it does not take
                throw new IOException(e.Message, e);
            }
        }

        private static FileStream OnDisk(FileStream stream, string path)
        {
            if (Windows && !stream.CanSeek)
            {
                stream.Dispose();
                throw new IOException("'" + path + "' is a device, not a file");
            }

            return stream;
        }

        /// <summary>
        /// Refuses the name of a device or of a stream of a file; says nothing of any other, the open does.
        /// </summary>
        public static void Check(string path)
        {
            if (!Windows || string.IsNullOrEmpty(path))
            {
                return;
            }

            int start = 0;
            if (path.Length >= 4 && IsSeparator(path[0]) && IsSeparator(path[1]) && IsSeparator(path[3]))
            {
                if (path[2] == '.')
                {
                    throw new IOException("'" + path + "' is a device, not a file");
                }

                if (path[2] == '?')
                {
                    // the long form of a path: \\?\C:\dir\file keeps its drive colon; what else it can name is
                    // told apart when it is open
                    start = path.Length >= 6 && path[5] == ':' ? 6 : 4;
                }
            }
            else if (path.Length >= 2 && path[1] == ':' && IsLetter(path[0]))
            {
                start = 2;
            }

            if (path.IndexOf(':', start) >= 0)
            {
                throw new IOException("'" + path + "' is not the name of a file: a colon past the drive names a stream of one");
            }

            // The names of the devices are few, and the system knows best which a name is on this version of
            // Windows ("nul.xml" is the device up to Windows 10 and a file after): it is asked only about a name
            // that begins like one.
            int last = path.LastIndexOfAny(Separators) + 1;
            if (last < start)
            {
                last = start;
            }

            if (path.Length - last >= 3 && BeginsLikeDevice(path, last))
            {
                string full;
                try
                {
                    full = Path.GetFullPath(path);
                }
                catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException || e is System.Security.SecurityException)
                {
                    return;
                }

                if (full.StartsWith(@"\\.\", StringComparison.Ordinal))
                {
                    throw new IOException("'" + path + "' is a device, not a file");
                }
            }
        }

        private static readonly char[] Separators = { '\\', '/' };

        private static bool IsSeparator(char c)
        {
            return c == '\\' || c == '/';
        }

        private static bool IsLetter(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
        }

        // CON, PRN, AUX, NUL, COM1.., LPT1.., CONIN$, CONOUT$ in any case, with or without an extension: three
        // letters and what may follow them. "config.xml" and "contoso.xml" are not asked about.
        private static bool BeginsLikeDevice(string path, int at)
        {
            char a = (char)(path[at] | 0x20), b = (char)(path[at + 1] | 0x20), c = (char)(path[at + 2] | 0x20);
            char next = at + 3 < path.Length ? path[at + 3] : '.';
            bool ends = next == '.' || next == ' ';
            bool numbered = (next >= '0' && next <= '9') || next == (char)0xB9 || next == (char)0xB2 || next == (char)0xB3;
            switch (a)
            {
                case 'c':
                    return b == 'o' && ((c == 'n' && (ends || (next | 0x20) == 'i' || (next | 0x20) == 'o')) || (c == 'm' && numbered));
                case 'p':
                    return b == 'r' && c == 'n' && ends;
                case 'a':
                    return b == 'u' && c == 'x' && ends;
                case 'n':
                    return b == 'u' && c == 'l' && ends;
                case 'l':
                    return b == 'p' && c == 't' && numbered;
                default:
                    return false;
            }
        }
    }
}
