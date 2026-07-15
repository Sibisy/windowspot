using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WindowSpot.Models;
using WindowSpot.Services;

namespace WindowSpot.Providers;

/// <summary>
/// AppIndexer가 스캔한 시작 메뉴 앱 목록에서 퍼지 매칭(+ 한글 초성 검색)으로 검색한다.
/// Spotlight에서 앱 이름을 치면 바로 실행 후보로 뜨는 동작에 대응.
/// 실행할 때마다 UsageStore에 기록해 자주 쓰는 앱이 위로 올라오게 한다.
/// </summary>
public class AppSearchProvider : ISearchProvider
{
    private const int PriorityWeight = 500;
    private const int KoreanMatchScore = 40;
    private const int UsageWeightPerLaunch = 30;
    private readonly AppIndexer _indexer;
    private readonly UsageStore _usageStore;

    public AppSearchProvider(AppIndexer indexer, UsageStore usageStore)
    {
        _indexer = indexer;
        _usageStore = usageStore;
    }

    public string Name => "Applications";

    public bool CanHandle(string query) => !string.IsNullOrWhiteSpace(query);

    public Task<IEnumerable<SearchResult>> SearchAsync(string query, CancellationToken token)
    {
        var results = new List<SearchResult>();

        foreach (var app in _indexer.Apps)
        {
            token.ThrowIfCancellationRequested();

            bool fuzzyMatched = FuzzyMatcher.TryMatch(app.Name, query, out int score);
            bool koreanMatched = !fuzzyMatched && KoreanSearch.Matches(app.Name, query);
            if (!fuzzyMatched && !koreanMatched) continue;
            if (koreanMatched) score = KoreanMatchScore;

            string path = app.Path;
            string arguments = app.Arguments;
            int usageBoost = _usageStore.GetCount(path) * UsageWeightPerLaunch;

            results.Add(new SearchResult
            {
                Title = app.Name,
                Subtitle = path,
                Type = ResultType.Application,
                Icon = IconExtractor.GetIcon(path),
                Score = score + PriorityWeight + usageBoost,
                Execute = () =>
                {
                    _usageStore.Increment(path);
                    Process.Start(new ProcessStartInfo(path)
                    {
                        Arguments = arguments,
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(path),
                    });
                },
            });
        }

        return Task.FromResult<IEnumerable<SearchResult>>(results);
    }
}
