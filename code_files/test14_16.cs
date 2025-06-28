using System;

class Program
{
    static void Main(String []args)
    {
        int[] array = { 3,4,5,2,1 };

        Console.WriteLine("До сортировки:");
        foreach (int num in array)
            Console.Write(num + " ");
        
        // Сортировка вставками
        for (int i = 1; i < array.Length; i++)
        {
            int key = array[i];
            int j = i - 1;

            while (j >= 0 && array[j] > key)
            {
                array[j + 1] = array[j];
                j--;
            }
            array[j + 1] = key;
        }

        Console.WriteLine("\nПосле сортировки:");
        foreach (int num in array)
            Console.Write(num + " ");
    }
}