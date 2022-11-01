using static MultiprocessorSimulator.Config;
using static MultiprocessorSimulator.Logging;

namespace MultiprocessorSimulator
{
    public abstract class Cache
    {
        public static Cache[] CacheUnits = new Cache[CACHE_UNITS];

        public enum MESI
        {
            MODIFIED = 'M',
            EXCLUSIVE = 'E',
            SHARED = 'S',
            INVALID = 'I'
        }

        public delegate void CacheEvent(int tag);
        public event CacheEvent? ReadEvent;
        public event CacheEvent? WriteEvent;

        public class CacheSet
        {
            public int Tag { get; set; } = 0;
            public Block Block { get; set; } = new Block();
            public MESI Status { get; set; } = MESI.INVALID;

            public CacheSet() { }
        }
        protected CacheSet[] _sets = new CacheSet[CACHE_SETS];
        public CacheSet[] Sets { get => (CacheSet[])_sets.Clone(); }

        public Cache()
        {
            for (int i = 0; i < CACHE_SETS; i++)
                _sets[i] = new CacheSet();

            foreach (Cache c in Cache.CacheUnits.Where(c => c != null && c != this))
            {
                c.ReadEvent += HandleReadEvent;
                c.WriteEvent += HandleWriteEvent;
                ReadEvent += c.HandleReadEvent;
                WriteEvent += c.HandleWriteEvent;
            }
        }

        public static void InitializeUnits<T>() where T : Cache, new()
        {
            for (int i = 0; i < CACHE_UNITS; i++)
                Cache.CacheUnits[i] = new T();
            PrintInfo(UNITS_INITIALIZED, CACHE_UNITS);
        }

        protected abstract int GetReplacementSet(out CacheSet target);

        protected virtual int GetTargetSet()
        {
            CacheSet target;
            int i;
            for (i = 0; i < this._sets.Length; i++)
                if (this._sets[i].Status == MESI.INVALID)
                {
                    PrintInfo(AVAILABLE_LINE);
                    target = this._sets[i];
                    return i;
                }

            i = GetReplacementSet(out target);
            PrintInfo(REPLACING_SET, i, target.Tag);
            return i;
        }

        protected void WriteBack(ref CacheSet set)
        {
            PrintInfo(SAVING_DATA, set.Tag);
            PrintMemoryLines(5, set.Tag);
            Memory.Write(set.Tag, set.Block);
            PrintMemoryLines(5, set.Tag);
        }

        protected bool UpdateSet(int idx, ReadAddress ra, out CacheSet set)
        {
            set = this._sets[idx];

            if (set.Status == MESI.MODIFIED)
                WriteBack(ref set);

            set.Tag = ra.Tag;
            Block? block = CheckExclusive(this, set);
            if (block.HasValue) {
                set.Block = block.Value;
                return false;
            } else {
                PrintInfo(READING_MEMORY, ra.Tag);
                PrintMemoryLines(5, ra.Tag);
                set.Block = Memory.Read(ra.Tag);
                return true;
            }
        }

        protected void Access(ReadAddress ra, out CacheSet target, out bool hit, out bool exclusive)
        {
            int idx = -1;
            exclusive = true;
            for (int i = 0; i < _sets.Length; i++)
            {
                CacheSet set = _sets[i];
                if (set.Tag == ra.Tag && set.Status != MESI.INVALID)
                {
                    PrintInfo(HIT);
                    target = set;
                    hit = true;
                    return;
                }
            }
            PrintInfo(MISS);
            hit = false;

            CacheEvent? readEvent = ReadEvent;
            if (readEvent != null) {
                PrintInfo(READ_REQUEST, GetCacheUnitIndex(), ra.Tag);
                readEvent(ra.Tag);
            }

            idx = idx < 0 ? GetTargetSet() : idx;
            exclusive = UpdateSet(idx, ra, out target);
            
            PrintInfo(SET_UPDATED, idx, ra.Tag);

            return;
        }

        public int Read(ReadAddress ra)
        {
            PrintInfo(READ);
            Access(ra, out CacheSet set, out bool hit, out bool exclusive);

            if (!hit)
                ChangeSetStatus(set, exclusive ? MESI.EXCLUSIVE : MESI.SHARED);
                
            PrintCaches();

            return set.Block.Read(ra.Offset);
        }

        public int Write(ReadAddress ra, int newValue)
        {
            PrintInfo(WRITE);
            Access(ra, out CacheSet set, out bool hit, out bool _);
            set.Block.Write(newValue, ra.Offset);
            PrintInfo(REPLACING_WORD, ra.Offset, newValue);

            if (!hit || set.Status == MESI.SHARED)
            {
                CacheEvent? writeEvent = WriteEvent;
                if (writeEvent != null)
                {
                    PrintInfo(WRITE_REQUEST, GetCacheUnitIndex(), set.Tag);
                    writeEvent(set.Tag);
                }
            }
            
            ChangeSetStatus(set, MESI.MODIFIED);

            PrintCaches();
            
            return set.Block.Read(ra.Offset);
        }

        public int GetCacheUnitIndex() => Cache.CacheUnits.ToList().IndexOf(this);

        public void ChangeSetStatus(CacheSet set, MESI status)
        {
            if (set.Status != status)
            {
                set.Status = status;
                PrintInfo(SET_STATUS_UPDATED, GetCacheUnitIndex(), set.Tag, (char)status);
            }
        }

        public static Block? CheckExclusive(Cache cache, CacheSet set)
        {
            int ci = 0, si = 0;
            foreach (Cache c in Cache.CacheUnits.Where(c => c != cache))
            {
                si = 0;
                foreach (CacheSet s in c.Sets)
                {
                    if (s.Tag == set.Tag && s.Status != MESI.INVALID)
                    {
                        PrintInfo(COPYING_LINE, si, ci);
                        return new Block(s.Block.Words);
                    }
                    si++;
                }
                ci++;
            }
            return null;
        }

        public void HandleReadEvent(int tag)
        {
            for (int i = 0; i < this._sets.Length; i++)
            {
                CacheSet set = this._sets[i];
                if (set.Tag == tag)
                {
                    if (set.Status == MESI.MODIFIED)
                        WriteBack(ref set);
                    if (set.Status != MESI.INVALID)
                        ChangeSetStatus(set, MESI.SHARED);
                }
            }
        }

        public void HandleWriteEvent(int tag)
        {
            for (int i = 0; i < this._sets.Length; i++)
            {
                CacheSet set = this._sets[i];
                if (set.Tag == tag)
                {
                    if (set.Status == MESI.MODIFIED)
                        WriteBack(ref set);
                    ChangeSetStatus(set, MESI.INVALID);
                }
            }

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

        protected override int GetTargetSet()
        {
            int i = base.GetTargetSet();
            if (i != _buffer[start] || end == 0) put(i);
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