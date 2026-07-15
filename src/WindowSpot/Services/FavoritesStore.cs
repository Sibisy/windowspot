using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace WindowSpot.Services;

/// <summary>
/// 즐겨찾기 목록(앱 또는 URL)을 %AppData%\WindowSpot\favorites.json에 저장.
/// 키 형식은 "app:&lt;경로&gt;" 또는 "url:&lt;도메인&gt;". 안드로이드판 FavoritesRepository.kt 대응.
/// </summary>
public class FavoritesStore
{
    private readonly string _path = AppDataPaths.GetPath("favorites.json");
    private List<string> _keys;

    public FavoritesStore()
    {
        _keys = Load();
    }

    public static string AppKey(string appPath) => $"app:{appPath}";
    public static string UrlKey(string domain) => $"url:{domain}";

    private List<string> Load()
    {
        try
        {
            if (!File.Exists(_path)) return new List<string>();
            string json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(_keys));
        }
        catch
        {
            // 저장 실패는 조용히 무시
        }
    }

    public IReadOnlyList<string> Keys => _keys;

    public void Add(string key)
    {
        if (_keys.Contains(key)) return;
        _keys.Add(key);
        Save();
    }

    public void Remove(string key)
    {
        if (_keys.Remove(key)) Save();
    }
}
