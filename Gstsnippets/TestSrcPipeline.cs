using Gst;
using Gst.App;
using Constants = Gst.Constants;
using Value = GLib.Value;

namespace GstreamerSharp;

public class TestSrcPipeline
{
    private readonly AppSrc AudioTargetAppSrc;
    private readonly string Name;
    private readonly int VideoPattern;
    private readonly AppSrc VideoTargetAppSrc;

    public TestSrcPipeline(int videoPattern, string name, AppSrc videoTargetAppSrc, AppSrc audioTargetAppSrc)
    {
        VideoPattern = videoPattern;
        Name = name;
        VideoTargetAppSrc = videoTargetAppSrc ?? throw new ArgumentNullException(nameof(videoTargetAppSrc));
        AudioTargetAppSrc = audioTargetAppSrc ?? throw new ArgumentNullException(nameof(audioTargetAppSrc));
        Prerool();
    }

    public Pipeline Pipeline { get; private set; }

    public bool NeedDaa { get; set; }

    public void Prerool()
    {
        Console.WriteLine($"Starting test source pipeline {Name}...");

        Pipeline = new Pipeline($"testsrc-pipeline-{Name}");

        // Videoelementer
        var videotestsrc = ElementFactory.Make("videotestsrc", $"videotestsrc-{Name}");
        var videoconvert = ElementFactory.Make("videoconvert", $"videoconvert-{Name}");
        var videoAppsink = new AppSink($"video-appsink-{Name}");

        // Lydelementer
        var audiotestsrc = ElementFactory.Make("audiotestsrc", $"audiotestsrc-{Name}");
        var audioconvert = ElementFactory.Make("audioconvert", $"audioconvert-{Name}");
        var audioAppsink = new AppSink($"audio-appsink-{Name}");

        if (Pipeline == null || videotestsrc == null || videoconvert == null || videoAppsink == null ||
            audiotestsrc == null || audioconvert == null || audioAppsink == null)
            throw new Exception("Failed to create elements for TestSrcPipeline.");

        // Konfigurer videotestsrc
        videotestsrc.SetProperty("pattern", new Value(VideoPattern));
        videotestsrc.SetProperty("is-live", new Value(true));

        videoAppsink.SetProperty("emit-signals", new Value(true));
        videoAppsink.SetProperty("caps",
            new Value(Caps.FromString("video/x-raw,format=AYUV,width=1280,height=720,framerate=24/1")));
        videoAppsink.SetProperty("sync", new Value(true));
        videoAppsink.SetProperty("emit-signals", new Value(true));


        // Konfigurer audiotestsrc
        audiotestsrc.SetProperty("wave", new Value(0));
        audiotestsrc.SetProperty("freq", new Value(440));
        audiotestsrc.SetProperty("is-live", new Value(true));

        audioAppsink.SetProperty("emit-signals", new Value(true));
        audioAppsink.SetProperty("caps",
            new Value(Caps.FromString("audio/x-raw,format=F32LE,layout=interleaved,rate=44100,channels=2")));
        audioAppsink.SetProperty("sync", new Value(true));
        audioAppsink.SetProperty("wait-on-eos", new Value(false));
        audioAppsink.SetProperty("emit-signals", new Value(true));

        VideoTargetAppSrc.SetProperty("do-timestamp", new Value(true));
        AudioTargetAppSrc.SetProperty("do-timestamp", new Value(true));

        ulong currentTimestamp = 0;
        var frameDuration = (ulong) Constants.SECOND / 24; // For 24 fps

        // Video appsink event
        videoAppsink.NewSample += (o, args) => { };

        //AudioTargetAppSrc.DoTimestamp = true;

        VideoTargetAppSrc.NeedData += (src, size) =>
        {
            Console.WriteLine("VideoTargetAppSrc: Need data.");

            var sample = videoAppsink.PullSample();
            if (sample != null)
            {
                var buffer = sample.Buffer;
                buffer.Pts = currentTimestamp;
                buffer.Dts = currentTimestamp;
                currentTimestamp += frameDuration; // Opdater timestamp for næste buffer

                var ret = VideoTargetAppSrc.PushBuffer(buffer);
                if (ret != FlowReturn.Ok) Console.WriteLine($"Error pushing video buffer to AppSrc: {ret}");
                sample.Dispose();
            }
        };

        VideoTargetAppSrc.EnoughData += (src, remove) =>
        {
            Console.WriteLine("VideoTargetAppSrc: Enough data, stopping push.");
        };

        NeedDaa = false;
        // Opsæt signaler for AudioTargetAppSrc
        AudioTargetAppSrc.NeedData += (src, size) => { };

        AudioTargetAppSrc.EnoughData += (src, remove) =>
        {
            Console.WriteLine("----AudioTargetAppSrc: Enough data, stopping push.");
            if (NeedDaa) NeedDaa = false;
        };


        // Audio appsink event
        audioAppsink.NewSample += (o, args) =>
        {
            Console.WriteLine("AudioTargetAppSrc: Need data. Size");
            var sample = audioAppsink.TryPullSample(50000000);
            if (sample != null)
            {
                var buffer = sample.Buffer;
                var ret = AudioTargetAppSrc.PushBuffer(buffer);
                if (ret != FlowReturn.Ok)
                    Console.WriteLine($"Error pushing audio buffer to AppSrc: {ret}");
                sample.Dispose();
            }
        };

        // Tilføj elementer til pipeline
        Pipeline.Add(videotestsrc, videoconvert, videoAppsink, audiotestsrc, audioconvert, audioAppsink);

        // Link videoelementer
        if (!Element.Link(videotestsrc, videoconvert) || !Element.Link(videoconvert, videoAppsink))
            throw new Exception("Failed to link video elements.");

        // Link lydelementer
        if (!Element.Link(audiotestsrc, audioconvert) || !Element.Link(audioconvert, audioAppsink))
            throw new Exception("Failed to link audio elements.");

        // Start pipeline
        var ret = Pipeline.SetState(State.Ready);
        if (ret != StateChangeReturn.Success && ret != StateChangeReturn.Async)
            Console.WriteLine($"Failed to start test source pipeline {Name}: {ret}");
    }

