namespace MultibonkTestKit
{
    /// <summary>
    /// MultibonkTestKit - a plain .NET console app for testing the Multibonk co-op mod
    /// without two humans on two PCs. Two modes, see README.md:
    ///   join - the tool is a fake CLIENT connecting to a real hosted game (drives the
    ///          mod's host-side code).
    ///   host - the tool is a fake HOST that a real game client connects to (drives the
    ///          mod's client-side apply code - enemy death, pickups, level-up resume, ...).
    /// This file only dispatches to JoinMode / HostMode; all the logic lives there.
    /// </summary>
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], "join", StringComparison.OrdinalIgnoreCase))
                return await JoinMode.RunAsync(args);

            if (args.Length > 0 && string.Equals(args[0], "host", StringComparison.OrdinalIgnoreCase))
                return await HostMode.RunAsync(args);

            PrintUsage();
            return 1;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("MultibonkTestKit - test the Multibonk co-op mod solo, without a second PC.");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run -- join --host 127.0.0.1 --port 25565 --name TestBot [--character Warrior]");
            Console.WriteLine("      Fake CLIENT: connects to a real hosted game. Drives the mod's HOST-side code.");
            Console.WriteLine();
            Console.WriteLine("  dotnet run -- host [--port 25565] [--seed 12345] [--name TestKit-Host] [--character-byte 0]");
            Console.WriteLine("      Fake HOST: a real game client connects to this. Drives the mod's CLIENT-side apply code");
            Console.WriteLine("      (enemy death/health, pickups, chests/shrines, level-up resume, run-over, stage transition).");
        }
    }
}
