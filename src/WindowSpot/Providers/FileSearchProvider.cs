using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WindowSpot.Models;
using WindowSpot.Services;

namespace WindowSpot.Providers;

/// <summary>
/// Windows Search 인덱서(Windows Search 서비스)를 OLE DB로 질의해 파일/폴더를 검색한다.
/// macOS의 mdworker/mdimporter가 만드는 메타데이터 인덱스를 Spotlight가 조회하는 것과
/// 같은 위치에 대응 — 자체 인덱서를 새로 만들지 않고 OS가 이미 유지 중인 인덱스를 재사용한다.
/// </summary>
public class FileSearchProvider : ISearchProvider
{
    private const int PriorityWeight = 300;
    private const string ConnectionString = "Provider=Search.CollatorDSO;Extended Properties=\"Application=Windows\"";

    public string Name => "Files";

    public bool CanHandle(string query) => !string.IsNullOrWhiteSpace(query) && query.Trim().Length >= 2;

    public async Task<IEnumerable<SearchResult>> SearchAsync(string query, CancellationToken token)
    {
        var results = new List<SearchResult>();
        string escaped = query.Replace("'", "''");
        string sql =
            "SELECT TOP 15 System.ItemUrl, System.ItemNameDisplay " +
            "FROM SystemIndex " +
            $"WHERE CONTAINS(System.FileName, '\"{escaped}*\"') " +
            "ORDER BY System.DateModified DESC";

        try
        {
            await using var connection = new OleDbConnection(ConnectionString);
            await connection.OpenAsync(token);

            await using var command = new OleDbCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(token);

            while (await reader.ReadAsync(token))
            {
                string url = reader["System.ItemUrl"]?.ToString() ?? string.Empty;
                string name = reader["System.ItemNameDisplay"]?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(url)) continue;

                string localPath;
                try
                {
                    localPath = new Uri(url).LocalPath;
                }
                catch (UriFormatException)
                {
                    continue;
                }

                if (!FuzzyMatcher.TryMatch(name, query, out int score)) score = 5;

                bool isFolder = Directory.Exists(localPath);
                string capturedPath = localPath;

                results.Add(new SearchResult
                {
                    Title = name,
                    Subtitle = localPath,
                    Type = isFolder ? ResultType.Folder : ResultType.File,
                    Icon = IconExtractor.GetIcon(localPath),
                    Score = score + PriorityWeight,
                    Execute = () => Process.Start(new ProcessStartInfo(capturedPath) { UseShellExecute = true }),
                    OpenContainingFolder = () => Process.Start(new ProcessStartInfo(
                        "explorer.exe", $"/select,\"{capturedPath}\"") { UseShellExecute = true }),
                });
            }
        }
        catch (OleDbException)
        {
            // Windows Search 서비스가 꺼져 있거나 사용할 수 없는 경우: 파일 검색만 건너뛰고
            // 다른 provider(앱/계산기/웹검색) 결과는 정상적으로 반환되도록 예외를 삼킨다.
        }

        return results;
    }
}
