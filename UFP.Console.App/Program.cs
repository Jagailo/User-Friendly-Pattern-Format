using UFP.NET;

namespace UFP.Console.App
{
    internal static class Program
    {
        private static void Main()
        {
            const string patterns = "<Protocol>://<Domain>/<Page>?<FirstParameter.Key>=<FirstParameter.Value>|<Date>_<Name>.<Extension>|<StudyUid*>.<Type>.<Extension>";
            string[] values = [ "04-05-2024_fh5_image.png", "https://youtu.be/KoFSQeOAYz4?t=10", "04-05-2024_scan.jpg", "1.2.840.52394.3.152.235.2.12.187636473.patient.csv" ];

            System.Console.WriteLine(".NET:");
            foreach (var value in values)
            {
                var pairs = UfpParser.Parse(value, patterns);
                foreach (var pair in pairs)
                {
                    System.Console.WriteLine($"    {pair.Key}: \"{pair.Value}\"");
                }

                System.Console.WriteLine();
            }

            System.Console.WriteLine(".NET Standard:");
            foreach (var value in values)
            {
                var pairs = NET.Standard.UfpParser.Parse(value, patterns);
                foreach (var pair in pairs)
                {
                    System.Console.WriteLine($"    {pair.Key}: \"{pair.Value}\"");
                }

                System.Console.WriteLine();
            }

            System.Console.ReadKey();
        }
    }
}
