using System;

namespace YeonaMathAdventure.MathCore
{
    /// <summary>
    /// Small deterministic PRNG whose sequence does not depend on the .NET runtime.
    /// Do not use it for security-sensitive values.
    /// </summary>
    public sealed class DeterministicRandom
    {
        private uint state;

        public DeterministicRandom(uint seed)
        {
            state = seed == 0u ? 0x6D2B79F5u : seed;
        }

        public uint NextUInt()
        {
            uint value = state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            state = value;
            return value;
        }

        public int NextInt(int minimumInclusive, int maximumExclusive)
        {
            if (maximumExclusive <= minimumInclusive)
            {
                throw new ArgumentOutOfRangeException("maximumExclusive");
            }

            uint width = (uint)(maximumExclusive - minimumInclusive);
            return minimumInclusive + (int)(NextUInt() % width);
        }

        public bool NextBool()
        {
            return (NextUInt() & 1u) == 0u;
        }

        public void Shuffle<T>(T[] values)
        {
            if (values == null)
            {
                throw new ArgumentNullException("values");
            }

            for (int index = values.Length - 1; index > 0; index--)
            {
                int swapIndex = NextInt(0, index + 1);
                T temporary = values[index];
                values[index] = values[swapIndex];
                values[swapIndex] = temporary;
            }
        }
    }
}
