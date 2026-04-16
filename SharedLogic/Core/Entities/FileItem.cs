using organizadorCapitulos.Core.Enums;

namespace organizadorCapitulos.Core.Entities
{
    public class FileItem
    {
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public FileStatus Status { get; set; }
    }
}
