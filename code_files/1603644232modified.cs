using System;
namespace HelloWorldApp
{
    class Program
VariableTracker.TrackVariables(("x", x), ("arr", arr), ("matrix", matrix));
VariableTracker.TrackVariables(("x", x), ("arr", arr), ("matrix", matrix));
    {
        static void Main(string[] args)
        {
            int x = 5;
            int[] arr = { 1, 2 };
            int[,] matrix = { { 1, 2 }, { 3, 4 } };
            Console.WriteLine("Step 1");
            Console.WriteLine("Step 2");
        }
    }
}

public static class VariableTracker
{
    public static void TrackVariables(params (string Name, object Value)[] variables)
    {
        InterpretatorService.Services.VariableTracker.TrackDelegate?.Invoke("1603644232", variables);
    }
}