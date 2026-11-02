using System.ComponentModel;
using System.Runtime.CompilerServices;
using Zametki.Models;
using Zametki.Services;

namespace Zametki.ViewModels;

public enum AuthState { Loading, LoggedOut, LoggedIn }
public enum SaveState { Idle, Saving, Saved, Error }

public class NotesViewModel : INotifyPropertyChanged
{
    private readonly TrpcClient _api;
    private CancellationTokenSource? _autosaveCts;

    public AuthState Auth { get; private set; } = AuthState.Loading;
    public SaveState Save { get; private set; } = SaveState.Idle;

    private User? _user;
    public User? CurrentUser
    {
        get => _user;
        private set { _user = value; OnChanged(); }
    }

    private List<Folder> _folders = [];
    public List<Folder> Folders
    {
        get => _folders;
        private set { _folders = value; OnChanged(); OnChanged(nameof(FilteredNotes)); }
    }

    private long? _folderId;
    public long? FolderId
    {
        get => _folderId;
        set { _folderId = value; OnChanged(); _ = LoadNotesAsync(); }
    }

    private string _search = "";
    public string Search
    {
        get => _search;
        set { _search = value; OnChanged(); _ = LoadNotesAsync(); }
    }

    private List<Note> _notes = [];
    public List<Note> Notes
    {
        get => _notes;
        private set { _notes = value; OnChanged(); OnChanged(nameof(FilteredNotes)); }
    }

    public List<Note> FilteredNotes => Notes; // фильтрация делается на сервере

    private Note? _selected;
    public Note? Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            OnChanged();
            OnChanged(nameof(HasSelected));
            OnChanged(nameof(HasNoSelected));
        }
    }

    public bool HasSelected => Selected is not null;
    public bool HasNoSelected => Selected is null;

    public string? ErrorMessage { get; private set; }

    public NotesViewModel(TrpcClient api)
    {
        _api = api;
    }

    public async Task CheckAuthAsync()
    {
        try
        {
            CurrentUser = await _api.MeAsync();
            Auth = AuthState.LoggedIn;
        }
        catch
        {
            Auth = AuthState.LoggedOut;
        }
        OnChanged(nameof(Auth));
        if (Auth == AuthState.LoggedIn) await LoadAllAsync();
    }

    public async Task LoadAllAsync()
    {
        await Task.WhenAll(LoadFoldersAsync(), LoadNotesAsync());
    }

    public async Task LoadFoldersAsync()
    {
        try { Folders = await _api.ListFoldersAsync(); }
        catch { /* офлайн: оставляем текущее */ }
    }

    public async Task LoadNotesAsync()
    {
        try { Notes = await _api.ListNotesAsync(FolderId, string.IsNullOrWhiteSpace(Search) ? null : Search); }
        catch { /* офлайн */ }
    }

    public async Task<Folder> CreateFolderAsync(string name)
    {
        var folder = await _api.CreateFolderAsync(name);
        await LoadFoldersAsync();
        return folder;
    }

    public async Task RenameFolderAsync(Folder folder, string name)
    {
        await _api.RenameFolderAsync(folder.Id, name);
        await LoadFoldersAsync();
    }

    public async Task DeleteFolderAsync(Folder folder)
    {
        await _api.DeleteFolderAsync(folder.Id);
        if (FolderId == folder.Id) FolderId = null;
        await LoadAllAsync();
    }

    public async Task<Note> CreateNoteAsync()
    {
        var note = await _api.CreateNoteAsync(FolderId);
        await LoadNotesAsync();
        Selected = note;
        return note;
    }

    // ---- Редактор и автосохранение ----

    public void ScheduleAutosave(Note note, string title, string content)
    {
        _autosaveCts?.Cancel();
        _autosaveCts = new CancellationTokenSource();
        var ct = _autosaveCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(700, ct);
                await SaveNowAsync(note, title, content, ct);
            }
            catch (OperationCanceledException) { }
        });
    }

    private async Task SaveNowAsync(Note note, string title, string content, CancellationToken ct)
    {
        Save = SaveState.Saving; OnChanged(nameof(Save));
        try
        {
            var updated = await _api.UpdateNoteAsync(note.Id, title, content, null, null, ct);
            var list = Notes.Select(n => n.Id == updated.Id ? updated : n).ToList();
            Notes = list;
            Selected = updated;
            Save = SaveState.Saved;
        }
        catch
        {
            Save = SaveState.Error;
        }
        OnChanged(nameof(Save));
    }

    public async Task TogglePinAsync(Note note)
    {
        var updated = await _api.UpdateNoteAsync(note.Id, null, null, null, !note.Pinned);
        Notes = Notes.Select(n => n.Id == updated.Id ? updated : n)
                     .OrderByDescending(n => n.Pinned)
                     .ThenByDescending(n => n.UpdatedAt)
                     .ToList();
        if (Selected?.Id == updated.Id) Selected = updated;
    }

    public async Task MoveToFolderAsync(Note note, long? folderId)
    {
        var updated = await _api.UpdateNoteAsync(note.Id, null, null, folderId, null);
        Notes = Notes.Select(n => n.Id == updated.Id ? updated : n).ToList();
        if (Selected?.Id == updated.Id) Selected = updated;
        await LoadFoldersAsync();
    }

    public async Task DeleteNoteAsync(Note note)
    {
        await _api.DeleteNoteAsync(note.Id);
        Notes = Notes.Where(n => n.Id != note.Id).ToList();
        if (Selected?.Id == note.Id) Selected = null;
        await LoadFoldersAsync();
    }

    public async Task LogoutAsync()
    {
        await _api.LogoutAsync();
        Auth = AuthState.LoggedOut;
        OnChanged(nameof(Auth));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string name = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
