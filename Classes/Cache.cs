using static MultiprocessorSimulator.Config;
using static MultiprocessorSimulator.Logging;

namespace MultiprocessorSimulator
{
    /// <summary>
    /// Classe abstrata de cache, sem algoritmo de substituição definido
    /// </summary>
    public abstract class Cache
    {
        /// <summary>
        /// Vetor com as instâncias das unidades de cache
        /// </summary>
        public static Cache[] CacheUnits = new Cache[CACHE_UNITS];

        public enum MESI
        {
            MODIFIED = 'M',
            EXCLUSIVE = 'E',
            SHARED = 'S',
            INVALID = 'I'
        }

        /// <summary>
        /// Eventos disparados durante a leitura e escrita, para gerenciamento do MESI
        /// </summary>
        /// <param name="tag">Identificador do bloco na memória sendo lido/alterado</param>
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

            // Inscrição de todas as caches aos eventos de todas as outras unidades
            foreach (Cache c in Cache.CacheUnits.Where(c => c != null && c != this))
            {
                c.ReadEvent += HandleReadEvent;
                c.WriteEvent += HandleWriteEvent;
                ReadEvent += c.HandleReadEvent;
                WriteEvent += c.HandleWriteEvent;
            }
        }

        /// <summary>
        /// Instancia as unidades de uma subclasse específica de cache
        /// </summary>
        /// <typeparam name="T">Classe concreta implementando o algoritmo</typeparam>
        public static void InitializeUnits<T>() where T : Cache, new()
        {
            for (int i = 0; i < CACHE_UNITS; i++)
                Cache.CacheUnits[i] = new T();
            PrintInfo(UNITS_INITIALIZED, CACHE_UNITS);
        }

        /// <summary>
        /// Algortimo de substituição
        /// </summary>
        /// <param name="target">Referência à linha que deve ser substituída</param>
        /// <returns>Índice da linha que deve ser substituída</returns>
        protected abstract int GetReplacementSet(out CacheSet target);

        /// <summary>
        /// Encontra linha não utilizada (inválida) ou utiliza algoritmo de substituição
        /// </summary>
        /// <returns>Índice da linha que será utilizada</returns>
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

        /// <summary>
        /// Salva dados de uma linha na memória
        /// </summary>
        /// <param name="set">Linha a ser salva</param>
        protected void WriteBack(ref CacheSet set)
        {
            PrintInfo(SAVING_DATA, set.Tag);
            PrintMemoryLines(5, set.Tag);
            Memory.Write(set.Tag, set.Block);
            PrintMemoryLines(5, set.Tag);
        }

        /// <summary>
        /// Altera dados de uma linha com os dados de uma nova posição
        /// </summary>
        /// <param name="idx">índice da linha a ser alterada</param>
        /// <param name="ra">ReadAddress da palavra a ser acessada</param>
        /// <param name="set">Referência da linha a ser alterada</param>
        /// <returns>Se foi feito um acesso a memória</returns>
        protected bool UpdateSet(int idx, ReadAddress ra, out CacheSet set)
        {
            set = this._sets[idx];

            // Se os dados que serão substituídos foram modificados, salvá-los
            if (set.Status == MESI.MODIFIED)
                WriteBack(ref set);

            set.Tag = ra.Tag;
            Block? block = CheckExclusive(this, set);
            // Se alguma outra unidade de cache tem o mesmo bloco, copiá-lo
            if (block.HasValue) {
                set.Block = block.Value;
                return false;
            } else {
            // Se não, acessar a memória
                PrintInfo(READING_MEMORY, ra.Tag);
                PrintMemoryLines(5, ra.Tag);
                set.Block = Memory.Read(ra.Tag);
                return true;
            }
        }

        /// <summary>
        /// Realiza as operações necessárias para trazer os dados de um bloco para a cache
        /// </summary>
        /// <param name="ra">ReadAddress da palavra a ser acessada</param>
        /// <param name="target">Referência a linha com a palavra acessada</param>
        /// <param name="hit">Se ocorreu um HIT ou MISS</param>
        /// <param name="exclusive">Se foi utilizada uma cópia da linha de outra cache</param>
        protected void Access(ReadAddress ra, out CacheSet target, out bool hit, out bool exclusive)
        {
            exclusive = true;
            for (int i = 0; i < _sets.Length; i++)
            {
                CacheSet set = _sets[i];
                // Se o bloco está presente em uma linha válida, HIT
                if (set.Tag == ra.Tag && set.Status != MESI.INVALID)
                {
                    PrintInfo(HIT);
                    target = set;
                    hit = true;
                    return;
                }
            }
            // Se não, MISS
            PrintInfo(MISS);
            hit = false;

            // Emite evento de leitura
            CacheEvent? readEvent = ReadEvent;
            if (readEvent != null) {
                PrintInfo(READ_REQUEST, GetCacheUnitIndex(), ra.Tag);
                readEvent(ra.Tag);
            }

            int idx = GetTargetSet();
            exclusive = UpdateSet(idx, ra, out target);
            
            PrintInfo(SET_UPDATED, idx, ra.Tag);

            return;
        }

