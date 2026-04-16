using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using organizadorCapitulos.Core.Entities;

namespace organizadorCapitulos.Core.Interfaces.Services
{
    public interface IMetadataService
    {
        void Configure(string apiKey);
        bool IsConfigured();
        Task<List<SeriesSearchResult>> SearchSeriesAsync(string query, CancellationToken ct = default);
        Task<string?> GetEpisodeTitleAsync(int seriesId, int season, int episode, CancellationToken ct = default);
        Task<(int season, int episode, string title)?> FindEpisodeByTitleAsync(int seriesId, string title, CancellationToken ct = default);
        Task<ChapterInfo?> GetEpisodeMetadataAsync(int seriesId, int season, int episode, CancellationToken ct = default);
    }
}
