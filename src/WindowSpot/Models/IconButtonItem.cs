using System;
using System.Windows;
using System.Windows.Media;

namespace WindowSpot.Models;

/// <summary>즐겨찾기/자주 쓰는 앱 행에 쓰이는 아이콘 버튼 한 칸.</summary>
public class IconButtonItem
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public ImageSource? Icon { get; init; }
    public required Action OnClick { get; init; }

    public Visibility IconVisibility => Icon is not null ? Visibility.Visible : Visibility.Collapsed;
    public Visibility FallbackVisibility => Icon is null ? Visibility.Visible : Visibility.Collapsed;
    public string FallbackLetter => Label.Length > 0 ? Label[..1].ToUpperInvariant() : "?";
}
