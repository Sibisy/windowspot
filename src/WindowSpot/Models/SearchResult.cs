using System;
using System.Windows.Media;

namespace WindowSpot.Models;

public enum ResultType
{
    Application,
    File,
    Folder,
    Calculator,
    WebSearch
}

/// <summary>
/// 검색 결과 한 항목. 모든 Provider가 이 공통 타입으로 결과를 반환하고
/// SearchEngine이 Score 기준으로 병합/정렬한다.
/// </summary>
public class SearchResult
{
    public required string Title { get; init; }
    public string Subtitle { get; init; } = string.Empty;
    public ResultType Type { get; init; }
    public ImageSource? Icon { get; init; }

    /// <summary>높을수록 목록 상단에 표시됨.</summary>
    public int Score { get; init; }

    /// <summary>Enter 키로 실행되는 기본 동작.</summary>
    public Action? Execute { get; init; }

    /// <summary>Ctrl+Enter 등으로 실행되는 보조 동작 (예: 파일 위치 열기). 없으면 null.</summary>
    public Action? OpenContainingFolder { get; init; }
}