    public void Stop()
    {
        Console.WriteLine($"Stopping test source pipeline {Name}...");
        Pipeline?.SetState(State.Null);
    }

    public void Start()
    {
        Pipeline.SetState(State.Playing);
    }
}

public class TestSrcPipelineNoAudio
{
    private readonly string property;
    private readonly int propertyValue;
    private readonly string sourceElement;
    private readonly AppSrc TargetAppSrc;
    public Pipeline pipeline;

    public TestSrcPipelineNoAudio(string sourceElement, string property, int propertyValue, string name,
        AppSrc targetAppSrc)
    {
        this.sourceElement = sourceElement;
        this.property = property;
        this.propertyValue = propertyValue;
        Name = name;
        TargetAppSrc = targetAppSrc;
    }

    public string Name { get; }

    public void Start()
    {
        Console.WriteLine($"Starting secondary pipeline {Name}...");

        pipeline = new Pipeline($"secondary-pipeline-{propertyValue}");
        var src = ElementFactory.Make(sourceElement, $"src-{Name}");
        var convert = ElementFactory.Make("videoconvert", $"convert-{Name}");
        var appsink = new AppSink($"appsink-{propertyValue}");

        appsink.SetProperty("sync", new Value(false));


        if (pipeline == null || src == null || convert == null || appsink == null)
            throw new Exception($"Secondary pipeline {propertyValue} elements could not be created.");

        // Konfigurer src
        src.SetProperty("is-live", new Value(true)); // Sæt src til live
        src.SetProperty(property, new Value(propertyValue));

        // Konfigurer AppSink
        appsink.SetProperty("emit-signals", new Value(true));
        appsink.SetProperty("caps",
            new Value(Caps.FromString("video/x-raw,format=AYUV,width=1280,height=720,framerate=24/1")));

        appsink.NewSample += (o, args) =>
        {
            var sample = appsink.PullSample();
            if (sample != null)
            {
                var buffer = sample.Buffer;
                //Console.WriteLine($"Buffer from secondary pipeline {propertyValue} size: {buffer.Size}");

                // Push buffer til eksisterende AppSrc
                var ret = TargetAppSrc.PushBuffer(buffer);
                if (ret != FlowReturn.Ok) Console.WriteLine($"Error pushing buffer to AppSrc: {ret}");

                sample.Dispose();
            }
        };

        // Tilføj elementer
        pipeline.Add(src, convert, appsink);

        // Link elementer
        if (!Element.Link(src, convert) || !Element.Link(convert, appsink))
        {
            Console.WriteLine($"Failed to link elements in secondary pipeline {propertyValue}.");
            throw new Exception($"Secondary pipeline {propertyValue} elements could not be linked.");
        }

        // Start pipeline
        var ret = pipeline.SetState(State.Playing);
        if (ret != StateChangeReturn.Success && ret != StateChangeReturn.Async)
            Console.WriteLine($"Failed to start secondary pipeline {propertyValue}: {ret}");
    }

    public void Stop()
    {
        Console.WriteLine($"Stopping secondary pipeline {propertyValue}...");
        pipeline?.SetState(State.Null);
    }
}