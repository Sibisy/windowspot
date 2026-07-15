using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Threading.Timer;

namespace WindowSpot.Services;

public class AppEntry
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public string Arguments { get; init; } = string.Empty;
}

/// <summary>
/// Windows에는 macOS LaunchServices 같은 "설치된 앱" 데이터베이스가 없으므로,
/// 시작 메뉴(전체 사용자 + 현재 사용자)의 .lnk 바로가기를 스캔해 자체 인덱스를 만든다.
/// FileSystemWatcher로 변경을 감지해 재스캔한다 (Spotlight의 실시간 인덱스 갱신에 대응).
/// </summary>
public class AppIndexer : IDisposable
{
    private static readonly string[] ScanRoots =
    {
        Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
        Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
    };

    private readonly object _lock = new();
    private readonly List<FileSystemWatcher> _watchers = new();
    private List<AppEntry> _apps = new();
    private Timer? _debounceTimer;

    public IReadOnlyList<AppEntry> Apps
    {
        get { lock (_lock) return _apps; }
    }

    public async Task InitializeAsync()
    {
        await RebuildAsync();
        StartWatchers();
    }

    private void StartWatchers()
    {
        foreach (var root in ScanRoots.Distinct())
        {
            if (!Directory.Exists(root)) continue;

            try
            {
                var watcher = new FileSystemWatcher(root)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                };
                watcher.Created += (_, _) => ScheduleRebuild();
                watcher.Deleted += (_, _) => ScheduleRebuild();
                watcher.Renamed += (_, _) => ScheduleRebuild();
                watcher.EnableRaisingEvents = true;

                _watchers.Add(watcher);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                // 이 폴더에 대한 실시간 감시 권한이 없는 환경(보안 소프트웨어의 사전 실행
                // 검사 등)에서는 그냥 그 폴더의 실시간 갱신만 포기하고 계속 진행한다.
                // 최초 스캔(RebuildAsync)은 이미 끝난 뒤라 앱 목록 자체는 정상 동작한다.
            }
        }
    }

    private void ScheduleRebuild()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(async _ => await RebuildAsync(), null, 2000, Timeout.Infinite);
    }

    private async Task RebuildAsync()
    {
        var found = await Task.Run(() =>
        {
            var list = new List<AppEntry>();
            foreach (var root in ScanRoots.Distinct())
            {
                if (!Directory.Exists(root)) continue;

                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories);
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (var lnk in files)
                {
                    var resolved = ShortcutResolver.Resolve(lnk);
                    if (resolved is null) continue;

                    var (target, args) = resolved.Value;
                    if (string.IsNullOrWhiteSpace(target) || !File.Exists(target)) continue;

                    list.Add(new AppEntry
                    {
                        Name = System.IO.Path.GetFileNameWithoutExtension(lnk),
                        Path = target,
                        Arguments = args,
                    });
                }
            }
            return list;
        });

        lock (_lock)
        {
            _apps = found
                .GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }
    }

    public void Dispose()
    {
        foreach (var watcher in _watchers) watcher.Dispose();
        _debounceTimer?.Dispose();
    }
}
