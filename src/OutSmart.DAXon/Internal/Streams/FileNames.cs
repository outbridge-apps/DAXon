using System;
using System.Collections.Generic;
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
            return OnDisk(Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize), path);
        }

        public static FileStream Create(string path)
        {
            Check(path);
            return OnDisk(Open(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096), path);
        }

        /// <summary>Whether a file of this name is there.</summary>
        public static bool IsFile(string path)
        {
#if !NET
            string name = LongForm(path);
            if (name != null)
            {
                uint attributes = GetFileAttributes(name);
                return attributes != NoAttributes && (attributes & AttributeDirectory) == 0;
            }
#endif
            return File.Exists(path);
        }

        /// <summary>Whether a directory of this name is there.</summary>
        public static bool IsDirectory(string path)
        {
#if !NET
            string name = LongForm(path);
            if (name != null)
            {
                uint attributes = GetFileAttributes(name);
                return attributes != NoAttributes && (attributes & AttributeDirectory) != 0;
            }
#endif
            return Directory.Exists(path);
        }

        /// <summary>Whether a file of this name may not be written.</summary>
        public static bool IsReadOnly(string path)
        {
#if !NET
            string name = LongForm(path);
            if (name != null)
            {
                uint attributes = GetFileAttributes(name);
                return attributes != NoAttributes && (attributes & (uint)FileAttributes.ReadOnly) != 0;
            }
#endif
            return new FileInfo(path).IsReadOnly;
        }

        /// <summary>The form of a path that the classes of System.IO take, however long it is, on this runtime where they can.</summary>
        public static string ForListing(string path)
        {
#if !NET
            return LongForm(path) ?? path;
#else
            return path;
#endif
        }

        /// <summary>An entry of a directory listing.</summary>
        internal struct Entry
        {
            public string Name;
            public bool IsDirectory;
            public bool IsReparsePoint;
        }

        /// <summary>
        /// The entries of a directory, in the system's order. On .NET Framework a listing through System.IO leaves out, with
        /// no word, what lies past MAX_PATH in a directory named the ordinary way, and its .NET 4.5 mode takes no long name at
        /// all: the system is asked itself there.
        /// </summary>
        public static List<Entry> ListDirectory(string path)
        {
            var entries = new List<Entry>();
#if !NET
            string name = Windows ? LongForm(path, 0) : null;
            if (name != null)
            {
                FindData data;
                IntPtr find = FindFirstFile(name.TrimEnd('\\') + @"\*", out data);
                if (find == InvalidHandle)
                {
                    int hr = System.Runtime.InteropServices.Marshal.GetHRForLastWin32Error();
                    throw System.Runtime.InteropServices.Marshal.GetExceptionForHR(hr) ?? new IOException("Cannot list " + path);
                }

                try
                {
                    do
                    {
                        if (data.Name != "." && data.Name != "..")
                        {
                            entries.Add(new Entry
                            {
                                Name = data.Name,
                                IsDirectory = (data.Attributes & AttributeDirectory) != 0,
                                IsReparsePoint = (data.Attributes & (uint)FileAttributes.ReparsePoint) != 0,
                            });
                        }
                    }
                    while (FindNextFile(find, out data));

                    // The end of a listing is "no more files"; any other failure part way is no shorter listing.
                    if (System.Runtime.InteropServices.Marshal.GetLastWin32Error() != NoMoreFiles)
                    {
                        int hr = System.Runtime.InteropServices.Marshal.GetHRForLastWin32Error();
                        throw System.Runtime.InteropServices.Marshal.GetExceptionForHR(hr) ?? new IOException("Cannot list " + path);
                    }
                }
                finally
                {
                    FindClose(find);
                }

                return entries;
            }
#endif
            // The long form keeps a name the ordinary form trims: "dir." and "sp " are other directories than "dir" and "sp".
            string listed = LongForm(path, 0) ?? path;
#if NET
            // Each entry read as it is listed, no FileSystemInfo between; what GetFileSystemInfos lists, failures included.
            var options = new EnumerationOptions { AttributesToSkip = 0, IgnoreInaccessible = false, MatchType = MatchType.Win32 };
            entries.AddRange(new System.IO.Enumeration.FileSystemEnumerable<Entry>(listed,
                (ref System.IO.Enumeration.FileSystemEntry entry) => new Entry
                {
                    Name = entry.FileName.ToString(),
                    IsDirectory = entry.IsDirectory,
                    IsReparsePoint = (entry.Attributes & FileAttributes.ReparsePoint) != 0,
                },
                options));
#else
            foreach (FileSystemInfo info in new DirectoryInfo(listed).GetFileSystemInfos())
            {
                entries.Add(new Entry
                {
                    Name = info.Name,
                    IsDirectory = info is DirectoryInfo,
                    IsReparsePoint = (info.Attributes & FileAttributes.ReparsePoint) != 0,
                });
            }
#endif

            return entries;
        }

        /// <summary>A path as a listing gives it back, without the form <see cref="ForListing"/> asked for.</summary>
        public static string WithoutLongForm(string path)
        {
            if (path.StartsWith(@"\\?\UNC\", StringComparison.Ordinal))
            {
                return @"\\" + path.Substring(8);
            }

            return path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path.Substring(4) : path;
        }

#if !NET
        // .NET Framework gives up at MAX_PATH wherever it does not add the long form itself, and in its .NET 4.5 mode everywhere.
        // The system takes the long form of any full path. 248 is its limit for a directory.
        private const int LongestOrdinary = 247;
        private const uint NoAttributes = 0xFFFFFFFF;
        private const uint AttributeDirectory = 0x10;
        private const int NoMoreFiles = 18;   // ERROR_NO_MORE_FILES

        [System.Runtime.InteropServices.DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        private static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", EntryPoint = "GetFileAttributesW", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFileAttributes(string name);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private struct FindData
        {
            public uint Attributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME Created;
            public System.Runtime.InteropServices.ComTypes.FILETIME Accessed;
            public System.Runtime.InteropServices.ComTypes.FILETIME Written;
            public uint SizeHigh;
            public uint SizeLow;
            public uint Reserved0;
            public uint Reserved1;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 260)]
            public string Name;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 14)]
            public string ShortName;
        }

        private static readonly IntPtr InvalidHandle = new IntPtr(-1);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", EntryPoint = "FindFirstFileW", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindFirstFile(string name, out FindData data);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", EntryPoint = "FindNextFileW", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool FindNextFile(IntPtr find, out FindData data);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool FindClose(IntPtr find);

        // The long form of a full path past the ordinary limit; null for any other, which goes the ordinary way.
        private static string LongForm(string path)
        {
            return LongForm(path, LongestOrdinary + 1);
        }

        private static FileStream OpenLong(string name, FileMode mode, FileAccess access, FileShare share, int bufferSize)
        {
            uint wanted = access == FileAccess.Read ? 0x80000000u : 0x40000000u;
            uint disposition = mode == FileMode.Open ? 3u : 2u;   // OPEN_EXISTING, CREATE_ALWAYS
            Microsoft.Win32.SafeHandles.SafeFileHandle handle = CreateFile(name, wanted, (uint)share, IntPtr.Zero, disposition, 0x80, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                int hr = System.Runtime.InteropServices.Marshal.GetHRForLastWin32Error();
                handle.Dispose();
                throw System.Runtime.InteropServices.Marshal.GetExceptionForHR(hr) ?? new IOException("Cannot open " + name);
            }

            return new FileStream(handle, access, bufferSize);
        }
