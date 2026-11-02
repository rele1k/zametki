namespace Zametki.Services;

/// <summary>
/// Адрес опубликованного веб-приложения. Впишите сюда URL, который вы
/// получили после публикации сайта (например https://vash-adres.ok.kimi.link).
/// Должен совпадать с BASE_URL в ApiConfig.kt (Android) и APIConfig.swift (iOS).
/// </summary>
public static class ApiConfig
{
    public const string BaseUrl = https://qiwlyzcahmvc4.kimi.pro;

    public static bool IsConfigured =>
        !BaseUrl.Contains(https://qiwlyzcahmvc4.kimi.pro, StringComparison.OrdinalIgnoreCase);
}
