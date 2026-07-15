using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WindowSpot.Models;

namespace WindowSpot.Providers;

/// <summary>
/// 입력이 그 자체로 URL/도메인처럼 보이면 바로 "열기" 결과를 최상단 근처에 추가한다.
/// 안드로이드판에서 Patterns.WEB_URL 매치 시 바로 브라우저를 여는 동작에 대응.
/// </summary>
public class UrlProvider : ISearchProvider
{
    /// <summary>앱 목록에 없어도 Tab 자동완성 후보로 쓸 수 있는 흔한 도메인 목록.</summary>
    public static readonly string[] CommonDomains =
    {
        "google.com", "youtube.com", "naver.com", "daum.net", "gmail.com",
        "github.com", "facebook.com", "instagram.com", "x.com", "wikipedia.org",
        "amazon.com", "netflix.com", "chatgpt.com", "kakao.com",
    };

    private static readonly Regex UrlPattern = new(
        @"^(https?:\/\/)?([\w-]+\.)+[a-zA-Z]{2,}(\/\S*)?$",
        RegexOptions.Compiled);

    public string Name => "Url";

    public bool CanHandle(string query)
    {
        string trimmed = query.Trim();
        return !string.IsNullOrEmpty(trimmed) && !trimmed.Contains(' ') && UrlPattern.IsMatch(trimmed);
    }

    public Task<IEnumerable<SearchResult>> SearchAsync(string query, CancellationToken token)
    {
        string trimmed = query.Trim();
        string target = trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"https://{trimmed}";

        var result = new SearchResult
        {
            Title = trimmed,
            Subtitle = "이 주소 열기",
            Type = ResultType.WebSearch,
            Score = 5_000,
            Execute = () => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }),
        };

        return Task.FromResult<IEnumerable<SearchResult>>(new[] { result });
    }
}
