using System.Net;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Zametki.Services;

namespace Zametki;

public partial class LoginWindow : Window
{
    private bool _handled;

    public LoginWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            try
            {
                StatusText.Text = "WebView2: " + CoreWebView2Environment.GetAvailableBrowserVersionString();
                var options = new CoreWebView2EnvironmentOptions("--disable-gpu");
                var env = await CoreWebView2Environment.CreateAsync(null, null, options);
                await WebView.EnsureCoreWebView2Async(env);

                WebView.CoreWebView2.NavigationStarting += (_, args) =>
                {
                    StatusText.Text = "Загрузка…";
                    UrlText.Text = args.Uri;
                };
                WebView.NavigationCompleted += OnNavigated;
                WebView.CoreWebView2.ProcessFailed += (_, args) =>
                {
                    StatusText.Text = "Сбой процесса WebView2: " + args.ProcessFailedKind;
                };

                WebView.Source = new Uri($"{ApiConfig.BaseUrl}/login");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка инициализации WebView2:\n" + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
    }

    private async void OnNavigated(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        StatusText.Text = e.IsSuccess
            ? "Страница загружена"
            : "ОШИБКА ЗАГРУЗКИ: " + e.WebErrorStatus;
        UrlText.Text = WebView.Source?.ToString() ?? "";

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
