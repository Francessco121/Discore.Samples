# Discore.Samples.VoiceSending
This sample provides an example implementation of a bot that plays audio in voice channels. This could be used as an example for creating a music bot!

**Important Note:** This sample makes use of ffmpeg, opus, and libsodium. All of these binaries are meant for Windows 10 64-bit and thus this sample has only been tested in that environment.

**Other Note:** This sample makes (light) use of the third-party Nito.Async.Coordination package.

## Setup

### 1. Add Correct Binaries For Your Platform
**If you are running Windows 10 64-bit, you can skip this step!**

Inside the project are the binaries for ffmpeg, opus, and libsodium. If you are running an older version of Windows, or a different operating system altogether, you will need to replace these with the versions designed for your platform.

### 2. Add Your Bot Token
Replace the `TOKEN` constant on line 29 in Program.cs with your bot's token.

## Usage
This sample bot responds to 4 commands:
- `!join` - Tells the bot to join the voice channel that the invoking user is currently in.
- `!leave` - Tells the bot to leave the current voice channel that it is in.
- `!play <uri>` - Tells the bot to begin streaming the audio from the specified URI in the voice channel. The URI must be a direct audio source (e.g. a file path to an mp3) as this is fed directly into ffmpeg!
- `!stop` - Tells the bot to stop streaming audio if it currently is.

## Discore Version
This sample uses Discore 4.0.0.