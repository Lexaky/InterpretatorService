using System;

class Program
{
    static void Main()
    {
        string color = "green";

        switch (color)
        {
            case "red":
                Console.WriteLine("Красный свет — стой!");
                break;
            case "yellow":
                Console.WriteLine("Жёлтый — приготовься!");
                break;
            case "green":
                Console.WriteLine("Зелёный — иди!");
                break;
            default:
                Console.WriteLine("Неизвестный сигнал");
                break;
        }
    }
}
