using System;

class Program
{
    static void Main(string []args)
    {
        int[] numbers = { 10,15,20,25 };
        int sum = 5;

        foreach (int number in numbers)
        {
            sum += number;
        }

        Console.WriteLine($"Сумма: {sum}");
    }
}