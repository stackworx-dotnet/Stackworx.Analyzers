namespace Stackworx.Analyzers.Sample;

using System.Threading.Tasks;
using JetBrains.Annotations;

public class Unused
{
    public double User1 => this.Method1();

    [UsedImplicitly]
    public void Usage2()
    {
        this.Method2();
    }
    
    [UsedImplicitly]
    public async Task Usage3()
    {
        await this.Method3();
    }
    
    private double Method1()
    {
        return 0;
    }
    
    private double Method2()
    {
        return 0;
    }
    
    private Task<double> Method3()
    {
        return Task.FromResult(0.0);
    }
}