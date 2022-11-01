using static System.Console;
using MultiprocessorSimulator;
using static MultiprocessorSimulator.Config;
using static MultiprocessorSimulator.Util;
using static MultiprocessorSimulator.Logging;

WriteLine("\nCACHE-SIMULATOR\n");
Reset();
string? opt;
do
{
    Write("(1) Acesso manual" +
        "\n(2) Acessos aleatórios" +
        "\n(3) Visualizar memória e unidades de cache" +
        "\n(4) Resetar unidades de cache e memória" +
        "\n(0) Sair" +
        "\n>>> ");
    opt = ReadLine();
    switch (opt)
    {
        case "1":
            ManualAccess();
            break;
        case "2":
            RandomAccesses();
            break;
        case "3":
            Visualize();
            break;
        case "4":
            Reset();
            break;
    }
} while (opt != "0");

void ManualAccess()
{
    string? cont;
    do
    {
        AskNumber(out int unit, $"Unidade de cache para acessar (MAX={CACHE_UNITS - 1})", unit => unit >= 0 && unit < CACHE_UNITS);
        AskNumber(out int tag, $"Posição na memória para acessar (MAX={TAGS - 1})", tag => tag >= 0 && tag < TAGS);
        AskNumber(out int offset, $"Palavra do bloco para acessar (MAX={BLOCK_WORDS - 1})", offset => offset >= 0 && offset < BLOCK_WORDS);

        Cache c = Cache.CacheUnits[unit];
        ReadAddress ra = new ReadAddress(tag, offset);

        int newValue = -1;
        bool write = false;
        do
        {
            Write("Novo valor ou <enter> para apenas leitura >>> ");
            write = int.TryParse(ReadLine(), out newValue);
            if (newValue < 0)
            {
                WriteLine("Número inválido");
            }
        } while (write && newValue < 0);

        int result;
        if (write)
            result = c.Write(ra, newValue);
        else
            result = c.Read(ra);
        WriteLine($"Valor da palavra acessada: {result}");

        Write("Realizar mais um acesso manual? (S) >>> ");
        cont = ReadLine();
    } while (cont != null && cont.ToUpper() == "S");
    return;
}

void RandomAccesses()
{
    AskNumber(out int n, "Número de acessos aleatórios a realizar", n => n > 0);

    for (int i = 0; i < n; i++)
        RandomAccess();
}

void Visualize()
{
    string? cont;
    do
    {
        int tag;
        AskNumber(out int n, $"Número de linhas da memória para visualizar (MAX={TAGS})", n => n >= 0 && n <= TAGS);
        if (n < TAGS)
            AskNumber(out tag, $"Posição na memória para centralizar visualização (MAX={TAGS-1})", tag => tag >= 0 && tag <= TAGS);
        else
            tag = n/2;

        PrintMemoryLines(n, tag);
        PrintCaches();

        Write("Realizar outra visualização? (S) >>> ");
        cont = ReadLine();
    } while (cont != null && cont.ToUpper() == "S");
}

void Reset() {
    Memory.Populate();
    Cache.InitializeUnits<FIFOCache>();
    WriteLine();
}

void AskNumber(out int number, string question, Func<int, bool> condition)
{
    bool read;
    do
    {
        Write(question + " >>> ");
        read = int.TryParse(ReadLine(), out number);
        if (!read)
            WriteLine("Erro ao ler número");
        else if (!condition(number))
            WriteLine("Número inválido");
    } while (!read || !condition(number));
}