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

        // Start pipeline
        pipeline.SetState(State.Playing);
        var filePlayer = new Fileplayer(@"C:\Users\tomi\RiderProjects\GstsnippetsVol1\AppsrcAppSink1\bun33s.mp4",
            pipeline,
            "appsrc-sink_0");


        var inputHandler = new KeyboardInputHandler();
        inputHandler.Start();

        // Lyt til input og udfør handlinger
        Console.WriteLine("Tryk på 1 for at aktivere videotestsrc, eller 2 for at afspille en video.");
        while (true)
        {
            var command = inputHandler.GetNextCommand();

            if (command == "1")
            {
                Console.WriteLine("\nAktivering af videotestsrc...");
                var videoTestSource = new VideoTestSrc(pipeline, "appsrc-sink_1");
            }
            else if (command == "2")
            {
                Console.WriteLine("\nAfspilning af video...");
                filePlayer.QuePlay();
            }

            else if (command == "p")
            {
                Console.WriteLine("\nPause/Play...");
                filePlayer.PlayPause();
            }
            else if (command == "p")
            {
                Console.WriteLine("\nPause/Play...");
                filePlayer.PlayPause();
            }

            else if (command == "r")
            {
                Console.WriteLine("\nPause/Play...");
                filePlayer.RestartPlayback();
            }

            else if (command == "q")
            {
                Console.WriteLine("\nAfslutter programmet...");
                break;
            }
            else
            {
                Console.WriteLine(
                    "\nUgyldig kommando. Tryk på 1 for videotestsrc, 2 for videoafspilning, eller q for at afslutte.");
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
        var autovideosink2 = ElementFactory.Make("autovideosink", "autovideosink2");

        var appsrc0 = new AppSrc("appsrc-sink_0");
        var appsrc1 = new AppSrc("appsrc-sink_1");

        // Verificer elementer
        if (compositor == null || capsfilter == null || tee == null || queue1 == null || queue2 == null
            || autovideosink1 == null || autovideosink2 == null || appsrc0 == null || appsrc1 == null)
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
            autovideosink1, autovideosink2);

        // Konfigurer og tilføj webcam
        var web = new AttachWebcam(pipeline, compositor);

        appsrc0.Link(appsrcQueue0);
        appsrcQueue0.Link(compositor);

        appsrc1.Link(appsrcQueue1);
        appsrcQueue1.Link(compositor);

        // Link compositor til resten af pipelinen
        compositor.Link(capsfilter);
        capsfilter.Link(tee);

        // Opret separate grene fra tee
        if (!Element.Link(tee, queue1, autovideosink1) || !Element.Link(tee, queue2, autovideosink2))
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
}