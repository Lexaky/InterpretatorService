using System;

class Program
{
    static void Main(String []args)
    {
        // Пример двумерной матрицы
        int[,] matrix = {
            { 3, 5, 1, 9 },
            { 4, 8, 2, 0 },
            { 7, 6, 3, 2 }
        };

        int rowCount = matrix.GetLength(0);
        int colCount = matrix.GetLength(1);

        int[] maxInRows = new int[rowCount];

        for (int i = 0; i < rowCount; i++)
        {
            int max = matrix[i, 0];
            for (int j = 1; j < colCount; j++)
            {
                if (matrix[i, j] > max)
                {
                    max = matrix[i, j];
                }
            }
            maxInRows[i] = max;
        }

        // Вывод результатов
        Console.WriteLine("Максимальные элементы в каждой строке:");
        for (int i = 0; i < rowCount; i++)
        {
            Console.WriteLine($"Строка {i}: {maxInRows[i]}");
        }
    }
}