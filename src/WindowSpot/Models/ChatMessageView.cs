using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace WindowSpot.Models;

/// <summary>AI 채팅 말풍선 한 개. 정렬/배경색을 미리 계산해 XAML 바인딩만으로 렌더링한다.</summary>
public class ChatMessageView
{
    private static readonly Brush UserBrush = Freeze(Color.FromArgb(217, 90, 158, 255));
    private static readonly Brush AssistantBrush = Freeze(Color.FromArgb(26, 255, 255, 255));

    public required string Text { get; init; }
    public required bool IsUser { get; init; }

    public HorizontalAlignment Alignment => IsUser ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    public Brush BubbleBrush => IsUser ? UserBrush : AssistantBrush;

    private static Brush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
