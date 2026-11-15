// WindowsConnectionOwners.cs
// The owner of a connection of the loopback as Windows knows it: the process of its client end by the TCP table, the account of that process by its token (ADR 075).

using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace Topogenia.Host.Desktop;

/// <summary>
/// Windows keeps a row for each end of a TCP connection, with the process that owns it. `GetExtendedTcpTable` (IPv4, the
/// connections and their process) gives the row whose local end is the client end of the connection received and whose
/// remote end is the address the server listens on: the client process. `OpenProcess` (limited query),
/// `OpenProcessToken` (query) and `GetTokenInformation` (`TokenUser`) give the SID of its account. Every failure is no
/// owner: no such row, or a row that no process owns any more (a closed connection), access denied (as a rule, a process
/// of another account), a process gone. Elsewhere than on Windows there is never an owner: the desktop mode runs on
/// Windows (spec 6.1), and nothing else is trusted to tell who is at the other end.
/// </summary>
public sealed class WindowsConnectionOwners : IConnectionOwners
{
    private const int AddressFamilyInet = 2;
    private const int TcpTableOwnerPidConnections = 4;
    private const uint NoError = 0;
    private const uint InsufficientBuffer = 122;
    private const int ProcessQueryLimitedInformation = 0x1000;
    private const int TokenQuery = 0x0008;
    private const int TokenUser = 1;

    /// <summary>`MIB_TCPROW_OWNER_PID`: state, local address, local port, remote address, remote port, process, 4 bytes each.</summary>
    private const int RowSize = 24;

    private const int LocalEnd = 4;
    private const int RemoteEnd = 12;
    private const int OwningProcess = 20;

    public ConnectionOwner? OwnerOf(IPEndPoint server, IPEndPoint client)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(client);
        if (!OperatingSystem.IsWindows())
            return null;
        if (ProcessOf(server, client) is not { } process)
            return null;
        return AccountOf(process) is { } account ? new ConnectionOwner(process, account) : null;
    }

    /// <summary>The number of the process that owns the client end of the connection; null when no process owns it.</summary>
    private static int? ProcessOf(IPEndPoint server, IPEndPoint client)
    {
        if (server.AddressFamily != AddressFamily.InterNetwork || client.AddressFamily != AddressFamily.InterNetwork || TcpTable() is not { } table)
            return null;
        var serverAddress = server.Address.GetAddressBytes();
        var clientAddress = client.Address.GetAddressBytes();
        var rows = Math.Min(BitConverter.ToUInt32(table, 0), (uint)((table.Length - sizeof(uint)) / RowSize));
        for (var row = 0; row < rows; row++)
        {
            var at = sizeof(uint) + (row * RowSize);
            if (!IsEnd(table.AsSpan(at + LocalEnd, 8), clientAddress, client.Port) || !IsEnd(table.AsSpan(at + RemoteEnd, 8), serverAddress, server.Port))
                continue;
            // A row in TIME_WAIT keeps the ends of a closed connection and belongs to no process (0).
            var process = BitConverter.ToInt32(table, at + OwningProcess);
            if (process > 0)
                return process;
        }
        return null;
    }

    /// <summary>An end of a row: the address (4 bytes, network order), then the port (the first 2 bytes of the next 4, network order).</summary>
    private static bool IsEnd(ReadOnlySpan<byte> end, byte[] address, int port) =>
        end[..4].SequenceEqual(address) && BinaryPrimitives.ReadUInt16BigEndian(end[4..]) == port;

    /// <summary>The IPv4 connections of the machine and their process (`MIB_TCPTABLE_OWNER_PID`); null when Windows does not give them.</summary>
    private static byte[]? TcpTable()
    {
        byte[]? table = null;
        var size = 0;
        // The table may grow between the call that measures it and the one that fills it: some rows of room, a few tries.
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var result = NativeMethods.GetExtendedTcpTable(table, ref size, false, AddressFamilyInet, TcpTableOwnerPidConnections, 0);
            if (result == NoError)
                return table;
            if (result != InsufficientBuffer)
                return null;
            table = new byte[size + (16 * RowSize)];
            size = table.Length;
        }
        return null;
    }

    /// <summary>The SID of the account of a process, read from its token; null when the process or its token cannot be opened.</summary>
    [SupportedOSPlatform("windows")]
    private static string? AccountOf(int process)
    {
        using var handle = NativeMethods.OpenProcess(ProcessQueryLimitedInformation, false, process);
        if (handle.IsInvalid)
            return null;
        var opened = NativeMethods.OpenProcessToken(handle, TokenQuery, out var token);
        using (token)
        {
            if (!opened)
                return null;
            // TOKEN_USER: the address of the SID and its attributes, then the SID itself, of SecurityIdentifier.MaxBinaryLength bytes at most.
            var length = (2 * IntPtr.Size) + SecurityIdentifier.MaxBinaryLength;
            var buffer = Marshal.AllocHGlobal(length);
            try
            {
                return NativeMethods.GetTokenInformation(token, TokenUser, buffer, length, out _)
                    ? new SecurityIdentifier(Marshal.ReadIntPtr(buffer)).Value
                    : null;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    private static class NativeMethods
    {
        [DllImport("iphlpapi.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern uint GetExtendedTcpTable([Out] byte[]? table, ref int size, [MarshalAs(UnmanagedType.Bool)] bool order, int addressFamily,
            int tableClass, int reserved);

        [DllImport("kernel32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern SafeProcessHandle OpenProcess(int access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int processId);

        [DllImport("advapi32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool OpenProcessToken(SafeProcessHandle process, int access, out SafeAccessTokenHandle token);

        [DllImport("advapi32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetTokenInformation(SafeAccessTokenHandle token, int informationClass, IntPtr information, int length, out int returned);
    }
}
