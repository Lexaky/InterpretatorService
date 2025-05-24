using System;

class Program
{
    static void Main()
    {
        int a = 7;
        int b = 3;

        int min = (a < b) ? a : b;
        Console.WriteLine($"Минимум: {min}");
    }
}
