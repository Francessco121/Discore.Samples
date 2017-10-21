using Discore.Http;
using Discore.Voice;
using Discore.WebSocket;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Discore.Samples.VoiceSending
{
    class Program
    {
        readonly ConcurrentDictionary<Snowflake, VoiceSession> voiceSessions;

        DiscordHttpClient httpClient;

        static void Main(string[] args)
        {
            Program program = new Program();
            program.Run().Wait();
        }

        public Program()
        {
            voiceSessions = new ConcurrentDictionary<Snowflake, VoiceSession>();
        }

        public async Task Run()
        {
            const string TOKEN = "<bot user token goes here>";

            using (httpClient = new DiscordHttpClient(TOKEN))
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

        /// <remarks>
        /// Note to the reader: This method will fail to retrieve the guild ID if the text channel
        /// the message was sent in is not cached. The implementation could be extended to fallback
        /// on an HTTP call to retrieve the text channel for more reliability. Luckily this scenario
        /// is uncommon.
        /// </remarks>
        bool TryGetGuildIdFromMessage(DiscordShardCache cache, DiscordMessage message, out Snowflake guildId)
        {
            DiscordGuildTextChannel textChannel = cache.GetGuildTextChannel(message.ChannelId);
            if (textChannel == null)
            {
                guildId = Snowflake.None;
                return false;
            }
            else
            {
                guildId = textChannel.GuildId;
                return true;
            }
        }

        /// <summary>
        /// Responds to the invoker of the specified message.
        /// </summary>
        async Task Respond(DiscordMessage to, string withMessage)
        {
            await httpClient.CreateMessage(to.ChannelId, $"<@!{to.Author.Id}> {withMessage}");
        }

        async void Gateway_OnMessageCreated(object sender, MessageEventArgs e)
        {
            try
            {
                Shard shard = e.Shard;
                DiscordMessage message = e.Message;

                if (message.Author.Id == shard.UserId)
                    // Ignore all messages sent by our bot.
                    return;

                Snowflake guildId;
                if (!TryGetGuildIdFromMessage(shard.Cache, message, out guildId))
                    // Ignore the message if we cannot retrieve a guild ID since this is
                    // required to handle the audio commands.
                    //
                    // This will occur if either the message is not in a guild text channel
                    // or, with the current implementation of TryGetGuildIdFromMessage,
                    // if the text channel is not cached.
                    return;

                // Check if the message is a command
                if (message.Content == "!join")
                {
                    await HandleJoinCommand(shard, message, guildId);
                }
                else if (message.Content == "!leave")
                {
                    await HandleLeaveCommand(shard, message, guildId);
                }
                else if (message.Content.StartsWith("!play"))
                {
                    await HandlePlayCommand(shard, message, guildId);
                }
                else if (message.Content == "!stop")
                {
                    await HandleStopCommand(shard, message, guildId);
                }
            }
            // It's very important to catch all exceptions in this handler since the method is async void!
            // The process will quit if an exception is not handled in this method.
            catch (Exception ex)
            {
                Console.WriteLine($"An unhandled exception occured while processing a message: {ex}");
            }
        }

        async Task HandleJoinCommand(Shard shard, DiscordMessage message, Snowflake guildId)
        {
            // Check if we are already connected in this guild.
            //
            // Note: This implementation will not allow the bot to move between voice channels
            // without leaving first. This implementation could be extended to disconnect
            // a previous session before joining.
            if (voiceSessions.ContainsKey(guildId))
            {
                await Respond(message, "I'm already connected!");
                return;
            }

            // The channel to join is the voice channel the invoking user is currently in.
            DiscordVoiceState voiceState = shard.Cache.GetVoiceState(guildId, message.Author.Id);
            if (voiceState != null && voiceState.ChannelId.HasValue)
            {
                Snowflake voiceChannelId = voiceState.ChannelId.Value;

                // Get a voice connection for the guild.
                DiscordVoiceConnection connection = shard.Voice.CreateOrGetConnection(guildId);

                // Subscribe to the invalidation event so we can clean up our voice session wrapper.
                connection.OnInvalidated += Connection_OnInvalidated;

                // Create and add our new voice session.
                VoiceSession session = new VoiceSession(connection);
                // Note: If this fails then another join command was issued and is already 
                // handling the connection.
                if (voiceSessions.TryAdd(guildId, session))
                {
                    try
                    {
                        // Connect to the voice channel.
                        await session.Connect(voiceChannelId);

                        // Success!
                        await Respond(message, "Hello!");
                    }
                    catch (Exception ex) 
                        when (ex is DiscordPermissionException || ex is InvalidOperationException)
                    {
                        // Both DiscordPermissionException's and InvalidOperationException's can be
                        // thrown if the bot is not allowed/unable to join the voice channel.
                        await Respond(message, ":frowning: I can't join that voice channel.");
                    }
                }
            }
            else
                await Respond(message, "You are not in a voice channel!");
        }

        async Task HandleLeaveCommand(Shard shard, DiscordMessage message, Snowflake guildId)
        {
            // Remove and get the existing session for the guild.
            if (voiceSessions.TryRemove(guildId, out VoiceSession session))
            {
                // Disconnect the session.
                await session.Disconnect();

                // Success!
                await Respond(message, "Bye!");
            }
            else
                await Respond(message, "I'm not in a voice channel!");
        }

        async Task HandlePlayCommand(Shard shard, DiscordMessage message, Snowflake guildId)
        {
            // Get the current session for the guild.
            if (voiceSessions.TryGetValue(guildId, out VoiceSession session))
            {
                // Get the uri from the message.
                if (message.Content.Length >= 6)
                {
                    string uri = message.Content.Substring(6).Trim();

                    if (!string.IsNullOrWhiteSpace(uri))
                    {
                        // Notify the user we're starting.
                        await Respond(message, $"Playing {uri}...");

                        // Play the audio uri.
                        await session.Play(uri);
                    }
                    else
                        await Respond(message, "Usage: !play <uri>");
                }
                else
                    await Respond(message, "Usage: !play <uri>");
            }
            else
                await Respond(message, "I'm not in a voice channel!");
        }

        async Task HandleStopCommand(Shard shard, DiscordMessage message, Snowflake guildId)
        {
            // Get the current session for the guild.
            if (voiceSessions.TryGetValue(guildId, out VoiceSession session))
            {
                // Stop the current play task.
                if (session.Stop())
                {
                    // Success!
                    await Respond(message, "Stopped playing audio.");
                }
            }
            else
                await Respond(message, "I'm not in a voice channel!");
        }

        void Connection_OnInvalidated(object sender, VoiceConnectionInvalidatedEventArgs e)
        {
            e.Connection.OnInvalidated -= Connection_OnInvalidated;

            // Remove our respective voice session for this connection.
            VoiceSession session;
            if (voiceSessions.TryRemove(e.Connection.GuildId, out session))
                session.Dispose();
        }
    }
}
