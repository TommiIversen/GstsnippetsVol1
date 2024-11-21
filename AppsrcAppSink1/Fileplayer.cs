using Gst;
using Gst.App;
using Constants = Gst.Constants;
using Task = System.Threading.Tasks.Task;

namespace AppsrcAppSink1;

public class Fileplayer
{
    private Element filesrc;
    
    public Fileplayer(string filePath, Pipeline pipeline, string appsrcName)
    {
        FilePipeline = new Pipeline("file-pipeline");
        // Opret elementer
        filesrc = ElementFactory.Make("filesrc", $"filesrc-{appsrcName}");
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

                    buffer.Pts = Constants.CLOCK_TIME_NONE;
                    //buffer.Dts = Gst.Constants.CLOCK_TIME_NONE;

                    var ret = appsrc.PushBuffer(buffer);
                    if (ret != FlowReturn.Ok)
                        Console.WriteLine($"Fejl ved push til AppSrc: {ret}");
                }

                sample.Dispose();
            }
        };

        // Tilføj elementer til pipeline
        FilePipeline.Add(filesrc, decodebin, d3d11convert, d3d11download, videoconvert, videoscale, videorate,
            capsfilter,
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
            Console.WriteLine("Fejl: Kunne ikke linke videoelementer.");
    }

    public Pipeline? FilePipeline { get; }

    public void PlayPause()
    {
        // Tjek den aktuelle tilstand
        State currentState;
        State pendingState;

        if (FilePipeline.GetState(out currentState, out pendingState, Constants.SECOND * 1) ==
            StateChangeReturn.Success)
        {
            // Skift tilstanden baseret på den nuværende state
            if (currentState == State.Playing)
            {
                FilePipeline.SetState(State.Paused);
                Console.WriteLine("Skiftet til PAUSE.");
            }
            else if (currentState == State.Paused || currentState == State.Ready)
            {
                FilePipeline.SetState(State.Playing);
                Console.WriteLine("Skiftet til PLAYING.");
            }
            else
            {
                Console.WriteLine(
                    $"Pipeline er i en uventet tilstand: {currentState}. Ingen ændring foretaget.");
            }
        }
        else
        {
            Console.WriteLine("Kunne ikke hente pipelineens tilstand.");
        }
    }


    public void RestartPlayback()
    {
        Console.WriteLine("Genstarter afspilningen fra begyndelsen...");

        var ret = FilePipeline.SetState(State.Null);
        if (ret != StateChangeReturn.Success && ret != StateChangeReturn.Async)
            Console.WriteLine("Fejl: Kunne ikke starte pipeline korrekt.");

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
        if (!FilePipeline.SendEvent(seekEvent))
            Console.WriteLine("Fejl: Kunne ikke sende seek-event for at starte forfra.");
        else
            Console.WriteLine("Afspilningen er genstartet fra begyndelsen.");

        // Sæt pipeline tilbage ti1l afspilningstilstand
        var ret2 = FilePipeline.SetState(State.Playing);
        if (ret2 != StateChangeReturn.Success && ret2 != StateChangeReturn.Async)
            Console.WriteLine("Fejl: Kunne ikke starte pipeline korrekt.");
    }

    
    public void QuePlay(string filePath)
    {
        Console.WriteLine($"Skifter til ny video: {filePath}");

        // Pause pipeline for at frigøre ressourcer
        FilePipeline.SetState(State.Null);

        // Unlink filesrc og relink med ny kilde
        try
        {
            filesrc.SetState(State.Null); // Stop kilden
            filesrc["location"] = filePath; // Opdater til ny fil
            filesrc.SetState(State.Ready); // Gør kilden klar igen

            Console.WriteLine("Ny kilde konfigureret.");

            // Sæt pipeline til Playing for at starte afspilning
            var ret2 = FilePipeline.SetState(State.Playing);
            if (ret2 != StateChangeReturn.Success && ret2 != StateChangeReturn.Async)
            {
                Console.WriteLine("Fejl: Kunne ikke sætte pipeline til Playing.");
                return;
            }

            const long stopPosition = 1000 * 1000000; // Stop ved 40 ms (nanosekunder)
            MonitorPosition(FilePipeline, stopPosition);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fejl ved skift af video: {ex.Message}");
        }
    }
    
    public void QuePlayold(string filePath)
    {
        Console.WriteLine("Queue video til første frame...");
        
        
        filesrc["location"] = filePath;


        // Sæt pipeline til Playing for at begynde afspilning
        var ret = FilePipeline.SetState(State.Playing);
        if (ret != StateChangeReturn.Success && ret != StateChangeReturn.Async)
        {
            Console.WriteLine("Fejl: Kunne ikke sætte pipeline til Playing.");
            return;
        }

        const long stopPosition = 1000 * 1000000; // Stop ved 40 ms (nanosekunder)
        MonitorPosition(FilePipeline, stopPosition);
    }

    private static void MonitorPosition(Pipeline pipeline, long targetPosition)
    {
        var monitorTask = new Task(() =>
        {
            while (true)
            {
                if (pipeline.QueryPosition(Format.Time, out var currentPosition))
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
                Thread.Sleep(10);
            }
        });

        monitorTask.Start();
    }
}