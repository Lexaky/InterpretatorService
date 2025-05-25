using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;

class Program
{
    static void Main(string []args)
    {
        int[] numbers = { 3,4,5,10 };
        int sum = 0;

        foreach (int number in numbers)
        {
            sum += number;
            var updates_12_0 = await TestVariableTracker.TrackVariables(12, 12, ("sum", sum));
            if (updates_12_0.ContainsKey("sum")) sum = (int)updates_12_0["sum"];
        }

        Console.WriteLine($"Сумма: {sum}");
    }
}