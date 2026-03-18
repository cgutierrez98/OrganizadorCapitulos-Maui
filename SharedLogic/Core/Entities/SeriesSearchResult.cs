namespace organizadorCapitulos.Core.Entities
{
    public class SeriesSearchResult
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Overview { get; set; } = string.Empty;
        public string? FirstAirDate { get; set; }
    }
}
