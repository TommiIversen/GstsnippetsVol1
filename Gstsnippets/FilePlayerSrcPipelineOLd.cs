using Gst;
using Gst.App;
using Value = GLib.Value;

namespace GstreamerSharp;

public class FilePlayerSrcPipelineOld
{
    private readonly AppSrc TargetAppSrc;

    public FilePlayerSrcPipelineOld(string filePath, string name, AppSrc targetAppSrc)
    {
        FilePath = filePath;
        Name = name;
        TargetAppSrc = targetAppSrc;

        if (TargetAppSrc == null)
            throw new ArgumentNullException(nameof(targetAppSrc), "Target AppSrc cannot be null.");
    }

    public Pipeline Pipeline { get; private set; }
    public string FilePath { get; }
    public string Name { get; }

    public void StartCpu()
    {
        Console.WriteLine($"Starting file player pipeline {Name} with file: {FilePath}");

        // Opret pipeline
        Pipeline = new Pipeline($"fileplayer-pipeline-{Name}");

        // Opret elementer
        var filesrc = ElementFactory.Make("filesrc", $"filesrc-{Name}");
        var decodebin = ElementFactory.Make("decodebin", $"decodebin-{Name}");
        decodebin.SetProperty("force-sw-decoders", new Value(true));

        var videoconvert = ElementFactory.Make("videoconvert", $"videoconvert-{Name}");
        var capsfilter = ElementFactory.Make("capsfilter", $"capsfilter-{Name}");
        var appsink = new AppSink($"appsink-{Name}");

        if (Pipeline == null || filesrc == null || decodebin == null || videoconvert == null || capsfilter == null ||
            appsink == null)
        {
            Console.WriteLine("Failed to create elements for file player pipeline.");
            throw new Exception($"Failed to create elements for file player pipeline {Name}.");
        }

        // Konfigurer elementer
        filesrc.SetProperty("location", new Value(FilePath));
        Console.WriteLine($"File source location set to: {FilePath}");

        appsink.SetProperty("emit-signals", new Value(true));
        appsink.SetProperty("caps", new Value(Caps.FromString("video/x-raw,format=RGBA")));

        // Bind event til decodebin for dynamisk linking
        decodebin.PadAdded += (o, args) =>
        {
            var pad = args.NewPad;
            Console.WriteLine($"Pad added in decodebin: {pad.Name}");
            var caps = pad.QueryCaps(null);
            Console.WriteLine($"Caps on pad {pad.Name}: {caps}");

            var sinkPad = videoconvert.GetStaticPad("sink");
            if (!sinkPad.IsLinked)
            {
                var result = pad.Link(sinkPad);
                if (result == PadLinkReturn.Ok)
                    Console.WriteLine($"Successfully linked decodebin pad to videoconvert: {pad.Name}");
                else
                    Console.WriteLine($"Failed to link decodebin pad to videoconvert: {result}");
            }
            else
            {
                Console.WriteLine("Sink pad already linked.");
            }
        };


        // Tilføj elementer til pipeline
        Pipeline.Add(filesrc, decodebin, videoconvert, capsfilter, appsink);
        Console.WriteLine("All elements added to pipeline.");

        // Link statiske elementer
        if (!Element.Link(filesrc, decodebin))
        {
            Console.WriteLine("Failed to link filesrc to decodebin.");
            throw new Exception("Failed to link filesrc to decodebin.");
        }

        if (!Element.Link(videoconvert, capsfilter) || !Element.Link(capsfilter, appsink))
        {
            Console.WriteLine("Failed to link elements in pipeline.");
            throw new Exception("Failed to link elements in file player pipeline.");
        }

        Console.WriteLine("All static elements linked successfully.");

        // Håndter samples fra AppSink
        appsink.NewSample += (o, args) =>
        {
            Console.WriteLine("New sample received in appsink.");
            var sample = appsink.PullSample();
            if (sample != null)
            {
                Console.WriteLine($"Sample size: {sample.Buffer.Size}");
                var buffer = sample.Buffer;

                var ret = TargetAppSrc.PushBuffer(buffer);
                Console.WriteLine(ret == FlowReturn.Ok
                    ? "Buffer successfully pushed to Target AppSrc."
                    : $"Error pushing buffer to Target AppSrc: {ret}");
                sample.Dispose();
            }
            else
            {
                Console.WriteLine("No sample data available.");
            }
        };

        // Start pipeline
        var ret = Pipeline.SetState(State.Playing);
        Console.WriteLine($"Pipeline state set to Playing: {ret}");

        if (ret != StateChangeReturn.Success && ret != StateChangeReturn.Async)
        {
            Console.WriteLine($"Failed to start file player pipeline: {ret}");
            throw new Exception($"Failed to start file player pipeline {Name}.");
        }

        Console.WriteLine("File player pipeline started successfully.");
    }


