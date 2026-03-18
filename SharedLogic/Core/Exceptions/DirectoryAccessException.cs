using System;

namespace organizadorCapitulos.Core.Exceptions
{
    public class DirectoryAccessException : Exception
    {
        public string Folder { get; }
        public DirectoryAccessException(string message, string folder, Exception inner) : base(message, inner)
        {
            Folder = folder;
        }
    }
}
