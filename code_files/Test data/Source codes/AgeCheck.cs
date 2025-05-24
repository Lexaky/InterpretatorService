using System;

class Program
{
    static void Main()
    {
        int age = 17;

        if (age >= 18)
        {
            Console.WriteLine("Доступ разрешён");
        }
        else
        {
            Console.WriteLine("Доступ запрещён");
        }
    }
}
