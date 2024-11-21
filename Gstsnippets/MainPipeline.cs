using Gst;
using Gst.App;
using Value = GLib.Value;

namespace GstreamerSharp;

public class MainPipeline
{
    private readonly AppSink audioAppSink; // AppSink til lyd
    private readonly Element audioMixer; // Lydmixer
    private readonly List<Pad> audioMixerPads = new(); // Lyd pads

    private readonly Element compositor; // Videokomposition

    private readonly List<Pad> compositorPads = new(); // Video pads
    private readonly AppSink videoAppSink; // AppSink til video
    private AppSrc audioTargetAppSrc;
    private AppSrc videoTargetAppSrc;


    public MainPipeline(int videoInputs, int audioInputs)
    {
        Pipeline = new Pipeline("main-pipeline");

        // Initialiser elementer
        compositor = ElementFactory.Make("compositor", "compositor");
        audioMixer = ElementFactory.Make("audiomixer", "audioMixer");
        videoAppSink = new AppSink("video-appsink");
        audioAppSink = new AppSink("audio-appsink");

        compositor.SetProperty("ignore-inactive-pads", new Value(false));

        // Konfigurer audioMixer-egenskaber
        audioMixer.SetProperty("ignore-inactive-pads", new Value(false));
        audioMixer.SetProperty("latency", new Value(1000000000)); // 1

        // Konfigurer AppSink
        videoAppSink.SetProperty("emit-signals", new Value(true));
        videoAppSink.SetProperty("caps",
            new Value(Caps.FromString("video/x-raw,format=AYUV,width=1280,height=720,framerate=24/1")));
        videoAppSink.NewSample += OnNewVideoSample;

        audioAppSink.SetProperty("emit-signals", new Value(true));
        audioAppSink.SetProperty("caps",
            new Value(Caps.FromString("audio/x-raw,format=S16LE,layout=interleaved,rate=44100,channels=2")));
        audioAppSink.NewSample += OnNewAudioSample;


        var videoscale = ElementFactory.Make("videoscale", "videoscale");
        var capsfilter = ElementFactory.Make("capsfilter", "capsfilter");
        var audioconvert = ElementFactory.Make("audioconvert", "audioconvert");
        var audioresample = ElementFactory.Make("audioresample", "audioresample");

        if (Pipeline == null || compositor == null || audioMixer == null || videoAppSink == null ||
            audioAppSink == null || videoscale == null || capsfilter == null || audioconvert == null ||
            audioresample == null)
            throw new Exception("Failed to create elements for MainPipeline.");

        // Konfigurer capsfilter for video
        capsfilter.SetProperty("caps", new Value(Caps.FromString("video/x-raw,width=1280,height=720")));

        // Tilføj elementer til pipeline
        Pipeline.Add(compositor, videoscale, capsfilter, videoAppSink, audioMixer, audioresample, audioconvert,
            audioAppSink);

        // Link videoelementer
        if (!Element.Link(compositor, videoscale) ||
            !Element.Link(videoscale, capsfilter) ||
            !Element.Link(capsfilter, videoAppSink))
            throw new Exception("Failed to link video elements.");

        // Link lydelementer
        if (!Element.Link(audioMixer, audioconvert) ||
            !Element.Link(audioconvert, audioresample) ||
            !Element.Link(audioresample, audioAppSink))
            throw new Exception("Failed to link audio elements.");
        CreateVideoInputs(videoInputs);
        CreateAudioInputs(audioInputs);
        //AddTestVideoInput();

        var bus = Pipeline.Bus;
        bus.AddSignalWatch();
        bus.Message += HandleMessage;
    }

    public Pipeline Pipeline { get; }
    public List<AppSrc> VideoAppSrcs { get; } = new();
    public List<AppSrc> AudioAppSrcs { get; } = new();

