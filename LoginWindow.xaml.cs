using System.Net;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Zametki.Services;

namespace Zametki;

public partial class LoginWindow : Window
{
    private bool _handled;
    private int _phase; // 0 = тест example.com, 1 = реальный вход
    private string _ver = "";
    private DispatcherTimer? _watchdog;

    public LoginWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            try
            {
                _ver = CoreWebView2Environment.GetAvailableBrowserVersionString();
                var options = new CoreWebView2EnvironmentOptions("--disable-gpu");
                var env = await CoreWebView2Environment.CreateAsync(null, null, options);
                await WebView.EnsureCoreWebView2Async(env);

                WebView.CoreWebView2.NavigationStarting += (_, args) =>
                {
                    StatusText.Text = $"WebView2: {_ver} | Загрузка: {args.Uri}";
                    UrlText.Text = args.Uri;
                    StartWatchdog();
                };
                WebView.NavigationCompleted += OnNavigated;
                WebView.CoreWebView2.ProcessFailed += (_, args) =>
                    StatusText.Text = $"WebView2: {_ver} | СБОЙ ПРОЦЕССА: {args.ProcessFailedKind}";

                // Тест 1: простая страница. Если зависнет и она — проблема в WebView2 на машине
                WebView.Source = new Uri("https://example.com");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка инициализации WebView2:\n" + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
    }

    private void StartWatchdog()
    {
        _watchdog?.Stop();
        _watchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _watchdog.Tick += (_, _) =>
        {
            _watchdog?.Stop();
            StatusText.Text += "  |  ЗАГРУЗКА >20 СЕК — похоже на зависание";
        };
        _watchdog.Start();
    }

    private async void OnNavigated(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        _watchdog?.Stop();
        StatusText.Text = $"WebView2: {_ver} | " + (e.IsSuccess
            ? "Загружено: " + (WebView.Source?.ToString() ?? "")
            : "ОШИБКА: " + e.WebErrorStatus);
        UrlText.Text = WebView.Source?.ToString() ?? "";

        if (_phase == 0)
        {
            // Тестовая страница загрузилась — переходим к реальному входу
            _phase = 1;
            WebView.Source = new Uri($"{ApiConfig.BaseUrl}/login");
            return;
        }

        if (_handled || !e.IsSuccess) return;

        var baseOrigin = new Uri(ApiConfig.BaseUrl).GetLeftPart(UriPartial.Authority).TrimEnd('/');
        var url = (WebView.Source?.ToString() ?? "").TrimEnd('/');
        if (!string.Equals(url, baseOrigin, StringComparison.OrdinalIgnoreCase)) return;

        _handled = true;
        var cookies = await WebView.CoreWebView2.CookieManager.GetCookiesAsync(ApiConfig.BaseUrl);
        App.Session.SetSessionCookies(cookies.Select(c =>
            new Cookie(c.Name, c.Value, c.Path ?? "/", c.Domain)));
        DialogResult = true;
        Close();
    }
}
