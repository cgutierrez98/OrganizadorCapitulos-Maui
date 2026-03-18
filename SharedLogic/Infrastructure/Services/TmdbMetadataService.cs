using System.Threading.Tasks;
using organizadorCapitulos.Core.Entities;
using organizadorCapitulos.Core.Interfaces.Services;

namespace organizadorCapitulos.Infrastructure.Services
{
    public class TmdbMetadataService : IMetadataService
    {
        private string? _apiKey;

        public void Configure(string apiKey)
        {
            _apiKey = apiKey;
        }

        public bool IsConfigured()
        {
            return !string.IsNullOrEmpty(_apiKey);
        }

        public async Task<List<SeriesSearchResult>> SearchSeriesAsync(string query)
        {
            await Task.CompletedTask;
            return new List<SeriesSearchResult>();
        }

        public async Task<string?> GetEpisodeTitleAsync(int seriesId, int season, int episode)
        {
            await Task.CompletedTask;
            return null;
        }

        public async Task<(int season, int episode, string title)?> FindEpisodeByTitleAsync(int seriesId, string title)
        {
            await Task.CompletedTask;
            return null;
        }

        public async Task<ChapterInfo?> GetEpisodeMetadataAsync(int seriesId, int season, int episode)
        {
            await Task.CompletedTask;
            return null;
        }
    }
}
