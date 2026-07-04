namespace WindowSpot.Services;

/// <summary>
/// VSCode/Sublime 스타일의 서브시퀀스 퍼지 매칭.
/// 패턴의 모든 문자가 순서대로 text 안에 등장하면 매치로 보고,
/// 연속 매치·단어 경계·문자열 시작 위치에 가중치를 주어 점수를 매긴다.
/// </summary>
public static class FuzzyMatcher
{
    public static bool TryMatch(string text, string pattern, out int score)
    {
        score = 0;
        if (string.IsNullOrEmpty(pattern)) return true;
        if (string.IsNullOrEmpty(text)) return false;

        int textIdx = 0, patternIdx = 0;
        int consecutive = 0;
        bool prevMatched = false;
        int total = 0;

        while (textIdx < text.Length && patternIdx < pattern.Length)
        {
            char tc = char.ToLowerInvariant(text[textIdx]);
            char pc = char.ToLowerInvariant(pattern[patternIdx]);

            if (tc == pc)
            {
                int charScore = 10;
                if (textIdx == 0) charScore += 15;
                if (IsWordBoundary(text, textIdx)) charScore += 10;
                if (prevMatched)
                {
                    consecutive++;
                    charScore += consecutive * 5;
                }
                else
                {
                    consecutive = 0;
                }

                total += charScore;
                prevMatched = true;
                patternIdx++;
            }
            else
            {
                prevMatched = false;
                consecutive = 0;
            }

            textIdx++;
        }

        if (patternIdx < pattern.Length) return false;

        // 패턴에 비해 텍스트가 너무 길면 (매치와 무관한 글자가 많으면) 약간 감점
        total -= (text.Length - pattern.Length) / 4;

        score = total;
        return true;
    }

    private static bool IsWordBoundary(string text, int index)
    {
        if (index == 0) return true;
        char prev = text[index - 1];
        char curr = text[index];
        if (prev is ' ' or '-' or '_' or '.') return true;
        if (char.IsLower(prev) && char.IsUpper(curr)) return true;
        return false;
    }
}
