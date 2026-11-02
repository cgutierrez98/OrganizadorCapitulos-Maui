namespace organizadorCapitulos.Core.Entities
{
    public class MovieSearchResult
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Overview { get; set; } = string.Empty;
        public string? ReleaseDate { get; set; }
    }
}
