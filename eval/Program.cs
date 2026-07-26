namespace PredictKitEval;

internal static class Program
{
    // summer-2026 (id 33022) is where PredictKit has actually forecasted, so it's
    // the only tournament with scoreable bot data.
    private const string DefaultTournament = "33022";

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
            case "score":
                return await Report.RunAsync(tournament);
            case "coverage":
                return await Coverage.RunAsync(tournament);
            default:
                Console.WriteLine("PredictKit eval harness.");
                Console.WriteLine("  dotnet run -- selftest              verify scoring math (no network)");
                Console.WriteLine("  dotnet run -- score [tournament]   score bot vs community (needs METACULUS_TOKEN)");
                Console.WriteLine("  dotnet run -- probe [tournament]   dump Metaculus API shape");
                return 0;
        }
    }
}
