namespace PredictKitEval;

internal static class Program
{
    // Past AIB seasons carry resolved questions, so a backtest can run today
    // rather than waiting for summer-2026 questions to resolve.
    private const string DefaultTournament = "fall-aib-2025";

    private static async Task<int> Main(string[] args)
    {
        string command = args.Length > 0 ? args[0] : "help";
        string tournament = args.Length > 1 ? args[1] : DefaultTournament;

        switch (command)
        {
            case "selftest":
                return SelfTest.Run();
            case "probe":
                return await Probe.RunAsync(tournament);
            default:
                Console.WriteLine("PredictKit eval harness.");
                Console.WriteLine("  dotnet run -- selftest              verify scoring math (no network)");
                Console.WriteLine("  dotnet run -- probe [tournament]   dump Metaculus API shape (needs METACULUS_TOKEN)");
                return 0;
        }
    }
}
