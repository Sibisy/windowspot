using System;
using System.IO;

namespace WindowSpot.Services;

/// <summary>
/// 예외를 %AppData%\WindowSpot\crash.log에 남긴다. MessageBox가 어떤 이유로든 뜨지 않는
/// 경우에도(예: 특수한 세션/보안 컨텍스트) 원인을 확인할 수 있는 최후의 안전장치.
/// </summary>
public static class CrashLogger
{
    public static void Log(string context, object error)
    {
        try
        {
            string path = AppDataPaths.GetPath("crash.log");
            string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}\n{error}\n\n";
            File.AppendAllText(path, entry);
        }
        catch
        {
            // 로그 기록 자체가 실패해도 원래 예외 처리 흐름을 막지 않는다.
        }
    }
}
