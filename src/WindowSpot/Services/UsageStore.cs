using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace WindowSpot.Services;

/// <summary>
/// 앱 실행 횟수를 %AppData%\WindowSpot\usage.json에 저장해 "자주 쓰는 앱" 랭킹에 쓴다.
/// 안드로이드판 UsageRepository.kt 대응.
/// </summary>
public class UsageStore
{
    private readonly string _path = AppDataPaths.GetPath("usage.json");
    private Dictionary<string, int> _counts;

    public UsageStore()
    {
        _counts = Load();
    }

    private Dictionary<string, int> Load()
    {
        try
        {
            if (!File.Exists(_path)) return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            string json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<Dictionary<string, int>>(json)
                   ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(_counts));
        }
        catch
        {
            // 저장 실패는 조용히 무시 (랭킹은 부가 기능일 뿐)
        }
    }

    public void Increment(string appPath)
    {
        _counts.TryGetValue(appPath, out int current);
        _counts[appPath] = current + 1;
        Save();
    }

    public int GetCount(string appPath) => _counts.GetValueOrDefault(appPath, 0);

    public List<AppEntry> GetTopApps(int count, IReadOnlyList<AppEntry> allApps)
    {
        return allApps
            .Where(a => GetCount(a.Path) > 0)
            .OrderByDescending(a => GetCount(a.Path))
            .Take(count)
            .ToList();
    }
}
