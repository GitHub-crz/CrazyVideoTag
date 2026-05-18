namespace CrazyVideoTag.Models;

public sealed class TagDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#4F8EF7";
    public TagKind Kind { get; set; }
    public int SortOrder { get; set; }
}
