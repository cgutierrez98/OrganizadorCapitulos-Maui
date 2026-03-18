using System;

namespace organizadorCapitulos.Core.Exceptions
{
    public class FileOperationException : Exception
    {
        public string Path { get; }
        public FileOperationException(string message, string path, Exception inner) : base(message, inner)
        {
            Path = path;
        }
    }
}
