namespace Zametki.Models;

public record User(
    long Id,
    string? Name,
    string? AvatarUrl,
    bool IsAdmin,
    DateTimeOffset CreatedAt
);

public record Folder(
    long Id,
    long UserId,
    string Name,
    DateTimeOffset CreatedAt,
    int? NoteCount = null
);

public record Note(
    long Id,
    long UserId,
    long? FolderId,
    string Title,
    string Content,
    bool Pinned,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
