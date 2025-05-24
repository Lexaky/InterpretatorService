using System;
namespace HelloWorldApp
{
    class Program
    {
	static void sum () {
	short[] arr = {3,2,1};
	}
        static void Main(string[] args)
        {
            int[] arr = { 4, 3, 2, 1 };

            for (int i = 0; i < arr.Length; i++)
            {
                for (int j = arr.Length - 1; j > i; j--)
                {
                    if (arr[j] < arr[j - 1])
                    {
                        int sw = arr[j];
                        arr[j] = arr[j - 1];
                        arr[j - 1] = sw;
                    }
                }
            }
        }
    }
}