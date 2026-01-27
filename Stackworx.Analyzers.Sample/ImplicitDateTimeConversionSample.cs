namespace Stackworx.Analyzers.Sample;

using System;
using JetBrains.Annotations;

[UsedImplicitly]
public class ImplicitDateTimeConversionSample
{
    public static void Run()
    {
        var dt = DateTime.Now;

        // ❌ This line will trigger the analyzer (implicit conversion)
        DateTimeOffset bad = dt;

        // ✅ Recommended explicit conversion
        DateTimeOffset good1 = new DateTimeOffset(dt);

        // ✅ Or use a specific offset/UTC conversion explicitly
        DateTimeOffset good2 = new DateTimeOffset(dt.ToUniversalTime(), TimeSpan.Zero);
        TimeZoneInfo.ConvertTime(bad, TimeZoneInfo.Local);

        Console.WriteLine($"Bad: {bad}");
        Console.WriteLine($"Good1: {good1}");
        Console.WriteLine($"Good2: {good2}");
    }
}