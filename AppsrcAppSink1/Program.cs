using System;
using Gst;
using Gst.App;
using Uri = Gst.Uri;
using Value = GLib.Value;

namespace DynamicCompositorWithTestSrc
{
    class Program
    {

        private static double playbackRate = 1.0; // Startafspilningshastighed
        private static bool isPlaying = true; // Starttilstand (afspilning)
        private static Element videoSink; // Bruges til seek-events
        
        static void Main(string[] args)
        {
            // Initialiser GStreamer
            Application.Init();

            // Opret pipeline med compositor
            var pipeline = CreatePipeline();

            // Start pipeline
            pipeline.SetState(State.Playing);

            Console.WriteLine("Tryk på 'n' for at dynamisk aktivere videotestsrc.");
            while (true)
            {
                var key = Console.ReadKey(intercept: true).Key;
                if (key == ConsoleKey.N)
                {
                    Console.WriteLine("\nAktivering af videotestsrc...");
                    StartVideoTestSource(pipeline, "appsrc-sink_1");
                    break;
                }
                else
                {
                    Console.WriteLine("\nUgyldig tast. Tryk på 'n' for at aktivere videotestsrc.");
                }
            }

            Console.WriteLine("Tryk på 'n' for at afspille video.");
            while (true)
            {
                var key = Console.ReadKey(intercept: true).Key;
                if (key == ConsoleKey.N)
                {
                    Console.WriteLine("\nAfspilning af video...");
                    PlayVideoFile(@"C:\Users\Tommi\Downloads\bun33s.mp4", pipeline, "appsrc-sink_0");
                    break;
                }
                else
                {
                    Console.WriteLine("\nUgyldig tast. Tryk på 'n' for at afspille video.");
                }
            }



            Console.WriteLine("Tryk på Enter for at afslutte.");
            Console.ReadLine();

            // Stop pipeline
            pipeline.SetState(State.Null);
            pipeline.Dispose();
        }

        private static Pipeline CreatePipeline()
        {
            var pipeline = new Pipeline("dynamic-compositor");

            // Opret compositor og autovideosink
            var compositor = ElementFactory.Make("compositor", "compositor");
            var capsfilter = ElementFactory.Make("capsfilter", "capsfilter");
            var autovideosink = ElementFactory.Make("autovideosink", "autovideosink");

            var appsrc0 = new AppSrc("appsrc-sink_0");
            var appsrc1 = new AppSrc("appsrc-sink_1");

            // Verificer elementer
            if (compositor == null || capsfilter == null || autovideosink == null || appsrc0 == null || appsrc1 == null)
            {
                Console.WriteLine("Fejl: Kunne ikke oprette elementer.");
                Environment.Exit(1);
            }
            
            // Konfigurer AppSrc
            ConfigureAppSrc(appsrc0);
            ConfigureAppSrc(appsrc1);

            // Konfigurer compositor
            compositor["background"] = 2; // Sort baggrund (0 = Transparent, 1 = Sort)

            // Håndter dynamisk oprettelse af pads
            compositor.PadAdded += (sender, args) =>
            {
                var pad = args.NewPad;
                Console.WriteLine($"Ny pad oprettet: {pad.Name}");

                // Tjek, om det er en sink pad
                if (pad.Direction == PadDirection.Sink)
                {
                    Console.WriteLine($"Ny sink pad oprettet: {pad.Name}");

                    // Indstil egenskaber baseret på pad navn
                    if (pad.Name == "sink_0")
                    {
                        pad.SetProperty("xpos", new Value(0));
                        pad.SetProperty("ypos", new Value(0));
                    }
                    else if (pad.Name == "sink_1")
                    {
                        pad.SetProperty("xpos", new Value(320));
                        pad.SetProperty("ypos", new Value(0));
                    }
                }
            };

            // Konfigurer capsfilter
            capsfilter["caps"] = Caps.FromString("video/x-raw,width=640,height=240");

            // Tilføj elementer til pipeline
            pipeline.Add(appsrc0, appsrc1, compositor, capsfilter, autovideosink);

            // Link elementerne
            appsrc0.Link(compositor);
            appsrc1.Link(compositor);
            compositor.Link(capsfilter);
            capsfilter.Link(autovideosink);

            return pipeline;
        }
        
