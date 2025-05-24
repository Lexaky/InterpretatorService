using System;

class Program
{
    static void Main(string []args)
    {
        int[] numbers = { 1,2,3,4,5,6,7 };
        int sum = 0;

        foreach (int number in numbers)
        {
            sum += number;
        }

        Console.WriteLine($"Сумма: {sum}");
    }
}