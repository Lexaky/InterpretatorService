using System;

class Program
{
    static void Main(string []args)
    {
        int[] numbers = { 4,3,2,10 };
        int sum = 0;

        foreach (int number in numbers)
        {
            sum += number;
        }

        Console.WriteLine($"Сумма: {sum}");
    }
}