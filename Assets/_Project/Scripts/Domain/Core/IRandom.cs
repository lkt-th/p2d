namespace P2D.Domain
{
    /// <summary>
    /// Random source for game rules. Rules never call System.Random directly so that
    /// battles and drops can be replayed from a seed (client and server validation).
    /// </summary>
    public interface IRandom
    {
        /// <summary>Returns a value in [0, 1).</summary>
        double NextDouble();

        /// <summary>Returns a value in [0, maxExclusive).</summary>
        int Next(int maxExclusive);
    }

    public sealed class SeededRandom : IRandom
    {
        readonly System.Random random;

        public SeededRandom(int seed)
        {
            random = new System.Random(seed);
        }

        public double NextDouble() => random.NextDouble();

        public int Next(int maxExclusive) => random.Next(maxExclusive);
    }
}