        private static void ConfigureAppSrc(AppSrc appsrc)
        {
            appsrc.Caps = Caps.FromString("video/x-raw,format=I420,width=320,height=240,framerate=30/1");
            appsrc.IsLive = true;
            appsrc.Block = true;
            appsrc.Format = Format.Time;
        }

        private static void StartVideoTestSource(Pipeline pipeline, string appsrcName)
        {
            // Opret videotestsrc pipeline
            var videoTestPipeline = new Pipeline("video-test-pipeline");
            var appsrc = pipeline.GetByName(appsrcName) as AppSrc;
            
            // Verificer AppSrc
            if (appsrc == null)
            {
                Console.WriteLine($"Fejl: Kunne ikke finde AppSrc med navn {appsrcName}.");
                return;
            }


            var videotestsrc = ElementFactory.Make("videotestsrc", "videotestsrc");
            var capsfilter = ElementFactory.Make("capsfilter", "capsfilter");
            var appsink = new AppSink("appsink");

            // Verificer elementer
            if (videotestsrc == null || capsfilter == null || appsink == null)
            {
                Console.WriteLine("Fejl: Kunne ikke oprette videotestsrc pipeline.");
                Environment.Exit(1);
            }

            // Konfigurer videotestsrc
            videotestsrc["pattern"] = 0;

            // Konfigurer capsfilter
            capsfilter["caps"] = Caps.FromString("video/x-raw,format=I420,width=320,height=240,framerate=30/1");

            // Konfigurer AppSink
            appsink.EmitSignals = true;
            appsink.Sync = false;

            // Håndter "new-sample"-signal
            appsink.NewSample += (sender, args) =>
            {
                var sample = appsink.PullSample();
                if (sample != null)
                {
                    var buffer = sample.Buffer;
                    appsrc.PushBuffer(buffer);
                    sample.Dispose();
                }
            };

            // Tilføj elementer til videotestsrc pipeline og link dem
            videoTestPipeline.Add(videotestsrc, capsfilter, appsink);
            videotestsrc.Link(capsfilter);
            capsfilter.Link(appsink);


            
            // Start videotestsrc pipeline
            videoTestPipeline.SetState(State.Playing);
        }


private static void PlayVideoFile(string filePath, Pipeline pipeline, string appsrcName)
{
    
    var filePipeline = new Pipeline("file-pipeline");
    // Opret elementer
    var filesrc = ElementFactory.Make("filesrc", $"filesrc-{appsrcName}");
    var decodebin = ElementFactory.Make("decodebin", $"decodebin-{appsrcName}");
    var d3d11convert = ElementFactory.Make("d3d11convert", $"d3d11convert-{appsrcName}");
    var d3d11download = ElementFactory.Make("d3d11download", $"d3d11download-{appsrcName}");
    var videoconvert = ElementFactory.Make("videoconvert", $"videoconvert-{appsrcName}");
    var videoscale = ElementFactory.Make("videoscale", $"videoscale-{appsrcName}");
    var videorate = ElementFactory.Make("videorate", $"videorate-{appsrcName}");
    var capsfilter = ElementFactory.Make("capsfilter", $"capsfilter-{appsrcName}");
    var appsink = new AppSink($"appsink-{appsrcName}");

    // Verificer elementer
    if (filesrc == null || decodebin == null || d3d11convert == null || d3d11download == null ||
        videoconvert == null || videoscale == null || videorate == null || capsfilter == null || appsink == null)
    {
        Console.WriteLine("Fejl: Kunne ikke oprette nødvendige elementer.");
        Environment.Exit(1);
    }

    // Konfigurer elementer
    filesrc["location"] = filePath;
    capsfilter["caps"] = Caps.FromString("video/x-raw,format=I420,width=320,height=240,framerate=30/1");

    appsink.EmitSignals = true;
    appsink.Sync = true;

    // Find eksisterende AppSrc
    var appsrc = pipeline.GetByName(appsrcName) as AppSrc;
    if (appsrc == null)
    {
        Console.WriteLine($"Fejl: Kunne ikke finde AppSrc med navn {appsrcName}.");
        return;
    }

    // Håndter dynamisk pad-linking fra decodebin
    decodebin.PadAdded += (sender, args) =>
    {
        Console.WriteLine($"Decodebin oprettede en ny pad: {args.NewPad.Name}");

        if (args.NewPad.QueryCaps(null).ToString().Contains("video"))
        {
            var sinkPad = d3d11convert.GetStaticPad("sink");
            if (!sinkPad.IsLinked && args.NewPad.CanLink(sinkPad))
            {
                var result = args.NewPad.Link(sinkPad);
                Console.WriteLine(result == PadLinkReturn.Ok
                    ? $"Video pad linket til d3d11convert: {result}"
                    : $"Kunne ikke linke video pad: {result}");
            }
        }
    };
    appsrc["do-timestamp"] = true;
    // Håndter "NewSample" fra appsink og skub data til appsrc
    appsink.NewSample += (sender, args) =>
    {
        var sample = appsink.PullSample();
        if (sample != null)
        {
            var buffer = sample.Buffer;
            if (buffer != null)
            {
                Console.WriteLine($"Pusher buffer til AppSrc: {buffer.Duration}");
                
                buffer.Pts = Gst.Constants.CLOCK_TIME_NONE;
                //buffer.Dts = Gst.Constants.CLOCK_TIME_NONE;
                
                var ret = appsrc.PushBuffer(buffer);
                if (ret != FlowReturn.Ok)
                    Console.WriteLine($"Fejl ved push til AppSrc: {ret}");
            }
            sample.Dispose();
        }
    };

    // Tilføj elementer til pipeline
    filePipeline.Add(filesrc, decodebin, d3d11convert, d3d11download, videoconvert, videoscale, videorate, capsfilter,
        appsink);

    // Link elementer i pipelinen
    if (!filesrc.Link(decodebin))
    {
        Console.WriteLine("Fejl: Kunne ikke linke filesrc til decodebin.");
        return;
    }

    if (!Element.Link(d3d11convert, d3d11download) || !Element.Link(d3d11download, videoconvert) ||
        !Element.Link(videoconvert, videoscale) || !Element.Link(videoscale, videorate) ||
        !Element.Link(videorate, capsfilter) || !capsfilter.Link(appsink))
    {
        Console.WriteLine("Fejl: Kunne ikke linke videoelementer.");
        return;
    }
    

    //filePipeline.SetState(State.Ready);
    // var ret = filePipeline.SetState(State.Playing);
    // if (ret != StateChangeReturn.Success && ret != StateChangeReturn.Async)
    // {
    //     Console.WriteLine("Fejl: Kunne ikke starte pipeline korrekt.");
    // }
    //
    QuePlay(filePipeline);
    
    HandlePlaybackControl(filePipeline);
}


private static void HandlePlaybackControl(Pipeline filePipeline)
{
    Console.WriteLine("Afspilningskontrol: ");
    Console.WriteLine(" 'P' for PAUSE/PLAY.");
    Console.WriteLine(" 'S'/'s' for at ændre hastighed.");
    Console.WriteLine(" 'D' for at ændre retning.");
    Console.WriteLine(" 'Q' for at afslutte kontrol.");

    bool running = true;
    while (running)
    {
        var key = Console.ReadKey(intercept: true).Key;
        switch (key)
        {
            case ConsoleKey.P:
                // Tjek den aktuelle tilstand
                State currentState;
                State pendingState;

                if (filePipeline.GetState(out currentState, out pendingState, Gst.Constants.SECOND * 1) == StateChangeReturn.Success)
                {
                    // Skift tilstanden baseret på den nuværende state
                    if (currentState == State.Playing)
                    {
                        filePipeline.SetState(State.Paused);
                        Console.WriteLine("Skiftet til PAUSE.");
                    }
                    else if (currentState == State.Paused || currentState == State.Ready)
                    {
                        filePipeline.SetState(State.Playing);
                        Console.WriteLine("Skiftet til PLAYING.");
                    }
                    else
                    {
                        Console.WriteLine($"Pipeline er i en uventet tilstand: {currentState}. Ingen ændring foretaget.");
                    }
                }
                else
                {
                    Console.WriteLine("Kunne ikke hente pipelineens tilstand.");
                }
                break;

            case ConsoleKey.S:
                playbackRate *= 2.0;
                SendSeekEvent(filePipeline);
                break;

            // case ConsoleKey.S when ConsoleModifiers.Shift != 0:
            //     playbackRate /= 2.0;
            //     SendSeekEvent(filePipeline);
            //     break;

            case ConsoleKey.D:
                playbackRate *= -1.0;
                SendSeekEvent(filePipeline);
                break;

            case ConsoleKey.R:
                RestartPlayback(filePipeline);
                Console.WriteLine("Afspilningen er startet forfra.");
                break;

            
            case ConsoleKey.Q:
                running = false;
                filePipeline.SetState(State.Null);
                break;

            default:
                Console.WriteLine("Ugyldig tast.");
                break;
        }
    }
}

private static void RestartPlayback(Pipeline pipeline)
{
    Console.WriteLine("Genstarter afspilningen fra begyndelsen...");

    var ret = pipeline.SetState(State.Null);
    if (ret != StateChangeReturn.Success && ret != StateChangeReturn.Async)
    {
        Console.WriteLine("Fejl: Kunne ikke starte pipeline korrekt.");
    }

    long position = 0; // Start fra begyndelsen (0 nanosekunder)

    // Opret seek-event til at starte fra begyndelsen
    var seekEvent = Event.NewSeek(
        1.0, // Afspilningshastighed (normal hastighed)
        Format.Time, // Tid som format
        SeekFlags.Flush | SeekFlags.Accurate, // Rens pipeline og præcis seek
        SeekType.Set, // Sæt seek til absolut position
        position, // Start ved 0
        SeekType.None, // Ingen slutgrænse
        0);
    

    // Send seek-event
    if (!pipeline.SendEvent(seekEvent))
    {
        Console.WriteLine("Fejl: Kunne ikke sende seek-event for at starte forfra.");
    }
    else
    {
        Console.WriteLine("Afspilningen er genstartet fra begyndelsen.");
    }

    // Sæt pipeline tilbage til afspilningstilstand
    var ret2 = pipeline.SetState(State.Playing);
    if (ret2 != StateChangeReturn.Success && ret2 != StateChangeReturn.Async)
    {
        Console.WriteLine("Fejl: Kunne ikke starte pipeline korrekt.");
    }

}



private static void QuePlay(Pipeline pipeline)
{
    Console.WriteLine("Queue video til første frame...");

    // Sæt pipeline til Playing for at begynde afspilning
    var ret = pipeline.SetState(State.Playing);
    if (ret != StateChangeReturn.Success && ret != StateChangeReturn.Async)
    {
        Console.WriteLine("Fejl: Kunne ikke sætte pipeline til Playing.");
        return;
    }
    const long stopPosition = 1000 * 1000000; // Stop ved 40 ms (nanosekunder)
    MonitorPosition(pipeline, stopPosition);
}

private static void MonitorPosition(Pipeline pipeline, long targetPosition)
{
    var monitorTask = new System.Threading.Tasks.Task(() =>
    {
        while (true)
        {
            if (pipeline.QueryPosition(Format.Time, out long currentPosition))
            {
                Console.WriteLine($"Aktuel position: {currentPosition} nanosekunder.");

                if (currentPosition >= targetPosition)
                {
                    // Pause pipeline når vi når target-position
                    pipeline.SetState(State.Paused);
                    Console.WriteLine("Pipeline pauset ved første frame.");
                    break;
                }
            }

            // Sov lidt for at undgå konstant query
            System.Threading.Thread.Sleep(10);
        }
    });

    monitorTask.Start();
}



private static void SendSeekEvent(Pipeline filePipeline)
{
    if (videoSink == null)
    {
        Console.WriteLine("Video sink ikke fundet!");
        return;
    }

    long position = 0;
    if (!filePipeline.QueryPosition(Format.Time, out position))
    {
        Console.WriteLine("Kunne ikke hente aktuel position.");
        return;
    }

    var seekEvent = Event.NewSeek(
        playbackRate,
        Format.Time,
        SeekFlags.Flush | SeekFlags.Accurate,
        SeekType.Set,
        position,
        SeekType.None,
        0);

    if (!videoSink.SendEvent(seekEvent))
    {
        Console.WriteLine("Kunne ikke sende seek-event.");
    }
    else
    {
        Console.WriteLine($"Afspilningshastighed ændret til {playbackRate}.");
    }
}
    }
}