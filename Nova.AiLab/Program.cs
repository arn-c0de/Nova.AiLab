using System;

namespace Nova.AiLab
{
    /// <summary>
    /// Command line of the AI lab (docs/feature-ideas/AiSimulationEnvironment.md).
    /// LOCAL TOOL, NOT A CONTRIBUTION: it never enters a PR branch, and a green
    /// lab run is DIAGNOSIS, never proof — what was not seen in the running
    /// game is reported as not seen.
    ///
    /// Nothing but dispatch lives here. Each mode is one file in Cli/, the
    /// flags are in Cli/Options.cs, the help text in Cli/Usage.cs.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "-h" || args[0] == "--help")
            {
                Console.WriteLine(Usage.Text);
                return args.Length == 0 ? 1 : 0;
            }

            Options options;
            try
            {
                options = Options.Parse(args, args[0]);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"{ex.Message}\n\n{Usage.Text}");
                return 1;
            }

            return args[0] switch
            {
                "match" => MatchCommand.Run(options),
                "sweep" => SweepCommand.Run(options),
                "duel" => DuelCommand.Run(options),
                "movement" => MovementCommand.Run(options),
                "compare" => CompareCommand.Run(options),
                "player" => PlayerCommand.Run(options),
                "live" => LiveCommand.Run(options),
                _ => Fail($"unknown mode '{args[0]}'"),
            };
        }

        private static int Fail(string message)
        {
            Console.Error.WriteLine($"{message}\n\n{Usage.Text}");
            return 1;
        }
    }
}
