# QUIC-File-Transfer-Example
# Description
This is an attempt to replicate the deadlock issue I encountered while using the System.Net.Quic dll on C# .NET 7 preview 7.
It is both a client and a server spun on the same system where the client tries to send a video file to the server which then tries to write it into its own directory.
The file is sent by reading 1024*1024 bytes from the file at a time, storing it into a buffer, and then sending that buffer through QuicStream.WriteAsync() to the server.
The server then reads 1024 * 1024 bytes at a time from the stream and writes it into a new file.

# What The Issue Is:
When sending video files above a certain size (~9 GB or so) through a QUIC connection, it deadlocks on QuicStream.ReadAsync() on the listener's side (i.e the server receiving the file).

# Environment
Operating System: Windows 11 Pro 64 Bit
Processor: 11th Gen Intel(R) Core(TM) i7-1185G7 @ 3.00GHz   1.80 GHz

