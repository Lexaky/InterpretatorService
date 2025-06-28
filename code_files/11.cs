using System;

class Program
{
    static void Main(string []args)
    {
        int[] arr = { 5, 3, 8,
         1, 4 };

        for (int i = 0; i < arr.Length - 1; i++)
        {
            for (int j = 0; j < arr.Length - i - 1; j++)
            {
                if (arr[j] > arr[j + 1])
                {
                    int temp = arr[j];
                    arr[j] = arr[j + 1];
                    arr[j + 1] = temp;
                }
            }
        }

        Console.WriteLine("Отсортированный массив: " + string.Join(", ", arr));
    }
}
