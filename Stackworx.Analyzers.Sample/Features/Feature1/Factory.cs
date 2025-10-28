namespace Stackworx.Analyzers.Sample.Features.Feature1;

using Stackworx.Analyzers.Sample.Features.Feature1.Internal;

public static class Factory
{
    public static IFeature1Service Create()
    {
        return new Feature1Service();
    }
}