using System;

class Program
{
    static void Main()
    {
        int[] arr = { 7, 2, 9, 1, 6, 3 };

        QuickSort(arr, 0, arr.Length - 1);

        Console.WriteLine("Отсортированный массив: " + string.Join(", ", arr));
    }

    static void QuickSort(int[] arr, int left, int right)
    {
        if (left >= right) return;

        int pivot = arr[(left + right) / 2];
        int index = Partition(arr, left, right, pivot);
        QuickSort(arr, left, index - 1);
        QuickSort(arr, index, right);
    }

    static int Partition(int[] arr, int left, int right, int pivot)
    {
        while (left <= right)
        {
            while (arr[left] < pivot) left++;
            while (arr[right] > pivot) right--;

            if (left <= right)
            {
                int temp = arr[left];
                arr[left] = arr[right];
                arr[right] = temp;
                left++;
                right--;
            }
        }
        return left;
    }
}
