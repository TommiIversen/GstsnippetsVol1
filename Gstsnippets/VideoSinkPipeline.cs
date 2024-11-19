using Gst;
using Gst.App;
using Value = GLib.Value;

namespace GstreamerSharp;

public class VideoSinkPipeline
{
    public VideoSinkPipeline(string name)
    {
        Pipeline = new Pipeline($"video-sink-pipeline-{name}");
        VideoAppSrc = new AppSrc($"video-appsrc-{name}");

        // Konfigurer AppSrc
        VideoAppSrc.SetProperty("format", new Value(Format.Time));
        VideoAppSrc.SetProperty("caps",
            new Value(Caps.FromString("video/x-raw,format=AYUV,width=1280,height=720,framerate=24/1")));

        // Opret en queue
        var videoQueue = ElementFactory.Make("queue", $"video-queue-{name}");
        if (videoQueue == null)
            throw new Exception($"Failed to create queue element for VideoSinkPipeline {name}.");

        // Opret videosink
        var videoSinkElement = ElementFactory.Make("autovideosink", $"video-sink-{name}");
        if (videoSinkElement == null)
            throw new Exception($"Failed to create video sink element for VideoSinkPipeline {name}.");

        videoSinkElement.SetProperty("sync", new Value(false)); // Midlertidigt for debugging

        if (Pipeline == null || VideoAppSrc == null)
            throw new Exception($"Failed to create elements for VideoSinkPipeline {name}.");

        // Tilføj elementer til pipelinen
        Pipeline.Add(VideoAppSrc, videoQueue, videoSinkElement);

        // Link AppSrc -> Queue -> Sink
        if (!Element.Link(VideoAppSrc, videoQueue))
            throw new Exception($"Failed to link video AppSrc to queue in VideoSinkPipeline {name}.");
        if (!Element.Link(videoQueue, videoSinkElement))
            throw new Exception($"Failed to link queue to video sink in VideoSinkPipeline {name}.");

        Pipeline.SetState(State.Ready);
    }

    public Pipeline Pipeline { get; }
    public AppSrc VideoAppSrc { get; }

    public void Start()
    {
        Console.WriteLine("Starting video sink pipeline...");
        var ret = Pipeline.SetState(State.Playing);
        if (ret != StateChangeReturn.Success && ret != StateChangeReturn.Async)
            throw new Exception($"Failed to start video sink pipeline: {ret}");
    }

    public void Stop()
    {
        Console.WriteLine("Stopping video sink pipeline...");
        Pipeline.SetState(State.Null);
        Pipeline.Dispose();
    }
}
