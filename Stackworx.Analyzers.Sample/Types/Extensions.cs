namespace Stackworx.Analyzers.Sample.Types;

using Microsoft.Extensions.DependencyInjection;

public static class Extensions
{
    public static void BuildGraphQL(this IServiceCollection services)
    {
        services.AddGraphQLServer();
    }
}