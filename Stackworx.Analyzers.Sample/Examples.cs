// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Stackworx.Analyzers.Sample;

using JetBrains.Annotations;

// If you don't see warnings, build the Analyzers Project.
[UsedImplicitly]
public class Examples
{
    public class MyCompanyClass // Try to apply quick fix using the IDE.
    {
    }

    public void ToStars()
    {
        var spaceship = new Spaceship();
        spaceship.SetSpeed(300000000); // Invalid value, it should be highlighted.
        spaceship.SetSpeed(42);
    }
}