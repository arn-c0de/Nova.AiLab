namespace Nova.AiLab
{
    /// <summary>
    /// Derives a sweep's seed list from one base seed.
    /// <para>
    /// A pure function of (baseSeed, index), so a result set is reproducible
    /// from a single number instead of carrying a list that might have been
    /// generated differently the next time (plan section 3.7: a result set
    /// carries its seed list, and a report refuses the comparison when it does
    /// not match).
    /// </para>
    /// <para>
    /// KNOWN TODAY: the seed axis is empty. No simulation system draws from the
    /// kernel PRNG, so every seed plays the identical match — see
    /// <c>MetricsAndSweepTests.DifferentSeeds_ProduceTheIdenticalMatch...</c>.
    /// This class stays because the axis becomes real the moment anything
    /// draws, and because the sweep itself is what proved the axis empty.
    /// </para>
    /// </summary>
    public static class SeedSeries
    {
        public static ulong[] Derive(ulong baseSeed, int count)
        {
            var seeds = new ulong[count];
            for (int i = 0; i < count; i++)
            {
                // splitmix64 finalizer — a fixed mixing function, not a draw.
                ulong z = baseSeed + 0x9E3779B97F4A7C15UL * (ulong)(i + 1);
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                seeds[i] = z ^ (z >> 31);
            }
            return seeds;
        }
    }
}
