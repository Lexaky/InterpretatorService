using System;

class Program
{
    static void Main()
    {
        int[] arr = { 9, 5, 1, 4, 3 };

        for (int i = 1; i < arr.Length; i++)
        {
            int key = arr[i];
            int j = i - 1;

            while (j >= 0 && arr[j] > key)
            {
                arr[j + 1] = arr[j];
                j--;
            }

            arr[j + 1] = key;
        }

        Console.WriteLine("Отсортированный массив: " + string.Join(", ", arr));
    }
}
