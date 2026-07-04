using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WindowSpot.Models;
using WindowSpot.Providers;

namespace WindowSpot.Services;

/// <summary>
/// 등록된 모든 ISearchProvider를 병렬로 실행하고, 결과를 Score 기준으로 합쳐
/// 상위 N개만 반환한다. 개별 provider의 예외는 검색 전체를 막지 않도록 격리한다.
/// </summary>
public class SearchEngine
{
    private const int MaxResults = 9;
    private readonly List<ISearchProvider> _providers;

    public SearchEngine(IEnumerable<ISearchProvider> providers)
    {
        _providers = providers.ToList();
    }

    public async Task<List<SearchResult>> SearchAsync(string query, CancellationToken token)
    {
        var tasks = _providers
            .Where(p => p.CanHandle(query))
            .Select(p => SafeSearchAsync(p, query, token));

        var resultGroups = await Task.WhenAll(tasks);
        token.ThrowIfCancellationRequested();

        return resultGroups
            .SelectMany(r => r)
            .OrderByDescending(r => r.Score)
            .Take(MaxResults)
            .ToList();
    }

    private static async Task<IEnumerable<SearchResult>> SafeSearchAsync(ISearchProvider provider, string query, CancellationToken token)
    {
        try
        {
            return await provider.SearchAsync(query, token);
        }
        catch (System.OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return System.Linq.Enumerable.Empty<SearchResult>();
        }
    }
}
