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

                foreach (var lnk in SafeEnumerateLnkFiles(root))
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

    /// <summary>
    /// Directory.EnumerateFiles(..., SearchOption.AllDirectories)는 지연 실행이라, 트리 중간의
    /// 권한 없는 하위 폴더를 만나면 그 시점에 순회하다가 예외가 새어나간다(호출 시점에 감싸는
    /// try/catch로는 못 막음). 폴더 단위로 직접 재귀하며 각 폴더에서 나는 예외를 그 자리에서
    /// 잡아 건너뛰고, 나머지 형제/하위 폴더는 계속 스캔한다.
    /// </summary>
    private static List<string> SafeEnumerateLnkFiles(string root)
    {
        var result = new List<string>();
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            string dir = pending.Pop();
            List<string> subDirs;
            List<string> files;

            try
            {
                subDirs = Directory.EnumerateDirectories(dir).ToList();
                files = Directory.EnumerateFiles(dir, "*.lnk").ToList();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                continue;
            }

            foreach (var sub in subDirs) pending.Push(sub);
            result.AddRange(files);
        }

        return result;
    }
}
