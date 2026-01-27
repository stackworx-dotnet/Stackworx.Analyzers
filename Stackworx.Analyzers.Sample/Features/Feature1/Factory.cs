namespace Stackworx.Analyzers.Sample.Features.Feature1;

using JetBrains.Annotations;
using Stackworx.Analyzers.Sample.Features.Feature1.Internal;

[UsedImplicitly]
public static class Factory
{
    public static IFeature1Service Create()
    {
        return new Feature1Service();
    }
}