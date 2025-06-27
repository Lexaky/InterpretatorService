using System;

class Program
{
    static void Main(string []args)
    {
        int[] numbers = { 1, 15, 25, 40, 19 };
        int sum = 0;

        foreach (int number in numbers)
        {
            sum += number;
        }

        Console.WriteLine($"Сумма: {sum}");
    }
}