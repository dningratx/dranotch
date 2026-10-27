using NotchMusic.Models;

namespace NotchMusic.Services.Todo;

public interface ITodoService
{
    List<TodoItem> LoadTodos();
    void SaveTodos(IEnumerable<TodoItem> todos);
    IReadOnlySet<string> GetDeletedIds();
    void MarkDeleted(string id);
    void MarkDeleted(IEnumerable<string> ids);
    void ClearDeletedIds(IEnumerable<string> idsToClear);
}
