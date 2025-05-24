using System;

class Program
{
    static void Main()
    {
        double[] numbers = { 1.2, 3.5, 4.0, 2.8 };
        double sum = 0;

        foreach (double n in numbers)
        {
            sum += n;
        }

        double average = sum / numbers.Length;
        Console.WriteLine($"Среднее значение: {average}");
    }
}
