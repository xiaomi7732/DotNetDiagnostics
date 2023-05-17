namespace CPUIntensive;

public static class CPUIntensiveService
{
    public static void BurnCPU(int baseNumber, CancellationToken cancellationToken)
    {
        double result = 0;
        for (var i = Math.Pow(baseNumber, 7); i >= 0; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            
            result += Math.Atan(i) * Math.Tan(i);
        }
    }
}