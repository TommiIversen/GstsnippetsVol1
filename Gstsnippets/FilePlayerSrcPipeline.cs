using Gst;
using Gst.App;
using Constants = Gst.Constants;
using Value = GLib.Value;

namespace ConsoleApp11;

public class FilePlayerSrcPipeline
{
    private readonly AppSrc? AudioAppSrc;
    private Element? imageFreeze;
    private readonly AppSrc? VideoAppSrc;


    public FilePlayerSrcPipeline(string filePath, string name, AppSrc? videoAppSrc = null, AppSrc? audioAppSrc = null)
    {
        FilePath = filePath;
        Name = name;
        VideoAppSrc = videoAppSrc;
        AudioAppSrc = audioAppSrc;
        Preroll();
    }

    public Pipeline Pipeline { get; private set; }
    public string FilePath { get; private set; }
    public string Name { get; }

    public void Preroll()
    {
        Console.WriteLine($"Starting file player pipeline {Name} with file: {FilePath}");

        Pipeline = new Pipeline($"fileplayer-pipeline-{Name}");

        var filesrc = ElementFactory.Make("filesrc", $"filesrc-{Name}");
        var decodebin = ElementFactory.Make("decodebin", $"decodebin-{Name}");

        // Video chain
        var d3d11convert = VideoAppSrc != null ? ElementFactory.Make("d3d11convert", $"d3d11convert-{Name}") : null;
        var d3d11download = VideoAppSrc != null ? ElementFactory.Make("d3d11download", $"d3d11download-{Name}") : null;
        var videoCapsFilter =
            VideoAppSrc != null ? ElementFactory.Make("capsfilter", $"video-capsfilter-{Name}") : null;
        var videoAppsink = VideoAppSrc != null ? new AppSink($"video-appsink-{Name}") : null;

        // Audio chain
        var audioConvert = AudioAppSrc != null ? ElementFactory.Make("audioconvert", $"audioconvert-{Name}") : null;
        var audioResample = AudioAppSrc != null ? ElementFactory.Make("audioresample", $"audioresample-{Name}") : null;
        var audioCapsFilter =
            AudioAppSrc != null ? ElementFactory.Make("capsfilter", $"audio-capsfilter-{Name}") : null;
        var audioAppsink = AudioAppSrc != null ? new AppSink($"audio-appsink-{Name}") : null;

        if (Pipeline == null || filesrc == null || decodebin == null ||
            (VideoAppSrc != null && (d3d11convert == null || d3d11download == null || videoCapsFilter == null ||
                                     videoAppsink == null)) ||
            (AudioAppSrc != null && (audioConvert == null || audioResample == null || audioCapsFilter == null ||
                                     audioAppsink == null)))
            throw new Exception($"Failed to create elements for file player pipeline {Name}.");

        filesrc.SetProperty("location", new Value(FilePath));

        if (VideoAppSrc != null)
        {
            videoCapsFilter.SetProperty("caps",
                new Value(Caps.FromString("video/x-raw,format=AYUV,width=1280,height=720,framerate=24/1")));
            videoAppsink.SetProperty("emit-signals", new Value(true));
            videoAppsink.SetProperty("sync", new Value(false));
        }

        if (AudioAppSrc != null)
        {
            audioCapsFilter.SetProperty("caps",
                new Value(Caps.FromString("audio/x-raw,format=F32LE,rate=44100,channels=2,layout=interleaved")));
            audioAppsink.SetProperty("emit-signals", new Value(true));
            audioAppsink.SetProperty("sync", new Value(false));
        }

        decodebin.PadAdded += (o, args) =>
        {
            var pad = args.NewPad;
            Console.WriteLine($"Pad added in decodebin: {pad.Name}");

            if (pad.QueryCaps(null).ToString().Contains("video") && VideoAppSrc != null)
            {
                var sinkPad = d3d11convert.GetStaticPad("sink");
                if (!sinkPad.IsLinked)
                {
                    var result = pad.Link(sinkPad);
                    if (result != PadLinkReturn.Ok) Console.WriteLine($"Failed to link video pad: {result}");
                }
            }
            else if (pad.QueryCaps(null).ToString().Contains("audio") && AudioAppSrc != null)
            {
                var sinkPad = audioConvert.GetStaticPad("sink");
                if (!sinkPad.IsLinked)
                {
                    var result = pad.Link(sinkPad);
                    if (result != PadLinkReturn.Ok) Console.WriteLine($"Failed to link audio pad: {result}");
                }
            }
        };

        Pipeline.Add(filesrc, decodebin);

        // if (VideoAppSrc != null)
        // {
        //     Pipeline.Add(d3d11convert, d3d11download, videoCapsFilter, videoAppsink);
        //     if (!Element.Link(d3d11convert, d3d11download) ||
        //         !Element.Link(d3d11download, videoCapsFilter) ||
        //         !Element.Link(videoCapsFilter, videoAppsink))
        //     {
        //         throw new Exception("Failed to link video elements in file player pipeline.");
        //     }
        //
        //     videoAppsink.NewSample += (o, args) =>
        //     {
        //         var sample = videoAppsink.PullSample();
        //         if (sample != null)
        //         {
        //             Console.WriteLine($"New video sample received in appsink: {sample.Buffer.Size}");
        //             var buffer = sample.Buffer;
        //             var ret = VideoAppSrc.PushBuffer(buffer);
        //             if (ret != FlowReturn.Ok)
        //             {
        //                 Console.WriteLine($"Error pushing video buffer to VideoAppSrc in {Name}: {ret}");
        //             }
        //             sample.Dispose();
        //         }
        //     };
        // }

        if (VideoAppSrc != null)
        {
            // Opret og tilføj videorate for framerate-konvertering
            var videoRate = ElementFactory.Make("videorate", $"videorate-{Name}");
            if (videoRate == null) throw new Exception($"Failed to create videorate for pipeline {Name}.");

            // Definer caps med framerate og andre egenskaber
            videoCapsFilter.SetProperty("caps",
                new Value(Caps.FromString("video/x-raw,format=AYUV,width=1280,height=720,framerate=24/1")));

            // Tilføj elementer til pipeline
            Pipeline.Add(videoRate, d3d11convert, d3d11download, videoCapsFilter, videoAppsink);

            // Link elementerne i korrekt rækkefølge
            if (!Element.Link(videoRate, d3d11convert) ||
                !Element.Link(d3d11convert, d3d11download) ||
                !Element.Link(d3d11download, videoCapsFilter) ||
                !Element.Link(videoCapsFilter, videoAppsink))
                throw new Exception("Failed to link video elements in file player pipeline.");

            // Håndtering af videopads
            decodebin.PadAdded += (o, args) =>
            {
                var pad = args.NewPad;
                if (pad.QueryCaps(null).ToString().Contains("video"))
                {
                    var sinkPad = videoRate.GetStaticPad("sink");
                    if (!sinkPad.IsLinked)
                    {
                        var result = pad.Link(sinkPad);
                        if (result != PadLinkReturn.Ok) Console.WriteLine($"Failed to link video pad: {result}");
                    }
                }
            };

            VideoAppSrc.NeedData += (src, size) =>
            {
                //Console.WriteLine($"------ VideoFileeeeeeeeAppSrc NeedData: {size}");
                // var sample = audioAppsink.TryPullSample(50000000);
                // if (sample != null)
                // {
                //     var buffer = sample.Buffer;
                //     var ret = VideoAppSrc.PushBuffer(buffer);
                //     if (ret != FlowReturn.Ok)
                //         Console.WriteLine($"Error pushing audio buffer to AppSrc: {ret}");
                //     sample.Dispose();
                // }


            };            
            
            
            ulong currentTimestamp = 0;
            ulong frameDuration = (ulong)Constants.SECOND / 24; // For 24 fps

            // Håndtering af nye samples i appsink
            videoAppsink.NewSample += (o, args) =>
            {
                var sample = videoAppsink.PullSample();
                if (sample != null)
                {
                    var buffer = sample.Buffer;
                    // buffer.Pts = currentTimestamp;
                    // buffer.Dts = currentTimestamp;
                    // currentTimestamp += frameDuration; // Opdater timestamp for næste buffer

                    var ret = VideoAppSrc.PushBuffer(buffer);
                    if (ret != FlowReturn.Ok)
                        Console.WriteLine($"Error pushing video buffer to VideoAppSrc in {Name}: {ret}");
                    sample.Dispose();
                }
            };
        }


        if (AudioAppSrc != null)
        {
            Pipeline.Add(audioConvert, audioResample, audioCapsFilter, audioAppsink);
            if (!Element.Link(audioConvert, audioResample) ||
                !Element.Link(audioResample, audioCapsFilter) ||
                !Element.Link(audioCapsFilter, audioAppsink))
                throw new Exception("Failed to link audio elements in file player pipeline.");

            
            AudioAppSrc.NeedData += (src, size) =>
            {
                Console.WriteLine($"------ AudioFileeeeeeeeAppSrc NeedData: {size}");
                //
                // var sample = audioAppsink.TryPullSample(5000000);
                // if (sample != null)
                // {
                //     var buffer = sample.Buffer;
                //     var ret = AudioAppSrc.PushBuffer(buffer);
                //     if (ret != FlowReturn.Ok)
                //         Console.WriteLine($"Error pushing audio buffer to AudioAppSrc in {Name}: {ret}");
                //     sample.Dispose();
                // }
            };
            
            ulong audioTimestamp = 0;
            ulong audioFrameDuration = (ulong)Constants.SECOND / 44100; // For 44.1 kHz

            
            audioAppsink.NewSample += (o, args) =>
            {
                var sample = audioAppsink.PullSample();
                if (sample != null)
                {
                    var buffer = sample.Buffer;
                    
                    // buffer.Pts = audioTimestamp;
                    // buffer.Dts = audioTimestamp;
                    //
                    // audioTimestamp += audioFrameDuration;
                    var ret = AudioAppSrc.PushBuffer(buffer);
                    if (ret != FlowReturn.Ok)
                        Console.WriteLine($"Error pushing audio buffer to AudioAppSrc in {Name}: {ret}");
                    sample.Dispose();
                }
            };
        }

        if (!Element.Link(filesrc, decodebin)) throw new Exception("Failed to link filesrc to decodebin.");

        // Sæt pipeline til PAUSED eller PLAYING
        var ret = Pipeline.SetState(State.Ready);
        if (ret != StateChangeReturn.Success && ret != StateChangeReturn.Async)
            Console.WriteLine($"Failed to start test source pipeline {Name}: {ret}");
    }

