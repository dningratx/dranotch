namespace NotchMusic.Models;

public class TodoItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }

    public static (string category, string taskText) ParseCategory(string? rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle))
            return (string.Empty, string.Empty);

        int colonIdx = rawTitle.IndexOf(':');
        if (colonIdx > 0)
        {
            var category = rawTitle.Substring(0, colonIdx).Trim();
            var taskText = rawTitle.Substring(colonIdx + 1).Trim();
            if (!string.IsNullOrEmpty(taskText))
            {
                return (category, taskText);
            }
        }

        return (string.Empty, rawTitle.Trim());
    }
}
