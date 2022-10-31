using static MultiprocessorSimulator.Config;
using static System.Console;

namespace MultiprocessorSimulator
{
    public struct ReadAddress
    {
        public int Tag { get; set; }
        public int Offset { get; set; }
        public ReadAddress(int tag, int offset) {
            this.Tag = tag;
            this.Offset = offset;
        }
    }

    public struct Block
    {
        private int[] _words = new int[BLOCK_WORDS];

        public Block() { }

        public int Read(int offset) => _words[offset];

        public void Write(int newWord, int offset) => _words[offset] = newWord;
    }

    public static class Util
    {
        public static Random rnd = new Random();

        public static void RandomAccess(Cache c)
        {
            ReadAddress ra = new ReadAddress();
            ra.Tag = rnd.Next(TAGS);
            ra.Offset = rnd.Next(BLOCK_WORDS);

            if (rnd.Next(4) == 3) {
                int newValue = rnd.Next(MAX_MEMORY_RANDOM);
                int result = c.ReadModify(ra, newValue);
            } else {
                int result = c.Read(ra);
            }
        }
    }

    public static class Logging
    {
        public const string MEMORY_POPULATED = "Memória preenchida com valores aleatórios";
        public const string SET_UPDATED = "Linha #{0} preenchida com bloco de tag {1}";
        public const string REPLACING_SET = "Substituindo linha #{0} de tag: #{1}";
        public const string SAVING_DATA = "Salvando dados do bloco de tag {0} na memória";
        public const string FIFO_BUFFER = "Buffer circular FIFO: {0}";
        public const string REPLACING_WORD = "Alterando palavra #{0} do bloco para valor {1}";
        public const string HIT = "Hit";
        public const string MISS = "Miss";

        public static void PrintMemoryLines(int n, int tag) {
            int start = Math.Max(tag - n / 2, 0),
                end = Math.Min(tag + n / 2, TAGS - 1);
            WriteLine("\nMemória:");
            if (start != 0) WriteLine("...");
            for (int i = start; i <= end; i++)
            {
                Write("#" + i + "|");
                PrintBlock(Memory.Read(i));
            }
            if (end != TAGS - 1) WriteLine("...");

        }

        public static void PrintCache(Cache cache)
        {
            WriteLine("T\t|M\t|Data");
            foreach (Cache.CacheSet set in cache.Sets)
                PrintSet(set);
        }

        public static void PrintSet(Cache.CacheSet set)
        {
            Write($"{set.Tag}\t|{set.Modified}\t|");
            PrintBlock(set.Block);
        }

        public static void PrintBlock(Block block)
        {
            for (int i = 0; i < BLOCK_WORDS; i++)
                Write(block.Read(i) + " ");
            Write("\n");
        }

        public static void PrintInfo(string info, params object[] p) =>
            WriteLine(info, p);
    }
}