    public void StartAudio()
    {
        Console.WriteLine($"Starting file player pipeline {Name} with file: {FilePath}");

        Pipeline = new Pipeline($"fileplayer-pipeline-{Name}");

        var filesrc = ElementFactory.Make("filesrc", $"filesrc-{Name}");
        var decodebin = ElementFactory.Make("decodebin", $"decodebin-{Name}");
        var d3d11convert = ElementFactory.Make("d3d11convert", $"d3d11convert-{Name}");
        var d3d11download = ElementFactory.Make("d3d11download", $"d3d11download-{Name}");
        var capsfilter = ElementFactory.Make("capsfilter", $"capsfilter-{Name}");
        var appsink = new AppSink($"appsink-{Name}");
        appsink.SetProperty("sync", new Value(false));


        if (Pipeline == null || filesrc == null || decodebin == null || d3d11convert == null || d3d11download == null ||
            capsfilter == null ||
            appsink == null) throw new Exception($"Failed to create elements for file player pipeline {Name}.");

        filesrc.SetProperty("location", new Value(FilePath));

        capsfilter.SetProperty("caps", new Value(Caps.FromString("video/x-raw,format=AYUV,width=1280,height=720")));

        appsink.SetProperty("emit-signals", new Value(true));

        decodebin.PadAdded += (o, args) =>
        {
            var pad = args.NewPad;
            Console.WriteLine($"Pad added in decodebin: {pad.Name}");

            var sinkPad = d3d11convert.GetStaticPad("sink");
            if (!sinkPad.IsLinked)
            {
                var result = pad.Link(sinkPad);
                if (result != PadLinkReturn.Ok) Console.WriteLine($"Failed to link decodebin pad: {result}");
            }
            else
            {
                Console.WriteLine("Sink pad already linked.");
            }
        };

        Pipeline.Add(filesrc, decodebin, d3d11convert, d3d11download, capsfilter, appsink);

        if (!Element.Link(filesrc, decodebin)) throw new Exception("Failed to link filesrc to decodebin.");

        if (!Element.Link(d3d11convert, d3d11download) || !Element.Link(d3d11download, capsfilter) ||
            !Element.Link(capsfilter, appsink)) throw new Exception("Failed to link elements in file player pipeline.");

        appsink.NewSample += (o, args) =>
        {
            var sample = appsink.PullSample();
            if (sample != null)
            {
                Console.WriteLine($"New sample received in appsink: {sample.Buffer.Size}");
                var buffer = sample.Buffer;
                var ret = TargetAppSrc.PushBuffer(buffer);
                if (ret != FlowReturn.Ok) Console.WriteLine($"Error pushing buffer to Target AppSrc in {Name}: {ret}");
                sample.Dispose();
            }
        };

        var ret = Pipeline.SetState(State.Playing);
        if (ret != StateChangeReturn.Success && ret != StateChangeReturn.Async)
            Console.WriteLine($"Failed to start file player pipeline {Name}: {ret}");
    }


