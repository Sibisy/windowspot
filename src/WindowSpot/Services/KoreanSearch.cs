namespace WindowSpot.Services;

/// <summary>
/// 한글 초성/자모 검색. 안드로이드판 util/KoreanSearch.kt 포팅.
/// "카카오톡"을 "ㅋㅋㅇㅌ"(초성) 또는 "카카"(부분 문자열)로도 찾을 수 있게 한다.
/// </summary>
public static class KoreanSearch
{
    private static readonly char[] Chosung =
    {
        'ㄱ', 'ㄲ', 'ㄴ', 'ㄷ', 'ㄸ', 'ㄹ', 'ㅁ', 'ㅂ', 'ㅃ', 'ㅅ', 'ㅆ', 'ㅇ', 'ㅈ', 'ㅉ', 'ㅊ', 'ㅋ', 'ㅌ', 'ㅍ', 'ㅎ',
    };

    private static readonly char[] Jungsung =
    {
        'ㅏ', 'ㅐ', 'ㅑ', 'ㅒ', 'ㅓ', 'ㅔ', 'ㅕ', 'ㅖ', 'ㅗ', 'ㅘ', 'ㅙ', 'ㅚ', 'ㅛ', 'ㅜ', 'ㅝ', 'ㅞ', 'ㅟ', 'ㅠ', 'ㅡ', 'ㅢ', 'ㅣ',
    };

    private static readonly char[] Jongsung =
    {
        ' ', 'ㄱ', 'ㄲ', 'ㄳ', 'ㄴ', 'ㄵ', 'ㄶ', 'ㄷ', 'ㄹ', 'ㄺ', 'ㄻ', 'ㄼ', 'ㄽ', 'ㄾ', 'ㄿ', 'ㅀ', 'ㅁ', 'ㅂ', 'ㅄ', 'ㅅ', 'ㅆ', 'ㅇ', 'ㅈ', 'ㅊ', 'ㅋ', 'ㅌ', 'ㅍ', 'ㅎ',
    };

    private static bool IsKoreanSyllable(char ch) => ch is >= '가' and <= '힣';
    private static bool IsConsonantJamo(char ch) => ch is >= 'ㄱ' and <= 'ㅎ';
    private static bool IsVowelJamo(char ch) => ch is >= 'ㅏ' and <= 'ㅣ';
    private static bool IsKorean(char ch) => IsKoreanSyllable(ch) || IsConsonantJamo(ch) || IsVowelJamo(ch);

    /// <summary>음절 문자열을 자모 시퀀스로 분해. 예: "가다" → "ㄱㅏㄷㅏ"</summary>
    private static string Decompose(string text)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char ch in text)
        {
            if (IsKoreanSyllable(ch))
            {
                int code = ch - '가';
                int jong = code % 28;
                int jung = (code / 28) % 21;
                int cho = code / 28 / 21;
                sb.Append(Chosung[cho]);
                sb.Append(Jungsung[jung]);
                if (jong != 0) sb.Append(Jongsung[jong]);
            }
            else
            {
                sb.Append(ch);
            }
        }
        return sb.ToString();
    }

    /// <summary>각 음절의 초성만 추출. 예: "가나다" → "ㄱㄴㄷ"</summary>
    private static string ExtractChosung(string text)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char ch in text)
        {
            if (IsKoreanSyllable(ch))
            {
                int cho = (ch - '가') / 28 / 21;
                sb.Append(Chosung[cho]);
            }
            else if (IsConsonantJamo(ch))
            {
                sb.Append(ch);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// query가 target에 매칭되는지 확인.
    /// - 일반 텍스트: contains (대소문자 무시)
    /// - 순수 초성(ㄱㄷ 등): target의 초성 시퀀스에 포함되는지 검사
    /// - 자모 분리(갇 → ㄱㅏㄷ): 분해된 자모 시퀀스에 포함되는지 검사
    /// </summary>
    public static bool Matches(string target, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return false;
        if (target.Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;

        bool hasKorean = false;
        foreach (char c in query)
        {
            if (IsKorean(c)) { hasKorean = true; break; }
        }
        if (!hasKorean) return false;

        var koreanChars = new System.Text.StringBuilder();
        foreach (char c in query)
        {
            if (IsKorean(c)) koreanChars.Append(c);
        }
        string koreanQuery = koreanChars.ToString();

        bool allConsonants = true;
        foreach (char c in koreanQuery)
        {
            if (!IsConsonantJamo(c)) { allConsonants = false; break; }
        }

        return allConsonants
            ? ExtractChosung(target).Contains(koreanQuery, System.StringComparison.Ordinal)
            : Decompose(target).Contains(Decompose(query), System.StringComparison.OrdinalIgnoreCase);
    }
}
