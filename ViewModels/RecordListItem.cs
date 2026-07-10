using SecretList.Models;

namespace SecretList.ViewModels;

public class RecordListItem
{
    public EntityRecord Record { get; }

    public RecordListItem(EntityRecord record)
    {
        Record = record;
    }

    public string DisplayLine => $"{Record.NickName} ({Record.Category})";
}