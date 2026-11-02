using System.Windows;
using Zametki.Services;

namespace Zametki;

public partial class App : Application
{
    public static SessionService Session { get; } = new();
    public static TrpcClient Api { get; } = new(Session);
}
