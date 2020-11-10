﻿using Cupscale.IO;
using Cupscale.OS;
using Cupscale.UI;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Cupscale
{
    class FFmpeg
    {
        public static async Task Run(string args)
        {
            Process ffmpeg = OSUtils.NewProcess(true);
            ffmpeg.StartInfo.Arguments = "/C cd /D " + Paths.esrganPath.Wrap() + " & ffmpeg.exe -hide_banner -loglevel warning -y -stats " + args;
            Logger.Log("Running ffmpeg...");
            Logger.Log("cmd.exe " + ffmpeg.StartInfo.Arguments);
            ffmpeg.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
            ffmpeg.ErrorDataReceived += new DataReceivedEventHandler(OutputHandler);
            ffmpeg.Start();
            ffmpeg.BeginOutputReadLine();
            ffmpeg.BeginErrorReadLine();
            while (!ffmpeg.HasExited)
                await Task.Delay(100);
            Logger.Log("Done running ffmpeg.");
        }

        static void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            Logger.Log("[FFmpeg] " + outLine.Data);
        }

        public static async Task RunGifski (string args)
        {
            Process ffmpeg = OSUtils.NewProcess(true);
            ffmpeg.StartInfo.Arguments = $"/C cd /D {Paths.esrganPath.Wrap()} & gifski.exe {args}";
            Logger.Log("Running gifski...");
            Logger.Log("cmd.exe " + ffmpeg.StartInfo.Arguments);
            ffmpeg.OutputDataReceived += new DataReceivedEventHandler(OutputHandlerGifski);
            ffmpeg.ErrorDataReceived += new DataReceivedEventHandler(OutputHandlerGifski);
            ffmpeg.Start();
            ffmpeg.BeginOutputReadLine();
            ffmpeg.BeginErrorReadLine();
            while (!ffmpeg.HasExited)
                await Task.Delay(100);
            Logger.Log("Done running gifski.");
        }

        static void OutputHandlerGifski (object sendingProcess, DataReceivedEventArgs outLine)
        {
            Logger.Log("[gifski] " + outLine.Data);
        }

        public static string RunAndGetOutput (string args)
        {
            Process ffmpeg = OSUtils.NewProcess(true);
            ffmpeg.StartInfo.Arguments = "/C cd /D " + Paths.esrganPath.Wrap() + " & ffmpeg.exe -hide_banner -y -stats " + args;
            ffmpeg.Start();
            ffmpeg.WaitForExit();
            string output = ffmpeg.StandardOutput.ReadToEnd();
            string err = ffmpeg.StandardError.ReadToEnd();
            if (!string.IsNullOrWhiteSpace(err))
                output = output + "\n" + err;
            return output;
        }
    }
}