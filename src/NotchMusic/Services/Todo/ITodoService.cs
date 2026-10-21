using NotchMusic.Models;

namespace NotchMusic.Services.Todo;

public interface ITodoService
{
    List<TodoItem> LoadTodos();
    void SaveTodos(IEnumerable<TodoItem> todos);
}
