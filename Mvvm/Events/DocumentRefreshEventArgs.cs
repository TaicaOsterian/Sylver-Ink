namespace SylverInk.Mvvm.Events;

public class DocumentRefreshEventArgs(IEnumerable<Block> blocks) : EventArgs
{
    public IEnumerable<Block> NewBlocks { get; } = blocks;
}
