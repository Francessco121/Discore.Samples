using Discore.Voice;
using Nito.AsyncEx;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Discore.Samples.VoiceSending
{
    class VoiceSession : IDisposable
    {
        readonly DiscordVoiceConnection connection;
        readonly AsyncAutoResetEvent playResetEvent;

        CancellationTokenSource playCancellationTokenSource;

        public VoiceSession(DiscordVoiceConnection connection)
        {
            this.connection = connection;

            playResetEvent = new AsyncAutoResetEvent(set: true);
        }

        /// <exception cref="DiscordPermissionException">
        /// Thrown if the bot does not have permission to connect to the voice channel.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if connect is called more than once, 
        /// if the voice channel is full and the current bot is not an admin, 
        /// or if the shard behind this connection isn't running.
        /// </exception>
        public async Task Connect(Snowflake voiceChannelId)
        {
            await connection.ConnectAsync(voiceChannelId);
        }

        public async Task Disconnect()
        {
            await connection.DisconnectAsync();
        }

        public async Task Play(string uri)
        {
            // Cancel the existing task playing audio if it exists.
            playCancellationTokenSource?.Cancel();

            // Wait for the existing play task (if necessary) to exit.
            await playResetEvent.WaitAsync();

            playCancellationTokenSource = new CancellationTokenSource();

            try
            {
                // Start ffmpeg.
                using (Process ffmpeg = new Process())
                {
                    ffmpeg.StartInfo = new ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        Arguments = $"-loglevel quiet -i \"{uri}\" -vn -f s16le -ar 48000 -ac 2 pipe:1"
                    };

                    if (ffmpeg.Start())
                    {
                        // Notify Discord that we are starting.
                        await connection.SetSpeakingAsync(true);

                        // Create a buffer to move data from ffmpeg to the voice connection.
                        byte[] transferBuffer = new byte[DiscordVoiceConnection.PCM_BLOCK_SIZE];

                        // Keep moving data from ffmpeg until the stream is complete, the connection is invalidated,
                        // or the play task is cancelled.
                        while (!playCancellationTokenSource.IsCancellationRequested && connection.IsValid 
                            && !ffmpeg.StandardOutput.EndOfStream)
                        {
                            // Ensure the connection's buffer has room for a full block of audio.
                            if (connection.CanSendVoiceData(transferBuffer.Length))
                            {
                                // Read data from ffmpeg.
                                int read = ffmpeg.StandardOutput.BaseStream.Read(transferBuffer, 0, transferBuffer.Length);

                                // Send the data over the voice connection.
                                connection.SendVoiceData(transferBuffer, 0, read);
                            }
                            else
                                // Otherwise wait a short amount of time to avoid burning CPU cycles.
                                await Task.Delay(1);
                        }

                        // Let everything get written to the socket before exiting
                        while (!playCancellationTokenSource.IsCancellationRequested && connection.IsValid 
                            && connection.BytesToSend > 0)
                            await Task.Delay(1);
                    }
                    else
                        Console.WriteLine("Failed to start ffmpeg!");
                }
            }
            finally
            {
                // Notify Discord that we have stopped sending audo.
                await connection.SetSpeakingAsync(false);

                // Allow play to be called again.
                playResetEvent.Set();
            }
        }

        public bool Stop()
        {
            // Cancel the existing task playing audio if it exists.
            if (playCancellationTokenSource != null && !playCancellationTokenSource.IsCancellationRequested)
            {
                playCancellationTokenSource.Cancel();
                return true;
            }
            else
                return false;
        }

        public void Dispose()
        {
            connection.Dispose();
            playCancellationTokenSource?.Dispose();
        }
    }
}
