using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace SceneSkope.Utilities.CommandLineApplications
{
    public class LockFile : IDisposable
    {
        public static LockFile TryCreate(string fileName)
        {
            var lockFile = new LockFile(fileName);
            if (lockFile.TryLock())
            {
                return lockFile;
            }
            else
            {
                return null;
            }
        }

        private readonly string _fileName;
        private FileStream _stream;

        public LockFile(string fileName)
        {
            _fileName = fileName;
        }

        public bool TryLock()
        {
            try
            {
                _stream = new FileStream(_fileName, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose);
                using (var writer = new StreamWriter(_stream, Encoding.UTF8, 4096, true))
                {
                    var process = Process.GetCurrentProcess();
                    writer.WriteLine($"{process.Id}");
                    writer.Flush();
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to lock {ex.Message}");
                return false;
            }
        }

        public void Dispose() => _stream.Dispose();
    }
}
