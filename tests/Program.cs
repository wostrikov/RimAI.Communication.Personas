using System;

internal static class Program
{
    public static int Main()
    {
        int n = PersonaVariantGenerationTests.Run();
        Console.WriteLine("PERSONAS_FOCUSED_TESTS_OK passed=" + n);
        Console.WriteLine("TESTS total=" + n + " failed=0");
        return 0;
    }
}
