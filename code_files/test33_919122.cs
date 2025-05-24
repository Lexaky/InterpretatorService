using System;

class Program
{
    static void Main(string []args)
    {
        int[] arr = { 5,4,3,2,1 };








                    arr[j] = arr[j + 1];
                    arr[j + 1] = temp;
                }
            }
        }

        Console.WriteLine("Отсортированный массив: " + string.Join(", ", arr));
    }
}