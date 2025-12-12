using System.Collections.Generic;
using Verse;

namespace Companionship
{
    /// <summary>
    /// Per-save (Game-level) component that tracks temporary "lovin is allowed" pairings.
    /// Also stores one-time payout info so we can trigger payment/moodlet when lovin ends.
    /// </summary>
    public class GameComponent_Companionship : GameComponent
    {
        public class PairData : IExposable
        {
            public int expireTick;
            public int mapId;
            public IntVec3 bedPos;
            public int paymentSilver;

            public void ExposeData()
            {
                Scribe_Values.Look(ref expireTick, "expireTick", 0);
                Scribe_Values.Look(ref mapId, "mapId", -1);
                Scribe_Values.Look(ref bedPos, "bedPos", default(IntVec3));
                Scribe_Values.Look(ref paymentSilver, "paymentSilver", 0);
            }
        }

        // key -> data
        private Dictionary<long, PairData> allowedLovinPairs = new Dictionary<long, PairData>();

        public GameComponent_Companionship(Game game) : base()
        {
        }

        public override void GameComponentTick()
        {
            // Lightweight cleanup: once per ~10 seconds.
            if (Find.TickManager != null && Find.TickManager.TicksGame % 600 == 0)
            {
                CleanupExpired();
            }
        }

        public override void ExposeData()
        {
            // Short-lived state, but scribed so saving mid-date doesn't break things.
            Scribe_Collections.Look(ref allowedLovinPairs, "allowedLovinPairs", LookMode.Value, LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (allowedLovinPairs == null)
                    allowedLovinPairs = new Dictionary<long, PairData>();
            }
        }

        public void AllowLovin(Pawn a, Pawn b, IntVec3 bedPos, int mapId, int paymentSilver, int expireTick)
        {
            if (a == null || b == null) return;

            long key = MakePairKey(a, b);

            PairData data = new PairData();
            data.expireTick = expireTick;
            data.mapId = mapId;
            data.bedPos = bedPos;
            data.paymentSilver = paymentSilver;

            allowedLovinPairs[key] = data;
        }

        public void DisallowLovin(Pawn a, Pawn b)
        {
            if (a == null || b == null) return;
            long key = MakePairKey(a, b);
            allowedLovinPairs.Remove(key);
        }

        public bool IsLovinAllowed(Pawn a, Pawn b)
        {
            if (a == null || b == null) return false;
            if (Find.TickManager == null) return false;

            long key = MakePairKey(a, b);

            PairData data;
            if (!allowedLovinPairs.TryGetValue(key, out data))
                return false;

            int now = Find.TickManager.TicksGame;
            if (data == null || now > data.expireTick)
            {
                allowedLovinPairs.Remove(key);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Consume payout info exactly once. Returns true only for the first caller for that pair.
        /// Also removes the pair authorization (so it can't leak into vanilla behavior).
        /// </summary>
        public bool TryConsumePayout(Pawn a, Pawn b, out PairData data)
        {
            data = null;
            if (a == null || b == null) return false;

            long key = MakePairKey(a, b);

            PairData found;
            if (!allowedLovinPairs.TryGetValue(key, out found))
                return false;

            allowedLovinPairs.Remove(key);
            data = found;
            return true;
        }

        private void CleanupExpired()
        {
            if (Find.TickManager == null) return;
            if (allowedLovinPairs == null || allowedLovinPairs.Count == 0) return;

            int now = Find.TickManager.TicksGame;
            List<long> toRemove = null;

            foreach (KeyValuePair<long, PairData> kv in allowedLovinPairs)
            {
                if (kv.Value == null || now > kv.Value.expireTick)
                {
                    if (toRemove == null) toRemove = new List<long>();
                    toRemove.Add(kv.Key);
                }
            }

            if (toRemove == null) return;

            for (int i = 0; i < toRemove.Count; i++)
                allowedLovinPairs.Remove(toRemove[i]);
        }

        private static long MakePairKey(Pawn a, Pawn b)
        {
            int id1 = a.thingIDNumber;
            int id2 = b.thingIDNumber;

            int min = id1 < id2 ? id1 : id2;
            int max = id1 < id2 ? id2 : id1;

            return ((long)min << 32) | (uint)max;
        }

        public static GameComponent_Companionship GetOrCreate()
        {
            if (Current.Game == null) return null;

            GameComponent_Companionship comp = Current.Game.GetComponent<GameComponent_Companionship>();
            if (comp != null) return comp;

            comp = new GameComponent_Companionship(Current.Game);
            Current.Game.components.Add(comp);
            return comp;
        }
    }
}
