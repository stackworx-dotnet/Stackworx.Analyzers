namespace Stackworx.Analyzers.Sample.Types;

public record Author
{
    public int Id { get; init; }
    
    public required string Name { get; init; }
}