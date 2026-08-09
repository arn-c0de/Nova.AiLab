using System.Reflection;

namespace Nova.AiLab
{
    /// <summary>
    /// Which checkout of the game this lab binary was built against.
    /// <para>
    /// The lab lives OUTSIDE the game repository so it can measure whatever is
    /// checked out over there — any branch, without being merged into it. A
    /// measurement tool that has to be carried onto every branch it wants to
    /// look at can only ever look at its own.
    /// </para>
    /// <para>
    /// The path is stamped into the assembly by <c>Nova.AiLab.csproj</c> at
    /// build time, from the same <c>NovaRepo</c> property that decides which
    /// sources are compiled in. That is the point: it cannot drift from what
    /// was actually measured, because it IS what was measured. Reading it from
    /// the working directory or an environment variable at run time would let
    /// the two disagree, and the disagreement would look exactly like
    /// agreement.
    /// </para>
    /// <para>
    /// Empty when the attribute is missing (a hand-rolled build). Callers fall
    /// back to their own behaviour rather than inventing a path — an artifact
    /// that says <c>unknown</c> is honest, one that names the wrong checkout
    /// is not.
    /// </para>
    /// </summary>
    public static class NovaRepo
    {
        /// <summary>Absolute or relative path of the measured checkout; empty when unknown.</summary>
        public static readonly string Path = ReadFromAssembly();

        private static string ReadFromAssembly()
        {
            Assembly assembly = typeof(NovaRepo).Assembly;
            foreach (AssemblyMetadataAttribute attribute in
                     assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false))
            {
                if (attribute.Key == "NovaRepo") return attribute.Value ?? string.Empty;
            }
            return string.Empty;
        }
    }
}
