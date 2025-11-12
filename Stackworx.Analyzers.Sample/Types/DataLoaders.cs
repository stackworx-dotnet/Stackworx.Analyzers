namespace Stackworx.Analyzers.Sample.Types;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GreenDonut;

internal static class DataLoaders
{
    [DataLoader]
    internal static Task<Dictionary<int, Book>> BooksById(IReadOnlyList<int> keys, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
    
    [DataLoader]
    internal static Task<IDictionary<int, Author>> AuthorById(IReadOnlyList<int> keys, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}