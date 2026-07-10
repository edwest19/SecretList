namespace SecretList.Models;

public class EntityRecord
{
    public string Category { get; set; } = string.Empty;
    public string NickName { get; set; } = string.Empty;
    public Dictionary<string, string> Values { get; set; } = new();
}