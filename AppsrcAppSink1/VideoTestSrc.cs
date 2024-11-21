using Gst;
using Gst.App;

namespace AppsrcAppSink1;

public class VideoTestSrc
{
    public VideoTestSrc(Pipeline mainPipeline, string appsrcName)
    {
        // Opret videotestsrc pipeline
        var videoTestPipeline = new Pipeline("video-test-pipeline");
        var appsrc = mainPipeline.GetByName(appsrcName) as AppSrc;

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
}