#endif

        // The long form of a full path from the given length on, null if it has none; a path already in the long form is itself.
        private static string LongForm(string path, int from)
        {
            if (path != null && from == 0 && path.StartsWith(@"\\?\", StringComparison.Ordinal))
            {
                return path;
            }

            if (!Windows || path == null || path.Length < from || path.StartsWith(@"\\?\", StringComparison.Ordinal))
            {
                return null;
            }

            bool share = path.Length > 2 && IsSeparator(path[0]) && IsSeparator(path[1]);
            bool drive = path.Length > 2 && IsLetter(path[0]) && path[1] == ':' && IsSeparator(path[2]);
            if (!share && !drive)
            {
                return null;
            }

            string name = path.Replace('/', '\\');
            if (name.Contains(@"\.\") || name.Contains(@"\..\") || name.EndsWith(@"\.", StringComparison.Ordinal) || name.EndsWith(@"\..", StringComparison.Ordinal))
            {
                return null;
            }

            return share ? @"\\?\UNC\" + name.Substring(2) : @"\\?\" + name;
        }

        private static FileStream Open(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize)
        {
#if !NET
            string longForm = LongForm(path);
            if (longForm != null)
            {
                return OpenLong(longForm, mode, access, share, bufferSize);
            }
#endif
            try
            {
                return new FileStream(path, mode, access, share, bufferSize);
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
