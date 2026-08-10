using System;

namespace Nova.AiLab
{
    /// <summary>
    /// <c>live</c> — holds a match open on 127.0.0.1 so a human can watch what
    /// every unit is trying to do, and take the decision away from the AI.
    /// <para>
    /// THIS MODE PRODUCES NO MEASUREMENT and says so in everything it writes.
    /// Every other mode of this lab answers "what does the AI do"; this one
    /// answers "what would she have done if", which is a different question and
    /// worth exactly as much as its own honesty about that.
    /// </para>
    /// </summary>
    internal static class LiveCommand
    {
        public static int Run(Options options)
        {
            int port = options.Port;
            var match = new LiveMatch(options.Spec);
            match.Start();
            match.SetPaused(true);

            var server = new LiveServer(match, port, options.OutputDirectory);

            Console.WriteLine($"live session on http://127.0.0.1:{port}/  (loopback only — this can change what the AI does)");
            Console.WriteLine($"seed 0x{options.Spec.Seed:X}, budget {options.Spec.TickBudget} ticks, starting paused");
            if (options.OutputDirectory == null)
            {
                Console.WriteLine("no --out given: the session can be watched but not written down");
            }
            Console.WriteLine("stop with ctrl-c, or the button on the page (which writes first)");

            try
            {
                server.Run();
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"the live server stopped: {error.Message}");
                return 1;
            }
            return 0;
        }
    }
}
