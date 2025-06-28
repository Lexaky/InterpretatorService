using System;

class Program
{
    static void Main(String []args)
    {
        int[,] matrix = { { 16,15,14,13;12,11,10,9;8,7,6,5;4,3,2,1 } };






        Console.WriteLine("До изменения:");
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                Console.Write(matrix[i, j] + "\t");
            }
            Console.WriteLine();
        }

        // Замена нечётных элементов на 0
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                if (matrix[i, j] % 2 != 0)
                {
                    matrix[i, j] = 0;
                }
            }
        }

        Console.WriteLine("\nПосле изменения:");
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                Console.Write(matrix[i, j] + "\t");
            }
            Console.WriteLine();
        }
    }
}