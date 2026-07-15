using System;
using System.IO;

namespace WindowSpot.Services;

/// <summary>WindowSpot의 로컬 설정/상태 파일이 저장되는 폴더. 기본은 %AppData%\WindowSpot.</summary>
public static class AppDataPaths
{
    public static string RootDirectory { get; } = ResolveRootDirectory();

    private static string ResolveRootDirectory()
    {
        string preferred = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowSpot");
        try
        {
            Directory.CreateDirectory(preferred);
            return preferred;
        }
        catch (Exception)
        {
            // %AppData%\WindowSpot을 만들 수 없는 환경(권한 제한, 보안 소프트웨어의 사전 실행
            // 검사 등)에서는 임시 폴더로 대체해 앱이 크래시하지 않고 계속 동작하게 한다.
            // (CrashLogger도 이 경로를 쓰므로 여기서는 직접 호출하지 않는다.)
            string fallback = Path.Combine(Path.GetTempPath(), "WindowSpot");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }

    public static string GetPath(string fileName)
    {
        return Path.Combine(RootDirectory, fileName);
    }
}
