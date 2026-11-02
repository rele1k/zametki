using System.Net;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Zametki.Services;

namespace Zametki;

/// <summary>
/// Окно входа: WebView2 со страницей Kimi. После редиректа на корень сайта
/// cookie переносятся в общее хранилище сессии.
/// </summary>
public partial class LoginWindow : Window
{
    private bool _handled;

    public LoginWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            await WebView.EnsureCoreWebView2Async();
            WebView.NavigationCompleted += OnNavigated;
            WebView.Source = new Uri($"{ApiConfig.BaseUrl}/login");
        };
    }

    private async void OnNavigated(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
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
