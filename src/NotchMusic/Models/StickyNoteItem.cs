namespace NotchMusic.Models;

public class StickyNoteItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Catatan Baru";
    public string Content { get; set; } = string.Empty;
    public string Color { get; set; } = "Yellow"; // Yellow, Green, Pink, Blue, Purple, Orange, Dark
    public bool IsPinned { get; set; } = false;
    public bool IsLocked { get; set; } = false;
    public bool IsCollapsed { get; set; } = false;
    public bool IsClosed { get; set; } = false;
    public double Left { get; set; } = -1;
    public double Top { get; set; } = -1;
    public double Width { get; set; } = 320;
    public double Height { get; set; } = 320;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
