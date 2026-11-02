using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Zametki.Models;
using Zametki.Services;
using Zametki.ViewModels;

namespace Zametki;

public partial class MainWindow : Window
{
    private readonly NotesViewModel _vm;
    private bool _syncing;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new NotesViewModel(App.Api);
        DataContext = _vm;
        _vm.PropertyChanged += OnVmChanged;
        Loaded += async (_, _) => await CheckAuthAsync();
    }

    // ---- Навигация вход/заметки ----

    private async Task CheckAuthAsync()
    {
        if (ApiConfig.IsConfigured && App.Session.HasSession)
        {
            await _vm.CheckAuthAsync();
            if (_vm.Auth == AuthState.LoggedIn) { ShowNotes(); return; }
        }
        ShowLogin();
    }

    private void ShowLogin()
    {
        NotesView.Visibility = Visibility.Collapsed;
        LoginView.Visibility = Visibility.Visible;
    }

    private void ShowNotes()
    {
        LoginView.Visibility = Visibility.Collapsed;
        NotesView.Visibility = Visibility.Visible;
        UserNameLabel.Text = _vm.CurrentUser?.Name ?? "";
        RefreshFolderPicker();
    }

    private async void OnLoginClicked(object sender, RoutedEventArgs e)
    {
        if (!ApiConfig.IsConfigured)
        {
            LoginHint.Visibility = Visibility.Visible;
            return;
        }

        var login = new LoginWindow { Owner = this };
        if (login.ShowDialog() == true)
        {
            await _vm.CheckAuthAsync();
            if (_vm.Auth == AuthState.LoggedIn) ShowNotes();
        }
    }

    private async void OnLogoutClicked(object sender, RoutedEventArgs e)
    {
        await _vm.LogoutAsync();
        ShowLogin();
    }

    // ---- Реакция на ViewModel ----

    private async void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(NotesViewModel.Selected):
                await Dispatcher.InvokeAsync(SyncEditor);
                break;
            case nameof(NotesViewModel.Save):
                await Dispatcher.InvokeAsync(() => SaveStatus.Text = _vm.Save switch
                {
                    SaveState.Saving => "Сохранение…",
                    SaveState.Saved => "Сохранено",
                    SaveState.Error => "Не удалось сохранить",
                    _ => ""
                });
                break;
            case nameof(NotesViewModel.Folders):
                await Dispatcher.InvokeAsync(RefreshFolderPicker);
                break;
        }
    }

    private void SyncEditor()
    {
        var note = _vm.Selected;
        _syncing = true;
        try
        {
            TitleBox.Text = note?.Title ?? "";
            ContentBox.Text = note?.Content ?? "";
            PinButton.Content = note?.Pinned == true ? "📍" : "📌";
            EditorGrid.Visibility = note is null ? Visibility.Collapsed : Visibility.Visible;
            EmptyState.Visibility = note is null ? Visibility.Visible : Visibility.Collapsed;
            var idx = FolderOptions.FindIndex(o => o.FolderId == note?.FolderId);
            FolderPicker.SelectedIndex = idx;
        }
        finally
        {
            _syncing = false;
        }
    }

    // ---- Папки ----

    private sealed record FolderOption(long? FolderId, string Name)
    {
        public override string ToString() => Name;
    }

    private List<FolderOption> FolderOptions { get; set; } = [];

    private void RefreshFolderPicker()
    {
        FolderOptions = [new FolderOption(null, "Без папки"),
            .. _vm.Folders.Select(f => new FolderOption(f.Id, f.Name))];
        _syncing = true;
        try
        {
            var sel = FolderPicker.SelectedIndex;
            FolderPicker.ItemsSource = null;
            FolderPicker.ItemsSource = FolderOptions;
            FolderPicker.SelectedIndex = sel >= 0 && sel < FolderOptions.Count ? sel : 0;
        }
        finally
        {
            _syncing = false;
        }
    }

    private async void OnAllNotesClicked(object sender, RoutedEventArgs e)
    {
        FoldersList.SelectedItem = null;
        _vm.FolderId = null;
    }

    private async void OnFolderSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing) return;
        if (FoldersList.SelectedItem is Folder f) _vm.FolderId = f.Id;
    }

    private async void OnNewFolderClicked(object sender, RoutedEventArgs e)
    {
        var dlg = new InputDialog("Новая папка", "Название:", "Личное") { Owner = this };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.Result)) return;
        try
        {
            await _vm.CreateFolderAsync(dlg.Result.Trim());
        }
        catch (SuperJsonLite.TrpcException ex)
        {
            MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnFolderMenuClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: Folder folder }) return;
        var menu = new ContextMenu();
        var rename = new MenuItem { Header = "Переименовать" };
        rename.Click += async (_, _) =>
        {
            var dlg = new InputDialog("Переименовать", "Новое название:", folder.Name) { Owner = this };
            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.Result))
            {
                try { await _vm.RenameFolderAsync(folder, dlg.Result.Trim()); }
                catch (SuperJsonLite.TrpcException ex) { ShowError(ex); }
            }
        };
        var del = new MenuItem { Header = "Удалить папку" };
        del.Click += async (_, _) =>
        {
            if (MessageBox.Show("Заметки останутся в «Все заметки».", "Удалить папку?",
                    MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
            try { await _vm.DeleteFolderAsync(folder); }
            catch (SuperJsonLite.TrpcException ex) { ShowError(ex); }
        };
        menu.Items.Add(rename);
        menu.Items.Add(del);
        menu.IsOpen = true;
    }

    // ---- Заметки ----

    private async void OnNoteSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing) return;
        if (NotesList.SelectedItem is Note note) _vm.Selected = note;
    }

    private async void OnNewNoteClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            await _vm.CreateNoteAsync();
            NotesList.SelectedItem = _vm.Selected;
        }
        catch (SuperJsonLite.TrpcException ex)
        {
            ShowError(ex);
        }
    }

    // ---- Редактор ----

    private void OnEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing || _vm.Selected is null) return;
        _vm.ScheduleAutosave(_vm.Selected, TitleBox.Text, ContentBox.Text);
    }

    private async void OnFolderPicked(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _vm.Selected is null) return;
        if (FolderPicker.SelectedIndex < 0 || FolderPicker.SelectedIndex >= FolderOptions.Count) return;
        try
        {
            await _vm.MoveToFolderAsync(_vm.Selected, FolderOptions[FolderPicker.SelectedIndex].FolderId);
        }
        catch (SuperJsonLite.TrpcException ex)
        {
            ShowError(ex);
        }
    }

    private async void OnPinClicked(object sender, RoutedEventArgs e)
    {
        if (_vm.Selected is null) return;
        try { await _vm.TogglePinAsync(_vm.Selected); }
        catch (SuperJsonLite.TrpcException ex) { ShowError(ex); }
    }

    private async void OnDeleteClicked(object sender, RoutedEventArgs e)
    {
        if (_vm.Selected is not Note note) return;
        if (MessageBox.Show(note.Title, "Удалить заметку?",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        try { await _vm.DeleteNoteAsync(note); }
        catch (SuperJsonLite.TrpcException ex) { ShowError(ex); }
    }

    private void ShowError(SuperJsonLite.TrpcException ex) =>
        MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
}
