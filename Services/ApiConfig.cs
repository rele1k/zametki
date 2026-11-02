namespace Zametki.Services;

public static class ApiConfig
{
    public const string BaseUrl = "https://qiwlyzcahmvc4.kimi.pro";

    public static bool IsConfigured =>
        !BaseUrl.Contains("ВАШ-АДРЕС", StringComparison.OrdinalIgnoreCase);
}
