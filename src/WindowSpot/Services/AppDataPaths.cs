using System;
using System.IO;

namespace WindowSpot.Services;

/// <summary>WindowSpot의 로컬 설정/상태 파일이 저장되는 %AppData%\WindowSpot 폴더.</summary>
public static class AppDataPaths
{
    public static string RootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowSpot");

    public static string GetPath(string fileName)
    {
        Directory.CreateDirectory(RootDirectory);
        return Path.Combine(RootDirectory, fileName);
    }
}
