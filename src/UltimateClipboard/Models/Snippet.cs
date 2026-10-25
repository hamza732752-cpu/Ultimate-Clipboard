using System;

namespace UltimateClipboard.Models;

public class Snippet
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Collection { get; set; }
    public string? Tags { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    public int SortOrder { get; set; }
}
