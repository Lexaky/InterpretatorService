using System;

class Program
{
    static void Main()
    {
        int[] arr = { -2, 4, 0, -1, 7 };
        int pos = 0, neg = 0;

        foreach (int n in arr)
        {
            if (n > 0) pos++;
            else if (n < 0) neg++;
        }

        Console.WriteLine($"Положительных: {pos}, Отрицательных: {neg}");
    }
}
