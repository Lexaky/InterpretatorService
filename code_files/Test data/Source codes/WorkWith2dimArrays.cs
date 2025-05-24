using System;

class Program
{
    static void Main()
    {
        int[,] matrix = {
            { 1, 2 },
            { 3, 4 },
            { 5, 6 }
        };

        int sum = 0;

        for (int i = 0; i < matrix.GetLength(0); i++) // строки
        {
            for (int j = 0; j < matrix.GetLength(1); j++) // столбцы
            {
                sum += matrix[i, j];
            }
        }

        Console.WriteLine($"Сумма всех элементов: {sum}");
    }
}
