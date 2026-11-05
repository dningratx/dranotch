using NotchMusic.Models;

namespace NotchMusic.Services.StickyNotes;

public interface IStickyNoteService
{
    List<StickyNoteItem> LoadNotes();
    void SaveNotes(IEnumerable<StickyNoteItem> notes);
    IReadOnlySet<string> GetDeletedIds();
    void MarkDeleted(string id);
    void MarkDeleted(IEnumerable<string> ids);
    void ClearDeletedIds(IEnumerable<string> idsToClear);
}