        /// <summary>
        /// Lê certa palavra para a cache
        /// </summary>
        /// <param name="ra">ReadAddress da palavra a ser lida</param>
        /// <returns>Valor da palavra lida</returns>
        public int Read(ReadAddress ra)
        {
            PrintInfo(READ);
            Access(ra, out CacheSet set, out bool hit, out bool exclusive);

            // Se não estava presente, alterar status para EXCLUSIVE se não o bloco não é acessado
            // por nenhuma outra unidade; caso contrário, para SHARED
            if (!hit)
                ChangeSetStatus(set, exclusive ? MESI.EXCLUSIVE : MESI.SHARED);
                
            PrintCaches();

            return set.Block.Read(ra.Offset);
        }

        /// <summary>
        /// Lê e altera certa palavra na cache
        /// </summary>
        /// <param name="ra">ReadAddress da palavra a ser alterada</param>
        /// <param name="newValue">Novo valor para a palavra</param>
        /// <returns>Valor da palavra após alteração</returns>
        public int Write(ReadAddress ra, int newValue)
        {
            PrintInfo(WRITE);
            Access(ra, out CacheSet set, out bool hit, out bool _);
            set.Block.Write(newValue, ra.Offset);
            PrintInfo(REPLACING_WORD, ra.Offset, newValue);

            // Se não estava presente ou alguma outra unidade tem uma cópia do mesmo dado,
            // emite evento de escrita
            if (!hit || set.Status == MESI.SHARED)
            {
                CacheEvent? writeEvent = WriteEvent;
                if (writeEvent != null)
                {
                    PrintInfo(WRITE_REQUEST, GetCacheUnitIndex(), set.Tag);
                    writeEvent(set.Tag);
                }
            }
            
            // Marca linha como modificada, status MODIFIED
            ChangeSetStatus(set, MESI.MODIFIED);

            PrintCaches();
            
            return set.Block.Read(ra.Offset);
        }

        /// <summary>
        /// Helper para encontrar índice da cache na lista de unidades
        /// </summary>
        /// <returns>índice da cache</returns>
        public int GetCacheUnitIndex() => Cache.CacheUnits.ToList().IndexOf(this);

        /// <summary>
        /// Helper para alterar status de uma linha da cache
        /// </summary>
        /// <param name="set">Linha a ter status alterado</param>
        /// <param name="status">Novo status para a linha</param>
        public void ChangeSetStatus(CacheSet set, MESI status)
        {
            if (set.Status != status)
            {
                set.Status = status;
                PrintInfo(SET_STATUS_UPDATED, GetCacheUnitIndex(), set.Tag, (char)status);
            }
        }

        /// <summary>
        /// Verifica se a linha existe em outra unidade de cache
        /// </summary>
        /// <param name="cache">Cache que deseja acessar uma cópia</param>
        /// <param name="set">Linha que será procurada</param>
        /// <returns>Dados da linha, caso exista, ou null</returns>
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

        /// <summary>
        /// Verifica se ocorreu um snoop read, ou seja, se outra cache leu os dados de um bloco
        /// presente nessa unidade
        /// </summary>
        /// <param name="tag">Identificador do bloco lido</param>
        public void HandleReadEvent(int tag)
        {
            for (int i = 0; i < this._sets.Length; i++)
            {
                CacheSet set = this._sets[i];
                if (set.Tag == tag)
                {
                    // Se havia uma cópia modificada, salvar modificações (dirty writeback)
                    if (set.Status == MESI.MODIFIED)
                        WriteBack(ref set);
                    // Se os dados estão válidos, marcar como SHARED
                    if (set.Status != MESI.INVALID)
                        ChangeSetStatus(set, MESI.SHARED);
                }
            }
        }

        /// <summary>
        /// Verifica se ocooreu um snoop write, ou seja, se outra cache alterou os dados de um bloco
        /// presente nessa unidade
        /// </summary>
        /// <param name="tag">Identificador do bloco lido na memória</param>
        public void HandleWriteEvent(int tag)
        {
            for (int i = 0; i < this._sets.Length; i++)
            {
                CacheSet set = this._sets[i];
                if (set.Tag == tag)
                {
                    // Se havia uma cópia modificada, salvar modificações (dirty writeback)
                    if (set.Status == MESI.MODIFIED)
                        WriteBack(ref set);
                    // Marcar dados dessa unidade como INVALID, visto que estarão desatualizados
                    ChangeSetStatus(set, MESI.INVALID);
                }
            }

        }
    }

    /// <summary>
    /// Implementação concreta de cache com algoritmo de substituição FIFO utilizando buffer circular
    /// </summary>
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