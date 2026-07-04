using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WindowSpot.Services;

/// <summary>
/// 파일/폴더 경로로부터 셸 아이콘을 추출해 WPF ImageSource로 변환한다.
/// SHGetFileInfo를 쓰는 이유는 파일과 폴더 양쪽 모두에서 동작하기 때문
/// (System.Drawing.Icon.ExtractAssociatedIcon은 폴더 경로에서는 실패한다).
/// 같은 경로는 캐시해서 반복 추출 비용을 피한다.
/// </summary>
public static class IconExtractor
{
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0;

    [StructLayout(LayoutKind.Sequential)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static ImageSource? GetIcon(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (Cache.TryGetValue(path, out var cached)) return cached;

        ImageSource? result = null;
        var info = new SHFILEINFO();
        IntPtr handle = IntPtr.Zero;

        try
        {
            handle = SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON);
            if (handle != IntPtr.Zero && info.hIcon != IntPtr.Zero)
            {
                var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                    info.hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                bitmapSource.Freeze();
                result = bitmapSource;
            }
        }
        catch
        {
            result = null;
        }
        finally
        {
            if (info.hIcon != IntPtr.Zero) DestroyIcon(info.hIcon);
        }

        Cache[path] = result;
        return result;
    }
}
