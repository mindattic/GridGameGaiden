namespace GridGame.Core.Battle
{
    /// <summary>
    /// The party's single shared, capped pool of generic mana orbs (bible §3.1, GG-LAW-6).
    /// Every orb is the same; any spell can be paid from any orbs. Minting past capacity overflows (is lost).
    /// </summary>
    public sealed class ManaBank
    {
        public const int DefaultCapacity = 12;

        public int Capacity { get; }
        public int Count { get; private set; }
        public bool IsFull => Count >= Capacity;

        public ManaBank(int capacity = DefaultCapacity) { Capacity = capacity; }

        public ManaBank Clone() => new ManaBank(Capacity) { Count = Count };

        /// <summary>Adds up to <paramref name="n"/> orbs; returns how many landed (the rest overflowed).</summary>
        public int Add(int n = 1)
        {
            int landed = System.Math.Max(0, System.Math.Min(n, Capacity - Count));
            Count += landed;
            return landed;
        }

        public bool CanAfford(int cost) => cost <= Count;

        /// <summary>Removes <paramref name="cost"/> orbs; false (and nothing spent) if unaffordable.</summary>
        public bool Spend(int cost)
        {
            if (cost < 0 || cost > Count) return false;
            Count -= cost;
            return true;
        }

        public override string ToString() => $"{Count}/{Capacity}";
    }
}
