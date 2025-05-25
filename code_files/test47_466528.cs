using System;

class Program
{
    static void Main(string []args)
    {
        int[] numbers = { 3,4,5,10 };
        int sum = 0;

        foreach (int number in numbers)
        {
            sum += number;
        }

        Console.WriteLine($"Сумма: {sum}");
    }
}