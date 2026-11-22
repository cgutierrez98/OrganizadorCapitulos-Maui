namespace organizadorCapitulos.Core.Entities
{
    public class MovieSearchResult
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Overview { get; set; } = string.Empty;
        public string? ReleaseDate { get; set; }
        public string? PosterPath { get; set; }
        public string? PosterUrl => string.IsNullOrEmpty(PosterPath) ? null : $"https://image.tmdb.org/t/p/w92{PosterPath}";
    }
}
