using static MultiprocessorSimulator.Config;
using static MultiprocessorSimulator.Util;
using static MultiprocessorSimulator.Logging;

namespace MultiprocessorSimulator
{
    public abstract class Cache
    {
        public class CacheSet
        {
            public int Tag { get; set; } = 0;
            public Block Block { get; set; } = new Block();
            public bool Modified { get; set; } = false;
            public bool Used { get; set; } = false;

            public CacheSet() { }
        }
        protected CacheSet[] _sets = new CacheSet[CACHE_SETS];
        public CacheSet[] Sets { get => (CacheSet[])_sets.Clone(); }

        public Cache()
        {
            for (int i = 0; i < CACHE_SETS; i++)
                _sets[i] = new CacheSet();
        }

        protected abstract int GetReplacementSet(out CacheSet target);

        protected virtual int GetTargetSet(out CacheSet target)
        {
            int i;
            for (i = 0; i < this._sets.Length; i++)
                if (!this._sets[i].Used)
                {
                    target = this._sets[i];
                    return i;
                }

            i = GetReplacementSet(out target);
            PrintInfo(REPLACING_SET, i, target.Tag);
            return i;
        }

        protected void WriteBack(ref CacheSet set)
        {
            PrintMemoryLines(5, set.Tag);
            PrintInfo(SAVING_DATA, set.Tag);
            Memory.Write(set.Tag, set.Block);
            set.Modified = false;
            PrintMemoryLines(5, set.Tag);
        }

        protected void Access(ReadAddress ra, out CacheSet target)
        {
            PrintMemoryLines(5, ra.Tag);
            for (int i = 0; i < _sets.Length; i++)
            {
                CacheSet set = _sets[i];
                if (set.Used && set.Tag.Equals(ra.Tag))
                {
                    PrintInfo(HIT);
                    target = set;
                    return;
                }
            }
            PrintInfo(MISS);

            int idx = GetTargetSet(out target);
            PrintInfo(SET_UPDATED, idx, ra.Tag);

            if (target.Modified)
                WriteBack(ref target);

            target.Tag = ra.Tag;
            target.Block = Memory.Read(ra.Tag);
            target.Used = true;

            return;
        }

        public int Read(ReadAddress ra)
        {
            Access(ra, out CacheSet set);
            PrintCache(this);
            return set.Block.Read(ra.Offset);
        }

        public int ReadModify(ReadAddress ra, int newValue)
        {
            Access(ra, out CacheSet set);
            set.Block.Write(newValue, ra.Offset);
            set.Modified = true;
            PrintInfo(REPLACING_WORD, ra.Offset, newValue);
            PrintCache(this);
            return set.Block.Read(ra.Offset);
        }
    }

    public class FIFOCache : Cache
    {
        private int[] _buffer = new int[CACHE_SETS];
        int end = 0;
        int start = 0;

        private void put(int set)
        {
            _buffer[end++] = set;
            end %= CACHE_SETS;
            PrintInfo(FIFO_BUFFER, GetBufferString());
        }

        private int get()
        {
            PrintInfo(FIFO_BUFFER, GetBufferString());
            int set = _buffer[start++];
            start %= CACHE_SETS;
            return set;
        }

        protected override int GetReplacementSet(out CacheSet target)
        {
            int i = get();
            target = this._sets[i];
            return i;
        }

        protected override int GetTargetSet(out CacheSet target)
        {
            int i = base.GetTargetSet(out target);
            put(i);
            return i;
        }

        private String GetBufferString()
        {
            int i = start;
            String str = "> ";
            do
            {
                int set = _buffer[i];
                i = (i + 1) % CACHE_SETS;
                str += set + " > ";
            } while (i != end);
            return str;
        }
    }
}