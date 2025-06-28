using System;

class Program
{
    static void Main(String []args)
    {
        // Пример массива
        int[] numbers = { 3, 6, 2, 9, 10, 5, 8 };

        int sum = 0;

        for (int i = 0; i < numbers.Length; i++)
        {
            if (numbers[i] % 2 == 0) // проверка на чётность
            {
                sum += numbers[i];
            }
        }

        Console.WriteLine("Сумма чётных элементов массива: " + sum);
    }
}
