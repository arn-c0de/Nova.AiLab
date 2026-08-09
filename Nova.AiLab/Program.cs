using System;
using System.IO;

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

            string mode = args[0];

            // THE MODE IS CHECKED FIRST, before the flags are read. The other
            // way round a mistyped mode was reported as a bad OPTION — the
            // parser rejected the first flag that does not belong to a mode it
            // could not resolve — and the message named the wrong mistake.
            Options options;
            try
            {
                options = Options.Parse(args, mode);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"{ex.Message}\n\n{Usage.Text}");
                return 1;
            }

            // THE EXIT CODE IS A CONTRACT (AGENTS.md): 0 ran, 1 is an operator
            // error, 2 is a determinism finding an agent must stop on. An
            // unhandled exception is none of the three — a missing --against
            // archive or an unwritable --out used to leave a stack trace and a
            // code the contract does not describe, which an agent reading $?
            // cannot tell from a crash mid-match.
            //
            // Only the exception types an OPERATOR causes are caught here. A
            // defect inside the lab or the simulation still crashes loudly with
            // its stack trace: that is a finding, not a usage mistake, and
            // dressing it up as exit code 1 would hide it.
            try
            {
                return mode switch
                {
                    "match" => MatchCommand.Run(options),
                    "sweep" => SweepCommand.Run(options),
                    "duel" => DuelCommand.Run(options),
                    "movement" => MovementCommand.Run(options),
                    "compare" => CompareCommand.Run(options),
                    _ => Fail($"unknown mode '{mode}'"),
                };
            }
            catch (Exception ex) when (ex is IOException
                                       || ex is UnauthorizedAccessException
                                       || ex is FormatException
                                       || ex is ArgumentException)
            {
                Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
                return 1;
            }
        }

        private static int Fail(string message)
        {
            Console.Error.WriteLine($"{message}\n\n{Usage.Text}");
            return 1;
        }
    }
}
