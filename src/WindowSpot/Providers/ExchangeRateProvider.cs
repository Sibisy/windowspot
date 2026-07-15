using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WindowSpot.Models;
using Clipboard = System.Windows.Clipboard;

namespace WindowSpot.Providers;

/// <summary>
/// "10usd", "10 usd", "$10" 같은 입력을 원화(KRW)로 실시간 환율 변환한다.
/// frankfurter.app의 키 없는 공개 API를 사용. 안드로이드판 QuickAnswerRepository.kt 대응.
/// </summary>
public class ExchangeRateProvider : ISearchProvider
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    private static readonly Regex CurrencyPattern =
        new(@"^([0-9]+(?:\.[0-9]+)?)\s*([a-zA-Z]{3})$", RegexOptions.Compiled);

    private static readonly Regex DollarPattern =
        new(@"^\$([0-9]+(?:\.[0-9]+)?)$", RegexOptions.Compiled);

    public string Name => "ExchangeRate";

    public bool CanHandle(string query)
    {
        string trimmed = query.Trim();
        return CurrencyPattern.IsMatch(trimmed) || DollarPattern.IsMatch(trimmed);
    }

    public async Task<IEnumerable<SearchResult>> SearchAsync(string query, CancellationToken token)
    {
        string trimmed = query.Trim();
        var dollarMatch = DollarPattern.Match(trimmed);
        var currencyMatch = CurrencyPattern.Match(trimmed);

        double amount;
        string fromCode;
        if (dollarMatch.Success)
        {
            amount = double.Parse(dollarMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            fromCode = "USD";
        }
        else
        {
            amount = double.Parse(currencyMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            fromCode = currencyMatch.Groups[2].Value.ToUpperInvariant();
        }

        try
        {
            string url = $"https://api.frankfurter.app/latest?amount={amount.ToString(CultureInfo.InvariantCulture)}&from={fromCode}&to=KRW";
            using var response = await Http.GetAsync(url, token);
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync(token);

            using var doc = JsonDocument.Parse(body);
            double krw = doc.RootElement.GetProperty("rates").GetProperty("KRW").GetDouble();
            string date = doc.RootElement.GetProperty("date").GetString() ?? string.Empty;

            string formatted = FormatNumber(krw);
            string asOf = FormatDate(date);

            var result = new SearchResult
            {
                Title = $"₩{formatted}",
                Subtitle = $"{FormatNumber(amount)} {fromCode} ({asOf}, Enter를 눌러 결과 복사)",
                Type = ResultType.Calculator,
                Score = 10_000,
                Execute = () => Clipboard.SetText(formatted),
            };
            return new[] { result };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new[]
            {
                new SearchResult
                {
                    Title = "환율 조회 실패",
                    Subtitle = trimmed,
                    Type = ResultType.Calculator,
                    Score = 10_000,
                },
            };
        }
    }

    private static string FormatDate(string isoDate)
    {
        var parts = isoDate.Split('-');
        if (parts.Length != 3) return isoDate;
        return $"{int.Parse(parts[1])}/{int.Parse(parts[2])} 기준";
    }

    private static string FormatNumber(double value)
    {
        return value == Math.Truncate(value)
            ? ((long)value).ToString("N0", CultureInfo.InvariantCulture)
            : value.ToString("N2", CultureInfo.InvariantCulture);
    }
}
