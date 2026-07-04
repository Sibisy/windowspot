using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WindowSpot.Models;
using WindowSpot.Services;

namespace WindowSpot.Providers;

/// <summary>
/// AppIndexer가 스캔한 시작 메뉴 앱 목록에서 퍼지 매칭으로 검색한다.
/// Spotlight에서 앱 이름을 치면 바로 실행 후보로 뜨는 동작에 대응.
/// </summary>
public class AppSearchProvider : ISearchProvider
{
    private const int PriorityWeight = 500;
    private readonly AppIndexer _indexer;

    public AppSearchProvider(AppIndexer indexer)
    {
        _indexer = indexer;
    }

    public string Name => "Applications";

    public bool CanHandle(string query) => !string.IsNullOrWhiteSpace(query);

    public Task<IEnumerable<SearchResult>> SearchAsync(string query, CancellationToken token)
    {
        var results = new List<SearchResult>();

        foreach (var app in _indexer.Apps)
        {
            token.ThrowIfCancellationRequested();
            if (!FuzzyMatcher.TryMatch(app.Name, query, out int score)) continue;

            string path = app.Path;
            string arguments = app.Arguments;

            results.Add(new SearchResult
            {
                Title = app.Name,
                Subtitle = path,
                Type = ResultType.Application,
                Icon = IconExtractor.GetIcon(path),
                Score = score + PriorityWeight,
                Execute = () => Process.Start(new ProcessStartInfo(path)
                {
                    Arguments = arguments,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(path),
                }),
            });
        }

        return Task.FromResult<IEnumerable<SearchResult>>(results);
    }
}