    private void AddTestVideoInput()
    {
        // Opret videotestsrc
        var videoTestSrc = ElementFactory.Make("videotestsrc", "videotestsrc-mainpipeline");
        var queue = ElementFactory.Make("queue", "queue-videotestsrc");
        var capsFilter = ElementFactory.Make("capsfilter", "capsfilter-videotestsrc");

        if (videoTestSrc == null || queue == null || capsFilter == null)
            throw new Exception("Failed to create videotestsrc, queue, or capsfilter.");

        // Konfigurer videotestsrc egenskaber
        videoTestSrc.SetProperty("is-live", new Value(true));
        videoTestSrc.SetProperty("pattern", new Value(11)); // 0 = Snow pattern

        // Konfigurer capsfilter egenskaber
        capsFilter.SetProperty("caps",
            new Value(Caps.FromString("video/x-raw,format=AYUV,width=1280,height=720,framerate=24/1")));

        // Tilføj videotestsrc, queue og capsfilter til pipeline
        Pipeline.Add(videoTestSrc, queue, capsFilter);

        // Link videotestsrc -> queue -> capsfilter
        if (!Element.Link(videoTestSrc, queue) || !Element.Link(queue, capsFilter))
            throw new Exception("Failed to link videotestsrc to queue and capsfilter.");

        // Få en sink pad fra compositor
        var sinkPad = compositor.GetRequestPad("sink_%u");
        sinkPad.SetProperty("is-live", new Value(true));

        if (sinkPad == null) throw new Exception("Failed to request sink pad for videotestsrc.");

        compositorPads.Add(sinkPad);

        // Link capsfilter -> compositor
        if (capsFilter.GetStaticPad("src").Link(sinkPad) != PadLinkReturn.Ok)
            throw new Exception("Failed to link capsfilter to compositor.");

        Console.WriteLine("Linked videotestsrc to compositor with queue and capsfilter.");
    }


    private void CreateAudioInputs(int audioInputs)
    {
        for (var i = 0; i < audioInputs; i++)
        {
            var audioAppSrc = new AppSrc($"audio-appsrc-{i}");

            audioAppSrc.SetProperty("is-live", new Value(true));
            audioAppSrc.SetProperty("do-timestamp", new Value(true));
            audioAppSrc.SetProperty("format", new Value(Format.Time));
            audioAppSrc.SetProperty("caps",
                new Value(Caps.FromString("audio/x-raw,format=F32LE,layout=interleaved,rate=44100,channels=2")));
            audioAppSrc.SetProperty("block", new Value(true));
            //audioAppSrc.SetProperty("handle-segment-change", new Value(true));

            //audioAppSrc.SetProperty("leaky-type", new Value(2));
            //audioAppSrc.SetProperty("max-time", new Value(80000000));

            Pipeline.Add(audioAppSrc);
            AudioAppSrcs.Add(audioAppSrc);

            var sinkPad = audioMixer.GetRequestPad("sink_%u");
            if (sinkPad == null) throw new Exception($"Failed to request sink pad for audio AppSrc {i}");

            audioMixerPads.Add(sinkPad);

            if (audioAppSrc.GetStaticPad("src").Link(sinkPad) != PadLinkReturn.Ok)
                throw new Exception($"Failed to link audio AppSrc {i} to audioMixer.");

            Console.WriteLine($"Linked audio AppSrc {i} to audioMixer.");
        }
    }

    private void CreateVideoInputs(int videoInputs)
    {
        for (var i = 0; i < videoInputs; i++)
        {
            var videoAppSrc = new AppSrc($"video-appsrc-{i}");
            videoAppSrc.SetProperty("is-live", new Value(true));
            videoAppSrc.SetProperty("format", new Value(Format.Time));
            videoAppSrc.SetProperty("caps",
                new Value(Caps.FromString("video/x-raw,format=AYUV,width=1280,height=720,framerate=24/1")));
            //videoAppSrc.SetProperty("do-timestamp", new Value(true));
            //videoAppSrc.SetProperty("leaky-type", new Value(2));
            //videoAppSrc.SetProperty("max-time", new Value(800000000));
            //videoAppSrc.SetProperty("max-latency", new Value(-1));

            Pipeline.Add(videoAppSrc);
            VideoAppSrcs.Add(videoAppSrc);

            var sinkPad = compositor.GetRequestPad("sink_%u");
            if (sinkPad == null) throw new Exception($"Failed to request sink pad for video AppSrc {i}");

            compositorPads.Add(sinkPad);

            if (videoAppSrc.GetStaticPad("src").Link(sinkPad) != PadLinkReturn.Ok)
                throw new Exception($"Failed to link video AppSrc {i} to compositor.");

            Console.WriteLine($"Linked video AppSrc {i} to compositor.");
        }
    }

    public void SetVideoTargetAppSrc(AppSrc videoTargetAppSrcIn)
    {
        videoTargetAppSrc = videoTargetAppSrcIn;
    }

