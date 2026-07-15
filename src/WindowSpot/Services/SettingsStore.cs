using System;
using System.IO;
using System.Text.Json;

namespace WindowSpot.Services;

public class AppSettings
{
    public string OpenRouterApiKey { get; set; } = string.Empty;
    public string OpenRouterModel { get; set; } = string.Empty;
}

/// <summary>
/// OpenRouter API 키/모델 같은 사용자 설정을 %AppData%\WindowSpot\settings.json에 저장.
/// 안드로이드판 SettingsRepository.kt 대응.
/// </summary>
public class SettingsStore
{
    private readonly string _path = AppDataPaths.GetPath("settings.json");
    private AppSettings _settings;

    public SettingsStore()
    {
        _settings = Load();
    }

    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return new AppSettings();
            string json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(_settings));
        }
        catch
        {
            // 저장 실패는 조용히 무시
        }
    }

    public string GetOpenRouterApiKey() => _settings.OpenRouterApiKey;
    public string GetOpenRouterModel() => _settings.OpenRouterModel;

    public void SetOpenRouterCredentials(string apiKey, string model)
    {
        _settings.OpenRouterApiKey = apiKey.Trim();
        _settings.OpenRouterModel = model.Trim();
        Save();
    }
}
