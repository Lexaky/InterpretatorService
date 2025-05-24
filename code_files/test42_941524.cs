using System;

class Program
{
    static void Main(string []args)
    {
        int[] numbers = { 6,11,16,21 };
        int sum = 10;

        foreach (int number in numbers)
        {
            sum += number;
        }

        Console.WriteLine($"Сумма: {sum}");
    }
}