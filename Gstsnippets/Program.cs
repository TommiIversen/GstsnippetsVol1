using ConsoleApp11;
using GLib;
using Application = Gst.Application;
using Task = System.Threading.Tasks.Task;

namespace GstreamerSharp;

internal class Program
{
    private static MainLoop mainLoop;

    private static void Main(string[] args)
    {
        Application.Init();
        mainLoop = new MainLoop();

        var videoSinkPipeline = new VideoSinkPipeline("MainVideoSink");
        var audioSinkPipeline = new AudioSinkPipeline("MainAudioSink");
        videoSinkPipeline.Start();
        audioSinkPipeline.Start();

        // Start pipelines
        var mainPipeline = new MainPipeline(3, 3);
        mainPipeline.SetVideoTargetAppSrc(videoSinkPipeline.VideoAppSrc);
        mainPipeline.SetAudioTargetAppSrc(audioSinkPipeline.AudioAppSrc);

        var videoTestSrc1 =
            new TestSrcPipeline(0, "TestVideo1", mainPipeline.VideoAppSrcs[0], mainPipeline.AudioAppSrcs[0]);
        var videoTestSrc2 =
            new TestSrcPipeline(11, "TestVideo2", mainPipeline.VideoAppSrcs[2], mainPipeline.AudioAppSrcs[2]);
        var filePlayerPipeline = new FilePlayerSrcPipeline(@"C:\Users\Tommi\Downloads\sintel_trailer-1080p.mp4",
            "file1", mainPipeline.VideoAppSrcs[1], mainPipeline.AudioAppSrcs[1]);

        // Start hovedpipeline
        mainPipeline.Start();

        Task.Run(async () =>
        {
            try
            {
                videoTestSrc1.Start();
                await Task.Delay(1000);
                mainPipeline.SetAudioVolume(0, 0.05);


                mainPipeline.SetAlpha(0, 0.4);

                await Task.Delay(5000);

                Console.WriteLine("-------Starting secondary pipeline 2");

                mainPipeline.SetAlpha(2, 0.7);
                videoTestSrc2.Start();
                mainPipeline.SetAlpha(2, 0.5);

                mainPipeline.SetAudioVolume(2, 0.5);

                filePlayerPipeline.Start();
                mainPipeline.SetAudioVolume(1, 0.05);
                mainPipeline.SetAlpha(1, 0.5);

                await Task.Delay(2500);
                //mainPipeline.SetAudioVolume(2, 0.0);
                mainPipeline.SetAudioVolume(2, 0.0);
                mainPipeline.SetAudioVolume(0, 0.0);

                await Task.Delay(1500);
                filePlayerPipeline.Stop();

                await Task.Delay(2500);

                mainPipeline.SetAlpha(1, 0.0);

                await Task.Delay(5500);
                filePlayerPipeline.LoadNewFile(@"C:\Users\Tommi\Downloads\bun33s.mp4");
                mainPipeline.SetAlpha(1, 1.0);
                //mainPipeline.SetAlpha(2, 0.1);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        });

        // Start main loop
        mainLoop.Run();

        // Ryd op
        mainPipeline.Stop();
        //secondaryPipeline1.Stop();
        //secondaryPipeline2.Stop();
    }
}