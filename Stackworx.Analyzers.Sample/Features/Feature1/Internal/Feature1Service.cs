namespace Stackworx.Analyzers.Sample.Features.Feature1.Internal;

using System;
using System.Threading.Tasks;
using JetBrains.Annotations;

[UsedImplicitly]
public class Feature1Service : IFeature1Service
{
    public Task SayHello()
    {
        Console.WriteLine("Hello");
        return Task.CompletedTask;
    }
}