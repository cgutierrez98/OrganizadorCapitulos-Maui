namespace organizadorCapitulos.Core.Entities
{
    public class SeriesSearchResult
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Overview { get; set; } = string.Empty;
        public string? FirstAirDate { get; set; }
        public string? PosterPath { get; set; }
        public string? PosterUrl => string.IsNullOrEmpty(PosterPath) ? null : $"https://image.tmdb.org/t/p/w92{PosterPath}";
    }
}
