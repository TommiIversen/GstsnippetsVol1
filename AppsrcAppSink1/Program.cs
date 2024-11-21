using System.Xml;
using Gst;
using Gst.App;
using Value = GLib.Value;

namespace AppsrcAppSink1;

internal class Program
{
    private static void Main(string[] args)
    {
        // Initialiser GStreamer
        Application.Init();

        // Opret pipeline med compositor
        var pipeline = CreateMainPipeline();
        
        // Opret MP4-optageren
        var appSink = (AppSink)pipeline.GetByName("appsink-mp4recorder"); // Antag, at der er en appsink til rådighed.
        var recorder = new RecordVideo(appSink);

        // Start pipeline
        pipeline.SetState(State.Playing);
        var filePlayer = new Fileplayer(@"C:\Users\tomi\RiderProjects\GstsnippetsVol1\AppsrcAppSink1\bun33s.mp4",
            pipeline,
            "appsrc-sink_0");


        var inputHandler = new KeyboardInputHandler();
        inputHandler.Start();

        // Lyt til input og udfør handlinger
        Console.WriteLine("Tryk på 1 for at aktivere videotestsrc, 2 for at afspille en video, eller q for at afslutte.");
        while (true)
        {
            var command = inputHandler.GetNextCommand();

            switch (command)
            {
                case "1":
                    Console.WriteLine("\nAktivering af videotestsrc...");
                    var videoTestSource = new VideoTestSrc(pipeline, "appsrc-sink_1");
                    break;

                case "2":
                    Console.WriteLine("\nAfspilning af video...");
                    filePlayer.QuePlay();
                    break;

                case "a":
                    Console.WriteLine("\nStarter optager...");
                    recorder.Start();
                    break;

                case "s":
                    Console.WriteLine("\nStopper optager...");
                    recorder.Stop();
                    break;

                case "p":
                    Console.WriteLine("\nPause/Play...");
                    filePlayer.PlayPause();
                    break;

                case "r":
                    Console.WriteLine("\nGenstarter videoafspilning...");
                    filePlayer.RestartPlayback();
                    break;

                case "q":
                    Console.WriteLine("\nAfslutter programmet...");
                    return; // Stopper løkken og programmet

                default:
                    Console.WriteLine("\nUgyldig kommando. Tryk på 1 for videotestsrc, 2 for videoafspilning, eller q for at afslutte.");
                    break;
            }
        }
        
        // Stop pipeline
        pipeline.SetState(State.Null);
        pipeline.Dispose();
    }

    private static Pipeline CreateMainPipeline()
    {
        var pipeline = new Pipeline("dynamic-compositor");

        // Opret elementer
        var compositor = ElementFactory.Make("compositor", "compositor");
        var capsfilter = ElementFactory.Make("capsfilter", "capsfilter");
        var tee = ElementFactory.Make("tee", "tee");
        var queue1 = ElementFactory.Make("queue", "queue1");
        var queue2 = ElementFactory.Make("queue2", "queue2");
        var autovideosink1 = ElementFactory.Make("autovideosink", "autovideosink1");
        //var autovideosink2 = ElementFactory.Make("autovideosink", "autovideosink2");

        var appsrc0 = new AppSrc("appsrc-sink_0");
        var appsrc1 = new AppSrc("appsrc-sink_1");

        // Verificer elementer
        if (compositor == null || capsfilter == null || tee == null || queue1 == null || queue2 == null
            || autovideosink1 == null || appsrc0 == null || appsrc1 == null)
        {
            Console.WriteLine("Fejl: Kunne ikke oprette elementer.");
            Environment.Exit(1);
        }

        // Konfigurer AppSrc
        ConfigureAppSrc(appsrc0);
        ConfigureAppSrc(appsrc1);

        // Konfigurer compositor
        compositor["background"] = 2; // Sort baggrund (0 = Transparent, 1 = Sort)

        compositor.PadAdded += (sender, args) =>
        {
            var pad = args.NewPad;
            if (pad.Direction == PadDirection.Sink)
            {
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
                else if (pad.Name == "sink_2")
                {
                    pad.SetProperty("xpos", new Value(640));
                    pad.SetProperty("ypos", new Value(0));
                }
            }
        };

        var appsrcQueue0 = ElementFactory.Make("queue", "appsrc0-queue");
        var appsrcQueue1 = ElementFactory.Make("queue", "appsrc1-queue");

        // Konfigurer capsfilter
        capsfilter["caps"] = Caps.FromString("video/x-raw,format=I420,width=960,height=240,framerate=30/1");

        // Tilføj elementer til pipeline
        pipeline.Add(appsrc0, appsrc1, appsrcQueue0, appsrcQueue1, compositor, capsfilter, tee, queue1, queue2,
            autovideosink1);

        // Konfigurer og tilføj webcam
        var web = new AttachWebcam(pipeline, compositor);

        appsrc0.Link(appsrcQueue0);
        appsrcQueue0.Link(compositor);

        appsrc1.Link(appsrcQueue1);
        appsrcQueue1.Link(compositor);

        // Link compositor til resten af pipelinen
        compositor.Link(capsfilter);
        capsfilter.Link(tee);

        var appSinkMp4 = ElementFactory.Make("appsink", "appsink-mp4recorder");
        appSinkMp4["emit-signals"] = true; // Aktiver signaler for at lytte til data
        appSinkMp4["sync"] = false; // Undgå at vente på clock
        appSinkMp4["caps"] = Caps.FromString("video/x-raw,format=I420,width=960,height=240,framerate=30/1");

        pipeline.Add(appSinkMp4);
        if (!Element.Link(tee, queue1, appSinkMp4))
        {
            throw new Exception("Fejl: Kunne ikke linke appsink.");
        }
        
        // Opret separate grene fra tee
        if (!Element.Link(tee, queue2, autovideosink1))
        {
            Console.WriteLine("Fejl: Kunne ikke linke tee til output.");
            Environment.Exit(1);
        }

        return pipeline;
    }
    
    private static void ConfigureAppSrc(AppSrc appsrc)
    {
        appsrc.Caps = Caps.FromString("video/x-raw,format=I420,width=320,height=240,framerate=30/1");
        appsrc.IsLive = true;
        appsrc.Block = true;
        appsrc.Format = Format.Time;
    }
    
    private static void HandleMessage(object sender, MessageArgs args)
    {
        var msg = args.Message;

        switch (msg.Type)
        {
            case MessageType.Error:
                msg.ParseError(out var error, out var debug);
                Console.WriteLine($"-Error: {error.Message}\nDebug info: {debug}");
                break;
            case MessageType.Eos: // End of Stream
                Console.WriteLine("-End of Stream reached.");
                break;
            case MessageType.Warning:
                msg.ParseWarning(out var warning, out debug);
                Console.WriteLine($"-Warning: {warning}\nDebug info: {debug}");
                break;
            case MessageType.Info:
                msg.ParseInfo(out var info, out debug);
                Console.WriteLine($"-Info: {info}\nDebug info: {debug}");
                break;
            default:
                Console.WriteLine($"-Message: {msg.Type}");
                break;
        }
    }

}