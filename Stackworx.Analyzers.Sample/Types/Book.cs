namespace Stackworx.Analyzers.Sample.Types;

using HotChocolate;

public record Book
{
    public int Id { get; init; }
    
    public required string Title { get; init; }

    [GraphQLIgnore]
    public int AuthorId { get; init; }
}