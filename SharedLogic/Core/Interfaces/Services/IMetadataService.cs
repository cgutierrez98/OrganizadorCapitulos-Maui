using System.Threading.Tasks;
using organizadorCapitulos.Core.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace organizadorCapitulos.Core.Interfaces.Services
{
    public interface IMetadataService
    {
        void Configure(string apiKey);
        bool IsConfigured();
        Task<List<SeriesSearchResult>> SearchSeriesAsync(string query);
        Task<string?> GetEpisodeTitleAsync(int seriesId, int season, int episode);
        Task<(int season, int episode, string title)?> FindEpisodeByTitleAsync(int seriesId, string title);
        Task<ChapterInfo?> GetEpisodeMetadataAsync(int seriesId, int season, int episode);
    }
}
