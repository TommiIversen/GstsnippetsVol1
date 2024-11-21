using Gst;
using Gst.App;
using Value = GLib.Value;

namespace GstreamerSharp;

public class AudioSinkPipeline
{
    public AudioSinkPipeline(string name)
    {
        Pipeline = new Pipeline($"audio-sink-pipeline-{name}");
        AudioAppSrc = new AppSrc($"audio-appsrc-{name}");

        // Konfigurer AppSrc
        //AudioAppSrc.SetProperty("is-live", new GLib.Value(true));
        AudioAppSrc.SetProperty("format", new Value(Format.Time));
        AudioAppSrc.SetProperty("caps",
            new Value(Caps.FromString("audio/x-raw,format=S16LE,layout=interleaved,rate=44100,channels=2")));
        AudioAppSrc.SetProperty("do-timestamp", new Value(true));
        //AudioAppSrc.SetProperty("block", new GLib.Value(true));
        //AudioAppSrc.SetProperty("handle-segment-change", new GLib.Value(true));

        // Opret nødvendige elementer
        var queue = ElementFactory.Make("queue", "queue");
        var audioConvert = ElementFactory.Make("audioconvert", "audio-convert");
        var audioResample = ElementFactory.Make("audioresample", "audio-resample");
        var audioSinkElement = ElementFactory.Make("autoaudiosink", "audio-sink");

        audioSinkElement.SetProperty("sync", new Value(false)); // Debugging formål

        if (Pipeline == null || AudioAppSrc == null || queue == null || audioConvert == null ||
            audioResample == null || audioSinkElement == null)
            throw new Exception($"Failed to create elements for AudioSinkPipeline {name}.");

        // Tilføj elementer til pipeline
        Pipeline.Add(AudioAppSrc, queue, audioConvert, audioResample, audioSinkElement);

        // Link elementerne
        if (!Element.Link(AudioAppSrc, queue) ||
            !Element.Link(queue, audioConvert) ||
            !Element.Link(audioConvert, audioResample) ||
            !Element.Link(audioResample, audioSinkElement))
            throw new Exception($"Failed to link elements in AudioSinkPipeline {name}.");

        Pipeline.SetState(State.Ready);
    }

    public Pipeline Pipeline { get; }
    public AppSrc AudioAppSrc { get; }

    public void Start()
    {
        Console.WriteLine("Starting audio sink pipeline...");
        var ret = Pipeline.SetState(State.Playing);
        if (ret != StateChangeReturn.Success && ret != StateChangeReturn.Async)
            throw new Exception($"Failed to start audio sink pipeline: {ret}");
    }

    public void Stop()
    {
        Console.WriteLine("Stopping audio sink pipeline...");
        Pipeline.SetState(State.Null);
        Pipeline.Dispose();
    }
}