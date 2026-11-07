using System;
using System.IO;
using System.Text;

namespace Xmana.Core.Library
{
    /// <summary>Where the library JSON lives. Failures are never fatal: a storage that cannot read behaves as empty.</summary>
    public interface ILibraryStorage
    {
        string? Read();

        void Write(string json);

        void Delete();
    }

    public sealed class MemoryLibraryStorage : ILibraryStorage
    {
        public string? Content { get; private set; }

        public int Writes { get; private set; }

        public string? Read() => Content;

        public void Write(string json)
        {
            Content = json;
            Writes++;
        }

        public void Delete() => Content = null;
    }

    /// <summary>One JSON file, written atomically (temp file then move).</summary>
    public sealed class FileLibraryStorage : ILibraryStorage
    {
        private readonly string _path;

        public FileLibraryStorage(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A file path is required.", nameof(path));
            }

            _path = path;
        }

        public string? Read()
        {
            try
            {
                return File.Exists(_path) ? File.ReadAllText(_path, Encoding.UTF8) : null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        public void Write(string json)
        {
            try
            {
                var directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var temp = _path + ".tmp";
                File.WriteAllText(temp, json, new UTF8Encoding(false));
                if (File.Exists(_path))
                {
                    File.Delete(_path);
                }

                File.Move(temp, _path);
            }
            catch (IOException)
            {
                // a failing disk must never break the app
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        public void Delete()
        {
            try
            {
                if (File.Exists(_path))
                {
                    File.Delete(_path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
