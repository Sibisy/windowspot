using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace WindowSpot.Services;

/// <summary>
/// RegisterHotKey Win32 API로 macOS의 Cmd+Space에 대응하는 전역 단축키를 등록한다.
/// 앱이 백그라운드/트레이에 있어도 시스템 전역에서 동작한다.
/// </summary>
public class HotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HotkeyId = 0x2A5F; // 임의의 고유 ID

    private readonly HwndSource _source;
    private bool _registered;

    public event Action? HotkeyPressed;

    public HotkeyManager(Window window, ModifierKeys modifiers, Key key)
    {
        var helper = new WindowInteropHelper(window);
        IntPtr hwnd = helper.EnsureHandle(); // Show() 없이 강제로 HWND 생성

        _source = HwndSource.FromHwnd(hwnd)
            ?? throw new InvalidOperationException("윈도우 핸들에서 HwndSource를 가져올 수 없습니다.");
        _source.AddHook(HwndHook);

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        _registered = NativeMethods.RegisterHotKey(hwnd, HotkeyId, (uint)modifiers, vk);
        if (!_registered)
        {
            throw new InvalidOperationException(
                "단축키 등록에 실패했습니다. 다른 프로그램이 이미 이 조합을 사용 중일 수 있습니다.");
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_registered)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
            _registered = false;
        }
        _source.RemoveHook(HwndHook);
    }
}
