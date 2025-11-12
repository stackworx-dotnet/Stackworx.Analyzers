namespace Stackworx.Analyzers.Sample.Types;

using Microsoft.Extensions.DependencyInjection;

public class Extensions
{
    public static void BuildGraphQL(IServiceCollection services)
    {
        services.AddGraphQLServer();
    }
}