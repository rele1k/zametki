using System.IO;
using System.Net;
using System.Text.Json;

namespace Zametki.Services;

/// <summary>
/// Cookie-сессия с сервером. Cookie выдаётся при входе через Kimi (WebView),
/// хранятся в файле в AppData и переживают перезапуски приложения.
/// </summary>
public class SessionService
{
    private readonly string _storePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Zametki", "session.json");

    public CookieContainer Cookies { get; } = new();

    public bool HasSession
    {
        get
        {
            var uri = new Uri(ApiConfig.BaseUrl);
            return Cookies.GetCookies(uri).Count > 0;
        }
    }

    public SessionService()
    {
        Load();
    }

    /// <summary>Вызывается после успешного входа в WebView: cookie переносятся из WebView2.</summary>
    public void SetSessionCookies(IEnumerable<Cookie> cookies)
    {
        var uri = new Uri(ApiConfig.BaseUrl);
        foreach (var c in cookies)
        {
            Cookies.Add(new Uri($"{uri.Scheme}://{c.Domain.TrimStart('.')}"), c);
        }
        Save();
    }

    public void Clear()
    {
        Cookies.GetAllCookies().Clear();
        if (File.Exists(_storePath)) File.Delete(_storePath);
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);
        var list = Cookies.GetAllCookies()
            .Select(c => new CookieDto(c.Name, c.Value, c.Path, c.Domain, c.Expires.ToUniversalTime()))
            .ToList();
        File.WriteAllText(_storePath, JsonSerializer.Serialize(list));
    }

    private void Load()
    {
        if (!File.Exists(_storePath)) return;
        try
        {
            var uri = new Uri(ApiConfig.BaseUrl);
            var list = JsonSerializer.Deserialize<List<CookieDto>>(File.ReadAllText(_storePath)) ?? [];
            foreach (var d in list)
            {
                var cookie = new Cookie(d.Name, d.Value, d.Path, d.Domain)
                {
                    Expires = d.Expires
                };
                if (cookie.Expires < DateTime.UtcNow) continue;
                Cookies.Add(uri, cookie);
            }
        }
        catch
        {
            // Повреждённый файл — начинаем с чистой сессии
        }
    }

    private sealed record CookieDto(string Name, string Value, string Path, string Domain, DateTime Expires);
}