    public void Pause()
    {
        Console.WriteLine($"Pausing pipeline {Name} and freezing frame...");
        // Sæt pipeline til PAUSED
        Pipeline.SetState(State.Paused);
    }

    public void Start()
    {
        Pipeline.SetState(State.Playing);
    }


    public void Stop()
    {
        if (Pipeline == null)
        {
            Console.WriteLine("Pipeline is already null.");
            return;
        }

        Console.WriteLine("Stopping file player pipeline...");

        try
        {
            // Sæt pipeline til PAUSED og derefter NULL
            Console.WriteLine("Setting pipeline state to NULL...");
            var stateChangeReturn = Pipeline.SetState(State.Null);
            if (stateChangeReturn != StateChangeReturn.Success)
                Console.WriteLine($"Warning: State change to NULL returned {stateChangeReturn}.");

            // Vent på, at pipelinen når NULL-stadiet
            State currentState;
            State pendingState;
            const ulong timeout = 2 * Constants.SECOND; // 2 sekunder i nanosekunder

            if (Pipeline.GetState(out currentState, out pendingState, timeout) != StateChangeReturn.Success)
                Console.WriteLine("Warning: Pipeline did not reach NULL state in time.");

            // Fjern alle child-elementer fra pipelinen
            Console.WriteLine("Cleaning up pipeline elements...");
            var elements = Pipeline.IterateElements();


            foreach (Element element in elements)
            {
                Console.WriteLine($"Removing element: {element.Name}");
                if (!Pipeline.Remove(element))
                {
                    Console.WriteLine($"Warning: Failed to remove element {element.Name}");
                }
                else
                {
                    element.SetState(State.Null);
                    element.Dispose(); // Frigør elementet korrekt
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while stopping pipeline: {ex.Message}");
        }
        finally
        {
            // Frigør pipeline-ressourcer
            Console.WriteLine("Disposing pipeline...");
            Pipeline.SetState(State.Null);
            Pipeline.Dispose();
            Pipeline = null;

            Console.WriteLine("Pipeline stopped and disposed.");
        }
    }

    public void LoadNewFile(string newFilePath)
    {
        Console.WriteLine($"Loading new file: {newFilePath} into pipeline {Name}...");
        Stop(); // Stop and dispose of the current pipeline
        FilePath = newFilePath; // Update the file path
        Start(); // Restart the pipeline with the new file
    }
}