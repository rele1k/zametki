# Заметки для Windows (WPF, .NET 8)

Нативное приложение заметок для Windows 10/11. Тот же сервер и протокол
(tRPC + cookie-сессия), что у веб-версии и приложений Android/iOS —
заметки синхронизируются на всех устройствах.

Приложение **портативное**: после сборки это папка с `Zametki.exe`,
ничего устанавливать не нужно, сертификаты не требуются.

## Как получить готовое приложение (без Visual Studio)

Вариант 1 — через GitHub Actions (ничего ставить не надо):
1. Создайте репозиторий на github.com и загрузите сюда все файлы
2. Впишите адрес сервера в `Services/ApiConfig.cs`
3. Actions → «Сборка приложения Windows» → Run workflow
4. Через ~5 минут скачайте артефакт `Zametki-Windows` — это готовая папка

Вариант 2 — на своём ПК:
1. Установите .NET 8 SDK
2. Впишите адрес сервера в `Services/ApiConfig.cs`
3. Запустите `build-installer.ps1` — готовая папка появится на рабочем столе

В обоих случаях запускаете `Zametki.exe`.

## Настройка

`Services/ApiConfig.cs` — замените `BaseUrl` на адрес опубликованного
веб-приложения (тот же, что в ApiConfig.kt и APIConfig.swift):
```csharp
public const string BaseUrl = "https://ваш-адрес.ok.kimi.link";
```

## Как это работает

- Вход через Kimi открывается в окне WebView2 (`/login` на сервере)
- После редиректа на корень сайта cookie сессии переносятся из WebView2
  и сохраняются в `%AppData%\Zametki` — переживают перезапуски
- Дальше приложение работает по тому же протоколу tRPC, что и другие клиенты

## Состав

```
Services/    ApiConfig (настроить!), TrpcClient, SuperJsonLite, SessionService
Models/      User, Folder, Note
ViewModels/  NotesViewModel (папки, поиск, автосохранение 700 мс)
MainWindow   три колонки: папки | список | редактор
LoginWindow  WebView2 с переносом cookie
InputDialog  мелкий диалог ввода
appicon.ico  иконка приложения
```

## Проверено

Логика клиента скомпилирована и протестирована на .NET 8. Интерфейс (XAML/WPF)
собирается стандартным `dotnet build` — Visual Studio не требуется.
