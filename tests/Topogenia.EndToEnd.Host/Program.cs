// Program.cs
// Entry point of the starter of the host for the journeys of the browser: runs it until it is stopped; a non-zero code when its options are wrong.

using Topogenia.EndToEnd;

return await EndToEndHost.RunAsync(args, Console.Out, CancellationToken.None);
