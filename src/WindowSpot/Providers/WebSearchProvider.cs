using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WindowSpot.Models;

namespace WindowSpot.Providers;

/// <summary>
/// 다른 provider가 마땅한 결과를 못 찾았을 때를 대비한 폴백.
/// 항상 목록 맨 아래에 "네이버에서 검색" 항목을 하나 추가한다
/// (안드로이드판 최신 커밋에서 기본 검색엔진을 네이버로 바꾼 것에 대응).
/// </summary>
public class WebSearchProvider : ISearchProvider
{
    public string Name => "Web";

    public bool CanHandle(string query) => !string.IsNullOrWhiteSpace(query);

    public Task<IEnumerable<SearchResult>> SearchAsync(string query, CancellationToken token)
    {
        string trimmed = query.Trim();
        var result = new SearchResult
        {
            Title = $"\"{trimmed}\" 네이버에서 검색",
            Subtitle = "기본 브라우저로 검색합니다",
            Type = ResultType.WebSearch,
            Score = 1,
            Execute = () => Process.Start(new ProcessStartInfo(
                $"https://search.naver.com/search.naver?query={Uri.EscapeDataString(trimmed)}")
            {
                UseShellExecute = true,
            }),
        };

        return Task.FromResult<IEnumerable<SearchResult>>(new[] { result });
    }
}
