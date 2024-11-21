using System.Collections.Concurrent;

namespace AppsrcAppSink1;

public class KeyboardInputHandler
{
    private readonly BlockingCollection<string> _commandQueue = new();
    private readonly Thread _inputThread;

    public KeyboardInputHandler()
    {
        _inputThread = new Thread(ListenForInput)
        {
            IsBackground = true
        };
    }

    public void Start()
    {
        _inputThread.Start();
    }

    public void Stop()
    {
        _inputThread.Interrupt();
        _inputThread.Join();
    }

    public string GetNextCommand()
    {
        return _commandQueue.Take();
    }

    private void ListenForInput()
    {
        try
        {
            while (true)
            {
                Console.WriteLine("Indtast en kommando: ");
                var key = Console.ReadKey(true).KeyChar;
                _commandQueue.Add(key.ToString());
            }
        }
        catch (ThreadInterruptedException)
        {
            // Tråden er stoppet
        }
    }
}