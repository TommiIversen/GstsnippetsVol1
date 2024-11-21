using Gst;
using Gst.App;
using DateTime = System.DateTime;
using Value = GLib.Value;

namespace AppsrcAppSink1;

public class RecordVideo
{
    private readonly Pipeline _pipeline;
    private readonly AppSink _appSink;
    private readonly AppSrc _appSrc = new AppSrc("record-appsrc");
    private bool _isRecording;
    private Element filesink;

    public RecordVideo(AppSink appSink)
    {
        _appSink = appSink;
        _appSink["max-time"] = 4000000;
        _appSink["drop"] = true;
        _pipeline = new Pipeline("record-pipeline");

        var queue = ElementFactory.Make("queue", "record-queue");
        var encoder = ElementFactory.Make("x264enc", "encoder");
        var muxer = ElementFactory.Make("mp4mux", "muxer");
        filesink = ElementFactory.Make("filesink", "filesink");

        // Sæt filnavnet til nuværende tid

        if (_appSrc == null || queue == null || encoder == null || muxer == null || filesink == null)
        {
            throw new Exception("Fejl: Kunne ikke oprette elementer til optager.");
        }

        _pipeline.Add(_appSrc, queue, encoder, muxer, filesink);
        if (!Element.Link(_appSrc, queue, encoder, muxer, filesink))
        {
            throw new Exception("Fejl: Kunne ikke linke elementer i optager.");
        }

        //_appSink.NewSample += OnNewSample;

        // Opsæt AppSrc
        _appSrc.Caps = Caps.FromString("video/x-raw,format=I420,width=960,height=240,framerate=30/1");
        //_appSrc.Block = true;
        //_appSrc.IsLive = true;
        _appSrc.Format = Format.Time;

        _isRecording = false;
    }

    private void OnNewSample(object sender, GLib.SignalArgs args)
    {
        try
        {
            
            var sample = _appSink.PullSample();
            if (!_isRecording)
            {
                Console.WriteLine("Optager er stoppet. Ignorerer sample.");
                sample.Dispose();
                return;
            }

            Console.WriteLine("Optager sample...");
            if (sample != null)
            {
                var buffer = sample.Buffer;
                var ret = _appSrc.PushBuffer(buffer);

                if (ret != FlowReturn.Ok)
                {
                    Console.WriteLine($"Fejl ved push af buffer til appsrc: {ret}");
                }

                sample.Dispose();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fejl i NewSample: {ex.Message}");
        }
    }

    public void Start()
    {
        var fileName = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.mp4";
        filesink.SetProperty("location", new Value(fileName));

        _pipeline.SetState(State.Playing);
        _isRecording = true;
        _appSink.NewSample += OnNewSample;
        
        
        
        Console.WriteLine("Optager startet...");
    }

    public void Stop()
    {
        try
        {
            Console.WriteLine("Stopper optager...");
            _isRecording = false;
            _appSink.NewSample -= OnNewSample;
            
            // Send end-of-stream for at lukke filen korrekt
            Console.WriteLine("Sender EOS...");
            _appSrc.EndOfStream();

            // Vent på at pipeline afslutter sig selv
            _pipeline.Bus.TimedPopFiltered (Gst.Constants.SECOND*5, MessageType.Error | MessageType.Eos);
            
            // Sæt pipeline til Null og ryd op
            _pipeline.SetState(State.Null);
            Console.WriteLine("Optager stoppet og filen er korrekt lukket.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fejl ved stop af optager: {ex.Message}");
        }
    }
}