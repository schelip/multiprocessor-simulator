using static System.Console;
using MultiprocessorSimulator;
using static MultiprocessorSimulator.Config;
using static MultiprocessorSimulator.Util;
using static MultiprocessorSimulator.Logging;

Cache c = new FIFOCache();
WriteLine("Cache-simulator");
Memory.Populate();
string? opt;
do
{
    Write("(1) Acesso manual" +
        "\n(2) Acessos aleatórios" +
        "\n(3) Visualizar memória" +
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
            VisualizeMemory();
            break;
        case "4":
            break;
    }
} while (opt != "0");

void ManualAccess()
{
    string? cont;
    do
    {
        AskNumber(out int tag, $"Posição na memória para acessar (MAX={TAGS - 1})", tag => tag >= 0 && tag < TAGS);
        AskNumber(out int offset, $"Palavra do bloco para acessar (MAX={BLOCK_WORDS - 1})", offset => offset >= 0 && offset < BLOCK_WORDS);

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

        if (write)
            c.ReadModify(ra, newValue);
        else
            c.Read(ra);

        Write("Realizar mais um acesso manual? (S) >>> ");
        cont = ReadLine();
    } while (cont != null && cont.ToUpper() == "S");
    return;
}

void RandomAccesses()
{
    AskNumber(out int n, "Número de acessos aleatórios a realizar", n => n > 0);

    for (int i = 0; i < n; i++)
        RandomAccess(c);
}

void VisualizeMemory()
{
    string? cont;
    do
    {
        AskNumber(out int n, $"Número de linhas para visualizar (MAX={TAGS})", n => n >= 0 && n <= TAGS);
        AskNumber(out int tag, $"Posição na memória para centralizar visualização (MAX={TAGS-1})", tag => tag >= 0 && tag <= TAGS);

        PrintMemoryLines(n, tag);
        Write("Realizar outra visualização? (S) >>> ");
        cont = ReadLine();
    } while (cont != null && cont.ToUpper() == "S");
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