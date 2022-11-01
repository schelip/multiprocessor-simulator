using static MultiprocessorSimulator.Config;
using static MultiprocessorSimulator.Util;

namespace MultiprocessorSimulator
{
    public static class Memory
    {
        private static Block[] _memory = new Block[TAGS];

        public static void Populate()
        {
            for (int i = 0; i < TAGS; i++) {
                Block block = new Block();
                _memory[i] = block;
                for (int j = 0; j < BLOCK_WORDS; j++)
                    block.Write(rnd.Next(MAX_MEMORY_RANDOM), j);
            }
            Logging.PrintInfo(Logging.MEMORY_POPULATED);
        }

        public static void Write(int tag, Block block) =>
            _memory[tag] = new Block(block.Words);

        public static Block Read(int tag) =>
            new Block(_memory[tag].Words);
    }
}