using Discore;
using Discore.Http;
using Discore.WebSocket;
using System;
using System.Threading.Tasks;

namespace Discore.Samples.PingPong
{
    class Program
    {
        DiscordHttpClient http;

        public static void Main(string[] args)
        {
            Program program = new Program();
            program.Run().Wait();
        }

        public async Task Run()
        {
            const string TOKEN = "<bot user token goes here>";

            // Create an HTTP client.
            http = new DiscordHttpClient(TOKEN);

            // Create a single shard.
            using (Shard shard = new Shard(TOKEN, 0, 1))
            {
                // Subscribe to the message creation event.
                shard.Gateway.OnMessageCreated += Gateway_OnMessageCreated;

                // Start the shard.
                await shard.StartAsync();
                Console.WriteLine("Bot started!");

                // Wait for the shard to end before closing the program.
                await shard.WaitUntilStoppedAsync();
            }
        }

        private async void Gateway_OnMessageCreated(object sender, MessageEventArgs e)
        {
            Shard shard = e.Shard;
            DiscordMessage message = e.Message;

            if (message.Author.Id == shard.UserId)
                // Ignore messages created by our bot.
                return;

            if (message.Content == "!ping")
            {
                try
                {
                    // Reply to the user who posted "!ping".
                    await http.CreateMessage(message.ChannelId, $"<@!{message.Author.Id}> Pong!");
                }
                catch (DiscordHttpApiException) { /* Message failed to send... :( */ }
            }
        }
    }
}
