using Gst;

namespace AppsrcAppSink1;

public class AttachWebcam
{
    public AttachWebcam(Pipeline pipeline, Element compositor)
    {
        var webcam = ElementFactory.Make("ksvideosrc", "webcam");
        var webcamQueue = ElementFactory.Make("queue", "webcam-queue");
        var webcamConvert = ElementFactory.Make("videoconvert", "webcam-convert");
        var webcamScale = ElementFactory.Make("videoscale", "webcam-scale");
        var webcamCapsFilter = ElementFactory.Make("capsfilter", "webcam-capsfilter");

        // Verificer elementer
        if (webcam == null || webcamQueue == null || webcamConvert == null || webcamScale == null ||
            webcamCapsFilter == null)
        {
            Console.WriteLine("Fejl: Kunne ikke oprette webcam-elementer.");
            Environment.Exit(1);
        }

        // Konfigurer capsfilter for webcam
        webcamCapsFilter["caps"] = Caps.FromString("video/x-raw,format=I420,width=320,height=240,framerate=30/1");

        // Tilføj elementer til pipeline
        pipeline.Add(webcam, webcamQueue, webcamConvert, webcamScale, webcamCapsFilter);

        // Link webcam-elementer
        webcam.Link(webcamQueue);
        webcamQueue.Link(webcamConvert);
        webcamConvert.Link(webcamScale);
        webcamScale.Link(webcamCapsFilter);
        webcamCapsFilter.Link(compositor);
    }
}