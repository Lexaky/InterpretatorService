using System;
namespace HelloWorldApp
{
    class Program
    {
        static void Main(string[] args)
        {
            int x = 5;
            int[] arr = { 1, 2 };
            int[,] matrix = { { 1, 2 }, { 3, 4 } };
            for (int i = 0; i < 10; i++)
            {
                Console.WriteLine(i);
            }
            Console.WriteLine("Step 1");
            Console.WriteLine("Step 2");
        }
    }
}