    // set audio
    public void SetAudioTargetAppSrc(AppSrc audioTargetAppSrcIn)
    {
        audioTargetAppSrc = audioTargetAppSrcIn;
    }

    private void OnNewVideoSample(object sender, NewSampleArgs args)
    {
        var appSink = (AppSink) sender;
        var sample = appSink.PullSample();

        if (sample != null)
        {
            var buffer = sample.Buffer;

            var result = videoTargetAppSrc.PushBuffer(buffer);
            if (result != FlowReturn.Ok) Console.WriteLine($"Failed to push video buffer: {result}");

            sample.Dispose(); // Ryd op
        }
    }


    private void OnNewAudioSample(object sender, NewSampleArgs args)
    {
        var appSink = (AppSink) sender;
        var sample = appSink.PullSample();
        if (sample != null)
        {
            var buffer = sample.Buffer;
            var result = audioTargetAppSrc.PushBuffer(buffer);
            if (result != FlowReturn.Ok) Console.WriteLine($"Failed to push audio buffer: {result}");
            sample.Dispose();
        }
    }


    private void HandleMessage(object o, MessageArgs args)
    {
        var msg = args.Message;
        switch (msg.Type)
        {
            case MessageType.Error:
                msg.ParseError(out var err, out var debug);
                Console.WriteLine($"Error: {err.Message}\nDebug info: {debug}");
                break;

            case MessageType.Eos:
                Console.WriteLine("End of stream");
                break;

            case MessageType.Buffering:

                Console.WriteLine("Buffering:%");

                break;

            case MessageType.StateChanged:
                if (msg.Src is Element element)
                {
                    msg.ParseStateChanged(out var oldState, out var newState, out var pendingState);
                    Console.WriteLine(
                        $"State changed in {element.Name}: {oldState} -> {newState} (Pending: {pendingState})");
                }

                break;

            case MessageType.StreamStatus:
                msg.ParseStreamStatus(out var status, out var owner);
                Console.WriteLine($"Stream status: {status} (Owner: {owner?.Name})");
                break;

            case MessageType.Latency:
                Console.WriteLine("Latency message received. Adjusting latency...");

                var latencyQuery = Query.NewLatency();
                if (Pipeline.Query(latencyQuery))
                    Console.WriteLine("Latency query successful:");
                else
                    Console.WriteLine("Latency query failed.");
                break;

            case MessageType.Warning:
                msg.ParseWarning(out var warn, out debug);
                Console.WriteLine($"Warning: {warn.ToString()}\nDebug info: {debug}");
                break;

            case MessageType.AsyncDone:
                Console.WriteLine("Asynchronous operation completed.");
                break;

            case MessageType.Qos:
                Console.WriteLine("QoS message received.");
                break;


            default:
                Console.WriteLine($"Unhandled message: {msg.Type} (Source: {msg.Src?.Name})");
                break;
        }
    }


    public void SetAudioVolume(int channelIndex, double volume)
    {
        if (channelIndex < 0 || channelIndex >= audioMixerPads.Count)
            throw new ArgumentOutOfRangeException(nameof(channelIndex), "Invalid channel index.");

        var sinkPad = audioMixerPads[channelIndex];
        sinkPad.SetProperty("volume", new Value(volume));
        sinkPad.SetProperty("mute", new Value(false));
        Console.WriteLine($"Set volume={volume} for audio channel {channelIndex}");
    }


    public void SetAlpha(int channelIndex, double alpha)
    {
        if (channelIndex < 0 || channelIndex >= compositorPads.Count)
            throw new ArgumentOutOfRangeException(nameof(channelIndex), "Invalid channel index.");

        var sinkPad = compositorPads[channelIndex];
        sinkPad.SetProperty("alpha", new Value(alpha));
        Console.WriteLine($"Set alpha={alpha} for video channel {channelIndex}");
    }

    public void Start()
    {
        Console.WriteLine("Starting main pipeline...");
        var ret = Pipeline.SetState(State.Playing);
        if (ret != StateChangeReturn.Success && ret != StateChangeReturn.Async)
            throw new Exception($"Failed to start main pipeline: {ret}");
    }

    public void Stop()
    {
        Console.WriteLine("Stopping main pipeline...");
        Pipeline.SetState(State.Null);
    }
}