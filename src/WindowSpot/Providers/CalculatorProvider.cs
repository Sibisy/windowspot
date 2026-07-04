using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WindowSpot.Models;
using WindowSpot.Services;

namespace WindowSpot.Providers;

/// <summary>
/// "12*4+3" 같은 산술식을 즉시 계산해 최상단에 보여준다.
/// Spotlight의 내장 계산기 기능에 대응.
/// </summary>
public class CalculatorProvider : ISearchProvider
{
    public string Name => "Calculator";

    private static readonly Regex MathPattern = new(@"^[\d\s\.\+\-\*/\(\)\^%]+$", RegexOptions.Compiled);

    public bool CanHandle(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return false;
        if (!MathPattern.IsMatch(query)) return false;
        return query.Any(char.IsDigit) && query.Any(c => "+-*/^%".Contains(c));
    }

    public Task<IEnumerable<SearchResult>> SearchAsync(string query, CancellationToken token)
    {
        var results = new List<SearchResult>();
        try
        {
            double value = ExpressionEvaluator.Evaluate(query);
            string formatted = value.ToString("G15", CultureInfo.InvariantCulture);

            results.Add(new SearchResult
            {
                Title = formatted,
                Subtitle = $"{query.Trim()} =  (Enter를 눌러 결과 복사)",
                Type = ResultType.Calculator,
                Score = 10_000,
                Execute = () => Clipboard.SetText(formatted),
            });
        }
        catch
        {
            // 완성되지 않은 식(예: "12*")은 조용히 무시
        }

        return Task.FromResult<IEnumerable<SearchResult>>(results);
    }
}
