namespace Stackworx.Analyzers.Sample.Features.Feature1;

using System.Threading.Tasks;
using JetBrains.Annotations;

[UsedImplicitly]
public interface IFeature1Service
{
    public Task SayHello();
}