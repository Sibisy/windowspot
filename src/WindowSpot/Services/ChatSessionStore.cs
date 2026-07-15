using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using WindowSpot.Models;

namespace WindowSpot.Services;

/// <summary>
/// 마지막 AI 채팅 세션을 %AppData%\WindowSpot\chat_session.json에 저장 ("/resume"용).
/// 안드로이드판 ChatSessionRepository.kt 대응.
/// </summary>
public class ChatSessionStore
{
    private record StoredMessage(string Text, bool IsUser);

    private readonly string _path = AppDataPaths.GetPath("chat_session.json");

    public bool HasSavedSession() => File.Exists(_path);

    public List<ChatMessageView> LoadLast()
    {
        try
        {
            if (!File.Exists(_path)) return new List<ChatMessageView>();
            string json = File.ReadAllText(_path);
            var stored = JsonSerializer.Deserialize<List<StoredMessage>>(json) ?? new List<StoredMessage>();
            return stored.Select(m => new ChatMessageView { Text = m.Text, IsUser = m.IsUser }).ToList();
        }
        catch
        {
            return new List<ChatMessageView>();
        }
    }

    public void Save(IReadOnlyList<ChatMessageView> messages)
    {
        try
        {
            var stored = messages.Select(m => new StoredMessage(m.Text, m.IsUser)).ToList();
            File.WriteAllText(_path, JsonSerializer.Serialize(stored));
        }
        catch
        {
            // 저장 실패는 조용히 무시
        }
    }
}
