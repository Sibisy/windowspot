using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WindowSpot.Models;

namespace WindowSpot.Providers;

/// <summary>
/// 검색 결과의 한 종류(앱, 파일, 계산기, 웹검색 등)를 담당하는 플러그인 인터페이스.
/// SearchEngine이 CanHandle을 통과한 provider들을 병렬로 호출해 결과를 합친다.
/// </summary>
public interface ISearchProvider
{
    string Name { get; }

    /// <summary>이 query에 대해 검색을 시도할지 여부를 빠르게 판단 (불필요한 작업 방지).</summary>
    bool CanHandle(string query);

    Task<IEnumerable<SearchResult>> SearchAsync(string query, CancellationToken token);
}