    public void Start()
    {
        Console.WriteLine($"Starting file player pipeline {Name} with file: {FilePath}");

        Pipeline = new Pipeline($"fileplayer-pipeline-{Name}");

        var filesrc = ElementFactory.Make("filesrc", $"filesrc-{Name}");
        var decodebin = ElementFactory.Make("decodebin", $"decodebin-{Name}");
        var d3d11convert = ElementFactory.Make("d3d11convert", $"d3d11convert-{Name}");
        var d3d11download = ElementFactory.Make("d3d11download", $"d3d11download-{Name}");
        var capsfilter = ElementFactory.Make("capsfilter", $"capsfilter-{Name}");
        var videoAppsink = new AppSink($"video-appsink-{Name}");
        var audioAppsink = new AppSink($"audio-appsink-{Name}");

        if (Pipeline == null || filesrc == null || decodebin == null || d3d11convert == null || d3d11download == null ||
            capsfilter == null || videoAppsink == null ||
            audioAppsink == null) throw new Exception($"Failed to create elements for file player pipeline {Name}.");

        // Konfigurer video
        filesrc.SetProperty("location", new Value(FilePath));
        capsfilter.SetProperty("caps", new Value(Caps.FromString("video/x-raw,format=AYUV,width=1280,height=720")));
        videoAppsink.SetProperty("emit-signals", new Value(true));

        // Konfigurer audio
        audioAppsink.SetProperty("emit-signals", new Value(true));
        audioAppsink.SetProperty("caps", new Value(Caps.FromString("audio/x-raw,format=S16LE,layout=interleaved")));

        decodebin.PadAdded += (o, args) =>
        {
            var pad = args.NewPad;
            Console.WriteLine($"Pad added in decodebin: {pad.Name}");
            var caps = pad.QueryCaps(null);
            Console.WriteLine($"Caps on pad {pad.Name}: {caps}");

            if (caps.ToString().Contains("video/x-raw"))
            {
                Console.WriteLine("Linking video pad.");
                var sinkPad = d3d11convert.GetStaticPad("sink");
                if (!sinkPad.IsLinked)
                {
                    var result = pad.Link(sinkPad);
                    if (result == PadLinkReturn.Ok)
                        Console.WriteLine($"Successfully linked video pad: {pad.Name}");
                    else
                        Console.WriteLine($"Failed to link video pad: {result}");
                }
                else
                {
                    Console.WriteLine("Video sink pad already linked.");
                }
            }
            else if (caps.ToString().Contains("audio/x-raw"))
            {
                Console.WriteLine("Linking audio pad.");
                var sinkPad = audioAppsink.GetStaticPad("sink");
                if (sinkPad != null && !sinkPad.IsLinked)
                {
                    var result = pad.Link(sinkPad);
                    if (result == PadLinkReturn.Ok)
                        Console.WriteLine($"Successfully linked audio pad: {pad.Name}");
                    else
                        Console.WriteLine($"Failed to link audio pad: {result}");
                }
            }
            else
            {
                Console.WriteLine("Skipping non-audio and non-video pad.");
            }
        };

        Pipeline.Add(filesrc, decodebin, d3d11convert, d3d11download, capsfilter, videoAppsink, audioAppsink);

        if (!Element.Link(filesrc, decodebin)) throw new Exception("Failed to link filesrc to decodebin.");

        if (!Element.Link(d3d11convert, d3d11download) || !Element.Link(d3d11download, capsfilter) ||
            !Element.Link(capsfilter, videoAppsink)) throw new Exception("Failed to link elements in video path.");

        // Håndter samples fra video appsink
        videoAppsink.NewSample += (o, args) =>
        {
            Console.WriteLine("New video sample received.");
            var sample = videoAppsink.PullSample();
            if (sample != null)
            {
                var buffer = sample.Buffer;
                var ret = TargetAppSrc.PushBuffer(buffer);
                if (ret != FlowReturn.Ok) Console.WriteLine($"Error pushing video buffer to Target AppSrc: {ret}");
                sample.Dispose();
            }
        };

        // Håndter samples fra audio appsink
        audioAppsink.NewSample += (o, args) =>
        {
            Console.WriteLine("New audio sample received.");
            var sample = audioAppsink.PullSample();
            if (sample != null)
            {
                var buffer = sample.Buffer;
                var ret = TargetAppSrc.PushBuffer(buffer);
                if (ret != FlowReturn.Ok) Console.WriteLine($"Error pushing audio buffer to Target AppSrc: {ret}");
                sample.Dispose();
            }
        };

        var ret = Pipeline.SetState(State.Playing);
        if (ret != StateChangeReturn.Success && ret != StateChangeReturn.Async)
            Console.WriteLine($"Failed to start file player pipeline {Name}: {ret}");
    }


    public void Stop()
    {
        Console.WriteLine($"Stopping file player pipeline {Name}...");
        Pipeline?.SetState(State.Null);
    }
}