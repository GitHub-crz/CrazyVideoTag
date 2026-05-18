using System.Collections.ObjectModel;

namespace CrazyVideoTag.Models;

public sealed class FolderNode
{
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public ObservableCollection<FolderNode> Children { get; } = [];
    public override string ToString() => Name;
}
