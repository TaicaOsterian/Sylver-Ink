namespace SylverInk.Mvvm.Events;

public sealed class DocumentRefreshEventArgs(IEnumerable<Block> blocks) : EventArgs
{
    public IEnumerable<Block> NewBlocks { get; } = blocks;
}
