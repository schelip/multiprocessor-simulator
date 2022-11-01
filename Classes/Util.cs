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

        public int[] Words { get => (int[])_words.Clone(); }

        public Block() { }

        public Block(int[] words) { this._words = words; }

        public int Read(int offset) => _words[offset];

        public void Write(int newWord, int offset) => _words[offset] = newWord;
    }

    public static class Util
    {
        public static Random rnd = new Random();

        public static void RandomAccess()
        {
            int cn = rnd.Next(CACHE_UNITS);
            Cache c = Cache.CacheUnits[cn];
            ReadAddress ra = new ReadAddress();
            ra.Tag = rnd.Next(TAGS);
            ra.Offset = rnd.Next(BLOCK_WORDS);

            if (rnd.Next(4) == 3) {
                int newValue = rnd.Next(MAX_MEMORY_RANDOM);
                WriteLine($"ACESSO ALEATÓRIO: cache #{cn} escrevendo valor {newValue} na palavra #{ra.Offset} da tag #{ra.Tag}");
                int result = c.Write(ra, newValue);
            } else {
                WriteLine($"ACESSO ALEATÓRIO: cache #{cn} lendo palavra #{ra.Offset} da tag #{ra.Tag}");
                int result = c.Read(ra);
            }
        }
    }

    public static class Logging
    {
        public const string READ = "READ";
        public const string WRITE = "WRITE";
        public const string HIT = "HIT";
        public const string MISS = "MISS";
        public const string MEMORY_POPULATED = "Memória preenchida com valores aleatórios";
        public const string UNITS_INITIALIZED = "Inicializadas {0} unidades de cache";
        public const string SET_UPDATED = "Linha #{0} preenchida com bloco de tag #{1}";
        public const string REPLACING_SET = "Substituindo linha #{0} de tag: #{1}";
        public const string SAVING_DATA = "Salvando dados do bloco de tag #{0} na memória";
        public const string FIFO_BUFFER = "Buffer circular FIFO: {0}";
        public const string REPLACING_WORD = "Alterando palavra #{0} do bloco para valor {1}";
        public const string READ_REQUEST = "Cache #{0} emitindo evento de leitura da tag #{1}";
        public const string WRITE_REQUEST = "Cache #{0} emitindo evento de escrita da tag #{1}";
        public const string SET_STATUS_UPDATED = "Cache #{0} teve status de linha de tag #{1} alterado para {2}";
        public const string COPYING_LINE = "Copiando linha #{0} da unidade de cache #{1}";
        public const string READING_MEMORY = "Lendo memória na linha #{0}";
        public const string AVAILABLE_LINE = "Linha livre encontrada";

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
            WriteLine("T\t|Status\t|Data");
            foreach (Cache.CacheSet set in cache.Sets)
                PrintSet(set);
        }

        public static void PrintCaches()
        {
            foreach (Cache c in Cache.CacheUnits)
            {
                PrintCache(c);
                WriteLine();
            }
        }

        public static void PrintSet(Cache.CacheSet set)
        {
            Write($"{set.Tag}\t|{(char)set.Status}\t|");
            PrintBlock(set.Block);
        }

        public static void PrintBlock(Block block)
        {
            for (int i = 0; i < BLOCK_WORDS; i++)
                Write(block.Read(i) + " ");
            WriteLine();
        }

        public static void PrintInfo(string info, params object[] p) =>
            WriteLine(info, p);
    }
}