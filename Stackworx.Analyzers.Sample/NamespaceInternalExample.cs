namespace Stackworx.Analyzers.Sample;

// Disallowed import
using Stackworx.Analyzers.Sample.Features.Feature1.Internal;

public class NamespaceInternalExample
{
    private Feature1Service service = new Feature1Service();
}