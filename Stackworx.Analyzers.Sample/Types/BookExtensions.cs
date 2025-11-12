namespace Stackworx.Analyzers.Sample.Types;

using System.Threading.Tasks;
using HotChocolate.Types;

[ExtendObjectType<Book>]
public static class BookExtensions
{
    public static Task<Author?> GetAuthorAsync(int id, IAuthorByIdDataLoader loader)
    {
        return loader.LoadAsync(id);
    }
}