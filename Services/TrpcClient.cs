using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zametki.Models;
using static Zametki.Services.SuperJsonLite;

namespace Zametki.Services;

/// <summary>
/// Минимальный клиент tRPC поверх HttpClient с cookie-авторизацией.
/// Протокол тот же, что в нативных Android/iOS приложениях:
///   query    → GET  /api/trpc/{proc}?input={"json":...}
///   mutation → POST /api/trpc/{proc}  body: {"json":...}
/// Ответ — всегда массив (superjson).
/// </summary>
public class TrpcClient
{
    private readonly HttpClient _http;
    private readonly SessionService _session;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public TrpcClient(SessionService session)
    {
        _session = session;
        var handler = new HttpClientHandler
        {
            CookieContainer = session.Cookies,
            UseCookies = true,
            AutomaticDecompression = DecompressionMethods.All
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    }

    // ---- Сессия ----

    public async Task<User> MeAsync(CancellationToken ct = default)
    {
        var el = await QueryAsync("auth.me", null, ct);
        return el.Deserialize<User>(JsonOpts)!;
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        try { await MutateAsync("auth.logout", null, ct); }
        catch { /* локальную сессию очищаем в любом случае */ }
        _session.Clear();
    }

    // ---- Папки ----

    public async Task<List<Folder>> ListFoldersAsync(CancellationToken ct = default)
    {
        var el = await QueryAsync("notes.folders.list", null, ct);
        return el.Deserialize<List<Folder>>(JsonOpts) ?? [];
    }

    public async Task<Folder> CreateFolderAsync(string name, CancellationToken ct = default)
    {
        var el = await MutateAsync("notes.folders.create", new { name }, ct);
        return el.Deserialize<Folder>(JsonOpts)!;
    }

    public async Task<Folder> RenameFolderAsync(long id, string name, CancellationToken ct = default)
    {
        var el = await MutateAsync("notes.folders.rename", new { id, name }, ct);
        return el.Deserialize<Folder>(JsonOpts)!;
    }

    public async Task DeleteFolderAsync(long id, CancellationToken ct = default)
    {
        await MutateAsync("notes.folders.delete", new { id }, ct);
    }

    // ---- Заметки ----

    public async Task<List<Note>> ListNotesAsync(long? folderId, string? search, CancellationToken ct = default)
    {
        object? input = (folderId, search) switch
        {
            (null, null) => null,
            (null, _) => new { search },
            (_, null) => new { folderId },
            _ => new { folderId, search }
        };
        var el = await QueryAsync("notes.list", input, ct);
        return el.Deserialize<List<Note>>(JsonOpts) ?? [];
    }

    public async Task<Note> CreateNoteAsync(long? folderId, CancellationToken ct = default)
    {
        var el = await MutateAsync("notes.create", new { folderId }, ct);
        return el.Deserialize<Note>(JsonOpts)!;
    }

    public async Task<Note> UpdateNoteAsync(long id, string? title, string? content,
        long? folderId, bool? pinned, CancellationToken ct = default)
    {
        var el = await MutateAsync("notes.update",
            new { id, title, content, folderId, pinned }, ct);
        return el.Deserialize<Note>(JsonOpts)!;
    }

    public async Task DeleteNoteAsync(long id, CancellationToken ct = default)
    {
        await MutateAsync("notes.delete", new { id }, ct);
    }

    // ---- Транспорт ----

    private async Task<JsonElement> QueryAsync(string proc, object? input, CancellationToken ct)
    {
        var url = $"{ApiConfig.BaseUrl}/api/trpc/{proc}";
        if (input is not null)
        {
            var json = JsonSerializer.Serialize(new { json = input }, JsonOpts);
            // EscapeDataString кодирует пробел как %20 — сервер декодирует строго
            url += $"?input={Uri.EscapeDataString(json)}";
        }
        using var resp = await _http.GetAsync(url, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new TrpcException($"Сервер вернул {(int)resp.StatusCode}");
        return GetData(body);
    }

    private async Task<JsonElement> MutateAsync(string proc, object? input, CancellationToken ct)
    {
        var url = $"{ApiConfig.BaseUrl}/api/trpc/{proc}";
        var payload = JsonSerializer.Serialize(new { json = input }, JsonOpts);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync(url, content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new TrpcException($"Сервер вернул {(int)resp.StatusCode}");
        return GetData(body);
    }
}
