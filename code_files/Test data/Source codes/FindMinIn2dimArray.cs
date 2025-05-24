using System;

class Program
{
    static void Main()
    {
        int[,] matrix = {
            { 3, 9, 1 },
            { 7, 5, 2 }
        };

        int max = matrix[0, 0];

        for (int i = 0; i < matrix.GetLength(0); i++)
        {
            for (int j = 0; j < matrix.GetLength(1); j++)
            {
                if (matrix[i, j] > max)
                {
                    max = matrix[i, j];
                }
            }
        }

        Console.WriteLine($"Максимум в матрице: {max}");
    }
}
